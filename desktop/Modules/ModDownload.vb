Imports System.IO
Imports System.Text
Imports Newtonsoft.Json.Linq

Namespace PCLNeo

    ''' <summary>版本清单中的一条版本</summary>
    Public Class McVersionEntry
        Public Property Id As String = ""
        Public Property Type As String = "release"
        Public Property Url As String = ""
        Public Property ReleaseTime As String = ""
        Public Overrides Function ToString() As String
            Return Id
        End Function
    End Class

    ''' <summary>
    ''' 下载模块：版本清单、客户端 jar、依赖库、资源索引与资源文件
    ''' </summary>
    Public Module ModDownload

        ' ---------- 版本清单 ----------
        Public Function ManifestUrl() As String
            Return If(UseMirror,
                "https://bmclapi2.bangbang93.com/mc/game/version_manifest_v2.json",
                "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json")
        End Function

        ''' <summary>获取远程版本列表（可筛选 release / snapshot / old_beta / old_alpha）</summary>
        Public Function GetRemoteVersions(Optional Types As String() = Nothing) As List(Of McVersionEntry)
            Dim list As New List(Of McVersionEntry)()
            Dim json = HttpGetJson(ManifestUrl())
            For Each v In json("versions")
                Dim e As New McVersionEntry With {
                    .Id = v("id").ToString(),
                    .Type = v("type").ToString(),
                    .Url = v("url").ToString(),
                    .ReleaseTime = If(v("releaseTime")?.ToString(), "")
                }
                If Types Is Nothing OrElse Types.Contains(e.Type) Then list.Add(e)
            Next
            Log($"获取到 {list.Count} 个可下载版本")
            Return list
        End Function

        ''' <summary>已安装版本（versions 下带 json 的目录）</summary>
        Public Function GetLocalVersions() As List(Of String)
            Dim list As New List(Of String)()
            EnsureDirs()
            If Not Directory.Exists(DirVersions) Then Return list
            For Each d In Directory.GetDirectories(DirVersions)
                Dim name = Path.GetFileName(d)
                If File.Exists(Path.Combine(d, name & ".json")) Then list.Add(name)
            Next
            list.Sort()
            Return list
        End Function

        ' ---------- 安装版本 ----------
        ''' <summary>下载并安装一个原版 Minecraft（含库、资源、 natives 解压）</summary>
        Public Sub InstallVersion(Entry As McVersionEntry)
            Dim name = Entry.Id
            Dim dir = Path.Combine(DirVersions, name)
            Dim jsonPath = Path.Combine(dir, name & ".json")
            Directory.CreateDirectory(dir)

            ' 1. 版本 JSON
            EventBus.RaiseProgress("安装版本", 0, "下载版本信息…")
            If Not File.Exists(jsonPath) Then DownloadFile(Entry.Url, jsonPath)
            Dim json = JObject.Parse(File.ReadAllText(jsonPath))

            ' 2. 客户端 jar
            Dim dl = json("downloads")?("client")
            If dl IsNot Nothing Then
                Dim jarPath = Path.Combine(dir, name & ".jar")
                EventBus.RaiseProgress("安装版本", 0.05, "下载客户端 jar…")
                DownloadFile(dl("url").ToString(), jarPath, If(dl("sha1")?.ToString(), ""), "安装版本", name & ".jar")
            End If

            ' 3. 依赖库
            DownloadLibraries(json, name)

            ' 4. 资源
            DownloadAssets(json, name)

            EventBus.RaiseProgress("安装版本", 1, $"版本 {name} 安装完成")
            Log($"版本 {name} 安装完成", LogLevel.Info)
        End Sub

        ''' <summary>处理 inheritsFrom：把父版本的库、参数合并进子版本</summary>
        Public Function MergeInherits(json As JObject) As JObject
            Dim merged = CType(json.DeepClone(), JObject)
            Dim chain As New List(Of JObject) From {merged}
            Dim cur = merged
            Dim guard = 0
            While cur("inheritsFrom") IsNot Nothing AndAlso guard < 8
                guard += 1
                Dim parentName = cur("inheritsFrom").ToString()
                Dim pPath = Path.Combine(DirVersions, parentName, parentName & ".json")
                If Not File.Exists(pPath) Then
                    Throw New Exception($"缺少父版本 {parentName}，请先安装该版本")
                End If
                Dim parent = JObject.Parse(File.ReadAllText(pPath))
                chain.Insert(0, parent)
                cur = parent
            End While

            Dim result = CType(chain(0).DeepClone(), JObject)
            For i = 1 To chain.Count - 1
                Dim child = chain(i)
                For Each prop In child.Properties()
                    If prop.Name = "libraries" OrElse prop.Name = "arguments" OrElse prop.Name = "minecraftArguments" Then
                        result(prop.Name) = MergeArray(result(prop.Name), prop.Value)
                    Else
                        result(prop.Name) = prop.Value.DeepClone()
                    End If
                Next
            Next
            Return result
        End Function

        Private Function MergeArray(A As JToken, B As JToken) As JToken
            Dim arr As New JArray()
            If A IsNot Nothing AndAlso TypeOf A Is JArray Then
                For Each x In CType(A, JArray)
                    arr.Add(x.DeepClone())
                Next
            End If
            If B IsNot Nothing AndAlso TypeOf B Is JArray Then
                For Each x In CType(B, JArray)
                    arr.Add(x.DeepClone())
                Next
            End If
            Return arr
        End Function

        ' ---------- 依赖库 ----------
        Public Sub DownloadLibraries(json As JObject, VersionName As String)
            Dim libs = json("libraries")
            If libs Is Nothing Then Exit Sub
            Dim total As Integer = libs.Count()
            Dim i = 0
            For Each lib In libs
                i += 1
                If Not LibAllowed(lib) Then Continue For
                Dim dl = lib("downloads")
                Dim artifact = If(dl IsNot Nothing, dl("artifact"), Nothing)
                If artifact IsNot Nothing Then
                    Dim path = If(artifact("path")?.ToString(), MavenToPath(lib("name").ToString()))
                    Dim dest = Path.Combine(DirLibraries, path.Replace("/", "\"))
                    EventBus.RaiseProgress("安装版本", 0.1 + 0.6 * i / total, $"依赖库 {i}/{total}：{Path.GetFileName(path)}")
                    DownloadFile(artifact("url").ToString(), dest, If(artifact("sha1")?.ToString(), ""), "安装版本", Path.GetFileName(path))
                End If
                If dl Is Nothing Then Continue For
                ' natives
                Dim natives = lib("natives")
                If natives IsNot Nothing AndAlso natives("windows") IsNot Nothing Then
                    Dim key = natives("windows").ToString().Replace("${arch}", If(IntPtr.Size = 8, "64", "32"))
                    Dim classifierObj = If(dl("classifiers"), Nothing)
                    If classifierObj IsNot Nothing AndAlso classifierObj(key) IsNot Nothing Then
                        Dim na = classifierObj(key)
                        Dim npath = If(na("path")?.ToString(), "")
                        Dim ndest = Path.Combine(Path.Combine(DirVersions, VersionName), "natives\" & Path.GetFileName(npath))
                        DownloadFile(na("url").ToString(), ndest, If(na("sha1")?.ToString(), ""), "安装版本", "natives")
                        Try
                            ExtractZip(ndest, Path.Combine(DirVersions, VersionName, "natives"))
                        Catch ex As Exception
                            Log("解压 native 失败：" & ex.Message, LogLevel.Warn)
                        End Try
                    End If
                End If
            Next
        End Sub

        ''' <summary>library 的 rules 判定（只保留适用于 Windows 的）</summary>
        Public Function LibAllowed(lib As JToken) As Boolean
            Dim rules = lib("rules")
            If rules Is Nothing Then Return True
            Dim allow = False
            For Each r In rules
                Dim action = If(r("action")?.ToString(), "allow")
                Dim os = r("os")
                Dim match As Boolean = True
                If os IsNot Nothing Then
                    Dim name = If(os("name")?.ToString(), "")
                    If name <> "" AndAlso name <> "windows" Then match = False
                End If
                If match Then allow = (action = "allow")
            Next
            Return allow
        End Function

        ' ---------- 资源 ----------
        Public Sub DownloadAssets(json As JObject, VersionName As String)
            Dim idxInfo = json("assetIndex")
            If idxInfo Is Nothing Then Exit Sub
            Dim idxId = If(json("assets")?.ToString(), idxInfo("id").ToString())
            Dim idxPath = Path.Combine(DirAssets, "indexes", idxId & ".json")
            Directory.CreateDirectory(Path.Combine(DirAssets, "indexes"))
            If Not File.Exists(idxPath) Then
                DownloadFile(idxInfo("url").ToString(), idxPath, If(idxInfo("sha1")?.ToString(), ""), "安装版本", "资源索引")
            End If
            If Not File.Exists(idxPath) Then Exit Sub
            Dim idx = JObject.Parse(File.ReadAllText(idxPath))
            Dim objs = idx("objects")
            If objs Is Nothing Then Exit Sub
            Dim keys = CType(objs, JObject).Properties().Select(Function(p) p.Name).ToList()
            Dim total = keys.Count
            Dim i = 0
            For Each k In keys
                i += 1
                Dim o = objs(k)
                Dim hash = o("hash").ToString()
                Dim rel = hash.Substring(0, 2) & "\" & hash
                Dim dest = Path.Combine(DirAssets, "objects", rel)
                If File.Exists(dest) Then Continue For
                Dim url = $"https://resources.download.minecraft.net/{hash.Substring(0, 2)}/{hash}"
                If i Mod 20 = 0 OrElse i = total Then
                    EventBus.RaiseProgress("安装版本", 0.7 + 0.3 * i / total, $"资源 {i}/{total}")
                End If
                Try
                    DownloadFile(url, dest, hash, "安装版本", "资源")
                Catch ex As Exception
                    Log($"资源 {hash} 下载失败：{ex.Message}", LogLevel.Warn)
                End Try
            Next
        End Sub

        ' ---------- 删除 ----------
        Public Sub DeleteVersion(VersionName As String)
            Dim dir = Path.Combine(DirVersions, VersionName)
            If Directory.Exists(dir) Then Directory.Delete(dir, True)
            Log("已删除版本：" & VersionName)
        End Sub

    End Module

End Namespace
