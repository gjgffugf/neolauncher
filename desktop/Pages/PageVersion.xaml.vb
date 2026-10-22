Imports System.IO
Imports System.Diagnostics
Imports System.Threading.Tasks
Imports System.Windows
Imports System.Windows.Controls

Namespace PCLNeo
    ''' <summary>版本页：版本管理、加载器安装、模组管理</summary>
    Public Class PageVersion
        Inherits Page

        Public Sub New()
            InitializeComponent()
        End Sub

        Public Sub RefreshVersions()
            ListLocal.ItemsSource = GetLocalVersions()
            RefreshMods()
        End Sub

        Private Function CurrentVersion() As String
            Return If(ListLocal.SelectedItem, "").ToString()
        End Function

        Private Sub BtnRefresh_Click() Handles BtnRefresh.Click
            RefreshVersions()
        End Sub

        Private Sub BtnDelete_Click() Handles BtnDelete.Click
            Dim v = CurrentVersion()
            If v = "" Then Exit Sub
            If MessageBox.Show($"确定删除版本 {v}？（不会删除存档）", "确认", MessageBoxButton.YesNo) <> MessageBoxResult.Yes Then Exit Sub
            DeleteVersion(v)
            RefreshVersions()
        End Sub

        Private Sub BtnOpenDir_Click() Handles BtnOpenDir.Click
            Dim v = CurrentVersion()
            If v = "" Then Exit Sub
            Process.Start("explorer.exe", Path.Combine(DirVersions, v))
        End Sub

        Private Sub BtnOpenMods_Click() Handles BtnOpenMods.Click
            Dim v = CurrentVersion()
            If v = "" Then Exit Sub
            Dim dir = GetModsDir(v)
            Directory.CreateDirectory(dir)
            Process.Start("explorer.exe", dir)
        End Sub

        ' ---------- 加载器 ----------
        Private Sub BtnLoadLoaderList_Click() Handles BtnLoadLoaderList.Click
            Dim v = CurrentVersion()
            If v = "" Then
                MessageBox.Show("请先选择一个游戏版本")
                Exit Sub
            End If
            Dim kind = CType(ComboLoaderKind.SelectedItem, ComboBoxItem).Content.ToString()
            Dim mc = BaseMcVersion(v)
            BtnLoadLoaderList.IsEnabled = False
            Task.Run(Sub()
                         Try
                             Dim list As List(Of LoaderEntry) = Nothing
                             Select Case kind
                                 Case "Forge"
                                     list = GetForgeList(mc)
                                 Case "NeoForge"
                                     list = GetNeoForgeList(mc)
                                 Case "Fabric"
                                     list = GetFabricList(mc)
                                 Case "Quilt"
                                     list = GetQuiltList(mc)
                             End Select
                             Dispatcher.InvokeAsync(Sub()
                                                        ComboLoaderVersion.ItemsSource = list
                                                        If list IsNot Nothing AndAlso list.Count > 0 Then ComboLoaderVersion.SelectedIndex = 0
                                                        BtnLoadLoaderList.IsEnabled = True
                                                    End Sub)
                         Catch ex As Exception
                             Log("获取加载器列表失败：" & ex.Message, LogLevel.Error)
                             Dispatcher.InvokeAsync(Sub() BtnLoadLoaderList.IsEnabled = True)
                         End Try
                     End Sub)
        End Sub

        ''' <summary>从任意版本名推断其 Minecraft 原版号</summary>
        Private Function BaseMcVersion(VersionName As String) As String
            Dim json = GetVersionJson(VersionName)
            ' 向上追溯到不含加载器的原版号
            Dim cur = json
            Dim guard = 0
            While cur("inheritsFrom") IsNot Nothing AndAlso guard < 8
                guard += 1
                Dim p = cur("inheritsFrom").ToString()
                Dim f = Path.Combine(DirVersions, p, p & ".json")
                If Not File.Exists(f) Then Exit While
                cur = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(f))
            End While
            Return If(cur("id")?.ToString(), VersionName)
        End Function

        Private Sub BtnInstallLoader_Click() Handles BtnInstallLoader.Click
            Dim v = CurrentVersion()
            Dim e = TryCast(ComboLoaderVersion.SelectedItem, LoaderEntry)
            If v = "" Then
                MessageBox.Show("请先选择一个游戏版本")
                Exit Sub
            End If
            If e Is Nothing Then
                MessageBox.Show("请先获取并选择加载器版本")
                Exit Sub
            End If
            Dim kind = CType(ComboLoaderKind.SelectedItem, ComboBoxItem).Content.ToString()
            Dim mc = BaseMcVersion(v)
            BtnInstallLoader.IsEnabled = False
            Task.Run(Sub()
                         Try
                             Select Case kind
                                 Case "Forge"
                                     InstallForge(mc, e.Version)
                                 Case "NeoForge"
                                     InstallNeoForge(mc, e.Version)
                                 Case "Fabric"
                                     InstallFabric(mc, e.Version)
                                 Case "Quilt"
                                     InstallQuilt(mc, e.Version)
                             End Select
                             Dispatcher.InvokeAsync(Sub()
                                                        RefreshVersions()
                                                        MessageBox.Show($"{kind} {e.Version} 安装完成", "完成", MessageBoxButton.OK, MessageBoxImage.Information)
                                                    End Sub)
                         Catch ex As Exception
                             Log("安装加载器失败：" & ex.Message, LogLevel.Error)
                             Dispatcher.InvokeAsync(Sub() MessageBox.Show(ex.Message, "安装失败", MessageBoxButton.OK, MessageBoxImage.Error))
                         Finally
                             Dispatcher.InvokeAsync(Sub() BtnInstallLoader.IsEnabled = True)
                         End Try
                     End Sub)
        End Sub

        ' ---------- 模组 ----------
        Private Sub RefreshMods()
            Dim v = CurrentVersion()
            If v = "" Then
                ListMods.ItemsSource = Nothing
                Exit Sub
            End If
            ListMods.ItemsSource = ListInstalledMods(v)
        End Sub

        Private Sub BtnRefreshMods_Click() Handles BtnRefreshMods.Click
            RefreshMods()
        End Sub

        Private Sub BtnToggleMod_Click() Handles BtnToggleMod.Click
            Dim v = CurrentVersion()
            Dim f = If(ListMods.SelectedItem, "").ToString()
            If v = "" OrElse f = "" Then Exit Sub
            ToggleMod(v, f)
            RefreshMods()
        End Sub

        Private Sub BtnDeleteMod_Click() Handles BtnDeleteMod.Click
            Dim v = CurrentVersion()
            Dim f = If(ListMods.SelectedItem, "").ToString()
            If v = "" OrElse f = "" Then Exit Sub
            DeleteMod(v, f)
            RefreshMods()
        End Sub

        Private Sub BtnAddMod_Click() Handles BtnAddMod.Click
            CType(Application.Current.MainWindow, MainWindow).GoDownload(1)
        End Sub

        Private Sub OnVersionChanged(sender As Object, e As SelectionChangedEventArgs) Handles ListLocal.SelectionChanged
            RefreshMods()
        End Sub

    End Class
End Namespace
