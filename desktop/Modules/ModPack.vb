Imports System.IO
Imports System.IO.Compression
Imports Newtonsoft.Json.Linq

Namespace PCLNeo

    ''' <summary>整合包实例信息</summary>
    Public Class InstanceInfo
        Public Property Name As String = ""
        Public Property Dir As String = ""
        Public Property McVersion As String = ""
        Public Property Loader As String = ""
        Public Property Source As String = ""
        Public Overrides Function ToString() As String
            Return $"{Name}（{McVersion} {Loader}）"
        End Function
    End Class

    ''' <summary>
    ''' 整合包模块：Modrinth(.mrpack) 与 CurseForge(.zip) 导入
    ''' 安装结果是一个独立实例目录，并自动切换为当前游戏目录（可在「版本」页切回）
    ''' </summary>
    Public Module ModPack

        Private ReadOnly Property DirInstances As String
            Get
                Return Path.Combine(PathData, "instances")
            End Get
        End Property
        Private ReadOnly Property DirTemp As String
            Get
                Return Path.Combine(PathData, "temp")
            End Get
        End Property

        ' ---------- 实例表 ----------
        Public Function GetInstances() As List(Of InstanceInfo)
            Dim list As New List(Of InstanceInfo)()
            Dim f = Path.Combine(PathData, "instances.json")
            Try
                If File.Exists(f) Then
                    For Each o In JArray.Parse(File.ReadAllText(f))
                        list.Add(New InstanceInfo With {
                            .Name = If(o("name")?.ToString(), ""),
                            .Dir = If(o("dir")?.ToString(), ""),
                            .McVersion = If(o("mc")?.ToString(), ""),
                            .Loader = If(o("loader")?.ToString(), ""),
                            .Source = If(o("source")?.ToString(), "")
                        })
                    Next
                End If
            Catch ex As Exception
                Log("读取实例列表失败：" & ex.Message, LogLevel.Warn)
            End Try
            Return list
        End Function

        Public Sub AddInstance(info As InstanceInfo)
            Dim list = GetInstances()
            list.RemoveAll(Function(x) x.Dir = info.Dir)
            list.Add(info)
            Dim arr As New JArray()
            For Each i In list
                arr.Add(New JObject From {{"name", i.Name}, {"dir", i.Dir}, {"mc", i.McVersion}, {"loader", i.Loader}, {"source", i.Source}})
            Next
            Directory.CreateDirectory(PathData)
            File.WriteAllText(Path.Combine(PathData, "instances.json"), arr.ToString(Newtonsoft.Json.Formatting.Indented))
        End Sub

        ''' <summary>切换到某个实例（等同于 PCL2 的版本隔离）</summary>
        Public Sub SwitchInstance(info As InstanceInfo)
            PathMc = info.Dir
            EnsureDirs()
            Log($"已切换游戏目录：{info.Dir}")
        End Sub

        ' ---------- Modrinth 整合包 ----------
        ''' <summary>导入 .mrpack</summary>
        Public Function InstallMrpack(ZipPath As String) As InstanceInfo
            Directory.CreateDirectory(DirTemp)
            Dim work = Path.Combine(DirTemp, "mrpack-" & Guid.NewGuid().ToString("N").Substring(0, 8))
            ExtractZip(ZipPath, work)
            Dim indexPath = Path.Combine(work, "modrinth.index.json")
            If Not File.Exists(indexPath) Then Throw New Exception("不是有效的 Modrinth 整合包（缺少 modrinth.index.json）")
            Dim index = JObject.Parse(File.ReadAllText(indexPath))
            Dim name = If(index("name")?.ToString(), Path.GetFileNameWithoutExtension(ZipPath))
            Dim mc = If(index("dependencies")?("minecraft")?.ToString(), "")
            If mc = "" Then Throw New Exception("整合包未声明 Minecraft 版本")
            Dim loader = DetectLoader(index("dependencies"))

            Dim dir = Path.Combine(DirInstances, SafeName(name))
            Directory.CreateDirectory(dir)
            EventBus.RaiseProgress("安装整合包", 0, $"{name}：准备目录…")
            PathMc = dir
            EnsureDirs()

            ' 1. 原版
            InstallVanilla(mc, 0.05, 0.45)
            ' 2. 加载器
            Dim versionId = ApplyLoader(loader, mc, index("dependencies"))
            ' 3. 模组与文件
            Dim files = If(index("files"), New JArray())
            Dim total = files.Count
            Dim i = 0
            For Each f In files
                i += 1
                Dim rel = f("path").ToString().Replace("/", "\")
                If rel.StartsWith("mods\") OrElse rel.StartsWith("mods/") Then
                    ' 走 CDN
                End If
                Dim urls = If(f("downloads"), New JArray())
                If urls.Count = 0 Then Continue For
                Dim envObj = f("env")
                If envObj IsNot Nothing Then
                    Dim serverOnly = envObj("server")?.ToString()
                    If serverOnly = "required" Then Continue For
                End If
                Dim dest = Path.Combine(dir, rel)
                Directory.CreateDirectory(Path.GetDirectoryName(dest))
                EventBus.RaiseProgress("安装整合包", 0.45 + 0.45 * i / total, $"文件 {i}/{total}：{Path.GetFileName(rel)}")
                Try
                    DownloadFile(urls(0).ToString(), dest, If(f("hashes")?("sha1")?.ToString(), ""), "安装整合包", Path.GetFileName(rel))
                Catch ex As Exception
                    Log($"整合包文件下载失败 {rel}：{ex.Message}", LogLevel.Warn)
                End Try
            Next
            ' 4. overrides
            Dim ov = Path.Combine(work, "overrides")
            If Directory.Exists(ov) Then
                EventBus.RaiseProgress("安装整合包", 0.93, "解压 overrides…")
                CopyDirectory(ov, dir)
            End If
            Dim ov2 = Path.Combine(work, "server-overrides")
            If Directory.Exists(ov2) Then CopyDirectory(ov2, dir)

            Dim info As New InstanceInfo With {
                .Name = name, .Dir = dir, .McVersion = mc, .Loader = loader, .Source = "mrpack"
            }
            AddInstance(info)
            EventBus.RaiseProgress("安装整合包", 1, $"整合包「{name}」安装完成")
            Log($"整合包安装完成：{name} → {dir}（版本 {If(versionId = "", mc, versionId)}）")
            Try
                Directory.Delete(work, True)
            Catch
            End Try
            Return info
        End Function

        Private Function DetectLoader(deps As JToken) As String
            If deps Is Nothing Then Return ""
            If deps("fabric-loader") IsNot Nothing Then Return "fabric-loader " & deps("fabric-loader").ToString()
            If deps("quilt-loader") IsNot Nothing Then Return "quilt-loader " & deps("quilt-loader").ToString()
            If deps("forge") IsNot Nothing Then Return "forge " & deps("forge").ToString()
            If deps("neoforge") IsNot Nothing Then Return "neoforge " & deps("neoforge").ToString()
            Return ""
        End Function

        Private Function ApplyLoader(Loader As String, Mc As String, deps As JToken) As String
            Try
                If Loader.StartsWith("fabric-loader") Then
                    Return InstallFabric(Mc, deps("fabric-loader").ToString())
                ElseIf Loader.StartsWith("quilt-loader") Then
                    Return InstallQuilt(Mc, deps("quilt-loader").ToString())
                ElseIf Loader.StartsWith("forge") Then
                    InstallForge(Mc, deps("forge").ToString())
                    Return $"{Mc}-forge-{deps("forge").ToString()}"
                ElseIf Loader.StartsWith("neoforge") Then
                    InstallNeoForge(Mc, deps("neoforge").ToString())
                    Return $"neoforge-{deps("neoforge").ToString()}"
                End If
            Catch ex As Exception
                Log("加载器安装失败，可稍后在「版本」页手动安装：" & ex.Message, LogLevel.Warn)
            End Try
            Return ""
        End Function

        ' ---------- CurseForge 整合包 ----------
        ''' <summary>导入 CurseForge .zip（manifest.json）</summary>
        Public Function InstallCurseForgeZip(ZipPath As String) As InstanceInfo
            Directory.CreateDirectory(DirTemp)
            Dim work = Path.Combine(DirTemp, "cfpack-" & Guid.NewGuid().ToString("N").Substring(0, 8))
            ExtractZip(ZipPath, work)
            Dim manifestPath = Path.Combine(work, "manifest.json")
            If Not File.Exists(manifestPath) Then Throw New Exception("不是有效的 CurseForge 整合包（缺少 manifest.json）")
            Dim man = JObject.Parse(File.ReadAllText(manifestPath))
            Dim name = If(man("name")?.ToString(), Path.GetFileNameWithoutExtension(ZipPath))
            Dim mc = If(man("minecraft")?("version")?.ToString(), "")
            If mc = "" Then Throw New Exception("整合包未声明 Minecraft 版本")
            Dim loader As String = ""
            Dim loaderVer As String = ""
            Dim ld = man("minecraft")("modLoaders")
            If ld IsNot Nothing AndAlso ld.Count > 0 Then
                Dim id = ld(0)("id").ToString()   ' 例如 forge-47.1.0 / fabric-0.14.21 / neoforge-20.4.0
                Dim p = id.Split("-"c)
                loader = p(0)
                loaderVer = If(p.Length > 1, id.Substring(p(0).Length + 1), "")
            End If

            Dim dir = Path.Combine(DirInstances, SafeName(name))
            Directory.CreateDirectory(dir)
            PathMc = dir
            EnsureDirs()
            EventBus.RaiseProgress("安装整合包", 0.02, $"{name}：安装 {mc}…")
            InstallVanilla(mc, 0.05, 0.4)

            If loader = "forge" Then
                Try
                    InstallForge(mc, loaderVer)
                Catch ex As Exception
                    Log("Forge 安装失败：" & ex.Message, LogLevel.Warn)
                End Try
            ElseIf loader = "fabric" Then
                Try
                    InstallFabric(mc, loaderVer)
                Catch ex As Exception
                    Log("Fabric 安装失败：" & ex.Message, LogLevel.Warn)
                End Try
            ElseIf loader = "neoforge" Then
                Try
                    InstallNeoForge(mc, loaderVer)
                Catch ex As Exception
                    Log("NeoForge 安装失败：" & ex.Message, LogLevel.Warn)
                End Try
            End If

            ' mods
            Dim files = If(man("files"), New JArray())
            Dim total = files.Count
            Dim i = 0
            Dim modsDir = Path.Combine(dir, "mods")
            Directory.CreateDirectory(modsDir)
            For Each f In files
                i += 1
                Dim projectId = f("projectID").ToString()
                Dim fileId = f("fileID").ToString()
                EventBus.RaiseProgress("安装整合包", 0.4 + 0.5 * i / total, $"模组 {i}/{total}")
                Try
                    Dim url As String = ""
                    If ModMod.CfApiKey <> "" Then
                        Dim j = HttpGetJsonWithHeaders($"https://api.curseforge.com/v1/mods/{projectId}/files/{fileId}/download-url",
                                                       New Dictionary(Of String, String) From {{"x-api-key", ModMod.CfApiKey}})
                        url = If(j("data")?.ToString(), "")
                    End If
                    If url = "" Then url = $"https://mediafiles.forgecdn.net/files/{fileId.Substring(0, 4)}/{fileId.Substring(4)}/mod.jar"
                    DownloadFile(url, Path.Combine(modsDir, $"{projectId}-{fileId}.jar"), "", "安装整合包", $"mod {i}")
                Catch ex As Exception
                    Log($"整合包模组 {projectId}/{fileId} 下载失败：{ex.Message}", LogLevel.Warn)
                End Try
            Next

            Dim ov = Path.Combine(work, If(man("overrides")?.ToString(), "overrides"))
            If Directory.Exists(ov) Then CopyDirectory(ov, dir)

            Dim info As New InstanceInfo With {
                .Name = name, .Dir = dir, .McVersion = mc, .Loader = loader & " " & loaderVer, .Source = "curseforge"
            }
            AddInstance(info)
            EventBus.RaiseProgress("安装整合包", 1, $"整合包「{name}」安装完成")
            Log($"CurseForge 整合包安装完成：{name} → {dir}")
            Try
                Directory.Delete(work, True)
            Catch
            End Try
            Return info
        End Function

        ' ---------- 公共 ----------
        ''' <summary>安装原版（按版本号），用于整合包</summary>
        Public Sub InstallVanilla(McVersion As String, Optional FromProgress As Double = 0, Optional ToProgress As Double = 1)
            Dim entry As McVersionEntry = Nothing
            Try
                For Each e In GetRemoteVersions()
                    If e.Id = McVersion Then
                        entry = e
                        Exit For
                    End If
                Next
            Catch
            End Try
            If entry Is Nothing Then
                entry = New McVersionEntry With {
                    .Id = McVersion, .Type = "release",
                    .Url = If(UseMirror,
                        $"https://bmclapi2.bangbang93.com/version/{McVersion}/json",
                        $"https://bmclapi2.bangbang93.com/version/{McVersion}/json")
                }
            End If
            EventBus.RaiseProgress("安装整合包", FromProgress, $"安装原版 {McVersion}…")
            InstallVersion(entry)
        End Sub

        Public Sub CopyDirectory(Source As String, Target As String)
            Directory.CreateDirectory(Target)
            For Each f In Directory.GetFiles(Source)
                File.Copy(f, Path.Combine(Target, Path.GetFileName(f)), True)
            Next
            For Each d In Directory.GetDirectories(Source)
                CopyDirectory(d, Path.Combine(Target, Path.GetFileName(d)))
            Next
        End Sub

    End Module

End Namespace
