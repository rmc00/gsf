﻿//******************************************************************************************************
//  EmailNotifier.cs - Gbtc
//
//  Copyright © 2016, Grid Protection Alliance.  All Rights Reserved.
//
//  Licensed to the Grid Protection Alliance (GPA) under one or more contributor license agreements. See
//  the NOTICE file distributed with this work for additional information regarding copyright ownership.
//  The GPA licenses this file to you under the MIT License (MIT), the "License"; you may not use this
//  file except in compliance with the License. You may obtain a copy of the License at:
//
//      http://opensource.org/licenses/MIT
//
//  Unless agreed to in writing, the subject software distributed under the License is distributed on an
//  "AS-IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. Refer to the
//  License for the specific language governing permissions and limitations.
//
//  Code Modification History:
//  ----------------------------------------------------------------------------------------------------
//  05/16/2016 - J. Ritchie Carroll
//       Generated original version of source code.
//
//******************************************************************************************************

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using Ciloci.Flee;
using GSF;
using GSF.Diagnostics;
using GSF.Net.Smtp;
using GSF.TimeSeries;
using GSF.TimeSeries.Adapters;

namespace DynamicCalculator
{
    /// <summary>
    /// The EmailNotifier is an action adapter which takes multiple input measurements and defines
    /// a boolean expression such that when the expression is true an e-mail is triggered.
    /// </summary>
    [Description("E-Mail Notifier: Sends an e-mail based on a custom boolean expression")]
    public class EmailNotifier : ActionAdapterBase
    {
        #region [ Members ]

        // Constants

        /// <summary>
        /// Defines the default value for <see cref="Imports"/> property.
        /// </summary>
        public const string DefaultImports = "AssemblyName={mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089}, TypeName=System.Math";

        // Fields
        private string m_expressionText;
        private string m_variableList;
        private string m_imports;
        private bool m_supportsTemporalProcessing;

        private readonly HashSet<string> m_variableNames;
        private readonly Dictionary<MeasurementKey, string> m_keyMapping;
        private readonly SortedDictionary<int, string> m_nonAliasedTokens;

        private string m_aliasedExpressionText;
        private readonly ExpressionContext m_expressionContext;
        private IDynamicExpression m_expression;

        private readonly Mail m_mailClient;

        #endregion

        #region [ Constructors ]

        /// <summary>
        /// Creates a new instance of the <see cref="EmailNotifier"/>.
        /// </summary>
        public EmailNotifier()
        {
            m_variableNames = new HashSet<string>();
            m_keyMapping = new Dictionary<MeasurementKey, string>();
            m_nonAliasedTokens = new SortedDictionary<int, string>();
            m_expressionContext = new ExpressionContext();
            m_mailClient = new Mail();
        }

        #endregion

        #region [ Properties ]

        /// <summary>
        /// Gets or sets the textual representation of the boolean expression.
        /// </summary>
        [ConnectionStringParameter,
        Description("Define the boolean expression used to determine if an e-mail should be sent.")]
        public string ExpressionText
        {
            get
            {
                return m_expressionText;
            }
            set
            {
                m_expressionText = value;
                PerformAliasReplacement();
            }
        }

        /// <summary>
        /// Gets or sets the list of variables used in the expression.
        /// </summary>
        [ConnectionStringParameter,
        Description("Define the app-domain unique list of variables used in the expression. Any defined aliased variables must be unique per defined dynamic calculator or e-mail notifier instance")]
        public string VariableList
        {
            get
            {
                return m_variableList;
            }
            set
            {
                string keyList;

                if (m_variableList != value)
                {
                    // Store the variable list for get operations
                    m_variableList = value;

                    // Empty the collection of variable names
                    m_variableNames.Clear();
                    m_keyMapping.Clear();
                    m_nonAliasedTokens.Clear();

                    // If the value is null, do not attempt to process it
                    if ((object)value == null)
                        return;

                    // Build the collection of variable names with the new value
                    foreach (string token in value.Split(';'))
                        AddVariable(token);

                    // Perform alias replacement on tokens that were not explicitly aliased
                    PerformAliasReplacement();

                    // Build the key list which will define the input measurements for this adapter
                    keyList = m_keyMapping.Keys.Select(key => key.ToString())
                        .Aggregate((runningKeyList, nextKey) => runningKeyList + ";" + nextKey);

                    // Set the input measurements for this adapter
                    InputMeasurementKeys = AdapterBase.ParseInputMeasurementKeys(DataSource, true, keyList);
                }
            }
        }

