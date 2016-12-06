﻿//******************************************************************************************************
//  LogEventPublisherInternal.cs - Gbtc
//
//  Copyright © 2016, Grid Protection Alliance.  All Rights Reserved.
//
//  Licensed to the Grid Protection Alliance (GPA) under one or more contributor license agreements. See
//  the NOTICE file distributed with this work for additional information regarding copyright ownership.
//  The GPA licenses this file to you under the Eclipse Public License -v 1.0 (the "License"); you may
//  not use this file except in compliance with the License. You may obtain a copy of the License at:
//
//      http://www.opensource.org/licenses/eclipse-1.0.php
//
//  Unless agreed to in writing, the subject software distributed under the License is distributed on an
//  "AS-IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. Refer to the
//  License for the specific language governing permissions and limitations.
//
//  Code Modification History:
//  ----------------------------------------------------------------------------------------------------
//  10/24/2016 - Steven E. Chisholm
//       Generated original version of source code. 
//       
//
//******************************************************************************************************

using System;

namespace GSF.Diagnostics
{
    /// <summary>
    /// A publisher for a specific event of a <see cref="LogPublisher"/>.
    /// </summary>
    internal class LogEventPublisherInternal
    {
        private static LogPublisher Log = Logger.CreatePublisher(typeof(Logger), MessageClass.Component);
        private static LogEventPublisher MessageSuppressionIndication = Log.RegisterEvent(MessageLevel.NA, MessageFlags.SystemHealth, "Log Message Suppression Occurred");

        private readonly LogEventPublisherDetails m_owner;
        private readonly LogSuppressionEngine m_supressionEngine;
        private readonly LogPublisherInternal m_publisher;
        private readonly LoggerInternal m_logger;
        private int m_stackTraceDepth;
        private LogMessageAttributes m_attributes;
        private ShortTime m_suppressionMessageNextPublishTime;
        private long m_messagesSuppressed = 0;

        /// <summary>
        /// Gets/Sets if a log message should be generated when message suppression occurs.
        /// Default is true;
        /// </summary>
        public bool ShouldRaiseMessageSupressionNotifications = true;

        /// <summary>
        /// Creates a <see cref="LogEventPublisherInternal"/>.
        /// </summary>
        /// <param name="attributes"></param>
        /// <param name="owner">the owner of the log messages.</param>
        /// <param name="publisher">the publisher that is used to raise messages</param>
        /// <param name="logger">the callback for all new messages that are generated.</param>
        /// <param name="stackTraceDepth"></param>
        /// <param name="messagesPerSecond"></param>
        /// <param name="burstLimit"></param>
        internal LogEventPublisherInternal(LogMessageAttributes attributes, LogEventPublisherDetails owner, LogPublisherInternal publisher, LoggerInternal logger, int stackTraceDepth, double messagesPerSecond, int burstLimit)
        {
            if (owner == null)
                throw new ArgumentNullException(nameof(owner));
            if (publisher == null)
                throw new ArgumentNullException(nameof(publisher));
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));

            m_attributes = attributes;
            var supression = new LogSuppressionEngine(messagesPerSecond, burstLimit);

            m_owner = owner;
            m_logger = logger;
            m_stackTraceDepth = 0;
            m_supressionEngine = supression;
            m_publisher = publisher;
            m_stackTraceDepth = stackTraceDepth;
            m_suppressionMessageNextPublishTime = ShortTime.Now;
        }

        /// <summary>
        /// Checks if messages generated by this publisher will be received by a subscriber.
        /// </summary>
        public bool HasSubscribers => m_publisher.HasSubscribers(m_attributes);

        /// <summary>
        /// The attributes that will be used if not specified.
        /// </summary>
        public LogMessageAttributes DefaultAttributes => m_attributes;

        /// <summary>
        /// Raises a log message with the provided data.
        /// </summary>
        /// <param name="overriddenAttributes">attributes to use with this message</param>
        /// <param name="message"></param>
        /// <param name="details">A long text field with the details of the message.</param>
        /// <param name="exception">An exception object if one is provided.</param>
        /// <param name="initialStackMessage"></param>
        /// <param name="initialStackTrace"></param>
        public void Publish(LogMessageAttributes? overriddenAttributes, string message, string details, Exception exception, LogStackMessages initialStackMessage, LogStackTrace initialStackTrace)
        {
            if (Logger.ShouldSuppressLogMessages)
                return;

            LogMessageAttributes attributes = overriddenAttributes ?? m_attributes;

            if (!m_publisher.HasSubscribers(attributes))
                return;

            if (message == null)
                message = string.Empty;
            if (details == null)
                details = string.Empty;

            var suppressionFlags = m_supressionEngine.IncrementPublishCount();
            if (suppressionFlags != MessageSuppression.None)
            {
                m_messagesSuppressed++;

                if (ShouldRaiseMessageSupressionNotifications && m_suppressionMessageNextPublishTime <= ShortTime.Now)
                {
                    m_suppressionMessageNextPublishTime = ShortTime.Now.AddSeconds(10);
                    MessageSuppressionIndication.Publish($"Message Suppression Is Occurring To: '{ m_owner.TypeData.TypeName }' {m_messagesSuppressed.ToString()} total messages have been suppressed.", m_owner.ToString());
                }

                attributes += suppressionFlags;
                if (!m_publisher.HasSubscribers(attributes))
                    return;
            }

            LogStackMessages currentStackMessages = Logger.GetStackMessages();
            LogStackTrace currentStackTrace = LogStackTrace.Empty;
            if (m_stackTraceDepth > 0)
            {
                currentStackTrace = new LogStackTrace(true, 2, m_stackTraceDepth);
            }
            else if (exception != null || attributes.Level >= MessageLevel.Error)
            {
                currentStackTrace = new LogStackTrace(true, 2, 10);
            }

            var logMessage = new LogMessage(m_owner, initialStackMessage, initialStackTrace, currentStackMessages, currentStackTrace, attributes, message, details, exception);
            m_logger.OnNewMessage(logMessage, m_publisher);
        }

    }
}
