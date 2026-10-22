Imports System.Windows
Imports System.Windows.Media

Namespace PCLNeo
    ''' <summary>主窗口：左侧导航 + 页面容器 + 全局进度条</summary>
    Public Class MainWindow
        Inherits Window

        Private _pageHome As PageHome
        Private _pageDownload As PageDownload
        Private _pageVersion As PageVersion
        Private _pageSetup As PageSetup
        Private _pageLog As PageLog
        Private _current As Object = Nothing

        Public Sub New()
            InitializeComponent()
            ModBase.EnsureDirs()
            TxtVersion.Text = $"{AppName} {AppVersion}"
            AddHandler EventBus.OnProgress, AddressOf OnProgress
            AddHandler EventBus.OnLog, AddressOf OnLog
            NavigateHome()
        End Sub

        Private Sub OnProgress(TaskName As String, Progress As Double, Status As String)
            Dispatcher.InvokeAsync(Sub()
                                       TxtProgress.Text = $"{TaskName}：{Status}"
                                       If Progress < 0 Then
                                           BarProgress.IsIndeterminate = True
                                       Else
                                           BarProgress.IsIndeterminate = False
                                           BarProgress.Value = Progress * 100
                                       End If
                                   End Sub)
        End Sub

        Private Sub OnLog(Message As String, Level As LogLevel)
            If Level >= LogLevel.Info Then
                Dispatcher.InvokeAsync(Sub() TxtStatus.Text = Message)
            End If
            If _pageLog IsNot Nothing Then _pageLog.AppendLog(Message)
        End Sub

        ' ---------- 导航 ----------
        Private Sub SetSelected(btn As Button)
            For Each b In New Button() {BtnNavHome, BtnNavDownload, BtnNavVersion, BtnNavSetup, BtnNavLog}
                b.Style = CType(FindResource("BtnNav"), Style)
            Next
            btn.Style = CType(FindResource("BtnNavSel"), Style)
        End Sub

        Public Sub NavigateHome() Handles BtnNavHome.Click
            If _pageHome Is Nothing Then _pageHome = New PageHome()
            SetSelected(BtnNavHome)
            MainFrame.Content = _pageHome
            _current = _pageHome
        End Sub
        Private Sub NavigateDownload() Handles BtnNavDownload.Click
            If _pageDownload Is Nothing Then _pageDownload = New PageDownload()
            SetSelected(BtnNavDownload)
            MainFrame.Content = _pageDownload
            _current = _pageDownload
        End Sub
        Private Sub NavigateVersion() Handles BtnNavVersion.Click
            If _pageVersion Is Nothing Then _pageVersion = New PageVersion()
            SetSelected(BtnNavVersion)
            MainFrame.Content = _pageVersion
            _current = _pageVersion
            _pageVersion.RefreshVersions()
        End Sub
        Private Sub NavigateSetup() Handles BtnNavSetup.Click
            If _pageSetup Is Nothing Then _pageSetup = New PageSetup()
            SetSelected(BtnNavSetup)
            MainFrame.Content = _pageSetup
            _current = _pageSetup
        End Sub
        Private Sub NavigateLog() Handles BtnNavLog.Click
            If _pageLog Is Nothing Then _pageLog = New PageLog()
            SetSelected(BtnNavLog)
            MainFrame.Content = _pageLog
            _current = _pageLog
            _pageLog.Refresh()
        End Sub

        ''' <summary>供页面调用：切换到下载页并定位到版本下载</summary>
        Public Sub GoDownload(Optional TabIndex As Integer = 0)
            If _pageDownload Is Nothing Then _pageDownload = New PageDownload()
            SetSelected(BtnNavDownload)
            MainFrame.Content = _pageDownload
            _current = _pageDownload
            _pageDownload.SelectTab(TabIndex)
        End Sub

        Public ReadOnly Property CurrentPage As Object
            Get
                Return _current
            End Get
        End Property

    End Class
End Namespace
