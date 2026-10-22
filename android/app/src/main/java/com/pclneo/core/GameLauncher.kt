package com.pclneo.core

import com.pclneo.data.Account
import org.json.JSONObject
import java.io.File

/**
 * 启动模块：拼装 JVM / 游戏参数，并以独立进程方式拉起 java
 */
object GameLauncher {

    @Volatile
    var currentProcess: Process? = null
        private set

    fun isRunning(): Boolean = currentProcess?.isAlive == true

    fun kill() {
        runCatching { currentProcess?.destroy() }
        currentProcess = null
    }

    private fun argAllowed(node: JSONObject): Boolean {
        val rules = node.optJSONArray("rules") ?: return true
        var allow = false
        for (i in 0 until rules.length()) {
            val r = rules.getJSONObject(i)
            val action = r.optString("action", "allow")
            val os = r.optJSONObject("os")
            var match = true
            if (os != null) {
                val n = os.optString("name", "")
                if (n.isNotBlank() && n != "linux" && n != "android") match = false
            }
            // 自定义特性（如 is_demo_user）默认不启用
            if (node.has("features")) match = false
            if (match) allow = (action == "allow")
        }
        return allow
    }

    private fun collect(array: org.json.JSONArray?, acc: Account, version: String, json: JSONObject): List<String> {
        val out = mutableListOf<String>()
        array ?: return out
        for (i in 0 until array.length()) {
            val a = array.get(i)
            if (a is String) {
                out += replace(a, acc, version, json)
            } else if (a is JSONObject) {
                if (!argAllowed(a)) continue
                val v = a.opt("value") ?: continue
                if (v is org.json.JSONArray) {
                    for (j in 0 until v.length()) out += replace(v.getString(j), acc, version, json)
                } else {
                    out += replace(v.toString(), acc, version, json)
                }
            }
        }
        return out
    }

    private fun replace(t: String, acc: Account, version: String, json: JSONObject): String {
        val assetIndex = json.optString(
            "assets",
            json.optJSONObject("assetIndex")?.optString("id", "legacy") ?: "legacy"
        )
        val userType = if (acc.type == com.pclneo.data.AuthType.OFFLINE) "legacy" else "mojang"
        return t
            .replace("\${auth_player_name}", acc.name)
            .replace("\${auth_uuid}", acc.uuid)
            .replace("\${auth_access_token}", acc.accessToken)
            .replace("\${auth_session}", acc.accessToken)
            .replace("\${user_type}", userType)
            .replace("\${user_properties}", "{}")
            .replace("\${version_name}", version)
            .replace("\${game_directory}", Dirs.game.absolutePath)
            .replace("\${assets_root}", Dirs.assets.absolutePath)
            .replace("\${game_assets}", File(Dirs.assets, "virtual/legacy").absolutePath)
            .replace("\${assets_index_name}", assetIndex)
            .replace("\${natives_directory}", Dirs.nativesDir(version).absolutePath)
            .replace("\${launcher_name}", "PCLNeo")
            .replace("\${launcher_version}", "1.0.0")
    }

    /** 启动游戏，返回进程对象 */
    fun launch(version: String, account: Account): Process {
        Dirs.ensure()
        val acc = Auth.ensureValid(account)
        val json = VersionManager.versionJson(version)
        val java = JavaRuntime.javaExecutable()
            ?: throw RuntimeException("未安装 Java 运行时，请在「设置」中下载安装 JRE")

        Dirs.nativesDir(version).mkdirs()
        val classpath = VersionManager.buildClasspath(json, version)
        val mainClass = json.optString("mainClass", "net.minecraft.client.main.Main")

        // JVM 参数
        val jvm = mutableListOf<String>()
        jvm += "-Xmx${Settings.memoryMax}M"
        jvm += "-Djava.library.path=${Dirs.nativesDir(version).absolutePath}"
        jvm += "-Duser.home=${Dirs.game.absolutePath}"
        jvm += "-Djava.io.tmpdir=${Dirs.temp.absolutePath}"
        jvm += "-Dminecraft.launcher.brand=PCLNeo"
        jvm += "-Dminecraft.launcher.version=1.0.0"
        jvm += "-Dos.name=Linux"
        jvm += collect(json.optJSONObject("arguments")?.optJSONArray("jvm"), acc, version, json)
        Settings.jvmArgs.split(" ").filter { it.isNotBlank() }.forEach { jvm += replace(it, acc, version, json) }

        // 游戏参数
        val game = mutableListOf<String>()
        game += collect(json.optJSONObject("arguments")?.optJSONArray("game"), acc, version, json)
        if (game.isEmpty()) {
            json.optString("minecraftArguments", "").split(" ")
                .filter { it.isNotBlank() }
                .forEach { game += replace(it, acc, version, json) }
        }
        Settings.get("gameArgs", "").split(" ").filter { it.isNotBlank() }
            .forEach { game += replace(it, acc, version, json) }

        val args = mutableListOf<String>()
        args += jvm
        args += "-cp"
        args += classpath
        args += mainClass
        args += game

        val cmd = mutableListOf(java.absolutePath).apply { addAll(args) }
        val pb = ProcessBuilder(cmd)
        pb.directory(Dirs.game)
        pb.redirectErrorStream(true)
        JavaRuntime.applyEnv(pb.environment())

        LogBus.log("启动命令：${java.absolutePath} ${args.joinToString(" ")}")
        val proc = pb.start()
        currentProcess = proc
        Thread {
            proc.inputStream.bufferedReader().forEachLine { LogBus.log("[游戏] $it") }
            LogBus.log("游戏进程退出，退出码：${runCatching { proc.exitValue() }.getOrDefault(-1)}")
        }.start()
        LogBus.progress("启动", 1f, "游戏已启动")
        return proc
    }

    /** 安装模组到指定版本（供 UI 调用） */
    fun installMod(url: String, fileName: String, version: String): File {
        val dir = Dirs.modsDir(version)
        dir.mkdirs()
        val out = File(dir, fileName.replace(Regex("[\\\\/:*?\"<>|]"), "_"))
        Http.download(url, out, task = "下载模组")
        LogBus.log("模组已安装：${out.absolutePath}")
        return out
    }

    fun installedMods(version: String): List<String> {
        val dir = Dirs.modsDir(version)
        if (!dir.exists()) return emptyList()
        return dir.listFiles()?.filter { it.name.endsWith(".jar") || it.name.endsWith(".jar.disabled") }
            ?.map { it.name }?.sorted() ?: emptyList()
    }

    fun toggleMod(version: String, fileName: String) {
        val f = File(Dirs.modsDir(version), fileName)
        if (!f.exists()) return
        val target = if (fileName.endsWith(".disabled")) File(f.parentFile, fileName.removeSuffix(".disabled"))
        else File(f.parentFile, "$fileName.disabled")
        f.renameTo(target)
    }
}