        /// <summary>
        /// Gets or sets the list of types which define methods to be imported into the expression parser.
        /// </summary>
        [ConnectionStringParameter,
        Description("Define the list of types which define methods to be imported into the expression parser."),
        DefaultValue(DefaultImports)]
        public string Imports
        {
            get
            {
                return m_imports;
            }
            set
            {
                foreach (string typeDef in value.Split(';'))
                {
                    try
                    {
                        Dictionary<string, string> parsedTypeDef = typeDef.ParseKeyValuePairs(',');
                        string assemblyName = parsedTypeDef["assemblyName"];
                        string typeName = parsedTypeDef["typeName"];
                        Assembly assembly = Assembly.Load(new AssemblyName(assemblyName));
                        Type type = assembly.GetType(typeName);

                        m_expressionContext.Imports.AddType(type);
                    }
                    catch (Exception ex)
                    {
                        string message = $"Unable to load type from assembly: {typeDef}";
                        OnProcessException(MessageLevel.Error, new ArgumentException(message, ex));
                    }
                }

                m_imports = value;
            }
        }

        /// <summary>
        /// Gets or sets the e-mail address of the message sender.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value being assigned is a null or empty string.</exception>
        [ConnectionStringParameter,
        Description("Define the e-mail address of the message sender.")]
        public string From
        {
            get
            {
                return m_mailClient.From;
            }
            set
            {
                m_mailClient.From = value;
            }
        }

        /// <summary>
        /// Gets or sets the comma-separated or semicolon-separated e-mail address list of the message recipients.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value being assigned is a null or empty string.</exception>
        [ConnectionStringParameter,
        Description("Define the comma-separated or semicolon-separated e-mail address list of the e-mail message recipients."),
        DefaultValue("")]
        public string ToRecipients
        {
            get
            {
                return m_mailClient.ToRecipients;
            }
            set
            {
                m_mailClient.ToRecipients = value;
            }
        }

        /// <summary>
        /// Gets or sets the comma-separated or semicolon-separated e-mail address list of the message carbon copy (CC) recipients.
        /// </summary>
        [ConnectionStringParameter,
        Description("Define the comma-separated or semicolon-separated e-mail address list of the e-mail message carbon copy (CC) recipients."),
        DefaultValue("")]
        public string CcRecipients
        {
            get
            {
                return m_mailClient.CcRecipients;
            }
            set
            {
                m_mailClient.CcRecipients = value;
            }
        }

        /// <summary>
        /// Gets or sets the comma-separated or semicolon-separated e-mail address list of the message blank carbon copy (BCC) recipients.
        /// </summary>
        [ConnectionStringParameter,
        Description("Define the comma-separated or semicolon-separated e-mail address list of the e-mail message blank carbon copy (BCC) recipients."),
        DefaultValue("")]
        public string BccRecipients
        {
            get
            {
                return m_mailClient.BccRecipients;
            }
            set
            {
                m_mailClient.BccRecipients = value;
            }
        }

        /// <summary>
        /// Gets or sets the subject of the message.
        /// </summary>
        [ConnectionStringParameter,
        Description("Define the subject of the e-mail message.")]
        public string Subject
        {
            get
            {
                return m_mailClient.Subject;
            }
            set
            {
                m_mailClient.Subject = value;
            }
        }

        /// <summary>
        /// Gets or sets the body of the message.
        /// </summary>
        [ConnectionStringParameter,
        Description("Define the body of the e-mail message.")]
        public string Body
        {
            get
            {
                return m_mailClient.Body;
            }
            set
            {
                m_mailClient.Body = value;
            }
        }

        /// <summary>
        /// Gets or sets the name or IP address of the SMTP server to be used for sending the message.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value being assigned is a null or empty string.</exception>
        [ConnectionStringParameter,
        Description("Define the name or IP address of the SMTP server to be used for sending the e-mail message.")]
        public string SmtpServer
        {
            get
            {
                return m_mailClient.SmtpServer;
            }
            set
            {
                m_mailClient.SmtpServer = value;
            }
        }

        /// <summary>
        /// Gets or sets a boolean value that indicating whether the message body is to be formatted as HTML.
        /// </summary>
        [ConnectionStringParameter,
        Description("Define the boolean value that indicating whether the message body is to be formatted as HTML."),
        DefaultValue(false)]
        public bool IsBodyHtml
        {
            get
            {
                return m_mailClient.IsBodyHtml;
            }
            set
            {
                m_mailClient.IsBodyHtml = value;
            }
        }

