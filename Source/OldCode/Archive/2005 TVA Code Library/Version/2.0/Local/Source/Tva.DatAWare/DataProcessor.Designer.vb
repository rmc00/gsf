Partial Class DataProcessor
    Inherits System.ComponentModel.Component

    <System.Diagnostics.DebuggerNonUserCode()> _
    Public Sub New(ByVal Container As System.ComponentModel.IContainer)
        MyClass.New()

        'Required for Windows.Forms Class Composition Designer support
        Container.Add(Me)

    End Sub

    <System.Diagnostics.DebuggerNonUserCode()> _
    Public Sub New()
        MyBase.New()

        m_toArchiveFile = Tva.Collections.KeyedProcessQueue(Of Guid, IPacket).CreateRealTimeQueue(AddressOf SaveToArchiveFile)
        m_toMetadataFile = Tva.Collections.KeyedProcessQueue(Of Guid, IPacket).CreateRealTimeQueue(AddressOf SaveToMetadataFile)
        m_toReplySender = Tva.Collections.KeyedProcessQueue(Of Guid, IPacket).CreateRealTimeQueue(AddressOf ReplyToSender)

        'This call is required by the Component Designer.
        InitializeComponent()

    End Sub

    'Component overrides dispose to clean up the component list.
    <System.Diagnostics.DebuggerNonUserCode()> _
    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        If disposing AndAlso components IsNot Nothing Then
            components.Dispose()
        End If
        MyBase.Dispose(disposing)
    End Sub

    'Required by the Component Designer
    Private components As System.ComponentModel.IContainer

    'NOTE: The following procedure is required by the Component Designer
    'It can be modified using the Component Designer.
    'Do not modify it using the code editor.
    <System.Diagnostics.DebuggerStepThrough()> _
    Private Sub InitializeComponent()
        Me.components = New System.ComponentModel.Container
        Me.DataParser = New Tva.DatAWare.DataParser(Me.components)
        CType(Me.DataParser, System.ComponentModel.ISupportInitialize).BeginInit()
        '
        'DataParser
        '
        CType(Me.DataParser, System.ComponentModel.ISupportInitialize).EndInit()

    End Sub
    Friend WithEvents DataParser As Tva.DatAWare.DataParser

End Class
