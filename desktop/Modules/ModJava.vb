Imports System.Diagnostics
Imports System.IO
Imports System.Text.RegularExpressions
Imports Microsoft.Win32
Imports Newtonsoft.Json.Linq

Namespace PCLNeo

    ''' <summary>一个可用的 Java 运行时</summary>
    Public Class JavaInfo
        Public Property Path As String = ""
        Public Property Version As String = ""
        Public Property Feature As Integer = 8   ' 主版本号：8 / 17 / 21 …
        Public Property Is64Bit As Boolean = True
        Public Overrides Function ToString() As String
            Return $"Java {Feature}（{Version}） - {Path}"
        End Function
    End Class

    ''' <summary>
    ''' Java 模块：自动搜索本机 Java、判断版本、按游戏需要挑选、从 Adoptium 下载
    ''' </summary>
    Public Module ModJava

        Private ReadOnly Property DirJava As String
            Get
                Return Path.Combine(PathData, "java")
            End Get
        End Property

        ' ---------- 扫描 ----------
        ''' <summary>扫描本机所有 Java（注册表 + JAVA_HOME + 常见目录 + 启动器自带）</summary>
        Public Function ScanJava() As List(Of JavaInfo)
            Dim found As New List(Of String)()
            Dim add = Sub(p As String)
                          If Not String.IsNullOrEmpty(p) AndAlso File.Exists(p) Then found.Add(p)
                      End Sub

            Try
                Dim env = Environment.GetEnvironmentVariable("JAVA_HOME")
                If env <> "" Then
                    add(Path.Combine(env, "bin", "java.exe"))
                    add(Path.Combine(env, "bin", "javaw.exe"))
                End If
            Catch
            End Try

            Try
                Using key = Registry.LocalMachine.OpenSubKey("SOFTWARE\JavaSoft\Java Runtime Environment") ' 旧版本
                    If key IsNot Nothing Then
                        For Each subName In key.GetSubKeyNames()
                            Using k2 = key.OpenSubKey(subName)
                                add(Path.Combine(If(k2?.GetValue("JavaHome")?.ToString(), ""), "bin", "javaw.exe"))
                            End Using
                        Next
                    End If
                End Using
                Using key = Registry.LocalMachine.OpenSubKey("SOFTWARE\JavaSoft\JDK")
                    If key IsNot Nothing Then
                        For Each subName In key.GetSubKeyNames()
                            Using k2 = key.OpenSubKey(subName)
                                add(Path.Combine(If(k2?.GetValue("JavaHome")?.ToString(), ""), "bin", "javaw.exe"))
                            End Using
                        Next
                    End If
                End Using
            Catch ex As Exception
                Log("读取注册表 Java 失败：" & ex.Message, LogLevel.Debug)
            End Try

            For Each base In New String() {
                "C:\Program Files\Java", "C:\Program Files (x86)\Java",
                "C:\Program Files\Eclipse Adoptium", "C:\Program Files\Zulu",
                "C:\Program Files\BellSoft", "C:\Program Files\Microsoft",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Minecraft Launcher"),
                DirJava}
                Try
                    If Directory.Exists(base) Then
                        For Each d In Directory.GetDirectories(base)
                            For Each rel In New String() {"bin\javaw.exe", "bin\java.exe", "runtime\bin\javaw.exe"}
                                add(Path.Combine(d, rel))
                            Next
                        Next
                    End If
                Catch
                End Try
            Next

            Dim result As New List(Of JavaInfo)()
            For Each p In found.Distinct()
                Dim info = GetJavaInfo(p)
                If info IsNot Nothing Then result.Add(info)
            Next
            result.Sort(Function(a, b) b.Feature.CompareTo(a.Feature))
            ' 用户手动指定的优先
            Dim manual = SetupGet("JavaPath", "")
            If manual <> "" AndAlso File.Exists(manual) Then
                Dim mi = GetJavaInfo(manual)
                If mi IsNot Nothing Then result.Insert(0, mi)
            End If
            Return result
        End Function

        ''' <summary>执行 java -version 取得版本信息</summary>
        Public Function GetJavaInfo(JavaExe As String) As JavaInfo
            Try
                Dim psi As New ProcessStartInfo(JavaExe, "-version") With {
                    .UseShellExecute = False, .RedirectStandardError = True,
                    .RedirectStandardOutput = True, .CreateNoWindow = True}
                Using proc = Process.Start(psi)
                    Dim versionOutput = proc.StandardError.ReadToEnd() & proc.StandardOutput.ReadToEnd()
                    proc.WaitForExit(5000)
                    Dim m = Regex.Match(versionOutput, """(?<v>[0-9]+(\.[0-9]+)*)""")
                    If Not m.Success Then Return Nothing
                    Dim ver = m.Groups("v").Value
                    Dim feature = ParseFeature(ver)
                    Return New JavaInfo With {
                        .Path = JavaExe, .Version = ver, .Feature = feature,
                        .Is64Bit = versionOutput.Contains("64-Bit") OrElse Not versionOutput.ToLower().Contains("client vm")
                    }
                End Using
            Catch
                Return Nothing
            End Try
        End Function

        Public Function ParseFeature(Version As String) As Integer
            Dim parts = Version.Split("."c)
            Dim first = CInt(Val(parts(0)))
            If first = 1 AndAlso parts.Length > 1 Then Return CInt(Val(parts(1))) ' 1.8 → 8
            Return first
        End Function

        ''' <summary>按游戏版本推荐 Java 主版本</summary>
        Public Function RecommendFeature(McVersion As String) As Integer
            Dim p = McVersion.Split("."c)
            If p.Length >= 2 Then
                Dim minor = CInt(Val(p(1)))
                If McVersion.StartsWith("1.") Then
                    If minor >= 21 Then Return 21
                    If minor >= 18 Then Return 17
                    If minor = 17 Then Return 17
                    Return 8
                End If
            End If
            Return 17
        End Function

        ''' <summary>为指定版本挑选最合适的 Java</summary>
        Public Function GetBestJava(json As JObject) As String
            Dim manual = SetupGet("JavaPath", "")
            If manual <> "" AndAlso File.Exists(manual) Then Return manual
            Dim mc = If(json("id")?.ToString(), "1.20.1")
            Dim want = RecommendFeature(mc)
            Dim list = ScanJava()
            Dim best = list.FirstOrDefault(Function(j) j.Feature = want)
            If best Is Nothing Then best = list.FirstOrDefault(Function(j) j.Feature >= want)
            If best Is Nothing Then best = list.FirstOrDefault()
            If best Is Nothing Then
                Log("未找到任何 Java，尝试自动下载…")
                Return ""
            End If
            Log($"为 {mc} 选择 Java {best.Feature}：{best.Path}")
            Return best.Path
        End Function

        ' ---------- 下载（Adoptium） ----------
        ''' <summary>从 Adoptium 下载 JDK，返回 javaw.exe 路径</summary>
        Public Function DownloadJava(Feature As Integer) As String
            Directory.CreateDirectory(DirJava)
            Dim url = $"https://api.adoptium.net/v3/assets/latest/{Feature}/hotspot?architecture=x64&image_type=jdk&os=windows&vendor=eclipse"
            Dim arr = HttpGetJsonArray(url)
            If arr.Count = 0 Then Throw New Exception("Adoptium 未返回可用构建")
            Dim binary = arr(0)("binary")
            Dim link = binary("package")("link").ToString()
            Dim fileName = If(binary("package")("name")?.ToString(), $"jdk{Feature}.zip")
            Dim zipPath = Path.Combine(DirJava, SafeName(fileName))
            EventBus.RaiseProgress("下载 Java", 0, $"下载 JDK {Feature}…")
            DownloadFile(link, zipPath, "", "下载 Java", fileName)
            Dim target = Path.Combine(DirJava, "jdk-" & Feature)
            If Directory.Exists(target) Then Directory.Delete(target, True)
            Directory.CreateDirectory(target)
            ExtractZip(zipPath, target)
            ' Adoptium 压缩包内含一层目录
            Dim exe = FindJavaExe(target)
            If exe = "" Then Throw New Exception("解压后未找到 javaw.exe")
            SetupSet("JavaPath", exe)
            EventBus.RaiseProgress("下载 Java", 1, $"JDK {Feature} 安装完成")
            Log($"JDK {Feature} 安装完成：{exe}")
            Return exe
        End Function

        Private Function FindJavaExe(Root As String) As String
            For Each f In Directory.GetFiles(Root, "javaw.exe", SearchOption.AllDirectories)
                Return f
            Next
            For Each f In Directory.GetFiles(Root, "java.exe", SearchOption.AllDirectories)
                Return f
            Next
            Return ""
        End Function

    End Module

End Namespace
