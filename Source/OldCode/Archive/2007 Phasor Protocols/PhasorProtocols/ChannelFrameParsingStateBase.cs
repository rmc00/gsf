using System.Diagnostics;
using System;
////using PCS.Common;
using System.Collections;
using PCS.Interop;
using Microsoft.VisualBasic;
using PCS;
using System.Collections.Generic;
////using PCS.Interop.Bit;
using System.Linq;

//*******************************************************************************************************
//  ChannelFrameParsingStateBase.vb - Channel data frame parsing state base class
//  Copyright © 2009 - TVA, all rights reserved - Gbtc
//
//  Build Environment: VB.NET, Visual Studio 2008
//  Primary Developer: J. Ritchie Carroll, Operations Data Architecture [TVA]
//      Office: COO - TRNS/PWR ELEC SYS O, CHATTANOOGA, TN - MR 2W-C
//       Phone: 423/751-2827
//       Email: jrcarrol@tva.gov
//
//  Code Modification History:
//  -----------------------------------------------------------------------------------------------------
//  01/14/2005 - J. Ritchie Carroll
//       Initial version of source generated
//
//*******************************************************************************************************

namespace PCS.PhasorProtocols
{
    /// <summary>This class represents the protocol independent common implementation the parsing state used by any frame of data that can be sent or received from a PMU.</summary>
    [CLSCompliant(false)]
    public abstract class ChannelFrameParsingStateBase<T> : ChannelParsingStateBase, IChannelFrameParsingState<T> where T : IChannelCell
    {
        //private IChannelCellCollection<T> m_cells;
        private int m_cellCount;
        private int m_parsedBinaryLength;
        private CreateNewCellFunctionSignature<T> m_createNewCellFunction;
        //private object m_extraState;

        protected ChannelFrameParsingStateBase(/*IChannelCellCollection<T> cells,*/ int parsedBinaryLength, CreateNewCellFunctionSignature<T> createNewCellFunction)
        {

            //m_cells = cells;
            m_parsedBinaryLength = parsedBinaryLength;
            m_createNewCellFunction = createNewCellFunction;

        }

        public virtual CreateNewCellFunctionSignature<T> CreateNewCellFunction
        {
            get
            {
                return m_createNewCellFunction;
            }
        }

        //public virtual IChannelCellCollection<T> Cells
        //{
        //    get
        //    {
        //        return m_cells;
        //    }
        //}

        public virtual int CellCount
        {
            get
            {
                return m_cellCount;
            }
            set
            {
                m_cellCount = value;
            }
        }

        public virtual int ParsedBinaryLength
        {
            get
            {
                return m_parsedBinaryLength;
            }
            set
            {
                m_parsedBinaryLength = value;
            }
        }

        ///// <summary>
        ///// Gets or sets reference to extra protocol specific state.
        ///// </summary>
        //public object ExtraState
        //{
        //    get
        //    {
        //        return m_extraState;
        //    }
        //    set
        //    {
        //        m_extraState = value;
        //    }
        //}
    }
}