        /// <summary>
        /// Gets or sets the username used to authenticate to the SMTP server.
        /// </summary>
        [ConnectionStringParameter,
        Description("Define the username used to authenticate to the SMTP server."),
        DefaultValue("")]
        public string Username
        {
            get
            {
                return m_mailClient.Username;
            }
            set
            {
                m_mailClient.Username = value;
            }
        }

        /// <summary>
        /// Gets or sets the password used to authenticate to the SMTP server.
        /// </summary>
        [ConnectionStringParameter,
        Description("Define the password used to authenticate to the SMTP server."),
        DefaultValue("")]
        public string Password
        {
            get
            {
                return m_mailClient.Password;
            }
            set
            {
                m_mailClient.Password = value;
            }
        }

        /// <summary>
        /// Gets or sets the flag that determines whether to use SSL when communicating with the SMTP server.
        /// </summary>
        [ConnectionStringParameter,
        Description("Define the flag that determines whether to use SSL when communicating with the SMTP server."),
        DefaultValue(false)]
        public bool EnableSSL
        {
            get
            {
                return m_mailClient.EnableSSL;
            }
            set
            {
                m_mailClient.EnableSSL = value;
            }
        }

        /// <summary>
        /// Gets the flag indicating if this adapter supports temporal processing.
        /// </summary>
        [ConnectionStringParameter,
        Description("Define the flag indicating if this adapter supports temporal processing."),
        DefaultValue(false)]
        public override bool SupportsTemporalProcessing => m_supportsTemporalProcessing;

        #endregion

        #region [ Methods ]

        /// <summary>
        /// Initializes <see cref="EmailNotifier"/>.
        /// </summary>
        public override void Initialize()
        {
            const string MissingRequiredSetting = "\"{0}\" is missing from connection string settings - Example: expressionText = x > 0 || y > 0; variableList={{x = PPA:1; y = PPA:2}}";
            const string MissingRequiredMailSetting = "Missing required e-mail setting: \"{0}\"";
            Dictionary<string, string> settings;
            string setting;

            base.Initialize();
            settings = Settings;

            // Load required parameters
            if (!settings.TryGetValue("expressionText", out setting))
                throw new ArgumentException(string.Format(MissingRequiredSetting, "expressionText"));

            ExpressionText = settings["expressionText"];

            if (!settings.TryGetValue("variableList", out setting))
                throw new ArgumentException(string.Format(MissingRequiredSetting, "variableList"));

            VariableList = settings["variableList"];

            // Load optional parameters
            if (settings.TryGetValue("imports", out setting))
                Imports = setting;
            else
                Imports = DefaultImports;

            if (settings.TryGetValue("supportsTemporalProcessing", out setting))
                m_supportsTemporalProcessing = setting.ParseBoolean();
            else
                m_supportsTemporalProcessing = false;

            // Load required mail settings
            if (settings.TryGetValue("from", out setting) && !string.IsNullOrWhiteSpace(setting))
                From = setting;
            else
                throw new ArgumentException(string.Format(MissingRequiredMailSetting, "from"));

            if (settings.TryGetValue("subject", out setting) && !string.IsNullOrWhiteSpace(setting))
                Subject = setting;
            else
                throw new ArgumentException(string.Format(MissingRequiredMailSetting, "subject"));

            if (settings.TryGetValue("body", out setting) && !string.IsNullOrWhiteSpace(setting))
                Body = setting;
            else
                throw new ArgumentException(string.Format(MissingRequiredMailSetting, "body"));

            if (settings.TryGetValue("smtpServer", out setting) && !string.IsNullOrWhiteSpace(setting))
                SmtpServer = setting;
            else
                throw new ArgumentException(string.Format(MissingRequiredMailSetting, "smtpServer"));

            // Load optional mail settings
            if (settings.TryGetValue("toRecipients", out setting) && !string.IsNullOrWhiteSpace(setting))
                ToRecipients = setting;

            if (settings.TryGetValue("ccRecipients", out setting) && !string.IsNullOrWhiteSpace(setting))
                CcRecipients = setting;

            if (settings.TryGetValue("bccRecipients", out setting) && !string.IsNullOrWhiteSpace(setting))
                BccRecipients = setting;

            if (string.IsNullOrWhiteSpace(ToRecipients) && string.IsNullOrWhiteSpace(CcRecipients) && string.IsNullOrWhiteSpace(BccRecipients))
                throw new ArgumentException("At least one destination e-mail address for one of ToRecipients, CcRecipients or BccRecipients must be defined");

            if (settings.TryGetValue("isBodyHtml", out setting) && !string.IsNullOrWhiteSpace(setting))
                IsBodyHtml = setting.ParseBoolean();

            if (settings.TryGetValue("username", out setting) && !string.IsNullOrWhiteSpace(setting))
                Username = setting;

            if (settings.TryGetValue("password", out setting) && !string.IsNullOrWhiteSpace(setting))
                Password = setting;

            if (settings.TryGetValue("enableSSL", out setting) && !string.IsNullOrWhiteSpace(setting))
                EnableSSL = setting.ParseBoolean();
        }

