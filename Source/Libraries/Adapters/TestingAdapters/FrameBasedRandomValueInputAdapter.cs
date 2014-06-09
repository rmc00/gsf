﻿//******************************************************************************************************
//  FrameBasedRandomValueInputAdapter.cs - Gbtc
//
//  Copyright © 2014, Grid Protection Alliance.  All Rights Reserved.
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
//  04/26/2014 - Stephen C. Wills
//       Generated original version of source code.
//
//******************************************************************************************************

using System;
using System.Collections.Generic;
using System.ComponentModel;
using GSF;
using GSF.Threading;
using GSF.TimeSeries;
using GSF.TimeSeries.Adapters;

namespace TestingAdapters
{
    /// <summary>
    /// Represents a class used to stream frames of measurements with random values meant to simulate a frame-based input source.
    /// </summary>
    [Description("Random Frames: streams frames of measurements with random values meant to simulate a frame-based input source.")]
    public class FrameBasedRandomValueInputAdapter : InputAdapterBase
    {
        #region [ Members ]

        // Constants

        /// <summary>
        /// Default value for the <see cref="PublishRate"/> property.
        /// </summary>
        public const double DefaultPublishRate = 30.0;
        /// <summary>
        /// The default number of milliseconds for the dealy;
        /// </summary>
        public const double DefaultLatency = 125.0;
        /// <summary>
        /// The default jitter in the channel (1 standard deviation);
        /// </summary>
        public const double DefaultJitter = 30;


        // Fields
        private double m_publishRate;
        private GaussianDistribution m_latency;
        private ScheduledTask m_timer;
        private ScheduledTask m_statusUpdate;
        private Random m_random;
        private long m_nextPublicationTime;
        private long m_nextPublicationTimeWithLatency;
        private int m_unprocessedMeasurements;
        private bool m_disposed;

        #endregion

        #region [ Properties ]

        /// <summary>
        /// Gets or sets the number of frames generated by the adapter per second.
        /// </summary>
        [ConnectionStringParameter,
        DefaultValue(DefaultPublishRate),
        Description("Defines the number of frames generated by the adapter per second.")]
        public double PublishRate
        {
            get
            {
                return m_publishRate;
            }
            set
            {
                m_publishRate = value;
            }
        }

        /// <summary>
        /// Gets the flag indicating if this adapter supports temporal processing.
        /// </summary>
        public override bool SupportsTemporalProcessing
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets flag that determines if the data input connects asynchronously.
        /// </summary>
        /// <remarks>
        /// Derived classes should return true when data input source is connects asynchronously, otherwise return false.
        /// </remarks>
        protected override bool UseAsyncConnect
        {
            get
            {
                return false;
            }
        }

        #endregion

        #region [ Methods ]

        /// <summary>
        /// Initializes <see cref="MovingValueInputAdapter"/>.
        /// </summary>
        public override void Initialize()
        {
            m_random = new Random(Guid.NewGuid().GetHashCode());
            Dictionary<string, string> settings;
            string setting;

            base.Initialize();
            settings = Settings;

            if (!settings.TryGetValue("publishRate", out setting) || !double.TryParse(setting, out m_publishRate))
                m_publishRate = DefaultPublishRate;

            if (m_publishRate <= 0.0D)
                throw new InvalidOperationException(string.Format("publishRate({0}) must be greater than zero", m_publishRate));

            double latency;
            double jitter;
            if (!settings.TryGetValue("latency", out setting) || !double.TryParse(setting, out latency))
                latency = DefaultLatency;

            if (!settings.TryGetValue("jitter", out setting) || !double.TryParse(setting, out jitter))
                jitter = DefaultJitter;

            //Clips at 3 time the latency and 1/3 latency
            m_latency = new GaussianDistribution(latency, jitter, latency / 3, latency * 3);
        }

        /// <summary>
        /// Gets a short one-line status of this <see cref="MovingValueInputAdapter"/>.
        /// </summary>
        /// <param name="maxLength">Maximum number of available characters for display.</param>
        /// <returns>
        /// A short one-line summary of the current status of this <see cref="MovingValueInputAdapter"/>.
        /// </returns>
        public override string GetShortStatus(int maxLength)
        {
            return string.Format("{0} random values generated so far...", ProcessedMeasurements).CenterText(maxLength);
        }

