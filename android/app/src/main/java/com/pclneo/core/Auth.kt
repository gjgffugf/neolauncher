package com.pclneo.core

import com.pclneo.data.Account
import com.pclneo.data.AuthType
import org.json.JSONArray
import org.json.JSONObject
import java.io.File
import java.security.MessageDigest
import java.util.UUID

/**
 * 账号模块：离线 / 外置（Yggdrasil）/ 微软正版
 */
object Auth {

    /** 账号固定存放在启动器数据根目录，不随游戏目录变化 */
    private fun file(): File = File(Settings.dataDir, "accounts.json")

    private val MS_CLIENT_ID: String
        get() = Settings.msClientId.ifBlank { "1ae3d3b8-6b0a-4b1e-9c1a-6f4f2b8d0e11" }

    // ---------- 存储 ----------
    fun getAccounts(): MutableList<Account> {
        val f = file()
        if (!f.exists()) {
            val list = mutableListOf(loginOffline("Player"))
            save(list)
            return list
        }
        return try {
            val arr = JSONArray(f.readText())
            val list = mutableListOf<Account>()
            for (i in 0 until arr.length()) {
                val o = arr.getJSONObject(i)
                list.add(
                    Account(
                        name = o.optString("name"),
                        uuid = o.optString("uuid"),
                        accessToken = o.optString("accessToken"),
                        type = AuthType.valueOf(o.optString("type", "OFFLINE")),
                        yggServer = o.optString("yggServer"),
                        refreshToken = o.optString("refreshToken"),
                        clientToken = o.optString("clientToken")
                    )
                )
            }
            list
        } catch (e: Exception) {
            LogBus.log("读取账号失败：${e.message}", "W")
            mutableListOf(loginOffline("Player"))
        }
    }

    fun save(list: List<Account>) {
        val arr = JSONArray()
        list.forEach {
            arr.put(
                JSONObject().apply {
                    put("name", it.name)
                    put("uuid", it.uuid)
                    put("accessToken", it.accessToken)
                    put("type", it.type.name)
                    put("yggServer", it.yggServer)
                    put("refreshToken", it.refreshToken)
                    put("clientToken", it.clientToken)
                }
            )
        }
        file().parentFile?.mkdirs()
        file().writeText(arr.toString(2))
    }

    fun add(account: Account) {
        val list = getAccounts()
        list.removeAll { it.name == account.name && it.type == account.type }
        list.add(0, account)
        save(list)
    }

    fun remove(account: Account) {
        val list = getAccounts()
        list.removeAll { it.name == account.name && it.type == account.type }
        save(list)
    }

    // ---------- 离线 ----------
    fun loginOffline(name: String): Account {
        val uuid = offlineUuid(name)
        LogBus.log("离线登录：$name → $uuid")
        return Account(name, uuid, uuid.replace("-", ""), AuthType.OFFLINE)
    }

    /** 与 Minecraft 服务端 OfflinePlayer 一致的 UUID v3 */
    fun offlineUuid(name: String): String {
        val raw = "OfflinePlayer:$name".toByteArray()
        val md = MessageDigest.getInstance("MD5").digest(raw)
        md[6] = ((md[6].toInt() and 0x0f) or 0x30).toByte()
        md[8] = ((md[8].toInt() and 0x3f) or 0x80).toByte()
        val hex = md.joinToString("") { "%02x".format(it) }
        val u = UUID.fromString(
            hex.substring(0, 8) + "-" + hex.substring(8, 12) + "-" +
                hex.substring(12, 16) + "-" + hex.substring(16, 20) + "-" + hex.substring(20)
        )
        return u.toString()
    }

    // ---------- 外置登录 ----------
    fun loginYggdrasil(user: String, pwd: String, server: String): Account {
        val body = JSONObject().apply {
            put("agent", JSONObject().apply { put("name", "Minecraft"); put("version", 1) })
            put("username", user)
            put("password", pwd)
            put("clientToken", UUID.randomUUID().toString().replace("-", ""))
            put("requestUser", true)
        }
        val resp = Http.postJson("${server.trimEnd('/')}/authserver/authenticate", body)
        val sel = resp.optJSONObject("selectedProfile")
            ?: throw RuntimeException("外置登录失败：服务器未返回可用角色")
        val acc = Account(
            name = sel.getString("name"),
            uuid = sel.getString("id"),
            accessToken = resp.getString("accessToken"),
            type = AuthType.YGGDRASIL,
            yggServer = server,
            clientToken = resp.optString("clientToken")
        )
        LogBus.log("外置登录成功：${acc.name}")
        return acc
    }

