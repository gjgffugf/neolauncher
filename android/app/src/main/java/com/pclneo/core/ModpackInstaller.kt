package com.pclneo.core

import com.pclneo.data.InstanceInfo
import org.json.JSONObject
import java.io.File

/**
 * 整合包安装：Modrinth(.mrpack) 与 CurseForge(.zip)
 * 安装为一个独立实例目录（game/instances/<name>），并自动切换为当前游戏目录。
 */
object ModpackInstaller {

    private fun instanceFile() = File(Dirs.instances, "instances.json")

    fun instances(): List<InstanceInfo> {
        val f = instanceFile()
        if (!f.exists()) return emptyList()
        return try {
            val arr = org.json.JSONArray(f.readText())
            val list = mutableListOf<InstanceInfo>()
            for (i in 0 until arr.length()) {
                val o = arr.getJSONObject(i)
                list += InstanceInfo(
                    name = o.optString("name"),
                    dir = o.optString("dir"),
                    mcVersion = o.optString("mc"),
                    loader = o.optString("loader"),
                    source = o.optString("source")
                )
            }
            list
        } catch (e: Exception) {
            LogBus.log("读取实例失败：${e.message}", "W")
            emptyList()
        }
    }

    private fun add(info: InstanceInfo) {
        val list = instances().toMutableList()
        list.removeAll { it.dir == info.dir }
        list += info
        val arr = org.json.JSONArray()
        list.forEach {
            arr.put(
                JSONObject().apply {
                    put("name", it.name)
                    put("dir", it.dir)
                    put("mc", it.mcVersion)
                    put("loader", it.loader)
                    put("source", it.source)
                }
            )
        }
        Dirs.instances.mkdirs()
        instanceFile().writeText(arr.toString(2))
    }

    fun switchInstance(info: InstanceInfo) {
        Settings.gameDir = info.dir
        Dirs.ensure()
        LogBus.log("已切换实例：${info.name} → ${info.dir}")
    }

    // ---------- Modrinth ----------
    fun installMrpack(zip: File): InstanceInfo {
        Dirs.ensure(); Dirs.temp.mkdirs()
        val work = File(Dirs.temp, "mrpack-${System.currentTimeMillis()}")
        unzip(zip, work)
        val indexFile = File(work, "modrinth.index.json")
        if (!indexFile.exists()) throw RuntimeException("不是有效的 Modrinth 整合包")
        val index = JSONObject(indexFile.readText())
        val name = index.optString("name", zip.nameWithoutExtension)
        val mc = index.optJSONObject("dependencies")?.optString("minecraft", "")
            ?: throw RuntimeException("整合包未声明 Minecraft 版本")
        val deps = index.optJSONObject("dependencies")

        val dir = File(Dirs.instances, name.replace(Regex("[\\\\/:*?\"<>|]"), "_"))
        dir.mkdirs()
        Settings.gameDir = dir.absolutePath
        Dirs.ensure()
        LogBus.progress("安装整合包", 0f, "$name：准备目录…")

        VersionManager.installVanilla(mc)
        applyLoader(deps, mc)

        val files = index.optJSONArray("files") ?: org.json.JSONArray()
        for (i in 0 until files.length()) {
            val f = files.getJSONObject(i)
            val rel = f.getString("path")
            val urls = f.optJSONArray("downloads") ?: continue
            if (urls.length() == 0) continue
            val out = File(dir, rel)
            out.parentFile?.mkdirs()
            LogBus.progress("安装整合包", 0.5f + 0.45f * i / files.length(), "文件 ${i + 1}/${files.length()}")
            runCatching {
                Http.download(urls.getString(0), out, f.optJSONObject("hashes")?.optString("sha1", "") ?: "", "安装整合包")
            }.onFailure { LogBus.log("文件下载失败 $rel：${it.message}", "W") }
        }

        val overrides = File(work, "overrides")
        if (overrides.exists()) copyDir(overrides, dir)
        val serverOverrides = File(work, "server-overrides")
        if (serverOverrides.exists()) copyDir(serverOverrides, dir)

        val info = InstanceInfo(name, dir.absolutePath, mc, detectLoader(deps), "mrpack")
        add(info)
        LogBus.progress("安装整合包", 1f, "整合包「$name」安装完成")
        work.deleteRecursively()
        return info
    }

