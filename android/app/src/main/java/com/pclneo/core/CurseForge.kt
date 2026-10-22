package com.pclneo.core

import com.pclneo.data.ModFile
import com.pclneo.data.ModItem
import com.pclneo.data.ModSource
import org.json.JSONArray
import java.net.URLEncoder

/**
 * CurseForge API v1（需要 API Key，在设置中填写）
 * 申请地址：https://console.curseforge.com/
 */
object CurseForge {

    private const val API = "https://api.curseforge.com/v1"
    private const val GAME_ID = 432

    private fun headers(): Map<String, String> {
        val key = Settings.cfApiKey
        if (key.isBlank()) throw RuntimeException("请先在设置中填写 CurseForge API Key")
        return mapOf("x-api-key" to key)
    }

    fun search(keyword: String, mc: String = "", loader: String = ""): List<ModItem> {
        var url = "$API/mods/search?gameId=$GAME_ID&searchFilter=${enc(keyword)}&pageSize=20"
        if (mc.isNotBlank()) url += "&gameVersion=$mc"
        val type = when (loader.lowercase()) {
            "forge" -> "1"; "fabric" -> "4"; "quilt" -> "5"; "neoforge" -> "6"; else -> ""
        }
        if (type.isNotBlank()) url += "&modLoaderType=$type"
        val json = Http.getJson(url, headers())
        val data = json.optJSONArray("data") ?: JSONArray()
        val list = mutableListOf<ModItem>()
        for (i in 0 until data.length()) {
            val m = data.getJSONObject(i)
            list += ModItem(
                id = m.opt("id").toString(),
                title = m.optString("name"),
                description = m.optString("summary"),
                iconUrl = m.optJSONObject("logo")?.optString("thumbnailUrl", "") ?: "",
                source = ModSource.CURSEFORGE
            )
        }
        return list
    }

    fun files(modId: String, mc: String = ""): List<ModFile> {
        var url = "$API/mods/$modId/files?pageSize=50"
        if (mc.isNotBlank()) url += "&gameVersion=$mc"
        val json = Http.getJson(url, headers())
        val data = json.optJSONArray("data") ?: JSONArray()
        val list = mutableListOf<ModFile>()
        for (i in 0 until data.length()) {
            val f = data.getJSONObject(i)
            val fid = f.opt("id").toString()
            val dl = runCatching {
                Http.getJson("$API/mods/$modId/files/$fid/download-url", headers()).optString("data")
            }.getOrDefault(f.optString("downloadUrl"))
            if (dl.isBlank()) continue
            list += ModFile(
                name = f.optString("displayName", f.optString("fileName", fid)),
                url = dl,
                fileName = f.optString("fileName", "$fid.jar"),
                mcVersions = joinArray(f.optJSONArray("gameVersions"))
            )
        }
        return list
    }

    /** 整合包中的模组直链 */
    fun downloadUrl(projectId: String, fileId: String): String {
        return runCatching {
            Http.getJson("$API/mods/$projectId/files/$fileId/download-url", headers()).optString("data")
        }.getOrDefault("").ifBlank {
            "https://mediafiles.forgecdn.net/files/" +
                "${fileId.substring(0, 4)}/${fileId.substring(4)}/mod.jar"
        }
    }

    private fun joinArray(arr: JSONArray?): String {
        if (arr == null) return ""
        val out = mutableListOf<String>()
        for (i in 0 until arr.length()) out += arr.getString(i)
        return out.joinToString(",")
    }

    private fun enc(s: String) = URLEncoder.encode(s, "UTF-8")
}
