package com.pclneo.core

import com.pclneo.data.LoaderInfo
import org.json.JSONArray
import org.json.JSONObject
import java.io.File

/**
 * 模组加载器安装：Forge / NeoForge / Fabric / Quilt
 * Fabric / Quilt 直接写 profile JSON（不需要 Java）；
 * Forge / NeoForge 调用官方 installer（需要已安装 Java 运行时）。
 */
object LoaderInstaller {

    // ---------- Forge ----------
    fun forgeList(mc: String): List<LoaderInfo> = try {
        val arr = Http.getArray("https://bmclapi2.bangbang93.com/forge/minecraft/$mc")
        val list = mutableListOf<LoaderInfo>()
        for (i in 0 until arr.length()) {
            val v = arr.getJSONObject(i).optString("version", "")
            if (v.isNotBlank()) list += LoaderInfo(v, v)
        }
        list
    } catch (e: Exception) {
        LogBus.log("获取 Forge 列表失败：${e.message}", "W")
        emptyList()
    }

    fun installForge(mc: String, forge: String) {
        Dirs.ensure(); Dirs.temp.mkdirs()
        val url = if (Http.useMirror)
            "https://bmclapi2.bangbang93.com/forge/download?mcversion=$mc&version=$forge&category=installer&format=jar"
        else
            "https://maven.minecraftforge.net/net/minecraftforge/forge/$mc-$forge/forge-$mc-$forge-installer.jar"
        val jar = File(Dirs.temp, "forge-$mc-$forge-installer.jar")
        LogBus.progress("安装 Forge", 0.1f, "下载 Forge 安装器…")
        if (!jar.exists()) Http.download(url, jar, task = "安装 Forge")
        runInstaller(jar, listOf("--installClient", Dirs.game.absolutePath), "安装 Forge")
        LogBus.progress("安装 Forge", 1f, "Forge $forge 安装完成")
        LogBus.log("Forge $forge 安装完成")
    }

    // ---------- NeoForge ----------
    fun neoforgeList(mc: String): List<LoaderInfo> = try {
        val arr = Http.getArray("https://bmclapi2.bangbang93.com/neoforge/version/list?mcversion=$mc")
        val list = mutableListOf<LoaderInfo>()
        for (i in 0 until arr.length()) {
            val v = arr.getJSONObject(i).optString("version", "")
            if (v.isNotBlank()) list += LoaderInfo(v, v)
        }
        list
    } catch (e: Exception) {
        LogBus.log("获取 NeoForge 列表失败：${e.message}", "W")
        emptyList()
    }

    fun installNeoForge(mc: String, version: String) {
        Dirs.ensure(); Dirs.temp.mkdirs()
        val path = "net/neoforged/neoforge/$version/neoforge-$version-installer.jar"
        val url = if (Http.useMirror) "https://bmclapi2.bangbang93.com/maven/$path"
        else "https://maven.neoforged.net/releases/$path"
        val jar = File(Dirs.temp, "neoforge-$version-installer.jar")
        LogBus.progress("安装 NeoForge", 0.1f, "下载 NeoForge 安装器…")
        if (!jar.exists()) Http.download(url, jar, task = "安装 NeoForge")
        runInstaller(jar, listOf("--install-client", Dirs.game.absolutePath), "安装 NeoForge")
        LogBus.progress("安装 NeoForge", 1f, "NeoForge $version 安装完成")
    }

    // ---------- Fabric ----------
    fun fabricList(mc: String): List<LoaderInfo> = try {
        val arr = Http.getArray("https://meta.fabricmc.net/v2/versions/loader/$mc")
        val list = mutableListOf<LoaderInfo>()
        for (i in 0 until arr.length()) {
            val v = arr.getJSONObject(i).getJSONObject("loader").optString("version", "")
            if (v.isNotBlank()) list += LoaderInfo("fabric-loader $v", v)
        }
        list
    } catch (e: Exception) {
        LogBus.log("获取 Fabric 列表失败：${e.message}", "W")
        emptyList()
    }

    fun installFabric(mc: String, loader: String): String {
        Dirs.ensure()
        val json = Http.get("https://meta.fabricmc.net/v2/versions/loader/$mc/$loader/profile/json")
        val name = "fabric-loader-$loader-$mc"
        writeProfile(name, json)
        LogBus.progress("安装 Fabric", 1f, "Fabric $loader 安装完成")
        return name
    }

    // ---------- Quilt ----------
    fun quiltList(mc: String): List<LoaderInfo> = try {
        val arr = Http.getArray("https://meta.quiltmc.org/v3/versions/loader/$mc")
        val list = mutableListOf<LoaderInfo>()
        for (i in 0 until arr.length()) {
            val v = arr.getJSONObject(i).optString("version", "")
            if (v.isNotBlank()) list += LoaderInfo("quilt-loader $v", v)
        }
        list
    } catch (e: Exception) {
        LogBus.log("获取 Quilt 列表失败：${e.message}", "W")
        emptyList()
    }

    fun installQuilt(mc: String, loader: String): String {
        Dirs.ensure()
        val json = Http.get("https://meta.quiltmc.org/v3/versions/loader/$mc/$loader/profile/json")
        val name = "quilt-loader-$loader-$mc"
        writeProfile(name, json)
        LogBus.progress("安装 Quilt", 1f, "Quilt $loader 安装完成")
        return name
    }

    // ---------- 公共 ----------
    fun writeProfile(name: String, jsonText: String) {
        val dir = Dirs.versionDir(name)
        dir.mkdirs()
        Dirs.versionJson(name).writeText(jsonText)
        runCatching { VersionManager.downloadLibraries(JSONObject(jsonText), name) }
            .onFailure { LogBus.log("预下载依赖失败（启动时仍会补下）：${it.message}", "W") }
        LogBus.log("已写入版本：$name")
    }

    /**
     * 用设备上的 Java 运行 installer jar。
     * args 以列表形式传入，避免手工拼接时被空格/引号拆坏。
     */
    fun runInstaller(jar: File, args: List<String>, task: String) {
        val java = JavaRuntime.javaExecutable()
            ?: throw RuntimeException("安装该加载器需要 Java 运行时，请先在「设置」中安装")
        val cmd = mutableListOf(java.absolutePath, "-jar", jar.absolutePath).apply { addAll(args) }
        val pb = ProcessBuilder(cmd)
        pb.directory(Dirs.game)
        pb.redirectErrorStream(true)
        JavaRuntime.applyEnv(pb.environment())
        LogBus.progress(task, 0.3f, "正在安装（首次较慢）…")
        val proc = pb.start()
        proc.inputStream.bufferedReader().forEachLine { LogBus.log("[安装器] $it") }
        val code = proc.waitFor()
        if (code != 0) throw RuntimeException("安装器退出码 $code，请查看日志")
    }
}