    private fun detectLoader(deps: JSONObject?): String {
        deps ?: return ""
        return when {
            deps.has("fabric-loader") -> "fabric-loader ${deps.optString("fabric-loader")}"
            deps.has("quilt-loader") -> "quilt-loader ${deps.optString("quilt-loader")}"
            deps.has("forge") -> "forge ${deps.optString("forge")}"
            deps.has("neoforge") -> "neoforge ${deps.optString("neoforge")}"
            else -> ""
        }
    }

    private fun applyLoader(deps: JSONObject?, mc: String) {
        deps ?: return
        try {
            when {
                deps.has("fabric-loader") -> LoaderInstaller.installFabric(mc, deps.getString("fabric-loader"))
                deps.has("quilt-loader") -> LoaderInstaller.installQuilt(mc, deps.getString("quilt-loader"))
                deps.has("forge") -> LoaderInstaller.installForge(mc, deps.getString("forge"))
                deps.has("neoforge") -> LoaderInstaller.installNeoForge(mc, deps.getString("neoforge"))
            }
        } catch (e: Exception) {
            LogBus.log("加载器安装失败，可在「版本」页手动安装：${e.message}", "W")
        }
    }

    // ---------- CurseForge ----------
    fun installCurseForge(zip: File): InstanceInfo {
        Dirs.ensure(); Dirs.temp.mkdirs()
        val work = File(Dirs.temp, "cfpack-${System.currentTimeMillis()}")
        unzip(zip, work)
        val manifestFile = File(work, "manifest.json")
        if (!manifestFile.exists()) throw RuntimeException("不是有效的 CurseForge 整合包")
        val man = JSONObject(manifestFile.readText())
        val name = man.optString("name", zip.nameWithoutExtension)
        val mc = man.optJSONObject("minecraft")?.optString("version", "")
            ?: throw RuntimeException("整合包未声明 Minecraft 版本")
        var loader = ""
        var loaderVer = ""
        val ld = man.optJSONObject("minecraft")?.optJSONArray("modLoaders")
        if (ld != null && ld.length() > 0) {
            val id = ld.getJSONObject(0).optString("id")
            val p = id.split("-")
            loader = p[0]
            loaderVer = if (p.size > 1) id.substring(p[0].length + 1) else ""
        }

        val dir = File(Dirs.instances, name.replace(Regex("[\\\\/:*?\"<>|]"), "_"))
        dir.mkdirs()
        Settings.gameDir = dir.absolutePath
        Dirs.ensure()
        VersionManager.installVanilla(mc)
        try {
            when (loader) {
                "forge" -> LoaderInstaller.installForge(mc, loaderVer)
                "fabric" -> LoaderInstaller.installFabric(mc, loaderVer)
                "neoforge" -> LoaderInstaller.installNeoForge(mc, loaderVer)
                "quilt" -> LoaderInstaller.installQuilt(mc, loaderVer)
            }
        } catch (e: Exception) {
            LogBus.log("加载器安装失败：${e.message}", "W")
        }

        val mods = File(dir, "mods")
        mods.mkdirs()
        val files = man.optJSONArray("files") ?: org.json.JSONArray()
        for (i in 0 until files.length()) {
            val f = files.getJSONObject(i)
            val pid = f.opt("projectID").toString()
            val fid = f.opt("fileID").toString()
            LogBus.progress("安装整合包", 0.4f + 0.5f * i / files.length(), "模组 ${i + 1}/${files.length()}")
            runCatching {
                Http.download(CurseForge.downloadUrl(pid, fid), File(mods, "$pid-$fid.jar"), task = "安装整合包")
            }.onFailure { LogBus.log("模组 $pid/$fid 下载失败：${it.message}", "W") }
        }

        val overrides = File(work, man.optString("overrides", "overrides"))
        if (overrides.exists()) copyDir(overrides, dir)

        val info = InstanceInfo(name, dir.absolutePath, mc, "$loader $loaderVer", "curseforge")
        add(info)
        LogBus.progress("安装整合包", 1f, "整合包「$name」安装完成")
        work.deleteRecursively()
        return info
    }
}
