Imports System.Diagnostics
Imports System.IO
Imports System.Text
Imports Newtonsoft.Json.Linq

Namespace PCLNeo

    ''' <summary>
    ''' 启动模块：生成 classpath、拼装 JVM / 游戏参数、拉起 java 进程并收集日志
    ''' </summary>
    Public Module ModLaunch

        Private _proc As Process = Nothing
        ''' <summary>当前游戏进程（未运行时为 Nothing）</summary>
        Public ReadOnly Property GameProcess As Process
            Get
                Return _proc
            End Get
        End Property
        Public ReadOnly Property IsRunning As Boolean
            Get
                Return _proc IsNot Nothing AndAlso Not _proc.HasExited
            End Get
        End Property

        ''' <summary>读取并展开版本 JSON（处理 inheritsFrom）</summary>
        Public Function GetVersionJson(VersionName As String) As JObject
            Dim p = Path.Combine(DirVersions, VersionName, VersionName & ".json")
            If Not File.Exists(p) Then Throw New Exception($"版本 {VersionName} 不存在")
            Return MergeInherits(JObject.Parse(File.ReadAllText(p)))
        End Function

        ''' <summary>拼装 classpath：libraries + 客户端 jar</summary>
        Public Function BuildClasspath(json As JObject, VersionName As String) As String
            Dim sb As New StringBuilder()
            Dim libs = json("libraries")
            If libs IsNot Nothing Then
                For Each library In libs
                    If Not LibAllowed(library) Then Continue For
                    Dim dl = library("downloads")
                    If dl Is Nothing Then Continue For
                    Dim art = dl("artifact")
                    If art Is Nothing Then Continue For
                    Dim rel = If(art("path")?.ToString(), MavenToPath(library("name").ToString()))
                    Dim full = Path.Combine(DirLibraries, rel.Replace("/", "\"))
                    If File.Exists(full) Then
                        If sb.Length > 0 Then sb.Append(";"c)
                        sb.Append(full)
                    Else
                        Log("缺少依赖库（已跳过）：" & rel, LogLevel.Warn)
                    End If
                Next
            End If
            Dim jar = Path.Combine(DirVersions, VersionName, VersionName & ".jar")
            If Not File.Exists(jar) Then
                ' Fabric / Quilt 等 profile 自身没有 jar，沿用到父版本
                Dim parent = If(json("inheritsFrom")?.ToString(), "")
                If parent <> "" Then
                    Dim pj = Path.Combine(DirVersions, parent, parent & ".jar")
                    If File.Exists(pj) Then jar = pj
                End If
            End If
            If File.Exists(jar) Then
                If sb.Length > 0 Then sb.Append(";"c)
                sb.Append(jar)
            End If
            Return sb.ToString()
        End Function

        ''' <summary>把参数中的占位符替换成真实值</summary>
        Private Function ReplaceTokens(Arg As String, acc As Account, VersionName As String, json As JObject) As String
            Dim assetIndex = If(json("assets")?.ToString(), If(json("assetIndex")?("id")?.ToString(), "legacy"))
            Dim userType = If(acc.Type = AuthType.Offline, "legacy", "mojang")
            Arg = Arg.Replace("${auth_player_name}", acc.Name)
            Arg = Arg.Replace("${auth_uuid}", acc.Uuid)
            Arg = Arg.Replace("${auth_access_token}", acc.AccessToken)
            Arg = Arg.Replace("${auth_session}", acc.AccessToken)
            Arg = Arg.Replace("${user_type}", userType)
            Arg = Arg.Replace("${user_properties}", "{}")
            Arg = Arg.Replace("${version_name}", VersionName)
            Arg = Arg.Replace("${game_directory}", PathMc)
            Arg = Arg.Replace("${assets_root}", DirAssets)
            Arg = Arg.Replace("${game_assets}", Path.Combine(DirAssets, "virtual", "legacy"))
            Arg = Arg.Replace("${assets_index_name}", assetIndex)
            Arg = Arg.Replace("${natives_directory}", Path.Combine(DirVersions, VersionName, "natives"))
            Arg = Arg.Replace("${launcher_name}", AppName)
            Arg = Arg.Replace("${launcher_version}", AppVersion)
            Return Arg
        End Function

        ''' <summary>从 arguments（含 rules）中收集字符串参数</summary>
        Private Function CollectArgs(node As JToken, acc As Account, VersionName As String, json As JObject) As List(Of String)
            Dim list As New List(Of String)()
            If node Is Nothing Then Return list
            For Each a In node
                If TypeOf a Is JValue Then
                    list.Add(ReplaceTokens(a.ToString(), acc, VersionName, json))
                ElseIf TypeOf a Is JObject Then
                    Dim rules = a("rules")
                    Dim ok As Boolean = True
                    If rules IsNot Nothing Then
                        ok = False
                        For Each r In rules
                            Dim action = If(r("action")?.ToString(), "allow")
                            Dim os = r("os")
                            Dim match As Boolean = True
                            If os IsNot Nothing Then
                                Dim nm = If(os("name")?.ToString(), "")
                                If nm <> "" AndAlso nm <> "windows" Then match = False
                                If os("arch") IsNot Nothing AndAlso os("arch").ToString() = "x86" Then match = False
                            End If
                            Dim feats = a("features")
                            If match AndAlso feats IsNot Nothing Then match = False ' 默认不启用自定义特性
                            If match Then ok = (action = "allow")
                        Next
                    End If
                    If Not ok Then Continue For
                    Dim val = a("value")
                    If val Is Nothing Then Continue For
                    If TypeOf val Is JArray Then
                        For Each x In CType(val, JArray)
                            list.Add(ReplaceTokens(x.ToString(), acc, VersionName, json))
                        Next
                    Else
                        list.Add(ReplaceTokens(val.ToString(), acc, VersionName, json))
                    End If
                End If
            Next
            Return list
        End Function

        ''' <summary>
        ''' 启动游戏。VersionName 为版本名，acc 为账号，javaPath 为空则自动选择
        ''' </summary>
        Public Function Launch(VersionName As String, acc As Account, Optional JavaPath As String = "") As Process
            If IsRunning Then Throw New Exception("游戏已在运行中")
            EnsureDirs()
            acc = ModAuth.EnsureValid(acc)
            Dim json = GetVersionJson(VersionName)

            If String.IsNullOrWhiteSpace(JavaPath) Then JavaPath = ModJava.GetBestJava(json)
            If String.IsNullOrWhiteSpace(JavaPath) OrElse Not File.Exists(JavaPath) Then
                Throw New Exception("未找到可用的 Java，请在设置中指定或下载")
            End If
            Log($"使用 Java：{JavaPath}")

            Dim nativesDir = Path.Combine(DirVersions, VersionName, "natives")
            Directory.CreateDirectory(nativesDir)
            Dim classpath = BuildClasspath(json, VersionName)
            Dim mainClass = If(json("mainClass")?.ToString(), "net.minecraft.client.main.Main")

            ' ---- JVM 参数 ----
            Dim jvmArgs As New List(Of String)()
            Dim mem = SetupGetInt("MemoryMax", 2048)
            jvmArgs.Add($"-Xmx{mem}M")
            Dim memMin = SetupGetInt("MemoryMin", 512)
            If memMin > 0 Then jvmArgs.Add($"-Xms{memMin}M")
            jvmArgs.Add($"-Djava.library.path=""{nativesDir}""")
            jvmArgs.Add("-Dminecraft.launcher.brand=" & AppName.Replace(" ", ""))
            jvmArgs.Add("-Dminecraft.launcher.version=" & AppVersion)
            If SetupGetBool("UseZgc", False) Then jvmArgs.Add("-XX:+UseZGC")
            Dim jsonJvm = If(json("arguments")?("jvm"), Nothing)
            If jsonJvm IsNot Nothing Then jvmArgs.AddRange(CollectArgs(jsonJvm, acc, VersionName, json))
            Dim customJvm = SetupGet("JvmArgs", "")
            If Not String.IsNullOrWhiteSpace(customJvm) Then
                For Each s In customJvm.Split({" "c}, StringSplitOptions.RemoveEmptyEntries)
                    jvmArgs.Add(ReplaceTokens(s, acc, VersionName, json))
                Next
            End If

            ' ---- 游戏参数 ----
            Dim gameArgs As New List(Of String)()
            Dim jsonGame = If(json("arguments")?("game"), Nothing)
            If jsonGame IsNot Nothing Then
                gameArgs.AddRange(CollectArgs(jsonGame, acc, VersionName, json))
            ElseIf json("minecraftArguments") IsNot Nothing Then
                For Each s In json("minecraftArguments").ToString().Split({" "c}, StringSplitOptions.RemoveEmptyEntries)
                    gameArgs.Add(ReplaceTokens(s, acc, VersionName, json))
                Next
            End If
            If SetupGetBool("CustomSize", False) Then
                gameArgs.Add("--width")
                gameArgs.Add(SetupGetInt("WindowWidth", 854).ToString())
                gameArgs.Add("--height")
                gameArgs.Add(SetupGetInt("WindowHeight", 480).ToString())
            End If
            If SetupGetBool("FullScreen", False) Then gameArgs.Add("--fullscreen")
            Dim customGame = SetupGet("GameArgs", "")
            If Not String.IsNullOrWhiteSpace(customGame) Then
                For Each s In customGame.Split({" "c}, StringSplitOptions.RemoveEmptyEntries)
                    gameArgs.Add(ReplaceTokens(s, acc, VersionName, json))
                Next
            End If

            ' ---- 组装 ----
            Dim args As New List(Of String)()
            args.AddRange(jvmArgs)
            args.Add("-cp")
            args.Add("""" & classpath & """")
            args.Add(mainClass)
            args.AddRange(gameArgs)

            Dim psi As New ProcessStartInfo(JavaPath)
            psi.Arguments = String.Join(" ", args.Select(Function(s)
                                                             If s.Contains(" ") AndAlso Not s.StartsWith("""") Then Return """" & s & """"
                                                             Return s
                                                         End Function))
            psi.WorkingDirectory = PathMc
            psi.UseShellExecute = False
            psi.RedirectStandardOutput = True
            psi.RedirectStandardError = True
            psi.CreateNoWindow = SetupGetBool("HideConsole", False)
            psi.StandardOutputEncoding = Encoding.UTF8
            psi.StandardErrorEncoding = Encoding.UTF8

            Log("启动命令：" & JavaPath & " " & psi.Arguments)
            _proc = New Process() With {.StartInfo = psi, .EnableRaisingEvents = True}
            AddHandler _proc.OutputDataReceived, AddressOf OnOutput
            AddHandler _proc.ErrorDataReceived, AddressOf OnOutput
            AddHandler _proc.Exited, Sub(s, e)
                                         Log($"游戏进程退出，退出码：{_proc.ExitCode}")
                                         EventBus.RaiseProgress("启动", 1, "游戏已退出")
                                     End Sub
            _proc.Start()
            _proc.BeginOutputReadLine()
            _proc.BeginErrorReadLine()
            EventBus.RaiseProgress("启动", 1, "游戏已启动")
            Return _proc
        End Function

        Private Sub OnOutput(sender As Object, e As DataReceivedEventArgs)
            If e.Data Is Nothing Then Exit Sub
            Log("[游戏] " & e.Data, LogLevel.Debug)
        End Sub

        Public Sub KillGame()
            Try
                If IsRunning Then _proc.Kill(True)
            Catch ex As Exception
                Log("结束游戏失败：" & ex.Message, LogLevel.Warn)
            End Try
        End Sub

    End Module

End Namespace
