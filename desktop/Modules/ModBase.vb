Imports System.IO
Imports System.IO.Compression
Imports System.Net.Http
Imports System.Security.Cryptography
Imports System.Text
Imports Newtonsoft.Json.Linq

Namespace PCLNeo

    ''' <summary>日志级别</summary>
    Public Enum LogLevel
        Debug = 0
        Info = 1
        Warn = 2
        [Error] = 3
        Critical = 4
    End Enum

    ''' <summary>全局事件总线（日志 + 下载进度）</summary>
    Public Class EventBus
        ''' <summary>日志事件</summary>
        Public Shared Event OnLog(Message As String, Level As LogLevel)
        ''' <summary>进度事件：Progress 为 0~1，小于 0 表示不确定</summary>
        Public Shared Event OnProgress(Task As String, Progress As Double, Status As String)

        Public Shared Sub RaiseLog(Message As String, Level As LogLevel)
            RaiseEvent OnLog(Message, Level)
        End Sub
        Public Shared Sub RaiseProgress(Task As String, Progress As Double, Status As String)
            RaiseEvent OnProgress(Task, Progress, Status)
        End Sub
    End Class

    ''' <summary>
    ''' 全局常量、路径、设置存储与通用工具（对应 PCL2 的 ModBase.vb）
    ''' </summary>
    Public Module ModBase

        Public Const AppName As String = "PCL Neo"
        Public Const AppVersion As String = "1.0.0"
        ''' <summary>下载源：Official / BMCLAPI</summary>
        Public Const SourceOfficial As String = "Official"
        Public Const SourceBmclapi As String = "BMCLAPI"

        Private ReadOnly _http As New HttpClient()
        Private _setup As JObject = Nothing
        Public ReadOnly Property SetupPath As String
            Get
                Return Path.Combine(PathData, "setup.json")
            End Get
        End Property

        ' ---------- 路径 ----------
        ''' <summary>数据与游戏根目录（%APPDATA%\PCLNeo，可在设置中修改）</summary>
        Public ReadOnly Property PathData As String
            Get
                Static _dir As String =
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) & "\PCLNeo"
                Return _dir
            End Get
        End Property

        Public Property PathMc As String
            Get
                Return SetupGet("PathMc", PathData & "\.minecraft")
            End Get
            Set(value As String)
                SetupSet("PathMc", value)
            End Set
        End Property

        ''' <summary>版本目录</summary>
        Public ReadOnly Property DirVersions As String
            Get
                Return PathMc & "\versions"
            End Get
        End Property
        ''' <summary>依赖库目录</summary>
        Public ReadOnly Property DirLibraries As String
            Get
                Return PathMc & "\libraries"
            End Get
        End Property
        ''' <summary>资源目录</summary>
        Public ReadOnly Property DirAssets As String
            Get
                Return PathMc & "\assets"
            End Get
        End Property

        Public Sub EnsureDirs()
            Directory.CreateDirectory(PathData)
            Directory.CreateDirectory(PathMc)
            Directory.CreateDirectory(DirVersions)
            Directory.CreateDirectory(DirLibraries)
            Directory.CreateDirectory(DirAssets)
        End Sub

        ' ---------- 日志 ----------
        ''' <summary>写日志：文件 + 内存缓冲 + 事件</summary>
        Public Sub Log(Message As String, Optional Level As LogLevel = LogLevel.Info)
            Dim line As String = $"[{DateTime.Now:HH:mm:ss}] [{Level}] {Message}"
            Try
                SyncLock LogBuffer
                    LogBuffer.AppendLine(line)
                    If LogBuffer.Length > 400000 Then LogBuffer.Remove(0, 200000)
                End SyncLock
            Catch
            End Try
            Try
                EventBus.RaiseLog(line, Level)
            Catch
            End Try
            Try
                File.AppendAllText(PathData & "\PCLNeo.log", line & vbCrLf)
            Catch
            End Try
        End Sub

        Private ReadOnly LogBuffer As New StringBuilder()
        Public Function GetLogText() As String
            SyncLock LogBuffer
                Return LogBuffer.ToString()
            End SyncLock
        End Function

        ' ---------- 设置 ----------
        Private Function LoadSetup() As JObject
            If _setup Is Nothing Then
                Try
                    Directory.CreateDirectory(PathData)
                    If File.Exists(SetupPath) Then
                        _setup = JObject.Parse(File.ReadAllText(SetupPath))
                    Else
                        _setup = New JObject()
                    End If
                Catch ex As Exception
                    _setup = New JObject()
                End Try
            End If
            Return _setup
        End Function

        Public Function SetupGet(Key As String, Optional DefaultValue As String = "") As String
            Dim o = LoadSetup()
            If o(Key) Is Nothing Then Return DefaultValue
            Return CStr(o(Key))
        End Function
        Public Function SetupGetInt(Key As String, Optional DefaultValue As Integer = 0) As Integer
            Dim s = SetupGet(Key, DefaultValue.ToString())
            Dim i As Integer
            If Integer.TryParse(s, i) Then Return i
            Return DefaultValue
        End Function
        Public Function SetupGetBool(Key As String, Optional DefaultValue As Boolean = False) As Boolean
            Dim s = SetupGet(Key, If(DefaultValue, "True", "False"))
            Return s.Trim().ToLower() = "true"
        End Function
        Public Sub SetupSet(Key As String, Value As String)
            Dim o = LoadSetup()
            o(Key) = Value
            Try
                File.WriteAllText(SetupPath, o.ToString(Newtonsoft.Json.Formatting.Indented))
            Catch ex As Exception
                Log("保存设置失败：" & ex.Message, LogLevel.Warn)
            End Try
        End Sub

        ' ---------- 下载源 ----------
        Public ReadOnly Property UseMirror As Boolean
            Get
                Return SetupGet("DownloadSource", SourceBmclapi) = SourceBmclapi
            End Get
        End Property

        Public Function Mirror(url As String) As String
            If Not UseMirror Then Return url
            url = url.Replace("https://launchermeta.mojang.com", "https://bmclapi2.bangbang93.com")
            url = url.Replace("https://piston-meta.mojang.com", "https://bmclapi2.bangbang93.com")
            url = url.Replace("https://launcher.mojang.com", "https://bmclapi2.bangbang93.com")
            url = url.Replace("https://resources.download.minecraft.net", "https://bmclapi2.bangbang93.com/assets")
            url = url.Replace("https://libraries.minecraft.net", "https://bmclapi2.bangbang93.com/maven")
            Return url
        End Function

        ' ---------- HTTP ----------
        Public Function HttpGetString(Url As String) As String
            Using req As New HttpRequestMessage(HttpMethod.Get, Mirror(Url))
                req.Headers.UserAgent.ParseAdd($"PCLNeo/{AppVersion}")
                Dim resp = _http.Send(req)
                resp.EnsureSuccessStatusCode()
                Return resp.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            End Using
        End Function

        Public Function HttpGetStringWithHeaders(Url As String, Headers As Dictionary(Of String, String)) As String
            Using req As New HttpRequestMessage(HttpMethod.Get, Mirror(Url))
                req.Headers.UserAgent.ParseAdd($"PCLNeo/{AppVersion}")
                For Each kv In Headers
                    req.Headers.TryAddWithoutValidation(kv.Key, kv.Value)
                Next
                Dim resp = _http.Send(req)
                resp.EnsureSuccessStatusCode()
                Return resp.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            End Using
        End Function

        Public Function HttpGetJsonWithHeaders(Url As String, Headers As Dictionary(Of String, String)) As JObject
            Return JObject.Parse(HttpGetStringWithHeaders(Url, Headers))
        End Function

        Public Function HttpGetJsonArrayWithHeaders(Url As String, Headers As Dictionary(Of String, String)) As JArray
            Return JArray.Parse(HttpGetStringWithHeaders(Url, Headers))
        End Function

        Public Function HttpGetJson(Url As String) As JObject
            Return JObject.Parse(HttpGetString(Url))
        End Function

        Public Function HttpGetJsonArray(Url As String) As JArray
            Return JArray.Parse(HttpGetString(Url))
        End Function

        ''' <summary>POST JSON，返回 JObject</summary>
        Public Function HttpPostJson(Url As String, Body As Object) As JObject
            Dim json As String
            If TypeOf Body Is String Then
                json = CStr(Body)
            Else
                json = CType(Body, JObject).ToString()
            End If
            Using content As New StringContent(json, Encoding.UTF8, "application/json")
                Dim resp = _http.PostAsync(Mirror(Url), content).GetAwaiter().GetResult()
                Dim text = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult()
                If Not resp.IsSuccessStatusCode Then
                    Throw New Exception($"HTTP {CInt(resp.StatusCode)}: {text}")
                End If
                If String.IsNullOrWhiteSpace(text) Then Return New JObject()
                Return JObject.Parse(text)
            End Using
        End Function

        ''' <summary>POST 表单（微软 OAuth 使用）</summary>
        Public Function HttpPostForm(Url As String, Form As Dictionary(Of String, String)) As JObject
            Using content As New FormUrlEncodedContent(Form)
                Dim resp = _http.PostAsync(Mirror(Url), content).GetAwaiter().GetResult()
                Dim text = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult()
                If String.IsNullOrWhiteSpace(text) Then Return New JObject()
                Return JObject.Parse(text)
            End Using
        End Function

        ''' <summary>下载文件，可选校验 SHA1，task 用于进度事件</summary>
        Public Sub DownloadFile(Url As String, SavePath As String, Optional Sha1 As String = "",
                               Optional TaskName As String = "下载", Optional Hint As String = "")
            Directory.CreateDirectory(Path.GetDirectoryName(SavePath))
            Dim realUrl = Mirror(Url)
            If String.IsNullOrEmpty(Sha1) AndAlso File.Exists(SavePath) Then
                EventBus.RaiseProgress(TaskName, 1, $"已存在：{Path.GetFileName(SavePath)}")
                Return
            End If
            Using resp = _http.GetAsync(realUrl, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult()
                resp.EnsureSuccessStatusCode()
                Dim total = If(resp.Content.Headers.ContentLength, -1)
                Using fs As New FileStream(SavePath, FileMode.Create, FileAccess.Write, FileShare.None)
                    Using ns = resp.Content.ReadAsStreamAsync().GetAwaiter().GetResult()
                        Dim buf(81919) As Byte
                        Dim read As Integer
                        Dim done As Long = 0
                        Dim last As Integer = -1
                        Do
                            read = ns.Read(buf, 0, buf.Length)
                            If read <= 0 Then Exit Do
                            fs.Write(buf, 0, read)
                            done += read
                            If total > 0 Then
                                Dim p = CInt(done * 100 / total)
                                If p <> last Then
                                    last = p
                                    EventBus.RaiseProgress(TaskName, done / total, $"{Hint} {p}%  ({done \ 1024}KB/{total \ 1024}KB)")
                                End If
                            End If
                        Loop
                    End Using
                End Using
            End Using
            If Not String.IsNullOrEmpty(Sha1) AndAlso GetSha1(SavePath).ToLower() <> Sha1.ToLower() Then
                Throw New Exception("文件校验失败（SHA1 不一致）：" & SavePath)
            End If
        End Sub

        ' ---------- 工具 ----------
        Public Function GetSha1(FilePath As String) As String
            Using fs As New FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read)
                Using sha As New SHA1Managed()
                    Dim h = sha.ComputeHash(fs)
                    Return BitConverter.ToString(h).Replace("-", "").ToLower()
                End Using
            End Using
        End Function

        ''' <summary>解压 zip 到目录</summary>
        Public Sub ExtractZip(ZipPath As String, TargetDir As String, Optional FilterPrefix As String = "")
            Directory.CreateDirectory(TargetDir)
            Using zf = ZipFile.OpenRead(ZipPath)
                For Each entry In zf.Entries
                    If entry.FullName.EndsWith("/") Then Continue For
                    If FilterPrefix <> "" AndAlso Not entry.FullName.StartsWith(FilterPrefix) Then Continue For
                    Dim dest = Path.Combine(TargetDir, entry.FullName.Replace("/", "\"))
                    Directory.CreateDirectory(Path.GetDirectoryName(dest))
                    Try
                        entry.ExtractToFile(dest, True)
                    Catch
                    End Try
                Next
            End Using
        End Sub

        ''' <summary>把路径中的非法字符替换掉</summary>
        Public Function SafeName(Name As String) As String
            For Each c In Path.GetInvalidFileNameChars()
                Name = Name.Replace(c, "_"c)
            Next
            Return Name
        End Function

        ''' <summary>解析 Maven 坐标 → 相对路径（net.fabricmc:lib:1.0 → net/fabricmc/lib/1.0/lib-1.0.jar）</summary>
        Public Function MavenToPath(Name As String, Optional Extension As String = "jar") As String
            Dim p = Name.Split(":"c)
            If p.Length < 3 Then Return Name
            Dim group = p(0).Replace("."c, "/"c)
            Dim artifact = p(1)
            Dim version = p(2)
            Dim classifier = If(p.Length > 3, "-" & p(3), "")
            Return $"{group}/{artifact}/{version}/{artifact}-{version}{classifier}.{Extension}"
        End Function

    End Module

End Namespace