        /// <summary>
        /// Attempts to connect to data input source.
        /// </summary>
        protected override void AttemptConnection()
        {
            m_nextPublicationTime = ToPublicationTime(DateTime.UtcNow.Ticks);
            m_nextPublicationTimeWithLatency = m_nextPublicationTime + (long)(m_latency.Next() * TimeSpan.TicksPerMillisecond);

            if ((object)m_timer == null)
            {
                m_timer = new ScheduledTask(ThreadingMode.ThreadPool);
                m_timer.Running += m_timer_Running;
            }
            if ((object)m_statusUpdate == null)
            {
                m_statusUpdate = new ScheduledTask(ThreadingMode.ThreadPool);
                m_statusUpdate.Running += m_statusUpdate_Running;
            }
            m_timer.Start();
            m_statusUpdate.Start(10000);
        }

        void m_timer_Running(object sender, EventArgs<ScheduledTaskRunningReason> eventArgs)
        {
            if (eventArgs.Argument == ScheduledTaskRunningReason.Disposing)
                return;
            PublishFrames();
        }

        void m_statusUpdate_Running(object sender, EventArgs<ScheduledTaskRunningReason> eventArgs)
        {
            if (eventArgs.Argument == ScheduledTaskRunningReason.Disposing)
                return;

            if (!Enabled)
                return;

            if (m_unprocessedMeasurements > 10 * m_publishRate)
            {
                OnStatusMessage(string.Format("{0} unprocessed messages", m_unprocessedMeasurements));
            }
            m_statusUpdate.Start(10000);

        }

        /// <summary>
        /// Attempts to disconnect from data input source.
        /// </summary>
        protected override void AttemptDisconnection()
        {
            if ((object)m_timer != null)
            {
                m_timer.Dispose();
                m_timer = null;
            }
            if ((object)m_statusUpdate != null)
            {
                m_statusUpdate.Dispose();
                m_statusUpdate = null;
            }
        }

        /// <summary>
        /// Releases the unmanaged resources used by the <see cref="MovingValueInputAdapter"/> object and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected override void Dispose(bool disposing)
        {
            if (!m_disposed)
            {
                try
                {
                    if (disposing)
                    {
                        if ((object)m_timer != null)
                        {
                            m_timer.Dispose();
                            m_timer = null;
                        }
                    }
                }
                finally
                {
                    m_disposed = true;          // Prevent duplicate dispose.
                    base.Dispose(disposing);    // Call base class Dispose().
                }
            }
        }

        private void PublishFrames()
        {
            while (m_nextPublicationTimeWithLatency - DateTime.UtcNow.Ticks < 1 * TimeSpan.TicksPerMillisecond)
            {
                if (!Enabled)
                    return;

                IMeasurement[] outputMeasurements = OutputMeasurements;
                IMeasurement[] newMeasurements = new IMeasurement[outputMeasurements.Length];
                for (int x = 0; x < outputMeasurements.Length; x++)
                {
                    newMeasurements[x] = Measurement.Clone(outputMeasurements[x], m_random.NextDouble(), m_nextPublicationTime);
                }
                OnNewMeasurements(newMeasurements);

                m_nextPublicationTime = GetNextPublicationTime(m_nextPublicationTime);
                m_nextPublicationTimeWithLatency = m_nextPublicationTime + (long)(m_latency.Next() * TimeSpan.TicksPerMillisecond);

                m_unprocessedMeasurements = (int)((DateTime.UtcNow.Ticks - m_nextPublicationTime) / (Ticks.PerSecond / m_publishRate));

            }

            if (!Enabled)
                return;

            long delay = ((m_nextPublicationTimeWithLatency - DateTime.UtcNow.Ticks) / TimeSpan.TicksPerMillisecond);

            if (delay < 1)
                m_timer.Start();
            else if (delay > 10000)
                m_timer.Start(10000);
            else
                m_timer.Start((int)delay);

        }

        private long GetNextPublicationTime(long time)
        {
            double interval = Ticks.PerSecond / m_publishRate;
            long nextTime = (long)Math.Round(time + interval);
            return ToPublicationTime(nextTime);
        }

        private long ToPublicationTime(long time)
        {
            double interval = Ticks.PerSecond / m_publishRate;
            long seconds = time / Ticks.PerSecond;
            long subseconds = time % Ticks.PerSecond;
            long index = (long)Math.Round(subseconds / interval);
            return (seconds * Ticks.PerSecond) + (long)Math.Round(index * interval);
        }

        #endregion

    }
}