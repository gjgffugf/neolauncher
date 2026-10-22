Imports System.Diagnostics
Imports System.IO
Imports System.Windows
Imports System.Windows.Controls
Imports Microsoft.Win32

Namespace PCLNeo
    ''' <summary>设置页：目录、Java、内存、下载源、账号平台、启动参数</summary>
    Public Class PageSetup
        Inherits Page

        Public Sub New()
            InitializeComponent()
            LoadAll()
        End Sub

        Private Sub LoadAll()
            TxtMcPath.Text = PathMc
            TxtJava.Text = SetupGet("JavaPath", "")
            TxtMemMax.Text = SetupGetInt("MemoryMax", 2048).ToString()
            TxtMemMin.Text = SetupGetInt("MemoryMin", 512).ToString()
            ComboSource.SelectedIndex = If(UseMirror, 0, 1)
            ChkIsolate.IsChecked = SetupGetBool("VersionIsolate", True)
            TxtCfKey.Text = SetupGet("CfApiKey", "")
            TxtMsId.Text = SetupGet("MsClientId", "")
            TxtYggDef.Text = SetupGet("YggDefaultServer", "")
            TxtJvmArgs.Text = SetupGet("JvmArgs", "")
            TxtGameArgs.Text = SetupGet("GameArgs", "")
            ChkCustomSize.IsChecked = SetupGetBool("CustomSize", False)
            TxtWidth.Text = SetupGetInt("WindowWidth", 854).ToString()
            TxtHeight.Text = SetupGetInt("WindowHeight", 480).ToString()
            ChkFullscreen.IsChecked = SetupGetBool("FullScreen", False)
            ChkHideConsole.IsChecked = SetupGetBool("HideConsole", False)
            ChkZgc.IsChecked = SetupGetBool("UseZgc", False)
        End Sub

        Private Sub SaveAll()
            PathMc = TxtMcPath.Text.Trim()
            SetupSet("JavaPath", TxtJava.Text.Trim())
            SetupSet("MemoryMax", TxtMemMax.Text.Trim())
            SetupSet("MemoryMin", TxtMemMin.Text.Trim())
            SetupSet("DownloadSource", If(ComboSource.SelectedIndex = 0, SourceBmclapi, SourceOfficial))
            SetupSet("VersionIsolate", If(ChkIsolate.IsChecked = True, "True", "False"))
            SetupSet("CfApiKey", TxtCfKey.Text.Trim())
            SetupSet("MsClientId", TxtMsId.Text.Trim())
            SetupSet("YggDefaultServer", TxtYggDef.Text.Trim())
            SetupSet("JvmArgs", TxtJvmArgs.Text.Trim())
            SetupSet("GameArgs", TxtGameArgs.Text.Trim())
            SetupSet("CustomSize", If(ChkCustomSize.IsChecked = True, "True", "False"))
            SetupSet("WindowWidth", TxtWidth.Text.Trim())
            SetupSet("WindowHeight", TxtHeight.Text.Trim())
            SetupSet("FullScreen", If(ChkFullscreen.IsChecked = True, "True", "False"))
            SetupSet("HideConsole", If(ChkHideConsole.IsChecked = True, "True", "False"))
            SetupSet("UseZgc", If(ChkZgc.IsChecked = True, "True", "False"))
            EnsureDirs()
            TxtHint.Text = "已保存（" & DateTime.Now.ToString("HH:mm:ss") & "）"
            Log("设置已保存")
        End Sub

        Private Sub BtnSave_Click() Handles BtnSave.Click
            SaveAll()
        End Sub

        Private Sub BtnBrowseMc_Click() Handles BtnBrowseMc.Click
            Dim dlg As New System.Windows.Forms.FolderBrowserDialog() With {.SelectedPath = TxtMcPath.Text}
            If dlg.ShowDialog() = System.Windows.Forms.DialogResult.OK Then TxtMcPath.Text = dlg.SelectedPath
        End Sub

        Private Sub BtnBrowseJava_Click() Handles BtnBrowseJava.Click
            Dim dlg As New Microsoft.Win32.OpenFileDialog() With {.Filter = "javaw.exe|javaw.exe|java.exe|java.exe|所有文件|*.*"}
            If dlg.ShowDialog() = True Then TxtJava.Text = dlg.FileName
        End Sub

        Private Sub BtnOpenData_Click() Handles BtnOpenData.Click
            Process.Start("explorer.exe", PathData)
        End Sub

    End Class
End Namespace
