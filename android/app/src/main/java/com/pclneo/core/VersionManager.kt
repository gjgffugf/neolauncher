package com.pclneo.core

import com.pclneo.data.VersionEntry
import org.json.JSONArray
import org.json.JSONObject
import java.io.File
import java.util.zip.ZipFile

/**
 * 目录结构（对应桌面端 ModBase 的路径定义）。
 *
 * game 随实例切换而变化；dataRoot / instances / runtime / temp 固定，
 * 避免切换整合包实例后丢失 JRE、实例列表与账号。
 */
object Dirs {
    /** 固定的启动器数据根目录 */
    val dataRoot: File get() = File(Settings.dataDir)

    /** 当前游戏目录（可随实例切换） */
    val game: File get() = File(Settings.gameDir)
    val versions: File get() = File(game, "versions")
    val libraries: File get() = File(game, "libraries")
    val assets: File get() = File(game, "assets")

    /** 整合包实例目录（固定） */
    val instances: File get() = File(dataRoot, "instances")
    /** Java 运行时目录（固定） */
    val runtime: File get() = File(dataRoot, "runtime")
    /** 临时目录（固定） */
    val temp: File get() = File(dataRoot, "temp")

    fun ensure() {
        dataRoot.mkdirs()
        game.mkdirs(); versions.mkdirs(); libraries.mkdirs()
        assets.mkdirs(); instances.mkdirs(); runtime.mkdirs(); temp.mkdirs()
    }

    fun versionDir(name: String) = File(versions, name)
    fun versionJson(name: String) = File(versions, "$name/$name.json")
    fun versionJar(name: String) = File(versions, "$name/$name.jar")
    fun nativesDir(name: String) = File(versions, "$name/natives")
    fun modsDir(name: String) = File(versionDir(name), "mods")
}

/** 解压 zip（支持过滤前缀） */
fun unzip(zip: File, target: File, filterPrefix: String = "") {
    target.mkdirs()
    ZipFile(zip).use { zf ->
        val entries = zf.entries()
        while (entries.hasMoreElements()) {
            val entry = entries.nextElement()
            if (entry.isDirectory) continue
            if (filterPrefix.isNotBlank() && !entry.name.startsWith(filterPrefix)) continue
            val out = File(target, entry.name)
            out.parentFile?.mkdirs()
            runCatching { zf.getInputStream(entry).use { it.copyTo(out.outputStream()) } }
        }
    }
}

/** 复制目录 */
fun copyDir(src: File, dst: File) {
    dst.mkdirs()
    src.listFiles()?.forEach { f ->
        val t = File(dst, f.name)
        if (f.isDirectory) copyDir(f, t) else runCatching { f.copyTo(t, true) }
    }
}

/** Maven 坐标 → 相对路径 */
fun mavenToPath(name: String, ext: String = "jar"): String {
    val p = name.split(":")
    if (p.size < 3) return name
    val group = p[0].replace('.', '/')
    val artifact = p[1]
    val version = p[2]
    val classifier = if (p.size > 3) "-${p[3]}" else ""
    return "$group/$artifact/$version/$artifact-$version$classifier.$ext"
}

/**
 * 版本管理：清单获取、原版安装、资源与依赖库下载、继承合并
 */
object VersionManager {

    private fun manifestUrl(): String = if (Http.useMirror)
        "https://bmclapi2.bangbang93.com/mc/game/version_manifest_v2.json"
    else
        "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json"

    fun remoteVersions(types: Set<String> = setOf("release")): List<VersionEntry> {
        val json = Http.getJson(manifestUrl())
        val arr = json.getJSONArray("versions")
        val list = mutableListOf<VersionEntry>()
        for (i in 0 until arr.length()) {
            val o = arr.getJSONObject(i)
            val e = VersionEntry(
                id = o.getString("id"),
                type = o.optString("type", "release"),
                url = o.getString("url"),
                releaseTime = o.optString("releaseTime", "")
            )
            if (types.isEmpty() || e.type in types) list.add(e)
        }
        LogBus.log("获取到 ${list.size} 个可下载版本")
        return list
    }

    fun localVersions(): List<String> =
        (Dirs.versions.listFiles()?.filter { File(it, "${it.name}.json").exists() }?.map { it.name }
            ?: emptyList()).sorted()

    /** 读取并合并 inheritsFrom */
    fun versionJson(name: String): JSONObject = mergeInherits(JSONObject(Dirs.versionJson(name).readText()))

    private fun mergeInherits(json: JSONObject): JSONObject {
        if (!json.has("inheritsFrom")) return json
        val chain = mutableListOf<JSONObject>(json)
        var cur = json
        var guard = 0
        while (cur.has("inheritsFrom") && guard++ < 8) {
            val parent = cur.getString("inheritsFrom")
            val f = Dirs.versionJson(parent)
            if (!f.exists()) throw RuntimeException("缺少父版本 $parent")
            val p = JSONObject(f.readText())
            chain.add(0, p)
            cur = p
        }
        val result = JSONObject(chain[0].toString())
        for (i in 1 until chain.size) {
            val child = chain[i]
            for (key in child.keys()) {
                if (key == "libraries" || key == "arguments" || key == "minecraftArguments") {
                    val merged = JSONArray()
                    result.optJSONArray(key)?.let { for (j in 0 until it.length()) merged.put(it.get(j)) }
                    child.optJSONArray(key)?.let { for (j in 0 until it.length()) merged.put(it.get(j)) }
                    result.put(key, merged)
                } else {
                    result.put(key, child.get(key))
                }
            }
        }
        return result
    }

