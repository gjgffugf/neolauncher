package com.pclneo.core

import com.pclneo.data.ModFile
import com.pclneo.data.ModItem
import com.pclneo.data.ModSource
import org.json.JSONArray
import org.json.JSONObject
import java.net.URLEncoder

/**
 * Modrinth API v2（无需 Key）
 */
object Modrinth {

    private const val API = "https://api.modrinth.com/v2"

    fun search(keyword: String, mc: String = "", loader: String = "", limit: Int = 20): List<ModItem> {
        val facets = JSONArray()
        facets.put(JSONArray("[\"project_type:mod\"]"))
        if (mc.isNotBlank()) facets.put(JSONArray("[\"versions:$mc\"]"))
        if (loader.isNotBlank() && loader != "Any") facets.put(JSONArray("[\"categories:${loader.lowercase()}\"]"))

        val url = "$API/search?query=${enc(keyword)}&limit=$limit&index=downloads" +
            "&facets=${enc(facets.toString())}"
        val json = Http.getJson(url)
        val hits = json.optJSONArray("hits") ?: JSONArray()
        val list = mutableListOf<ModItem>()
        for (i in 0 until hits.length()) {
            val h = hits.getJSONObject(i)
            list += ModItem(
                id = h.optString("project_id", h.optString("slug")),
                title = h.optString("title"),
                description = h.optString("description"),
                iconUrl = h.optString("icon_url"),
                source = ModSource.MODRINTH
            )
        }
        return list
    }

    fun files(projectId: String, mc: String = "", loader: String = ""): List<ModFile> {
        var url = "$API/project/$projectId/version"
        val q = mutableListOf<String>()
        if (loader.isNotBlank() && loader != "Any") q += "loaders=" + enc("[\"${loader.lowercase()}\"]")
        if (mc.isNotBlank()) q += "game_versions=" + enc("[\"$mc\"]")
        if (q.isNotEmpty()) url += "?" + q.joinToString("&")
        val arr = Http.getArray(url)
        val list = mutableListOf<ModFile>()
        for (i in 0 until arr.length()) {
            val v = arr.getJSONObject(i)
            val files = v.optJSONArray("files") ?: continue
            var primary: JSONObject? = null
            for (j in 0 until files.length()) {
                val f = files.getJSONObject(j)
                if (f.optBoolean("primary")) { primary = f; break }
            }
            val f = primary ?: files.optJSONObject(0) ?: continue
            val gv = v.optJSONArray("game_versions")
            list += ModFile(
                name = v.optString("version_number", v.optString("name", "未命名")),
                url = f.getString("url"),
                fileName = f.getString("filename"),
                mcVersions = join(gv),
                loaders = join(v.optJSONArray("loaders"))
            )
        }
        return list
    }

    private fun enc(s: String) = URLEncoder.encode(s, "UTF-8")

    private fun join(arr: JSONArray?): String {
        if (arr == null) return ""
        val out = mutableListOf<String>()
        for (i in 0 until arr.length()) out += arr.getString(i)
        return out.joinToString(",")
    }
}
