Imports System.Threading.Tasks
Imports System.Windows
Imports System.Windows.Controls
Imports Microsoft.Win32

Namespace PCLNeo
    ''' <summary>下载页：版本下载 / Mod 下载 / 整合包导入</summary>
    Public Class PageDownload
        Inherits Page

        Private _versions As New List(Of McVersionEntry)()
        Private _mods As New List(Of ModItem)()
        Private _files As New List(Of ModFileItem)()

        Public Sub New()
            InitializeComponent()
            RefreshLocalTargets()
            RefreshInstances()
            LoadRemoteVersions()
        End Sub

        Public Sub SelectTab(Index As Integer)
            If Index >= 0 AndAlso Index < Tabs.Items.Count Then Tabs.SelectedIndex = Index
        End Sub

        ' ============ 版本下载 ============
        Private Sub LoadRemoteVersions()
            BtnRefreshList.IsEnabled = False
            Task.Run(Sub()
                         Try
                             Dim types As New List(Of String)()
                             Dispatcher.Invoke(Sub()
                                                   If ChkRelease.IsChecked Then types.Add("release")
                                                   If ChkSnapshot.IsChecked Then types.Add("snapshot")
                                                   If ChkOld.IsChecked Then
                                                       types.Add("old_beta")
                                                       types.Add("old_alpha")
                                                   End If
                                               End Sub)
                             Dim list = GetRemoteVersions(types.ToArray())
                             Dispatcher.InvokeAsync(Sub()
                                                        _versions = list
                                                        ApplyFilter()
                                                        BtnRefreshList.IsEnabled = True
                                                    End Sub)
                         Catch ex As Exception
                             Log("获取版本列表失败：" & ex.Message, LogLevel.Error)
                             Dispatcher.InvokeAsync(Sub() BtnRefreshList.IsEnabled = True)
                         End Try
                     End Sub)
        End Sub

        Private Sub ApplyFilter()
            Dim kw = TxtFilter.Text.Trim().ToLower()
            Dim view = _versions
            If kw <> "" Then view = view.Where(Function(v) v.Id.ToLower().Contains(kw)).ToList()
            ListVersions.ItemsSource = view
        End Sub

        Private Sub BtnRefreshList_Click() Handles BtnRefreshList.Click
            LoadRemoteVersions()
        End Sub
        Private Sub OnFilterCheckbox() Handles ChkRelease.Click, ChkSnapshot.Click, ChkOld.Click
            If _versions.Count > 0 Then ApplyFilter()
        End Sub
        Private Sub OnFilterText(sender As Object, e As TextChangedEventArgs) Handles TxtFilter.TextChanged
            If _versions.Count > 0 Then ApplyFilter()
        End Sub

        Private Sub BtnInstallVersion_Click() Handles BtnInstallVersion.Click
            Dim e = TryCast(ListVersions.SelectedItem, McVersionEntry)
            If e Is Nothing Then
                MessageBox.Show("请先选择一个版本")
                Exit Sub
            End If
            BtnInstallVersion.IsEnabled = False
            Task.Run(Sub()
                         Try
                             InstallVersion(e)
                             Dispatcher.InvokeAsync(Sub()
                                                        MessageBox.Show($"版本 {e.Id} 安装完成", "完成", MessageBoxButton.OK, MessageBoxImage.Information)
                                                        RefreshLocalTargets()
                                                    End Sub)
                         Catch ex As Exception
                             Log("安装版本失败：" & ex.Message, LogLevel.Error)
                             Dispatcher.InvokeAsync(Sub() MessageBox.Show(ex.Message, "安装失败", MessageBoxButton.OK, MessageBoxImage.Error))
                         Finally
                             Dispatcher.InvokeAsync(Sub() BtnInstallVersion.IsEnabled = True)
                         End Try
                     End Sub)
        End Sub

        ' ============ Mod 下载 ============
        Private Sub RefreshLocalTargets()
            Dim list = GetLocalVersions()
            ComboMcVer.ItemsSource = New List(Of String)(list)
            ComboTarget.ItemsSource = New List(Of String)(list)
            If list.Count > 0 Then
                ComboTarget.SelectedIndex = 0
                ComboMcVer.SelectedIndex = 0
            End If
        End Sub

        Private Sub BtnSearch_Click() Handles BtnSearch.Click
            Dim kw = TxtKeyword.Text.Trim()
            If kw = "" Then
                MessageBox.Show("请输入关键词")
                Exit Sub
            End If
            Dim useCf = ComboSource.SelectedIndex = 1
            Dim mc = If(ComboMcVer.SelectedItem, "").ToString()
            Dim loader = CType(ComboLoader.SelectedItem, ComboBoxItem).Content.ToString()
            BtnSearch.IsEnabled = False
            Task.Run(Sub()
                         Try
                             Dim list As List(Of ModItem)
                             If useCf Then
                                 list = SearchCurseForge(kw, mc, loader)
                             Else
                                 list = SearchModrinth(kw, mc, loader)
                             End If
                             Dispatcher.InvokeAsync(Sub()
                                                        _mods = list
                                                        ListMods.ItemsSource = list
                                                        BtnSearch.IsEnabled = True
                                                    End Sub)
                         Catch ex As Exception
                             Log("搜索失败：" & ex.Message, LogLevel.Error)
                             Dispatcher.InvokeAsync(Sub()
                                                        BtnSearch.IsEnabled = True
                                                        MessageBox.Show(ex.Message, "搜索失败", MessageBoxButton.OK, MessageBoxImage.Error)
                                                    End Sub)
                         End Try
                     End Sub)
        End Sub

        Private Sub BtnLoadFiles_Click() Handles BtnLoadFiles.Click
            Dim m = TryCast(ListMods.SelectedItem, ModItem)
            If m Is Nothing Then
                MessageBox.Show("请先选择一个模组")
                Exit Sub
            End If
            Dim mc = If(ComboMcVer.SelectedItem, "").ToString()
            Dim loader = CType(ComboLoader.SelectedItem, ComboBoxItem).Content.ToString()
            BtnLoadFiles.IsEnabled = False
            Task.Run(Sub()
                         Try
                             Dim files As List(Of ModFileItem)
                             If m.Source = ModSource.CurseForge Then
                                 files = GetCurseForgeFiles(m.Id, mc)
                             Else
                                 files = GetModrinthFiles(If(m.Id, m.Slug), mc, loader)
                             End If
                             Dispatcher.InvokeAsync(Sub()
                                                        _files = files
                                                        ListFiles.ItemsSource = files
                                                        BtnLoadFiles.IsEnabled = True
                                                    End Sub)
                         Catch ex As Exception
                             Log("获取版本列表失败：" & ex.Message, LogLevel.Error)
                             Dispatcher.InvokeAsync(Sub()
                                                        BtnLoadFiles.IsEnabled = True
                                                        MessageBox.Show(ex.Message, "失败", MessageBoxButton.OK, MessageBoxImage.Error)
                                                    End Sub)
                         End Try
                     End Sub)
        End Sub

        Private Sub BtnInstallMod_Click() Handles BtnInstallMod.Click
            Dim f = TryCast(ListFiles.SelectedItem, ModFileItem)
            Dim target = If(ComboTarget.SelectedItem, "").ToString()
            If f Is Nothing Then
                MessageBox.Show("请先选择一个模组版本")
                Exit Sub
            End If
            If target = "" Then
                MessageBox.Show("请先选择要安装到的游戏版本")
                Exit Sub
            End If
            BtnInstallMod.IsEnabled = False
            Task.Run(Sub()
                         Try
                             InstallMod(f, target)
                             Dispatcher.InvokeAsync(Sub() MessageBox.Show($"{f.Name} 安装完成", "完成"))
                         Catch ex As Exception
                             Log("安装模组失败：" & ex.Message, LogLevel.Error)
                             Dispatcher.InvokeAsync(Sub() MessageBox.Show(ex.Message, "安装失败"))
                         Finally
                             Dispatcher.InvokeAsync(Sub() BtnInstallMod.IsEnabled = True)
                         End Try
                     End Sub)
        End Sub

        ' ============ 整合包 ============
        Private Sub BtnBrowsePack_Click() Handles BtnBrowsePack.Click
            Dim dlg As New OpenFileDialog() With {
                .Filter = "整合包|*.mrpack;*.zip|Modrinth 整合包|*.mrpack|CurseForge 整合包|*.zip|所有文件|*.*"}
            If dlg.ShowDialog() = True Then TxtPackPath.Text = dlg.FileName
        End Sub

        Private Sub BtnImportPack_Click() Handles BtnImportPack.Click
            Dim path = TxtPackPath.Text.Trim()
            If path = "" OrElse Not IO.File.Exists(path) Then
                MessageBox.Show("请选择整合包文件")
                Exit Sub
            End If
            BtnImportPack.IsEnabled = False
            Task.Run(Sub()
                         Try
                             Dim info As InstanceInfo
                             If path.ToLower().EndsWith(".mrpack") Then
                                 info = InstallMrpack(path)
                             Else
                                 info = InstallCurseForgeZip(path)
                             End If
                             Dispatcher.InvokeAsync(Sub()
                                                        RefreshInstances()
                                                        RefreshLocalTargets()
                                                        MessageBox.Show($"整合包「{info.Name}」安装完成", "完成", MessageBoxButton.OK, MessageBoxImage.Information)
                                                    End Sub)
                         Catch ex As Exception
                             Log("导入整合包失败：" & ex.Message, LogLevel.Error)
                             Dispatcher.InvokeAsync(Sub() MessageBox.Show(ex.Message, "导入失败", MessageBoxButton.OK, MessageBoxImage.Error))
                         Finally
                             Dispatcher.InvokeAsync(Sub() BtnImportPack.IsEnabled = True)
                         End Try
                     End Sub)
        End Sub

        Private Sub RefreshInstances()
            ListInstances.ItemsSource = GetInstances()
        End Sub
        Private Sub BtnRefreshInstances_Click() Handles BtnRefreshInstances.Click
            RefreshInstances()
        End Sub
        Private Sub BtnSwitchInstance_Click() Handles BtnSwitchInstance.Click
            Dim info = TryCast(ListInstances.SelectedItem, InstanceInfo)
            If info Is Nothing Then
                MessageBox.Show("请选择实例")
                Exit Sub
            End If
            SwitchInstance(info)
            RefreshLocalTargets()
            MessageBox.Show("已切换到：" & info.Dir)
        End Sub

    End Class
End Namespace
