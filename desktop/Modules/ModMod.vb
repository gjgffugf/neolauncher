Imports System.IO
Imports Newtonsoft.Json.Linq

Namespace PCLNeo

    Public Enum ModSource
        Modrinth = 0
        CurseForge = 1
    End Enum

    ''' <summary>模组搜索结果</summary>
    Public Class ModItem
        Public Property Id As String = ""
        Public Property Slug As String = ""
        Public Property Title As String = ""
        Public Property Description As String = ""
        Public Property IconUrl As String = ""
        Public Property Downloads As Long = 0
        Public Property Source As ModSource = ModSource.Modrinth
        Public Overrides Function ToString() As String
            Dim src = If(Source = ModSource.Modrinth, "Modrinth", "CurseForge")
            Return $"{Title}（{src}）"
        End Function
    End Class

    ''' <summary>模组的一个可下载文件</summary>
    Public Class ModFileItem
        Public Property Name As String = ""
        Public Property Url As String = ""
        Public Property FileName As String = ""
        Public Property Sha1 As String = ""
        Public Property McVersions As String = ""
        Public Property Loaders As String = ""
        Public Property Source As ModSource = ModSource.Modrinth
        Public Overrides Function ToString() As String
            Return $"{Name}  [{McVersions}]"
        End Function
    End Class

    ''' <summary>
    ''' 模组模块：Modrinth / CurseForge 搜索与下载安装
    ''' </summary>
    Public Module ModMod

        ''' <summary>CurseForge API Key（设置中填写）</summary>
        Public ReadOnly Property CfApiKey As String
            Get
                Return SetupGet("CfApiKey", "")
            End Get
        End Property

        Private ReadOnly Property CfHeaders As Dictionary(Of String, String)
            Get
                Return New Dictionary(Of String, String) From {{"x-api-key", CfApiKey}}
            End Get
        End Property

        ''' <summary>取当前版本的 mods 目录（支持版本隔离）</summary>
        Public Function GetModsDir(VersionName As String) As String
            If SetupGetBool("VersionIsolate", True) Then
                Return Path.Combine(DirVersions, VersionName, "mods")
            End If
            Return Path.Combine(PathMc, "mods")
        End Function

        ' ---------- Modrinth ----------
        Public Function SearchModrinth(Keyword As String, McVersion As String, Loader As String,
                                       Optional Limit As Integer = 20) As List(Of ModItem)
            Dim list As New List(Of ModItem)()
            Dim facets As New JArray()
            facets.Add(JArray.Parse($"[""project_type:mod""]"))
            If McVersion <> "" Then facets.Add(JArray.Parse($"[""versions:{McVersion}""]"))
            If Loader <> "" AndAlso Loader <> "Any" Then facets.Add(JArray.Parse($"[""categories:{Loader.ToLower()}""]"))
            Dim url = $"https://api.modrinth.com/v2/search?query={Uri.EscapeDataString(Keyword)}" &
                      $"&limit={Limit}&index=downloads&facets={Uri.EscapeDataString(facets.ToString(Newtonsoft.Json.Formatting.None))}"
            Dim json = HttpGetJson(url)
            For Each h In If(json("hits"), New JArray())
                list.Add(New ModItem With {
                    .Id = If(h("project_id")?.ToString(), If(h("slug")?.ToString(), "")),
                    .Slug = If(h("slug")?.ToString(), ""),
                    .Title = If(h("title")?.ToString(), ""),
                    .Description = If(h("description")?.ToString(), ""),
                    .IconUrl = If(h("icon_url")?.ToString(), ""),
                    .Downloads = CLng(Val(If(h("downloads")?.ToString(), "0"))),
                    .Source = ModSource.Modrinth
                })
            Next
            Return list
        End Function

        Public Function GetModrinthFiles(ProjectId As String, McVersion As String, Loader As String) As List(Of ModFileItem)
            Dim list As New List(Of ModFileItem)()
            Dim url = $"https://api.modrinth.com/v2/project/{ProjectId}/version"
            If McVersion <> "" OrElse (Loader <> "" AndAlso Loader <> "Any") Then
                Dim q As New List(Of String)()
                If Loader <> "" AndAlso Loader <> "Any" Then q.Add("loaders=[""" & Loader.ToLower() & """]")
                If McVersion <> "" Then q.Add("game_versions=" & Uri.EscapeDataString("[""" & McVersion & """]"))
                url &= "?" & String.Join("&", q)
            End If
            Dim arr = HttpGetJsonArray(url)
            For Each v In arr
                Dim files = If(v("files"), New JArray())
                Dim primary = files.FirstOrDefault(Function(f) f("primary") IsNot Nothing AndAlso f("primary").ToString().ToLower() = "true")
                If primary Is Nothing Then primary = files.FirstOrDefault()
                If primary Is Nothing Then Continue For
                list.Add(New ModFileItem With {
                    .Name = If(v("version_number")?.ToString(), If(v("name")?.ToString(), "未命名")),
                    .Url = primary("url").ToString(),
                    .FileName = primary("filename").ToString(),
                    .Sha1 = If(primary("hashes")?("sha1")?.ToString(), ""),
                    .McVersions = String.Join(",", If(v("game_versions"), New JArray()).Select(Function(x) x.ToString())),
                    .Loaders = String.Join(",", If(v("loaders"), New JArray()).Select(Function(x) x.ToString())),
                    .Source = ModSource.Modrinth
                })
            Next
            Return list
        End Function

        ' ---------- CurseForge ----------
        Public Function SearchCurseForge(Keyword As String, McVersion As String, Loader As String) As List(Of ModItem)
            If CfApiKey = "" Then Throw New Exception("请先在设置中填写 CurseForge API Key")
            Dim loaderType = If(Loader = "forge", "1", If(Loader = "fabric", "4", If(Loader = "quilt", "5", "0")))
            Dim url = $"https://api.curseforge.com/v1/mods/search?gameId=432&searchFilter={Uri.EscapeDataString(Keyword)}&pageSize=20"
            If McVersion <> "" Then url &= "&gameVersion=" & McVersion
            If loaderType <> "0" Then url &= "&modLoaderType=" & loaderType
            Dim json = HttpGetJsonWithHeaders(url, CfHeaders)
            Dim list As New List(Of ModItem)()
            For Each m In If(json("data"), New JArray())
                Dim logo = If(m("logo")?("thumbnailUrl")?.ToString(), "")
                list.Add(New ModItem With {
                    .Id = If(m("id")?.ToString(), ""),
                    .Slug = If(m("slug")?.ToString(), ""),
                    .Title = If(m("name")?.ToString(), ""),
                    .Description = If(m("summary")?.ToString(), ""),
                    .IconUrl = logo,
                    .Downloads = CLng(Val(If(m("downloadCount")?.ToString(), "0"))),
                    .Source = ModSource.CurseForge
                })
            Next
            Return list
        End Function

        Public Function GetCurseForgeFiles(ModId As String, McVersion As String) As List(Of ModFileItem)
            Dim url = $"https://api.curseforge.com/v1/mods/{ModId}/files?pageSize=50"
            If McVersion <> "" Then url &= "&gameVersion=" & McVersion
            Dim json = HttpGetJsonWithHeaders(url, CfHeaders)
            Dim list As New List(Of ModFileItem)()
            For Each f In If(json("data"), New JArray())
                Dim fid = f("id").ToString()
                Dim dl = ""
                Try
                    Dim djson = HttpGetJsonWithHeaders($"https://api.curseforge.com/v1/mods/{ModId}/files/{fid}/download-url", CfHeaders)
                    dl = If(djson("data")?.ToString(), "")
                Catch
                End Try
                If dl = "" Then dl = If(f("downloadUrl")?.ToString(), "")
                If dl = "" Then Continue For
                list.Add(New ModFileItem With {
                    .Name = If(f("displayName")?.ToString(), If(f("fileName")?.ToString(), fid)),
                    .Url = dl,
                    .FileName = If(f("fileName")?.ToString(), fid & ".jar"),
                    .McVersions = String.Join(",", If(f("gameVersions"), New JArray()).Select(Function(x) x.ToString())),
                    .Source = ModSource.CurseForge
                })
            Next
            Return list
        End Function

        ' ---------- 安装 ----------
        ''' <summary>把模组下载到目标版本的 mods 目录</summary>
        Public Sub InstallMod(File As ModFileItem, VersionName As String)
            Dim dir = GetModsDir(VersionName)
            Directory.CreateDirectory(dir)
            Dim dest = Path.Combine(dir, SafeName(File.FileName))
            EventBus.RaiseProgress("下载模组", 0, File.FileName)
            DownloadFile(File.Url, dest, "", "下载模组", File.Name)
            EventBus.RaiseProgress("下载模组", 1, $"{File.Name} 已安装到 {dir}")
            Log($"模组已安装：{dest}")
        End Sub

        ''' <summary>列出已安装模组</summary>
        Public Function ListInstalledMods(VersionName As String) As List(Of String)
            Dim dir = GetModsDir(VersionName)
            Dim list As New List(Of String)()
            If Directory.Exists(dir) Then
                For Each f In Directory.GetFiles(dir, "*.jar")
                    list.Add(Path.GetFileName(f))
                Next
                For Each f In Directory.GetFiles(dir, "*.jar.disabled")
                    list.Add(Path.GetFileName(f))
                Next
            End If
            Return list
        End Function

        ''' <summary>禁用 / 启用模组（改名 .jar.disabled）</summary>
        Public Sub ToggleMod(VersionName As String, FileName As String)
            Dim dir = GetModsDir(VersionName)
            Dim src = Path.Combine(dir, FileName)
            If Not File.Exists(src) Then Exit Sub
            If src.EndsWith(".disabled") Then
                File.Move(src, src.Substring(0, src.Length - 9))
            Else
                File.Move(src, src & ".disabled")
            End If
        End Sub

        Public Sub DeleteMod(VersionName As String, FileName As String)
            Dim p = Path.Combine(GetModsDir(VersionName), FileName)
            If File.Exists(p) Then File.Delete(p)
        End Sub

    End Module

End Namespace