    /** library 是否适用于当前平台（Android 上接受 linux 与通用） */
    private fun libAllowed(lib: JSONObject): Boolean {
        val rules = lib.optJSONArray("rules") ?: return true
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
            if (match) allow = (action == "allow")
        }
        return allow
    }

    /** 下载依赖库与 natives */
    fun downloadLibraries(json: JSONObject, versionName: String) {
        val libs = json.optJSONArray("libraries") ?: return
        val natives = Dirs.nativesDir(versionName)
        natives.mkdirs()
        for (i in 0 until libs.length()) {
            val lib = libs.getJSONObject(i)
            if (!libAllowed(lib)) continue
            val dl = lib.optJSONObject("downloads") ?: continue
            val art = dl.optJSONObject("artifact") ?: continue
            val rel = art.optString("path", mavenToPath(lib.getString("name")))
            val out = File(Dirs.libraries, rel)
            LogBus.progress("安装版本", (i.toFloat() / libs.length()), "依赖库 ${i + 1}/${libs.length()}")
            Http.download(art.getString("url"), out, art.optString("sha1"), "安装版本")
            // natives（Android 上取 linux 分类）
            val nat = lib.optJSONObject("natives")
            if (nat != null && nat.has("linux")) {
                val key = nat.getString("linux").replace("\${arch}", "64")
                val cls = dl.optJSONObject("classifiers")?.optJSONObject(key)
                if (cls != null) {
                    val nout = File(natives, "native-${i}.jar")
                    Http.download(cls.getString("url"), nout, "", "安装版本")
                    runCatching { unzip(nout, natives) }
                        .onFailure { LogBus.log("解压 native 失败：${it.message}", "W") }
                }
            }
        }
    }

    /** 下载资源索引与资源文件 */
    fun downloadAssets(json: JSONObject, versionName: String) {
        val idxInfo = json.optJSONObject("assetIndex") ?: return
        val id = json.optString("assets", idxInfo.optString("id", "legacy"))
        val idxFile = File(Dirs.assets, "indexes/$id.json")
        idxFile.parentFile?.mkdirs()
        if (!idxFile.exists()) {
            Http.download(idxInfo.getString("url"), idxFile, idxInfo.optString("sha1"), "安装版本")
        }
        if (!idxFile.exists()) return
        val idx = JSONObject(idxFile.readText())
        val objs = idx.optJSONObject("objects") ?: return
        val keys = objs.keys()
        var i = 0
        var total = 0
        while (keys.hasNext()) { keys.next(); total++ }
        val it = objs.keys()
        while (it.hasNext()) {
            val k = it.next()
            i++
            val o = objs.getJSONObject(k)
            val hash = o.getString("hash")
            val out = File(Dirs.assets, "objects/${hash.substring(0, 2)}/$hash")
            if (out.exists()) continue
            if (i % 20 == 0) LogBus.progress("安装版本", i.toFloat() / total, "资源 $i/$total")
            runCatching {
                Http.download(
                    "https://resources.download.minecraft.net/${hash.substring(0, 2)}/$hash",
                    out, hash, "安装版本"
                )
            }.onFailure { LogBus.log("资源 $hash 下载失败：${it.message}", "W") }
        }
    }

    /** 安装一个原版 */
    fun installVersion(entry: VersionEntry) {
        Dirs.ensure()
        val name = entry.id
        val dir = Dirs.versionDir(name)
        dir.mkdirs()
        val jsonFile = Dirs.versionJson(name)
        LogBus.progress("安装版本", 0f, "下载版本信息…")
        if (!jsonFile.exists()) Http.download(entry.url, jsonFile, "", "安装版本")
        val json = JSONObject(jsonFile.readText())

        val dl = json.optJSONObject("downloads")?.optJSONObject("client")
        if (dl != null) {
            LogBus.progress("安装版本", 0.05f, "下载客户端 jar…")
            Http.download(dl.getString("url"), Dirs.versionJar(name), dl.optString("sha1"), "安装版本")
        }
        downloadLibraries(json, name)
        downloadAssets(json, name)
        LogBus.progress("安装版本", 1f, "版本 $name 安装完成")
        LogBus.log("版本 $name 安装完成")
    }

    /** 按版本号安装（用于整合包） */
    fun installVanilla(mc: String) {
        val entry = try {
            remoteVersions(setOf("release", "snapshot", "old_beta", "old_alpha")).firstOrNull { it.id == mc }
        } catch (e: Exception) {
            null
        } ?: VersionEntry(mc, "release", "https://bmclapi2.bangbang93.com/version/$mc/json")
        installVersion(entry)
    }

    fun deleteVersion(name: String) {
        Dirs.versionDir(name).deleteRecursively()
        LogBus.log("已删除版本 $name")
    }

    /** 生成 classpath */
    fun buildClasspath(json: JSONObject, versionName: String): String {
        val parts = mutableListOf<String>()
        val libs = json.optJSONArray("libraries")
        if (libs != null) {
            for (i in 0 until libs.length()) {
                val lib = libs.getJSONObject(i)
                if (!libAllowed(lib)) continue
                val art = lib.optJSONObject("downloads")?.optJSONObject("artifact") ?: continue
                val rel = art.optString("path", mavenToPath(lib.getString("name")))
                val f = File(Dirs.libraries, rel)
                if (f.exists()) parts += f.absolutePath
                else LogBus.log("缺少依赖库：${f.name}", "W")
            }
        }
        var jar = Dirs.versionJar(versionName)
        if (!jar.exists()) {
            val parent = json.optString("inheritsFrom")
            if (parent.isNotBlank()) {
                val pj = Dirs.versionJar(parent)
                if (pj.exists()) jar = pj
            }
        }
        if (jar.exists()) parts += jar.absolutePath
        return parts.joinToString(":")
    }
}
