package com.pclneo.ui.screens

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.FilterChip
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.pclneo.core.JavaRuntime
import com.pclneo.core.LogBus
import com.pclneo.core.Settings
import com.pclneo.data.DownloadSource
import com.pclneo.ui.SectionTitle
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

@Composable
fun SettingsScreen() {
    val scope = rememberCoroutineScope()

    var gameDir by remember { mutableStateOf(Settings.gameDir) }
    var javaPath by remember { mutableStateOf(Settings.javaPath) }
    var memory by remember { mutableStateOf(Settings.memoryMax.toString()) }
    var mirror by remember { mutableStateOf(Settings.downloadSource == DownloadSource.BMCLAPI) }
    var cfKey by remember { mutableStateOf(Settings.cfApiKey) }
    var msId by remember { mutableStateOf(Settings.msClientId) }
    var ygg by remember { mutableStateOf(Settings.yggServer) }
    var jvmArgs by remember { mutableStateOf(Settings.jvmArgs) }
    var jreUrl by remember { mutableStateOf(Settings.jreUrl) }
    var status by remember { mutableStateOf("") }

    Column(Modifier.fillMaxSize().padding(12.dp).verticalScroll(rememberScrollState()),
        verticalArrangement = Arrangement.spacedBy(10.dp)) {

        Card(Modifier.fillMaxWidth()) {
            Column(Modifier.padding(12.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                SectionTitle("基本设置")
                OutlinedTextField(gameDir, { gameDir = it }, label = { Text("游戏目录") }, modifier = Modifier.fillMaxWidth(), singleLine = true)
                OutlinedTextField(memory, { memory = it }, label = { Text("最大内存 (MB)") }, modifier = Modifier.fillMaxWidth(), singleLine = true)
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    FilterChip(mirror, { mirror = it }, label = { Text("BMCLAPI 镜像") })
                    FilterChip(!mirror, { mirror = !it }, label = { Text("官方源") })
                }
            }
        }

        Card(Modifier.fillMaxWidth()) {
            Column(Modifier.padding(12.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                SectionTitle("Java 运行时")
                Text("当前：${javaPath.ifBlank { "未安装" }}", style = MaterialTheme.typography.bodySmall)
                OutlinedTextField(jreUrl, { jreUrl = it }, label = { Text("JRE 下载地址（留空用默认）") }, modifier = Modifier.fillMaxWidth(), singleLine = true)
                Button(onClick = {
                    status = "正在下载安装 JRE…"
                    scope.launch(Dispatchers.IO) {
                        try {
                            JavaRuntime.install(jreUrl.ifBlank { JavaRuntime.defaultUrl() })
                            withContext(Dispatchers.Main) {
                                javaPath = Settings.javaPath
                                status = "JRE 安装完成：$javaPath"
                            }
                        } catch (e: Exception) {
                            LogBus.log("JRE 安装失败：${e.message}", "E")
                            withContext(Dispatchers.Main) { status = "JRE 安装失败：${e.message}" }
                        }
                    }
                }, modifier = Modifier.fillMaxWidth()) { Text("下载 / 重装 JRE（约 40~80MB）") }
                Text("提示：Minecraft Java 版在 Android 上还需要 OpenGL 兼容层，若启动后黑屏，请在下方「渲染器」中切换。",
                    style = MaterialTheme.typography.bodySmall)
                var renderer by remember { mutableStateOf(Settings.get("renderer", "")) }
                OutlinedTextField(renderer, {
                    renderer = it
                    Settings.set("renderer", it)
                }, label = { Text("渲染器 POJAV_RENDERER") }, modifier = Modifier.fillMaxWidth(), singleLine = true)
            }
        }

        Card(Modifier.fillMaxWidth()) {
            Column(Modifier.padding(12.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                SectionTitle("账号与平台")
                OutlinedTextField(cfKey, { cfKey = it }, label = { Text("CurseForge API Key") }, modifier = Modifier.fillMaxWidth(), singleLine = true)
                OutlinedTextField(msId, { msId = it }, label = { Text("微软 Client ID（留空用内置）") }, modifier = Modifier.fillMaxWidth(), singleLine = true)
                OutlinedTextField(ygg, { ygg = it }, label = { Text("外置登录服务器地址") }, modifier = Modifier.fillMaxWidth(), singleLine = true)
            }
        }

        Card(Modifier.fillMaxWidth()) {
            Column(Modifier.padding(12.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                SectionTitle("启动参数")
                OutlinedTextField(jvmArgs, { jvmArgs = it }, label = { Text("额外 JVM 参数") }, modifier = Modifier.fillMaxWidth(), singleLine = true)
            }
        }

        Button(onClick = {
            Settings.gameDir = gameDir
            Settings.memoryMax = memory.toIntOrNull() ?: 1024
            Settings.downloadSource = if (mirror) DownloadSource.BMCLAPI else DownloadSource.OFFICIAL
            Settings.cfApiKey = cfKey
            Settings.msClientId = msId
            Settings.yggServer = ygg
            Settings.jvmArgs = jvmArgs
            Settings.jreUrl = jreUrl
            status = "已保存"
            LogBus.log("设置已保存")
        }, modifier = Modifier.fillMaxWidth()) { Text("保存设置") }

        OutlinedButton(onClick = { status = "游戏目录：${Settings.gameDir}" }) { Text("查看当前游戏目录") }

        Text(status, style = MaterialTheme.typography.bodySmall)
    }
}
