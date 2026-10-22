package com.pclneo.core

import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import org.json.JSONArray
import org.json.JSONObject
import java.io.File
import java.security.MessageDigest
import java.util.concurrent.TimeUnit

/**
 * 网络层：镜像替换、GET/POST、带进度的文件下载、SHA1 校验
 */
object Http {

    val client: OkHttpClient by lazy {
        OkHttpClient.Builder()
            .connectTimeout(20, TimeUnit.SECONDS)
            .readTimeout(60, TimeUnit.SECONDS)
            .writeTimeout(60, TimeUnit.SECONDS)
            .followRedirects(true)
            .build()
    }

    private const val UA = "PCLNeo-Android/1.0"

    val useMirror: Boolean
        get() = Settings.downloadSource == com.pclneo.data.DownloadSource.BMCLAPI

    /** 按下载源做镜像替换 */
    fun mirror(url: String): String {
        if (!useMirror) return url
        var u = url
        u = u.replace("https://piston-meta.mojang.com", "https://bmclapi2.bangbang93.com")
        u = u.replace("https://launchermeta.mojang.com", "https://bmclapi2.bangbang93.com")
        u = u.replace("https://launcher.mojang.com", "https://bmclapi2.bangbang93.com")
        u = u.replace("https://libraries.minecraft.net", "https://bmclapi2.bangbang93.com/maven")
        u = u.replace("https://resources.download.minecraft.net", "https://bmclapi2.bangbang93.com/assets")
        return u
    }

    fun get(url: String, headers: Map<String, String> = emptyMap()): String {
        val b = Request.Builder().url(mirror(url)).get()
        b.header("User-Agent", UA)
        headers.forEach { (k, v) -> b.header(k, v) }
        client.newCall(b.build()).execute().use { resp ->
            if (!resp.isSuccessful) throw RuntimeException("HTTP ${resp.code} $url")
            return resp.body?.string() ?: ""
        }
    }

    fun getJson(url: String, headers: Map<String, String> = emptyMap()): JSONObject =
        JSONObject(get(url, headers))

    fun getArray(url: String, headers: Map<String, String> = emptyMap()): JSONArray =
        JSONArray(get(url, headers))

    fun postJson(url: String, body: JSONObject, headers: Map<String, String> = emptyMap()): JSONObject {
        val media = "application/json; charset=utf-8".toMediaType()
        val req = Request.Builder().url(mirror(url))
            .post(body.toString().toRequestBody(media))
            .apply { header("User-Agent", UA); headers.forEach { (k, v) -> header(k, v) } }
            .build()
        client.newCall(req).execute().use { resp ->
            val text = resp.body?.string() ?: ""
            if (!resp.isSuccessful) throw RuntimeException("HTTP ${resp.code}: $text")
            return if (text.isBlank()) JSONObject() else JSONObject(text)
        }
    }

    fun postForm(url: String, form: Map<String, String>): JSONObject {
        val body = okhttp3.FormBody.Builder().apply { form.forEach { (k, v) -> add(k, v) } }.build()
        val req = Request.Builder().url(mirror(url)).post(body).header("User-Agent", UA).build()
        client.newCall(req).execute().use { resp ->
            val text = resp.body?.string() ?: ""
            if (!resp.isSuccessful) throw RuntimeException("HTTP ${resp.code}: $text")
            return if (text.isBlank()) JSONObject() else JSONObject(text)
        }
    }

    /**
     * 下载文件。onProgress(已完成字节, 总字节)，sha1 非空时校验。
     */
    fun download(
        url: String,
        target: File,
        sha1: String = "",
        task: String = "下载",
        onProgress: ((Long, Long) -> Unit)? = null
    ) {
        target.parentFile?.mkdirs()
        if (target.exists() && sha1.isBlank()) {
            onProgress?.invoke(target.length(), target.length())
            return
        }
        val req = Request.Builder().url(mirror(url)).get().header("User-Agent", UA).build()
        client.newCall(req).execute().use { resp ->
            if (!resp.isSuccessful) throw RuntimeException("下载失败 ${resp.code}：$url")
            val total = resp.body?.contentLength() ?: -1L
            resp.body?.byteStream()?.use { input ->
                target.outputStream().use { out ->
                    val buf = ByteArray(64 * 1024)
                    var done = 0L
                    var n: Int
                    while (input.read(buf).also { n = it } > 0) {
                        out.write(buf, 0, n)
                        done += n
                        onProgress?.invoke(done, total)
                    }
                }
            }
        }
        if (sha1.isNotBlank() && sha1(target) != sha1.lowercase()) {
            throw RuntimeException("SHA1 校验失败：${target.name}")
        }
    }

    fun sha1(file: File): String {
        val md = MessageDigest.getInstance("SHA-1")
        file.inputStream().use { input ->
            val buf = ByteArray(64 * 1024)
            while (true) {
                val n = input.read(buf)
                if (n <= 0) break
                md.update(buf, 0, n)
            }
        }
        return md.digest().joinToString("") { "%02x".format(it) }
    }
}
