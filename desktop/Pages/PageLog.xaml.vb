Imports System.Diagnostics
Imports System.IO
Imports System.Windows
Imports Microsoft.Win32

Namespace PCLNeo
    ''' <summary>日志页：实时日志、保存、结束游戏进程</summary>
    Public Class PageLog
        Inherits Page

        Private _lines As New Text.StringBuilder()

        Public Sub New()
            InitializeComponent()
            AddHandler EventBus.OnLog, AddressOf OnLog
            Refresh()
        End Sub

        Public Sub AppendLog(Message As String)
            SyncLock _lines
                _lines.AppendLine(Message)
            End SyncLock
            Dispatcher.InvokeAsync(Sub()
                                       TxtLog.AppendText(Message & vbCrLf)
                                       TxtLog.ScrollToEnd()
                                   End Sub)
        End Sub

        Public Sub Refresh()
            TxtLog.Text = GetLogText()
            TxtLog.ScrollToEnd()
            TxtState.Text = If(ModLaunch.IsRunning, "游戏运行中", "未运行")
        End Sub

        Private Sub OnLog(Message As String, Level As LogLevel)
            AppendLog(Message)
        End Sub

        Private Sub BtnRefresh_Click() Handles BtnRefresh.Click
            Refresh()
        End Sub

        Private Sub BtnSave_Click() Handles BtnSave.Click
            Dim dlg As New SaveFileDialog() With {.Filter = "日志文件|*.log|文本|*.txt", .FileName = "PCLNeo-" & DateTime.Now.ToString("yyyyMMdd-HHmmss") & ".log"}
            If dlg.ShowDialog() = True Then
                File.WriteAllText(dlg.FileName, TxtLog.Text)
                TxtState.Text = "已保存"
            End If
        End Sub

        Private Sub BtnOpenFile_Click() Handles BtnOpenFile.Click
            Dim f = Path.Combine(PathData, "PCLNeo.log")
            If File.Exists(f) Then Process.Start("notepad.exe", f)
        End Sub

        Private Sub BtnKill_Click() Handles BtnKill.Click
            ModLaunch.KillGame()
            TxtState.Text = "已结束游戏进程"
        End Sub

    End Class
End Namespace
