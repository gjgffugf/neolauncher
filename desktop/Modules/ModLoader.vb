Imports System.Diagnostics
Imports System.IO
Imports Newtonsoft.Json.Linq

Namespace PCLNeo

    ''' <summary>加载器条目（Forge / NeoForge / Fabric / Quilt 通用）</summary>
    Public Class LoaderEntry
        Public Property Name As String = ""       ' 显示名
        Public Property Version As String = ""    ' 加载器版本
        Public Overrides Function ToString() As String
            Return Name
        End Function
    End Class

    ''' <summary>
    ''' 模组加载器安装模块：Forge / NeoForge / Fabric / Quilt
    ''' </summary>
    Public Module ModLoader

        Private ReadOnly Property DirTemp As String
            Get
                Return Path.Combine(PathData, "temp")
            End Get
        End Property

        ' ============ Forge ============
        Public Function GetForgeList(McVersion As String) As List(Of LoaderEntry)
            Dim list As New List(Of LoaderEntry)()
            Dim url = If(UseMirror,
                $"https://bmclapi2.bangbang93.com/forge/minecraft/{McVersion}",
                $"https://bmclapi2.bangbang93.com/forge/minecraft/{McVersion}")
            Try
                Dim arr = HttpGetJsonArray(url)
                For Each o In arr
                    Dim v = If(o("version")?.ToString(), "")
                    If v <> "" Then list.Add(New LoaderEntry With {.Name = v, .Version = v})
                Next
            Catch ex As Exception
                Log("获取 Forge 列表失败：" & ex.Message, LogLevel.Warn)
            End Try
            Return list
        End Function

        ''' <summary>安装 Forge：下载 installer 并以 --installClient 静默安装</summary>
        Public Sub InstallForge(McVersion As String, ForgeVersion As String)
            Directory.CreateDirectory(DirTemp)
            Dim url = If(UseMirror,
                $"https://bmclapi2.bangbang93.com/forge/download?mcversion={McVersion}&version={ForgeVersion}&category=installer&format=jar",
                $"https://maven.minecraftforge.net/net/minecraftforge/forge/{McVersion}-{ForgeVersion}/forge-{McVersion}-{ForgeVersion}-installer.jar")
            Dim jar = Path.Combine(DirTemp, $"forge-{McVersion}-{ForgeVersion}-installer.jar")
            EventBus.RaiseProgress("安装 Forge", 0.1, "下载 Forge 安装器…")
            If Not File.Exists(jar) Then DownloadFile(url, jar, "", "安装 Forge", "installer.jar")
            RunInstaller(jar, $"--installClient ""{PathMc}""", "安装 Forge")
            EventBus.RaiseProgress("安装 Forge", 1, $"Forge {ForgeVersion} 安装完成")
            Log($"Forge {ForgeVersion}（MC {McVersion}）安装完成")
        End Sub

        ' ============ NeoForge ============
        Public Function GetNeoForgeList(McVersion As String) As List(Of LoaderEntry)
            Dim list As New List(Of LoaderEntry)()
            Try
                Dim arr As JArray
                If UseMirror Then
                    arr = HttpGetJsonArray($"https://bmclapi2.bangbang93.com/neoforge/version/list?mcversion={McVersion}")
                Else
                    arr = HttpGetJsonArray($"https://bmclapi2.bangbang93.com/neoforge/version/list?mcversion={McVersion}")
                End If
                For Each o In arr
                    Dim v = If(o("version")?.ToString(), "")
                    If v <> "" Then list.Add(New LoaderEntry With {.Name = v, .Version = v})
                Next
            Catch ex As Exception
                Log("获取 NeoForge 列表失败：" & ex.Message, LogLevel.Warn)
            End Try
            Return list
        End Function

        Public Sub InstallNeoForge(McVersion As String, NeoVersion As String)
            Directory.CreateDirectory(DirTemp)
            Dim mavenPath = $"net/neoforged/neoforge/{NeoVersion}/neoforge-{NeoVersion}-installer.jar"
            Dim url = If(UseMirror,
                "https://bmclapi2.bangbang93.com/maven/" & mavenPath,
                "https://maven.neoforged.net/releases/" & mavenPath)
            Dim jar = Path.Combine(DirTemp, $"neoforge-{NeoVersion}-installer.jar")
            EventBus.RaiseProgress("安装 NeoForge", 0.1, "下载 NeoForge 安装器…")
            If Not File.Exists(jar) Then DownloadFile(url, jar, "", "安装 NeoForge", "installer.jar")
            RunInstaller(jar, $"--install-client ""{PathMc}""", "安装 NeoForge")
            EventBus.RaiseProgress("安装 NeoForge", 1, $"NeoForge {NeoVersion} 安装完成")
        End Sub

        ' ============ Fabric ============
        Public Function GetFabricList(McVersion As String) As List(Of LoaderEntry)
            Dim list As New List(Of LoaderEntry)()
            Try
                Dim arr = HttpGetJsonArray($"https://meta.fabricmc.net/v2/versions/loader/{McVersion}")
                For Each o In arr
                    Dim v = o("loader")?("version")?.ToString()
                    If v <> "" Then list.Add(New LoaderEntry With {.Name = "fabric-loader " & v, .Version = v})
                Next
            Catch ex As Exception
                Log("获取 Fabric 列表失败：" & ex.Message, LogLevel.Warn)
            End Try
            Return list
        End Function

        ''' <summary>安装 Fabric：直接写入官方 profile JSON（无需 Java 即可完成安装）</summary>
        Public Function InstallFabric(McVersion As String, LoaderVersion As String) As String
            Dim profileUrl = $"https://meta.fabricmc.net/v2/versions/loader/{McVersion}/{LoaderVersion}/profile/json"
            Dim jsonStr = HttpGetString(profileUrl)
            Dim name = $"fabric-loader-{LoaderVersion}-{McVersion}"
            WriteProfileJson(name, jsonStr)
            EventBus.RaiseProgress("安装 Fabric", 1, $"Fabric {LoaderVersion} 安装完成")
            Log($"Fabric {LoaderVersion}（MC {McVersion}）安装完成 → {name}")
            Return name
        End Function

        ' ============ Quilt ============
        Public Function GetQuiltList(McVersion As String) As List(Of LoaderEntry)
            Dim list As New List(Of LoaderEntry)()
            Try
                Dim arr = HttpGetJsonArray($"https://meta.quiltmc.org/v3/versions/loader/{McVersion}")
                For Each o In arr
                    Dim v = o("version")?.ToString()
                    If v <> "" Then list.Add(New LoaderEntry With {.Name = "quilt-loader " & v, .Version = v})
                Next
            Catch ex As Exception
                Log("获取 Quilt 列表失败：" & ex.Message, LogLevel.Warn)
            End Try
            Return list
        End Function

        Public Function InstallQuilt(McVersion As String, LoaderVersion As String) As String
            Dim profileUrl = $"https://meta.quiltmc.org/v3/versions/loader/{McVersion}/{LoaderVersion}/profile/json"
            Dim jsonStr = HttpGetString(profileUrl)
            Dim name = $"quilt-loader-{LoaderVersion}-{McVersion}"
            WriteProfileJson(name, jsonStr)
            EventBus.RaiseProgress("安装 Quilt", 1, $"Quilt {LoaderVersion} 安装完成")
            Return name
        End Function

        ' ============ 公共 ============
        ''' <summary>把加载器 profile JSON 写成独立版本（若缺 jar 则沿用父类）</summary>
        Public Sub WriteProfileJson(VersionName As String, JsonText As String)
            Dim dir = Path.Combine(DirVersions, VersionName)
            Directory.CreateDirectory(dir)
            File.WriteAllText(Path.Combine(dir, VersionName & ".json"), JsonText)
            Dim json = JObject.Parse(JsonText)
            ' 预下载依赖库，避免首次启动卡住
            Try
                DownloadLibraries(json, VersionName)
            Catch ex As Exception
                Log("预下载加载器依赖失败（启动时仍会补下）：" & ex.Message, LogLevel.Warn)
            End Try
        End Sub

        ''' <summary>用当前 Java 运行 installer jar</summary>
        Public Sub RunInstaller(JarPath As String, Args As String, TaskName As String)
            Dim java = SetupGet("JavaPath", "")
            If java = "" OrElse Not File.Exists(java) Then
                Dim all = ModJava.ScanJava()
                If all.Count = 0 Then Throw New Exception("安装加载器需要 Java，请先安装或指定 Java")
                java = all(0).Path
            End If
            Dim psi As New ProcessStartInfo(java, $"-jar ""{JarPath}"" {Args}") With {
                .UseShellExecute = False, .RedirectStandardOutput = True,
                .RedirectStandardError = True, .CreateNoWindow = True, .WorkingDirectory = PathMc}
            EventBus.RaiseProgress(TaskName, 0.3, "正在安装（首次较慢，请勿关闭）…")
            Using proc = Process.Start(psi)
                AddHandler proc.OutputDataReceived, Sub(s, e)
                                                       If e.Data IsNot Nothing Then
                                                           Log("[安装器] " & e.Data, LogLevel.Debug)
                                                           EventBus.RaiseProgress(TaskName, -1, e.Data)
                                                       End If
                                                   End Sub
                AddHandler proc.ErrorDataReceived, Sub(s, e)
                                                      If e.Data IsNot Nothing Then Log("[安装器] " & e.Data, LogLevel.Debug)
                                                  End Sub
                proc.BeginOutputReadLine()
                proc.BeginErrorReadLine()
                proc.WaitForExit()
                If proc.ExitCode <> 0 Then
                    Throw New Exception($"安装器返回非零退出码 {proc.ExitCode}，请查看日志")
                End If
            End Using
        End Sub

    End Module

End Namespace
