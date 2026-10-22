Imports System.IO
Imports System.Security.Cryptography
Imports System.Text
Imports Newtonsoft.Json.Linq

Namespace PCLNeo

    Public Enum AuthType
        Offline = 0
        Yggdrasil = 1     ' 外置登录（统一通行证 / 自建 Yggdrasil / Authlib-Injector 兼容）
        Microsoft = 2     ' 微软正版账号
    End Enum

    ''' <summary>一个游戏账号</summary>
    Public Class Account
        Public Property Name As String = ""
        Public Property Uuid As String = ""
        Public Property AccessToken As String = ""
        Public Property Type As AuthType = AuthType.Offline
        ''' <summary>外置登录服务器地址，如 https://example.com/api/yggdrasil</summary>
        Public Property YggServer As String = ""
        Public Property RefreshToken As String = ""
        Public Property ClientToken As String = NewGuid32()
        Public Property SkinUrl As String = ""

        Public Overrides Function ToString() As String
            Dim t = If(Type = AuthType.Offline, "离线", If(Type = AuthType.Yggdrasil, "外置", "微软"))
            Return $"{Name}（{t}）"
        End Function

        Public Function ToJson() As JObject
            Return New JObject From {
                {"name", Name}, {"uuid", Uuid}, {"accessToken", AccessToken},
                {"type", CInt(Type)}, {"yggServer", YggServer},
                {"refreshToken", RefreshToken}, {"clientToken", ClientToken},
                {"skinUrl", SkinUrl}
            }
        End Function
        Public Shared Function FromJson(o As JObject) As Account
            Return New Account With {
                .Name = If(o("name")?.ToString(), ""),
                .Uuid = If(o("uuid")?.ToString(), ""),
                .AccessToken = If(o("accessToken")?.ToString(), ""),
                .Type = If(o("type") Is Nothing, AuthType.Offline, CType(CInt(o("type")), AuthType)),
                .YggServer = If(o("yggServer")?.ToString(), ""),
                .RefreshToken = If(o("refreshToken")?.ToString(), ""),
                .ClientToken = If(o("clientToken")?.ToString(), NewGuid32()),
                .SkinUrl = If(o("skinUrl")?.ToString(), "")
            }
        End Function

        Private Shared Function NewGuid32() As String
            Return Guid.NewGuid().ToString("N")
        End Function
    End Class

    ''' <summary>
    ''' 账号模块：离线 / 外置（Yggdrasil）/ 微软正版
    ''' </summary>
    Public Module ModAuth

        ''' <summary>微软 OAuth 客户端 ID（设置中可覆盖）</summary>
        Public ReadOnly Property MsClientId As String
            Get
                Dim v = SetupGet("MsClientId", "")
                If String.IsNullOrWhiteSpace(v) Then Return "1ae3d3b8-6b0a-4b1e-9c1a-6f4f2b8d0e11"
                Return v
            End Get
        End Property

        Private ReadOnly Property AccountFile As String
            Get
                Return Path.Combine(PathData, "accounts.json")
            End Get
        End Property

        ' ---------- 账号列表 ----------
        Public Function GetAccounts() As List(Of Account)
            Dim list As New List(Of Account)()
            Try
                If File.Exists(AccountFile) Then
                    Dim arr = JArray.Parse(File.ReadAllText(AccountFile))
                    For Each o In arr
                        list.Add(Account.FromJson(CType(o, JObject)))
                    Next
                End If
            Catch ex As Exception
                Log("读取账号列表失败：" & ex.Message, LogLevel.Warn)
            End Try
            If list.Count = 0 Then
                list.Add(LoginOffline("Player"))
                SaveAccounts(list)
            End If
            Return list
        End Function

        Public Sub SaveAccounts(List As List(Of Account))
            Dim arr As New JArray()
            For Each a In List
                arr.Add(a.ToJson())
            Next
            Directory.CreateDirectory(PathData)
            File.WriteAllText(AccountFile, arr.ToString(Newtonsoft.Json.Formatting.Indented))
        End Sub

        Public Sub AddAccount(a As Account)
            Dim list = GetAccounts()
            list.RemoveAll(Function(x) x.Name = a.Name AndAlso x.Type = a.Type)
            list.Insert(0, a)
            SaveAccounts(list)
        End Sub

        ' ---------- 离线 ----------
        Public Function LoginOffline(UserName As String) As Account
            Dim uuid As String = OfflineUuid(UserName)
            Log($"离线登录：{UserName} → {uuid}")
            Return New Account With {
                .Name = UserName, .Uuid = uuid,
                .AccessToken = uuid.Replace("-", ""),
                .Type = AuthType.Offline
            }
        End Function

        ''' <summary>离线 UUID：与 Minecraft 服务端 OfflinePlayer 算法一致</summary>
        Public Function OfflineUuid(UserName As String) As String
            Dim raw = Encoding.UTF8.GetBytes("OfflinePlayer:" & UserName)
            Dim hash As Byte()
            Using md5 As MD5 = MD5.Create()
                hash = md5.ComputeHash(raw)
            End Using
            ' 设置为 UUID v3
            hash(6) = CByte((hash(6) And &HF) Or &H30)
            hash(8) = CByte((hash(8) And &H3F) Or &H80)
            Dim s = BitConverter.ToString(hash).Replace("-", "").ToLower()
            Return s.Insert(8, "-").Insert(13, "-").Insert(18, "-").Insert(23, "-")
        End Function

        ' ---------- 外置登录（Yggdrasil） ----------
        ''' <summary>POST {server}/authserver/authenticate</summary>
        Public Function LoginYggdrasil(UserName As String, Password As String, Server As String) As Account
            Dim body As New JObject From {
                {"agent", New JObject From {{"name", "Minecraft"}, {"version", 1}}},
                {"username", UserName}, {"password", Password},
                {"clientToken", New Guid().ToString("N")}, {"requestUser", True}
            }
            Dim url = Server.TrimEnd("/"c) & "/authserver/authenticate"
            Dim resp = HttpPostJson(url, body)
            Dim sel = resp("selectedProfile")
            If sel Is Nothing Then Throw New Exception("外置登录失败：服务器未返回可用角色")
            Dim acc As New Account With {
                .Name = sel("name").ToString(),
                .Uuid = sel("id").ToString(),
                .AccessToken = resp("accessToken").ToString(),
                .ClientToken = If(resp("clientToken")?.ToString(), ""),
                .Type = AuthType.Yggdrasil,
                .YggServer = Server
            }
            Log($"外置登录成功：{acc.Name}（{Server}）")
            Return acc
        End Function

        ''' <summary>用 refreshToken 续期</summary>
        Public Function RefreshYggdrasil(a As Account) As Account
            If a.Type <> AuthType.Yggdrasil OrElse String.IsNullOrEmpty(a.RefreshToken) Then Return a
            Dim url = a.YggServer.TrimEnd("/"c) & "/authserver/refresh"
            Dim body As New JObject From {{"accessToken", a.AccessToken}, {"clientToken", a.ClientToken}, {"requestUser", True}}
            Dim resp = HttpPostJson(url, body)
            a.AccessToken = resp("accessToken").ToString()
            Dim sel = resp("selectedProfile")
            If sel IsNot Nothing Then a.Name = sel("name").ToString()
            Log($"外置登录已刷新：{a.Name}")
            Return a
        End Function

        ''' <summary>校验 accessToken 是否有效</summary>
        Public Function ValidateYggdrasil(a As Account) As Boolean
            Try
                Dim url = a.YggServer.TrimEnd("/"c) & "/authserver/validate"
                Dim body As New JObject From {{"accessToken", a.AccessToken}, {"clientToken", a.ClientToken}}
                Dim resp = HttpPostJson(url, body)
                Return resp("error") Is Nothing
            Catch ex As Exception
                Log("外置登录校验失败：" & ex.Message, LogLevel.Warn)
                Return False
            End Try
        End Function

        ' ---------- 微软正版（设备代码流） ----------
        ''' <summary>第 1 步：取得设备代码，返回 (device_code, user_code, verification_uri, expires, interval)</summary>
        Public Function MicrosoftGetDeviceCode() As JObject
            Dim form As New Dictionary(Of String, String) From {
                {"client_id", MsClientId},
                {"scope", "XboxLive.signin offline_access"}
            }
            Dim resp = HttpPostForm("https://login.microsoftonline.com/consumers/oauth2/v2.0/devicecode", form)
            If resp("user_code") Is Nothing Then Throw New Exception("获取设备代码失败：" & resp.ToString())
            Log("微软登录：请在浏览器打开 " & resp("verification_uri").ToString() & " 输入代码 " & resp("user_code").ToString())
            Return resp
        End Function

        ''' <summary>第 2 步：轮询令牌，成功后返回完整 Account</summary>
        Public Function MicrosoftLogin(DeviceCode As String, Optional Interval As Integer = 5, Optional MaxSeconds As Integer = 300) As Account
            Dim waited = 0
            While waited < MaxSeconds
                Threading.Thread.Sleep(Interval * 1000)
                waited += Interval
                Dim form As New Dictionary(Of String, String) From {
                    {"grant_type", "urn:ietf:params:oauth:grant-type:device_code"},
                    {"client_id", MsClientId},
                    {"device_code", DeviceCode}
                }
                Dim resp = HttpPostForm("https://login.microsoftonline.com/consumers/oauth2/v2.0/token", form)
                If resp("error") IsNot Nothing Then
                    Dim err = resp("error").ToString()
                    If err = "authorization_pending" Then Continue While
                    Throw New Exception("微软登录失败：" & err & " - " & If(resp("error_description")?.ToString(), ""))
                End If
                Dim msToken = resp("access_token").ToString()
                Dim refresh = If(resp("refresh_token")?.ToString(), "")
                Return MicrosoftFinish(msToken, refresh)
            End While
            Throw New Exception("微软登录超时，请重试")
        End Function

        ''' <summary>第 3~6 步：XBL → XSTS → Minecraft Token → 角色信息</summary>
        Public Function MicrosoftFinish(MsToken As String, Optional RefreshToken As String = "") As Account
            ' XBL
            Dim xblBody As New JObject From {
                {"Properties", New JObject From {
                    {"AuthMethod", "RPS"}, {"SiteName", "user.auth.xboxlive.com"}, {"RpsTicket", "d=" & MsToken}}},
                {"RelyingParty", "http://auth.xboxlive.com"},
                {"TokenType", "JWT"}
            }
            Dim xbl = HttpPostJson("https://user.auth.xboxlive.com/user/authenticate", xblBody)
            Dim xblToken = xbl("Token").ToString()
            Dim uhs = xbl("DisplayClaims")("xui")(0)("uhs").ToString()

            ' XSTS
            Dim xstsBody As New JObject From {
                {"Properties", New JObject From {{"SandboxId", "RETAIL"}, {"UserTokens", New JArray(xblToken)}}},
                {"RelyingParty", "rp://api.minecraftservices.com/"},
                {"TokenType", "JWT"}
            }
            Dim xsts = HttpPostJson("https://xsts.auth.xboxlive.com/xsts/authorize", xstsBody)
            Dim xstsToken = xsts("Token").ToString()

            ' Minecraft
            Dim mcBody As New JObject From {{"identityToken", "XBL3.0 x=" & uhs & ";" & xstsToken}}
            Dim mc = HttpPostJson("https://api.minecraftservices.com/authentication/login_with_xbox", mcBody)
            Dim accessToken = mc("access_token").ToString()

            ' 角色信息
            Dim profileJson = HttpGetString("https://api.minecraftservices.com/minecraft/profile")
            Dim profile = JObject.Parse(profileJson)
            If profile("name") Is Nothing Then Throw New Exception("该微软账号未拥有 Minecraft Java 版")
            Dim acc As New Account With {
                .Name = profile("name").ToString(),
                .Uuid = profile("id").ToString(),
                .AccessToken = accessToken,
                .RefreshToken = RefreshToken,
                .Type = AuthType.Microsoft
            }
            Log("微软登录成功：" & acc.Name)
            Return acc
        End Function

        ''' <summary>使用 refresh_token 静默续期微软账号</summary>
        Public Function MicrosoftRefresh(a As Account) As Account
            If a.Type <> AuthType.Microsoft OrElse String.IsNullOrEmpty(a.RefreshToken) Then Return a
            Dim form As New Dictionary(Of String, String) From {
                {"client_id", MsClientId},
                {"grant_type", "refresh_token"},
                {"refresh_token", a.RefreshToken},
                {"scope", "XboxLive.signin offline_access"}
            }
            Dim resp = HttpPostForm("https://login.microsoftonline.com/consumers/oauth2/v2.0/token", form)
            If resp("access_token") Is Nothing Then Throw New Exception("微软登录已过期，请重新登录")
            Return MicrosoftFinish(resp("access_token").ToString(), If(resp("refresh_token")?.ToString(), a.RefreshToken))
        End Function

        ''' <summary>启动前刷新账号（离线直接返回）</summary>
        Public Function EnsureValid(a As Account) As Account
            If a Is Nothing Then Return LoginOffline("Player")
            Select Case a.Type
                Case AuthType.Offline
                    Return a
                Case AuthType.Yggdrasil
                    Return If(ValidateYggdrasil(a), a, RefreshYggdrasil(a))
                Case Else
                    Try
                        Return MicrosoftRefresh(a)
                    Catch ex As Exception
                        Log("微软账号续期失败，按原令牌启动：" & ex.Message, LogLevel.Warn)
                        Return a
                    End Try
            End Select
        End Function

    End Module

End Namespace
