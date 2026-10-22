# PCL Neo 依赖的外部接口清单

所有网络请求均带 `User-Agent: PCLNeo/{version}`，桌面端与 Android 端共用同一套接口定义，便于两端行为一致。

## 1. 下载源（可在设置中切换「官方 / BMCLAPI 镜像」）

| 用途 | 官方 | BMCLAPI 镜像 |
|---|---|---|
| 版本清单 | `https://piston-meta.mojang.com/mc/game/version_manifest_v2.json` | `https://bmclapi2.bangbang93.com/mc/game/version_manifest_v2.json` |
| 版本 JSON | 清单内 `url` 字段 | `https://bmclapi2.bangbang93.com/version/{id}/json` |
| 客户端 jar | 清单内 `url` → `client` | `https://bmclapi2.bangbang93.com/version/{id}/client` |
| 资源索引 | JSON 内 `assetIndex.url` | `https://bmclapi2.bangbang93.com/version/{id}/json` 内已替换 |
| 资源文件 | `https://resources.download.minecraft.net/{前2位}/{hash}` | `https://bmclapi2.bangbang93.com/assets/{hash}` |
| 依赖库 | `https://libraries.minecraft.net/{path}` | `https://bmclapi2.bangbang93.com/maven/{path}` |

## 2. 模组加载器

| 加载器 | 版本列表 | 安装方式 |
|---|---|---|
| Forge | `https://bmclapi2.bangbang93.com/forge/minecraft/{mc}` 或官方 `https://maven.minecraftforge.net/net/minecraftforge/forge/maven-metadata.xml` | 下载 installer jar → `java -jar installer.jar --installClient {mcDir}` |
| NeoForge | `https://bmclapi2.bangbang93.com/neoforge/version/list?mcversion={mc}` | 同 Forge（新版 installer 支持 `--install-client`） |
| Fabric | `https://meta.fabricmc.net/v2/versions/loader/{mc}` | 取 `https://meta.fabricmc.net/v2/versions/loader/{mc}/{loader}/profile/json` 直接写版本 JSON（无需 Java） |
| Quilt | `https://meta.quiltmc.org/v3/versions/loader/{mc}` | 取 `https://meta.quiltmc.org/v3/versions/loader/{mc}/{loader}/profile/json` 直接写版本 JSON |

## 3. 模组 / 整合包平台

**Modrinth（无需 Key）**
- 搜索：`GET https://api.modrinth.com/v2/search?query={kw}&limit=20&index=relevance&facets=[["project_type:mod"],["categories:{loader}"],["versions:{mc}"]]`
- 项目：`GET https://api.modrinth.com/v2/project/{id|slug}`
- 版本：`GET https://api.modrinth.com/v2/project/{id}/version?loaders=["fabric"]&game_versions=["1.20.1"]`
- 下载：版本对象 `files[].url`（`primary=true` 优先）

**CurseForge（需 API Key，设置中填写）**
- 搜索：`GET https://api.curseforge.com/v1/mods/search?gameId=432&searchFilter={kw}&modLoaderType=1&gameVersion={mc}`，Header：`x-api-key: {key}`
- 文件：`GET https://api.curseforge.com/v1/mods/{modId}/files?gameVersion={mc}`
- 下载：`GET https://api.curseforge.com/v1/mods/{modId}/files/{fileId}/download-url` → 得到直链

## 4. 登录

| 方式 | 流程 |
|---|---|
| 离线 | 本地按用户名生成 UUID（`UUID.nameUUIDFromBytes("OfflinePlayer:"+name)`），不联网 |
| 外置（Yggdrasil） | `POST {base}/authserver/authenticate` → `{base}/authserver/refresh` → `{base}/authserver/validate` → `{base}/sessionserver/session/minecraft/join` |
| 微软正版 | ① `POST https://login.microsoftonline.com/consumers/oauth2/v2.0/devicecode`（scope `XboxLive.signin offline_access`）② `POST .../oauth2/v2.0/token` ③ `POST https://user.auth.xboxlive.com/user/authenticate` ④ `POST https://xsts.auth.xboxlive.com/xsts/authorize` ⑤ `GET https://api.minecraftservices.com/authentication/login_with_xbox` ⑥ `GET https://api.minecraftservices.com/minecraft/profile` |

> 微软登录需要一个 Azure 应用 Client ID，默认内置一个公开可用的占位值，请在「设置 → 登录」中替换为你自己的（否则可能因调用量限制失败）。

## 5. Java 运行时

- 桌面端：`https://api.adoptium.net/v3/assets/latest/{feature}/hotspot?architecture=x64&image_type=jdk&os=windows&vendor=eclipse`（下载链接在返回 JSON 的 `binaries[0].package.link`）
- Android 端：默认使用 Pojav 社区维护的 Android JRE 构建（可在设置中填写自定义 URL），解压到 `filesDir/runtime/jre` 后以独立进程方式 `java` 启动。
