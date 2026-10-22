package com.pclneo.data

/** 登录方式 */
enum class AuthType { OFFLINE, YGGDRASIL, MICROSOFT }

/** 一个游戏账号 */
data class Account(
    val name: String,
    val uuid: String,
    val accessToken: String,
    val type: AuthType = AuthType.OFFLINE,
    val yggServer: String = "",
    val refreshToken: String = "",
    val clientToken: String = ""
) {
    /** 显示名：Player（离线） */
    fun display(): String {
        val t = when (type) {
            AuthType.OFFLINE -> "离线"
            AuthType.YGGDRASIL -> "外置"
            AuthType.MICROSOFT -> "微软"
        }
        return "$name（$t）"
    }
}

/** 版本清单条目 */
data class VersionEntry(
    val id: String,
    val type: String = "release",
    val url: String = "",
    val releaseTime: String = ""
)

/** 加载器条目 */
data class LoaderInfo(
    val display: String,
    val version: String
)

/** 模组搜索结果 */
data class ModItem(
    val id: String,
    val title: String,
    val description: String,
    val iconUrl: String = "",
    val source: ModSource = ModSource.MODRINTH
)

enum class ModSource { MODRINTH, CURSEFORGE }

/** 模组可下载文件 */
data class ModFile(
    val name: String,
    val url: String,
    val fileName: String,
    val mcVersions: String = "",
    val loaders: String = ""
)

/** 整合包实例 */
data class InstanceInfo(
    val name: String,
    val dir: String,
    val mcVersion: String = "",
    val loader: String = "",
    val source: String = ""
)

/** Java 运行时信息 */
data class JavaInfo(
    val path: String,
    val feature: Int,
    val version: String = ""
)

/** 下载源 */
enum class DownloadSource { BMCLAPI, OFFICIAL }