    fun validate(a: Account): Boolean = try {
        val body = JSONObject().apply {
            put("accessToken", a.accessToken)
            put("clientToken", a.clientToken)
        }
        val r = Http.postJson("${a.yggServer.trimEnd('/')}/authserver/validate", body)
        !r.has("error")
    } catch (e: Exception) {
        LogBus.log("外置校验失败：${e.message}", "W")
        false
    }

    // ---------- 微软正版（设备代码流） ----------
    /** 返回 (deviceCode, userCode, verificationUri, interval) */
    fun msDeviceCode(): JSONObject {
        val r = Http.postForm(
            "https://login.microsoftonline.com/consumers/oauth2/v2.0/devicecode",
            mapOf("client_id" to MS_CLIENT_ID, "scope" to "XboxLive.signin offline_access")
        )
        if (!r.has("user_code")) throw RuntimeException("获取设备代码失败：$r")
        LogBus.log("微软登录：请在浏览器打开 ${r.getString("verification_uri")} 输入 ${r.getString("user_code")}")
        return r
    }

    /** 轮询令牌（阻塞，建议在后台线程调用） */
    fun msLogin(deviceCode: String, intervalSec: Int = 5, maxSec: Int = 300): Account {
        var waited = 0
        while (waited < maxSec) {
            Thread.sleep(intervalSec * 1000L)
            waited += intervalSec
            val r = Http.postForm(
                "https://login.microsoftonline.com/consumers/oauth2/v2.0/token",
                mapOf(
                    "grant_type" to "urn:ietf:params:oauth:grant-type:device_code",
                    "client_id" to MS_CLIENT_ID,
                    "device_code" to deviceCode
                )
            )
            if (r.has("error")) {
                val err = r.getString("error")
                if (err == "authorization_pending") continue
                throw RuntimeException("微软登录失败：$err ${r.optString("error_description")}")
            }
            return msFinish(r.getString("access_token"), r.optString("refresh_token"))
        }
        throw RuntimeException("微软登录超时，请重试")
    }

    /** XBL → XSTS → Minecraft Token → 角色信息 */
    fun msFinish(msToken: String, refresh: String = ""): Account {
        val xbl = Http.postJson(
            "https://user.auth.xboxlive.com/user/authenticate",
            JSONObject().apply {
                put(
                    "Properties",
                    JSONObject().apply {
                        put("AuthMethod", "RPS")
                        put("SiteName", "user.auth.xboxlive.com")
                        put("RpsTicket", "d=$msToken")
                    }
                )
                put("RelyingParty", "http://auth.xboxlive.com")
                put("TokenType", "JWT")
            }
        )
        val xblToken = xbl.getString("Token")
        val uhs = xbl.getJSONObject("DisplayClaims").getJSONArray("xui").getJSONObject(0).getString("uhs")

        val xsts = Http.postJson(
            "https://xsts.auth.xboxlive.com/xsts/authorize",
            JSONObject().apply {
                put(
                    "Properties",
                    JSONObject().apply {
                        put("SandboxId", "RETAIL")
                        put("UserTokens", JSONArray().apply { put(xblToken) })
                    }
                )
                put("RelyingParty", "rp://api.minecraftservices.com/")
                put("TokenType", "JWT")
            }
        )
        val xstsToken = xsts.getString("Token")

        val mc = Http.postJson(
            "https://api.minecraftservices.com/authentication/login_with_xbox",
            JSONObject().put("identityToken", "XBL3.0 x=$uhs;$xstsToken")
        )
        val token = mc.getString("access_token")

        val profile = Http.getJson("https://api.minecraftservices.com/minecraft/profile")
        if (!profile.has("name")) throw RuntimeException("该微软账号未拥有 Minecraft Java 版")
        val acc = Account(
            name = profile.getString("name"),
            uuid = profile.getString("id"),
            accessToken = token,
            type = AuthType.MICROSOFT,
            refreshToken = refresh
        )
        LogBus.log("微软登录成功：${acc.name}")
        return acc
    }

    /** 启动前确保令牌可用 */
    fun ensureValid(a: Account): Account = when (a.type) {
        AuthType.OFFLINE -> a
        AuthType.YGGDRASIL -> if (validate(a)) a else a
        AuthType.MICROSOFT -> try {
            val r = Http.postForm(
                "https://login.microsoftonline.com/consumers/oauth2/v2.0/token",
                mapOf(
                    "client_id" to MS_CLIENT_ID,
                    "grant_type" to "refresh_token",
                    "refresh_token" to a.refreshToken,
                    "scope" to "XboxLive.signin offline_access"
                )
            )
            if (r.has("access_token")) msFinish(r.getString("access_token"), r.optString("refresh_token", a.refreshToken))
            else a
        } catch (e: Exception) {
            LogBus.log("微软续期失败，按原令牌启动：${e.message}", "W")
            a
        }
    }
}
