package com.pclneo.core

import android.os.Build
import java.io.File

/**
 * Android 上的 Java 运行时管理。
 *
 * Minecraft Java 版在 Android 上运行需要一个可用的 OpenJDK 移植版（aarch64 / arm / x86_64）
 * 与 OpenGL 兼容层。本模块负责下载、解压、授权并提供进程环境变量；
 * 默认地址指向社区维护的 Android JRE 构建，可在「设置 → JRE 下载地址」中替换为你自己的镜像。
 */
object JavaRuntime {

    /** 默认 JRE 下载地址（按 ABI 选择，可能随上游发布变动，建议在设置中自定义） */
    fun defaultUrl(): String {
        val abi = Build.SUPPORTED_ABIS.firstOrNull() ?: "arm64-v8a"
        return when {
            abi.startsWith("arm64") || abi.startsWith("aarch64") ->
                "https://github.com/PojavLauncherTeam/android-openjdk-build-multiarch/releases/download/jre17-aarch64/jre17-aarch64.tar.gz"
            abi.startsWith("armeabi") ->
                "https://github.com/PojavLauncherTeam/android-openjdk-build-multiarch/releases/download/jre17-arm32/jre17-arm32.tar.gz"
            else ->
                "https://github.com/PojavLauncherTeam/android-openjdk-build-multiarch/releases/download/jre17-x86_64/jre17-x86_64.tar.gz"
        }
    }

    /** runtime 根目录 */
    fun root(): File = File(Dirs.runtime, "jre")

    /** 当前可用的 java 可执行文件；未安装返回 null */
    fun javaExecutable(): File? {
        val custom = Settings.javaPath
        if (custom.isNotBlank()) {
            val f = File(custom)
            if (f.exists() && f.canExecute()) return f
        }
        val fixed = File(root(), "bin/java")
        if (fixed.exists()) {
            fixed.setExecutable(true)
            Settings.javaPath = fixed.absolutePath
            return fixed
        }
        // 有些构建目录结构为 jre/<name>/bin/java
        root().listFiles()?.forEach { dir ->
            val j = File(dir, "bin/java")
            if (j.exists()) {
                j.setExecutable(true)
                Settings.javaPath = j.absolutePath
                return j
            }
        }
        return null
    }

    fun isInstalled(): Boolean = javaExecutable() != null

    /**
     * 安装 JRE：下载压缩包 → 解压到 runtime/jre → 递归赋可执行权限
     */
    fun install(url: String = Settings.jreUrl.ifBlank { defaultUrl() }) {
        Dirs.ensure()
        val target = root()
        if (target.exists()) target.deleteRecursively()
        target.mkdirs()
        val archive = File(Dirs.runtime, "jre-download")
        LogBus.progress("安装 Java", 0f, "下载 JRE…")
        Http.download(url, archive, task = "安装 Java") { done, total ->
            if (total > 0) LogBus.progress("安装 Java", done.toFloat() / total, "下载 ${done / 1024}KB / ${total / 1024}KB")
        }
        LogBus.progress("安装 Java", -1f, "解压中…")
        if (url.endsWith(".zip")) unzip(archive, target)
        else extractTarGz(archive, target)
        archive.delete()
        chmodRecursive(target)
        val exe = javaExecutable() ?: throw RuntimeException("未找到 java 可执行文件，请检查 JRE 包结构")
        Settings.javaPath = exe.absolutePath
        LogBus.progress("安装 Java", 1f, "Java 运行时安装完成：${exe.absolutePath}")
        LogBus.log("Java 运行时安装完成：${exe.absolutePath}")
    }

    private fun chmodRecursive(dir: File) {
        dir.walkTopDown().forEach { f ->
            if (f.isDirectory) f.setExecutable(true)
            else if (f.name == "java" || f.name.endsWith(".so") || !f.name.contains('.')) {
                runCatching { f.setExecutable(true, false) }
            }
        }
    }

    /** 解压 tar.gz（JRE 发布包常用格式） */
    fun extractTarGz(archive: File, target: File) {
        target.mkdirs()
        org.apache.commons.compress.archivers.tar.TarArchiveInputStream(
            java.util.zip.GZIPInputStream(archive.inputStream())
        ).use { tais ->
            var entry = tais.nextTarEntry
            while (entry != null) {
                val out = File(target, entry.name)
                if (entry.isDirectory) {
                    out.mkdirs()
                } else {
                    out.parentFile?.mkdirs()
                    out.outputStream().use { tais.copyTo(it) }
                }
                entry = tais.nextTarEntry
            }
        }
    }

    /**
     * 为 java 进程准备环境变量（LD_LIBRARY_PATH / JAVA_HOME / HOME / TMPDIR）
     * 渲染相关（OpenGL 兼容层）由设置项 RendererEnv 追加，例如：
     *   POJAV_RENDERER=opengles3_virgl
     */
    fun applyEnv(env: MutableMap<String, String>, gameDir: File = Dirs.game) {
        val java = javaExecutable()
        if (java != null) {
            val home = java.parentFile?.parentFile
            if (home != null) {
                env["JAVA_HOME"] = home.absolutePath
                val libDirs = mutableListOf<String>()
                listOf("lib", "lib/server", "lib/jli", "lib/aarch64", "lib/aarch64/server").forEach {
                    val d = File(home, it)
                    if (d.exists()) libDirs += d.absolutePath
                }
                env["LD_LIBRARY_PATH"] = (libDirs + (env["LD_LIBRARY_PATH"] ?: "")).joinToString(":")
            }
            env["PATH"] = java.parentFile!!.absolutePath + ":" + (env["PATH"] ?: "")
        }
        env["HOME"] = gameDir.absolutePath
        env["TMPDIR"] = Dirs.temp.absolutePath
        env["LANG"] = "en_US.UTF-8"
        env["JAVA_TMPDIR"] = Dirs.temp.absolutePath
        // 渲染层：可填 opengles2 / opengles3 / opengles3_virgl / vulkan 等，由外部运行时解释
        val renderer = Settings.get("renderer", "")
        if (renderer.isNotBlank()) env["POJAV_RENDERER"] = renderer
        val extra = Settings.get("extraEnv", "")
        extra.split("\n").forEach {
            val kv = it.split("=", limit = 2)
            if (kv.size == 2) env[kv[0].trim()] = kv[1].trim()
        }
    }
}
