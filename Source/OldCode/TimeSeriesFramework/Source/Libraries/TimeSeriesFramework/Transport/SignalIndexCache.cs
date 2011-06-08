﻿//******************************************************************************************************
//  SignalIndexCache.cs - Gbtc
//
//  Copyright © 2010, Grid Protection Alliance.  All Rights Reserved.
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
//  05/15/2011 - J. Ritchie Carroll
//       Generated original version of source code.
//
//******************************************************************************************************

using System;
using System.Collections.Concurrent;
using System.Linq;

namespace TimeSeriesFramework.Transport
{
    /// <summary>
    /// Represents a serializable <see cref="Guid"/> signal ID to <see cref="ushort"/> index cross reference.
    /// </summary>
    /// <remarks>
    /// This class is used to create a runtime index to be used for data exchange so that a 16-bit integer
    /// is exchanged in the data packets for signal identification instead of the 128-bit Guid signal ID
    /// to reduce bandwidth required for signal exchange. This means the total number of unique signal
    /// IDs that could be exchanged using this method in a single session is 65,535. This number seems
    /// reasonable for the currently envisioned use cases, however, multiple sessions each with their own
    /// runtime signal index cache could be established if this is a limitation for a given data set.
    /// </remarks>
    [Serializable]
    public class SignalIndexCache
    {
        #region [ Members ]

        // Fields
        private Guid m_subscriberID;
        private ConcurrentDictionary<ushort, Tuple<Guid, MeasurementKey>> m_reference;
        private MeasurementKey[] m_unauthorizedKeys;

        [NonSerialized] // SignalID reverse lookup runtime cache
        private ConcurrentDictionary<Guid, ushort> m_signalIDCache;

        [NonSerialized] // MeasurementKey reverse lookup runtime cache
        private ConcurrentDictionary<MeasurementKey, ushort> m_keyCache;

        #endregion

        #region [ Constructors ]

        /// <summary>
        /// Creates a new <see cref="SignalIndexCache"/> instance.
        /// </summary>
        /// <param name="subscriberID"><see cref="Guid"/> based subscriber ID.</param>
        public SignalIndexCache(Guid subscriberID)
        {
            m_subscriberID = subscriberID;
            m_reference = new ConcurrentDictionary<ushort, Tuple<Guid, MeasurementKey>>();
        }

        #endregion

        #region [ Properties ]

        /// <summary>
        /// Gets the <see cref="Guid"/> based subscriber ID of this <see cref="SignalIndexCache"/>.
        /// </summary>
        public Guid SubscriberID
        {
            get
            {
                return m_subscriberID;
            }
        }

        /// <summary>
        /// Gets or sets integer signal index cross reference dictionary.
        /// </summary>
        public ConcurrentDictionary<ushort, Tuple<Guid, MeasurementKey>> Reference
        {
            get
            {
                return m_reference;
            }
            set
            {
                m_reference = value;
            }
        }

        /// <summary>
        /// Gets reference to array of requested input measurement keys that were authorized.
        /// </summary>
        public MeasurementKey[] AuthorizedKeys
        {
            get
            {
                return m_reference.Select(kvp => kvp.Value.Item2).ToArray();
            }
        }

        /// <summary>
        /// Gets or sets reference to array of requested input measurement keys that were unauthorized.
        /// </summary>
        public MeasurementKey[] UnauthorizedKeys
        {
            get
            {
                return m_unauthorizedKeys;
            }
            set
            {
                m_unauthorizedKeys = value;
            }
        }

        #endregion

        #region [ Methods ]

        /// <summary>
        /// Gets runtime signal index for given <see cref="Guid"/> signal ID.
        /// </summary>
        /// <param name="signalID"><see cref="Guid"/> signal ID used to lookup associated runtime signal index.</param>
        /// <returns>Runtime signal index for given <see cref="Guid"/> <paramref name="signalID"/>.</returns>
        public ushort GetSignalIndex(Guid signalID)
        {
            // We create a runtime cache of these indexes by signal ID since they will be looked up over and over
            if (m_signalIDCache == null)
                m_signalIDCache = new ConcurrentDictionary<Guid, ushort>();

            return m_signalIDCache.GetOrAdd(signalID, id => m_reference.First(kvp => kvp.Value.Item1 == id).Key);
        }

        /// <summary>
        /// Gets runtime signal index for given <see cref="MeasurementKey"/>.
        /// </summary>
        /// <param name="key"><see cref="MeasurementKey"/> used to lookup associated runtime signal index.</param>
        /// <returns>Runtime signal index for given <see cref="MeasurementKey"/> <paramref name="key"/>.</returns>
        public ushort GetSignalIndex(MeasurementKey key)
        {
            // We create a runtime cache of these indexes by measurement key since they will be looked up over and over
            if (m_keyCache == null)
                m_keyCache = new ConcurrentDictionary<MeasurementKey, ushort>();

            return m_keyCache.GetOrAdd(key, mk => m_reference.First(kvp => kvp.Value.Item2.Equals(mk)).Key);
        }

        #endregion
    }
}
