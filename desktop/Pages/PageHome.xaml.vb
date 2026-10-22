Imports System.Diagnostics
Imports System.Threading.Tasks
Imports Newtonsoft.Json.Linq
Imports System.Windows
Imports System.Windows.Controls

Namespace PCLNeo
    ''' <summary>启动页：版本 / 账号 / Java 选择、启动游戏、三种登录</summary>
    Public Class PageHome
        Inherits Page

        Private _accounts As List(Of Account)
        Private _javas As List(Of JavaInfo)

        Public Sub New()
            InitializeComponent()
            AddHandler EventBus.OnLog, AddressOf OnLog
            RefreshVersions()
            RefreshAccounts()
            RefreshJavas()
        End Sub

        ' ---------- 刷新 ----------
        Public Sub RefreshVersions()
            Dim list = GetLocalVersions()
            ComboVersion.ItemsSource = list
            Dim last = SetupGet("LastVersion", "")
            If last <> "" AndAlso list.Contains(last) Then
                ComboVersion.SelectedItem = last
            ElseIf list.Count > 0 Then
                ComboVersion.SelectedIndex = 0
            End If
        End Sub

        Private Sub RefreshAccounts()
            _accounts = ModAuth.GetAccounts()
            ComboAccount.ItemsSource = _accounts
            If _accounts.Count > 0 Then ComboAccount.SelectedIndex = 0
        End Sub

        Private Sub RefreshJavas()
            Task.Run(Sub()
                         Dim list = ModJava.ScanJava()
                         Dispatcher.InvokeAsync(Sub()
                                                    _javas = list
                                                    ComboJava.ItemsSource = list
                                                    If list.Count > 0 Then ComboJava.SelectedIndex = 0
                                                End Sub)
                     End Sub)
        End Sub

        Private Sub OnLog(Message As String, Level As LogLevel)
            Dispatcher.InvokeAsync(Sub()
                                       TxtLog.AppendText(Message & vbCrLf)
                                       TxtLog.ScrollToEnd()
                                   End Sub)
        End Sub

        ' ---------- 启动 ----------
        Private Sub BtnLaunch_Click() Handles BtnLaunch.Click
            If ComboVersion.SelectedItem Is Nothing Then
                MessageBox.Show("请先下载一个游戏版本", "提示", MessageBoxButton.OK, MessageBoxImage.Information)
                Exit Sub
            End If
            Dim version = ComboVersion.SelectedItem.ToString()
            Dim acc = TryCast(ComboAccount.SelectedItem, Account)
            If acc Is Nothing Then acc = ModAuth.LoginOffline("Player")
            SetupSet("LastVersion", version)
            Dim javaPath As String = ""
            If ComboJava.SelectedItem IsNot Nothing Then javaPath = CType(ComboJava.SelectedItem, JavaInfo).Path

            BtnLaunch.IsEnabled = False
            TxtLaunch.Text = "正在启动…"
            Task.Run(Sub()
                         Try
                             ModLaunch.Launch(version, acc, javaPath)
                             Dispatcher.InvokeAsync(Sub() TxtLaunch.Text = "游戏已启动")
                         Catch ex As Exception
                             Log("启动失败：" & ex.Message, LogLevel.Error)
                             Dispatcher.InvokeAsync(Sub()
                                                        TxtLaunch.Text = "启动失败：" & ex.Message
                                                        MessageBox.Show(ex.Message, "启动失败", MessageBoxButton.OK, MessageBoxImage.Error)
                                                    End Sub)
                         Finally
                             Dispatcher.InvokeAsync(Sub() BtnLaunch.IsEnabled = True)
                         End Try
                     End Sub)
        End Sub

        Private Sub BtnGoDownload_Click() Handles BtnGoDownload.Click
            CType(Application.Current.MainWindow, MainWindow).GoDownload(0)
        End Sub

        Private Sub BtnScanJava_Click() Handles BtnScanJava.Click
            RefreshJavas()
        End Sub

        Private Sub BtnGetJava_Click() Handles BtnGetJava.Click
            Dim feature As Integer = 17
            Dim v = If(ComboVersion.SelectedItem, "1.20.1").ToString()
            feature = ModJava.RecommendFeature(v)
            Task.Run(Sub()
                         Try
                             Dim exe = ModJava.DownloadJava(feature)
                             Dispatcher.InvokeAsync(Sub()
                                                        RefreshJavas()
                                                        TxtLaunch.Text = "Java 已安装：" & exe
                                                    End Sub)
                         Catch ex As Exception
                             Log("下载 Java 失败：" & ex.Message, LogLevel.Error)
                         End Try
                     End Sub)
        End Sub

        ' ---------- 登录 ----------
        Private Sub BtnLoginOffline_Click() Handles BtnLoginOffline.Click
            Dim name = TxtUser.Text.Trim()
            If name = "" Then
                MessageBox.Show("请输入用户名", "提示")
                Exit Sub
            End If
            ModAuth.AddAccount(ModAuth.LoginOffline(name))
            RefreshAccounts()
            TxtLaunch.Text = "离线登录成功"
        End Sub

        Private Sub BtnLoginYgg_Click() Handles BtnLoginYgg.Click
            Dim user = TxtUser.Text.Trim()
            Dim pwd = TxtPwd.Password
            Dim server = TxtYgg.Text.Trim()
            If user = "" OrElse pwd = "" OrElse server = "" Then
                MessageBox.Show("请填写用户名、密码与外置登录服务器地址", "提示")
                Exit Sub
            End If
            BtnLoginYgg.IsEnabled = False
            Task.Run(Sub()
                         Try
                             Dim acc = ModAuth.LoginYggdrasil(user, pwd, server)
                             ModAuth.AddAccount(acc)
                             Dispatcher.InvokeAsync(Sub()
                                                        RefreshAccounts()
                                                        TxtLaunch.Text = "外置登录成功：" & acc.Name
                                                    End Sub)
                         Catch ex As Exception
                             Log("外置登录失败：" & ex.Message, LogLevel.Error)
                             Dispatcher.InvokeAsync(Sub()
                                                        MessageBox.Show(ex.Message, "外置登录失败", MessageBoxButton.OK, MessageBoxImage.Error)
                                                    End Sub)
                         Finally
                             Dispatcher.InvokeAsync(Sub() BtnLoginYgg.IsEnabled = True)
                         End Try
                     End Sub)
        End Sub

        Private Sub BtnLoginMs_Click() Handles BtnLoginMs.Click
            Dim code As JObject = Nothing
            Try
                code = ModAuth.MicrosoftGetDeviceCode()
            Catch ex As Exception
                MessageBox.Show(ex.Message, "微软登录失败")
                Exit Sub
            End Try
            Dim uri = code("verification_uri").ToString()
            Dim userCode = code("user_code").ToString()
            Dim r = MessageBox.Show($"请在浏览器中打开：{uri}" & vbCrLf & vbCrLf &
                                    $"输入代码：{userCode}" & vbCrLf & vbCrLf &
                                    "完成后点击「是」开始等待授权…", "微软登录", MessageBoxButton.YesNo, MessageBoxImage.Information)
            If r <> MessageBoxResult.Yes Then Exit Sub
            Try
                Process.Start(New ProcessStartInfo(uri) With {.UseShellExecute = True})
            Catch
            End Try

            BtnLoginMs.IsEnabled = False
            TxtLaunch.Text = "等待微软授权…"
            Dim deviceCode = code("device_code").ToString()
            Dim interval = ModBase.SetupGetInt("MsPollInterval", 5)
            Task.Run(Sub()
                         Try
                             Dim acc = ModAuth.MicrosoftLogin(deviceCode, interval)
                             ModAuth.AddAccount(acc)
                             Dispatcher.InvokeAsync(Sub()
                                                        RefreshAccounts()
                                                        TxtLaunch.Text = "微软登录成功：" & acc.Name
                                                    End Sub)
                         Catch ex As Exception
                             Log("微软登录失败：" & ex.Message, LogLevel.Error)
                             Dispatcher.InvokeAsync(Sub()
                                                        TxtLaunch.Text = "微软登录失败"
                                                        MessageBox.Show(ex.Message, "微软登录失败", MessageBoxButton.OK, MessageBoxImage.Error)
                                                    End Sub)
                         Finally
                             Dispatcher.InvokeAsync(Sub() BtnLoginMs.IsEnabled = True)
                         End Try
                     End Sub)
        End Sub

        Private Sub BtnDelAccount_Click() Handles BtnDelAccount.Click
            Dim acc = TryCast(ComboAccount.SelectedItem, Account)
            If acc Is Nothing Then Exit Sub
            Dim list = ModAuth.GetAccounts()
            list.RemoveAll(Function(x) x.Name = acc.Name AndAlso x.Type = acc.Type)
            ModAuth.SaveAccounts(list)
            RefreshAccounts()
        End Sub

    End Class
End Namespace
