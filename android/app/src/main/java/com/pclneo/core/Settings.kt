package com.pclneo.core

import android.content.Context
import com.pclneo.data.DownloadSource

/**
 * 设置存储（SharedPreferences 版，对应桌面端 setup.json）
 */
object Settings {

    private const val FILE = "pclneo"
    private lateinit var app: Context

    fun init(context: Context) {
        app = context.applicationContext
    }

    private fun prefs() = app.getSharedPreferences(FILE, Context.MODE_PRIVATE)

    fun get(key: String, def: String = ""): String = prefs().getString(key, def) ?: def
    fun getInt(key: String, def: Int): Int = prefs().getInt(key, def)
    fun getBool(key: String, def: Boolean): Boolean = prefs().getBoolean(key, def)
    fun set(key: String, value: String) = prefs().edit().putString(key, value).apply()
    fun setInt(key: String, value: Int) = prefs().edit().putInt(key, value).apply()
    fun setBool(key: String, value: Boolean) = prefs().edit().putBoolean(key, value).apply()

    /**
     * 数据根目录（固定，不随游戏目录变化）。
     * instances / runtime / temp / accounts 都放在这里，
     * 否则切换整合包实例后 JRE 与实例列表会丢失。
     */
    val dataDir: String
        get() = app.filesDir.resolve("pclneo").absolutePath

    // ---------- 常用项 ----------
    /** 游戏目录（默认 filesDir/game） */
    var gameDir: String
        get() = get("gameDir", app.filesDir.resolve("game").absolutePath)
        set(v) = set("gameDir", v)

    /** Java 可执行文件路径（Android 上为解压出的 java 二进制） */
    var javaPath: String
        get() = get("javaPath", "")
        set(v) = set("javaPath", v)

    var memoryMax: Int
        get() = getInt("memoryMax", 1024)
        set(v) = setInt("memoryMax", v)

    var downloadSource: DownloadSource
        get() = if (get("source", "BMCLAPI") == "BMCLAPI") DownloadSource.BMCLAPI else DownloadSource.OFFICIAL
        set(v) = set("source", v.name)

    var cfApiKey: String
        get() = get("cfApiKey", "")
        set(v) = set("cfApiKey", v)

    var msClientId: String
        get() = get("msClientId", "")
        set(v) = set("msClientId", v)

    var yggServer: String
        get() = get("yggServer", "")
        set(v) = set("yggServer", v)

    var jvmArgs: String
        get() = get("jvmArgs", "")
        set(v) = set("jvmArgs", v)

    var lastVersion: String
        get() = get("lastVersion", "")
        set(v) = set("lastVersion", v)

    /** 自定义 JRE 下载地址（留空则用内置默认地址） */
    var jreUrl: String
        get() = get("jreUrl", "")
        set(v) = set("jreUrl", v)
}