        /// <summary>
        /// Publish <see cref="IFrame"/> of time-aligned collection of <see cref="IMeasurement"/> values that arrived within the
        /// concentrator's defined <see cref="ConcentratorBase.LagTime"/>.
        /// </summary>
        /// <param name="frame"><see cref="IFrame"/> of measurements with the same timestamp that arrived within <see cref="ConcentratorBase.LagTime"/> that are ready for processing.</param>
        /// <param name="index">Index of <see cref="IFrame"/> within a second ranging from zero to <c><see cref="ConcentratorBase.FramesPerSecond"/> - 1</c>.</param>
        protected override void PublishFrame(IFrame frame, int index)
        {
            ConcurrentDictionary<MeasurementKey, IMeasurement> measurements;
            IMeasurement measurement;
            string name;

            measurements = frame.Measurements;
            m_expressionContext.Variables.Clear();

            // Set the values of variables in the expression
            foreach (MeasurementKey key in m_keyMapping.Keys)
            {
                name = m_keyMapping[key];

                if (measurements.TryGetValue(key, out measurement))
                    m_expressionContext.Variables[name] = measurement.AdjustedValue;
                else
                    m_expressionContext.Variables[name] = double.NaN;
            }

            // Compile the expression if it has not been compiled already
            if ((object)m_expression == null)
                m_expression = m_expressionContext.CompileDynamic(m_aliasedExpressionText);

            // Evaluate the expression and generate the measurement
            bool result = m_expression.Evaluate().ToString().ParseBoolean();

            if (result)
                m_mailClient.Send();
        }

        // Adds a variable to the key-variable map
        private void AddVariable(string token)
        {
            // This determines whether the variable has been explicitly aliased or not
            // and then delegates the work to the appropriate helper method
            if (token.Contains('='))
                AddAliasedVariable(token);
            else
                AddNotAliasedVariable(token);
        }

        // Adds an explicitly aliased variable to the key-variable map
        private void AddAliasedVariable(string token)
        {
            string[] splitToken = token.Split('=');
            MeasurementKey key;
            string alias;

            if (splitToken.Length > 2)
                throw new FormatException($"Too many equals signs: {token}");

            key = GetKey(splitToken[1].Trim());
            alias = splitToken[0].Trim();
            AddMapping(key, alias);
        }

        // Adds a variable to the key-variable map which has not been explicitly aliased
        private void AddNotAliasedVariable(string token)
        {
            string alias;
            MeasurementKey key;

            token = token.Trim();
            m_nonAliasedTokens.Add(-token.Length, token);

            key = GetKey(token);
            alias = token.ReplaceCharacters('_', c => !char.IsLetterOrDigit(c));

            // Ensure that the generated alias is unique
            while (m_variableNames.Contains(alias))
                alias += "_";

            AddMapping(key, alias);
        }

        // Adds the given mapping to the key-variable map
        private void AddMapping(MeasurementKey key, string alias)
        {
            if (m_variableNames.Contains(alias))
                throw new ArgumentException($"Variable name is not unique: {alias}");

            m_variableNames.Add(alias);
            m_keyMapping.Add(key, alias);
        }

        // Performs alias replacement on tokens that were not explicitly aliased
        private void PerformAliasReplacement()
        {
            StringBuilder aliasedExpressionTextBuilder = new StringBuilder(m_expressionText);
            MeasurementKey key;
            string alias;

            foreach (string token in m_nonAliasedTokens.Values)
            {
                key = GetKey(token);
                alias = m_keyMapping[key];
                aliasedExpressionTextBuilder.Replace(token, alias);
            }

            m_aliasedExpressionText = aliasedExpressionTextBuilder.ToString();
        }

        // Gets a measurement key based on a token which may be either a signal ID or measurement key
        private static MeasurementKey GetKey(string token)
        {
            Guid signalID;
            return Guid.TryParse(token, out signalID) ? MeasurementKey.LookUpBySignalID(signalID) : MeasurementKey.Parse(token);
        }

        #endregion
    }
}
