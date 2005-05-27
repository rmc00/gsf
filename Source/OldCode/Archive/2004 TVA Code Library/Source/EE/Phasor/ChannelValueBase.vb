'***********************************************************************
'  ChannelValueBase.vb - Channel data value base class
'  Copyright � 2004 - TVA, all rights reserved
'
'  Build Environment: VB.NET, Visual Studio 2003
'  Primary Developer: James R Carroll, System Analyst [TVA]
'      Office: COO - TRNS/PWR ELEC SYS O, CHATTANOOGA, TN - MR 2W-C
'       Phone: 423/751-2827
'       Email: jrcarrol@tva.gov
'
'  Code Modification History:
'  ---------------------------------------------------------------------
'  3/7/2005 - James R Carroll
'       Initial version of source generated
'
'***********************************************************************

Namespace EE.Phasor

    ' This class represents the common implementation of the protocol independent representation of any kind of data value.
    Public MustInherit Class ChannelValueBase

        Inherits ChannelBase
        Implements IChannelValue

        Private m_definition As IChannelDefinition

        Protected Sub New()            

        End Sub

        Protected Sub New(ByVal channelDefinition As IChannelDefinition)

            m_definition = channelDefinition

        End Sub

        ' Dervied classes are expected to expose a Protected Sub New(ByVal channelValue As IChannelValue)
        Protected Sub New(ByVal channelValue As IChannelValue)

            Me.New(channelValue.Definition)

        End Sub

        Public Property Definition() As IChannelDefinition Implements IChannelValue.Definition
            Get
                Return m_definition
            End Get
            Set(ByVal Value As IChannelDefinition)
                m_definition = Value
            End Set
        End Property

        Public Overridable ReadOnly Property DataFormat() As DataFormat Implements IChannelValue.DataFormat
            Get
                Return m_definition.DataFormat
            End Get
        End Property

        Public MustOverride ReadOnly Property IsEmpty() As Boolean Implements IChannelValue.IsEmpty

        Public MustOverride ReadOnly Property Values() As Double() Implements IChannelValue.Values

    End Class

End Namespace
