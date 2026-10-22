# PCL Neo

一个跨平台的 Minecraft Java 版启动器：电脑端（Windows）模仿 PCL2 的界面与操作逻辑，手机端（Android）以底部导航还原同样的五块功能。两端共用同一套接口设计，通过 GitHub Actions 自动构建。

## 功能

| 模块 | 桌面端 | Android 端 |
|---|---|---|
| 离线登录 | ✔ | ✔ |
| 外置登录（Yggdrasil / 统一通行证） | ✔ | ✔ |
| 微软正版登录（设备代码流） | ✔ | ✔ |
| 游戏版本下载（含库、资源、natives） | ✔ | ✔ |
| Forge / NeoForge 安装 | ✔（调用 installer） | ✔（需先装 JRE） |
| Fabric / Quilt 安装 | ✔（直接写版本 JSON） | ✔ |
| Modrinth 模组搜索下载 | ✔ | ✔ |
| CurseForge 模组搜索下载 | ✔ | ✔ |
| 整合包导入（.mrpack / CurseForge .zip） | ✔ | ✔ |
| 版本隔离与实例切换 | ✔ | ✔ |
| 模组启用 / 禁用 / 删除 | ✔ | ✔ |
| Java 运行时管理 | 自动扫描 + Adoptium 下载 | 下载并解压 Android JRE |
| 实时日志与进度 | ✔ | ✔ |

## 目录结构

```
PCLNeo/
├─ desktop/            电脑端：VB.NET + WPF
│  ├─ Modules/         核心逻辑（下载 / 启动 / 登录 / 加载器 / 模组 / 整合包）
│  ├─ Pages/           启动、下载、版本、设置、日志五个页面
│  └─ Windows/         主窗口（侧边导航 + 全局进度条）
├─ android/            手机端：Kotlin + Jetpack Compose
│  └─ app/src/main/java/com/pclneo/
│     ├─ core/         网络、登录、版本、加载器、整合包、Java 运行时、启动
│     ├─ ui/screens/   启动、下载、版本、设置、日志五个页面
│     └─ data/         数据模型
├─ docs/API.md         所有用到的外部接口清单
└─ .github/workflows/  三套自动构建：桌面端、手机端、打标签发布
```

## 构建

### 电脑端（Windows）

```powershell
dotnet publish desktop/PCLNeo.vbproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

产物在 `out/PCLNeo/`。推送到 `main` 分支或手动触发 `desktop.yml` 也会自动构建。

### 手机端（Android）

```
cd android
gradle :app:assembleDebug
```

产物在 `android/app/build/outputs/apk/debug/`。也可以手动触发 `android.yml`。

打 `v*` 标签时，`release.yml` 会同时产出 Windows 压缩包和 Android APK 并创建 Release。

### 自动构建

| 工作流 | 触发方式 | 说明 |
|---|---|---|
| `build.yml` | 推送 / PR / 手动 | **统一入口**，可勾选只建电脑端、只建手机端或两端都建 |
| `desktop.yml` | 手动 | 只构建电脑端的快捷入口 |
| `android.yml` | 手动 | 只构建手机端的快捷入口 |
| `release.yml` | 打 `v*` 标签 / 手动 | 两端一起构建并创建 GitHub Release |

推送到 main 时只有 `build.yml` 会跑，单端工作流不自动触发，避免重复消耗 Actions 时长。

> Android 的 Release 变体未配置签名，产出的是未签名 APK，需本地 `apksigner` 签名后才能安装。日常直接用 Debug 包即可。

## 使用要点

**登录**
- 离线：随便填用户名即可，UUID 按 `OfflinePlayer:<名字>` 生成，与服务端一致。
- 外置：填写服务器地址（形如 `https://example.com/api/yggdrasil`）+ 账号密码。
- 微软：点击后会给出一串设备代码，在浏览器打开提示的地址输入代码并授权，回到启动器点确认。默认内置一个公开 Client ID，调用量大时可能受限，建议在设置里换成自己的。

**CurseForge**：需要 API Key，到 console.curseforge.com 申请后填入设置。Modrinth 不需要。

**下载源**：默认使用 BMCLAPI 国内镜像，可在设置中切回官方源。

**Android 端运行**：Minecraft Java 版在手机上需要 JRE 与 OpenGL 兼容层。
- 先在「设置」里点「下载 / 重装 JRE」（约 40~80MB）。
- 启动后若黑屏或闪退，在「设置 → 渲染器 POJAV_RENDERER」中切换 `opengles2` / `opengles3` / `opengles3_virgl` 等取值，具体可用项取决于设备上的兼容层实现。
- 默认 JRE 地址指向社区维护的 Android OpenJDK 构建，版本可能变动，建议换成自己的镜像。

## 已知限制

- 电脑端面向 Windows，Linux / macOS 需要调整 natives 判定与路径分隔符。
- Forge 的 Maven 地址在 1.20.2 之后版本命名规则有变化，老版本规则可能取不到最新 Forge。
- Android 端首次安装 1.18+ 版本对内存要求较高，建议在设置中把最大内存调到设备允许的上限。
- 整合包中的 CurseForge 模组若无 API Key，会使用 ForgeCDN 的推测地址，有个别文件可能下载失败。

## 免责声明

本项目独立实现，仅界面与交互向 PCL2 致敬，不含 PCL2 源码，与其作者无隶属关系。不分发 Minecraft 游戏本体，请自行通过正版渠道获取。
