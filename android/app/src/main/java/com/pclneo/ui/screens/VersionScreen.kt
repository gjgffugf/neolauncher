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
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.FilterChip
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.pclneo.core.GameLauncher
import com.pclneo.core.LoaderInstaller
import com.pclneo.core.LogBus
import com.pclneo.core.VersionManager
import com.pclneo.data.LoaderInfo
import com.pclneo.ui.SectionTitle
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import org.json.JSONObject

@Composable
fun VersionScreen() {
    val scope = rememberCoroutineScope()
    var versions by remember { mutableStateOf(listOf<String>()) }
    var selected by remember { mutableStateOf("") }
    var mods by remember { mutableStateOf(listOf<String>()) }
    var status by remember { mutableStateOf("") }

    var loaderKind by remember { mutableStateOf("Fabric") }
    var loaderList by remember { mutableStateOf(listOf<LoaderInfo>()) }
    var loaderVersion by remember { mutableStateOf<LoaderInfo?>(null) }

    fun reload() {
        scope.launch(Dispatchers.IO) {
            versions = VersionManager.localVersions()
            withContext(Dispatchers.Main) {
                if (selected !in versions) selected = versions.firstOrNull() ?: ""
                if (selected.isNotBlank()) mods = GameLauncher.installedMods(selected)
            }
        }
    }
    LaunchedEffect(Unit) { reload() }

    Column(Modifier.fillMaxSize().padding(12.dp).verticalScroll(rememberScrollState()),
        verticalArrangement = Arrangement.spacedBy(10.dp)) {

        Card(Modifier.fillMaxWidth()) {
            Column(Modifier.padding(12.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                SectionTitle("已安装版本")
                versions.forEach { v ->
                    Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
                        Text(v, Modifier.weight(1f))
                        if (v == selected) Text("当前", color = MaterialTheme.colorScheme.primary)
                        OutlinedButton(onClick = { selected = v; mods = GameLauncher.installedMods(v) }) { Text("选中") }
                    }
                }
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    Button(onClick = { reload() }) { Text("刷新") }
                    OutlinedButton(onClick = {
                        if (selected.isBlank()) return@OutlinedButton
                        VersionManager.deleteVersion(selected)
                        reload()
                    }) { Text("删除选中版本") }
                }
            }
        }

        Card(Modifier.fillMaxWidth()) {
            Column(Modifier.padding(12.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                SectionTitle("安装模组加载器")
                Row(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                    listOf("Forge", "NeoForge", "Fabric", "Quilt").forEach { k ->
                        FilterChip(selected = loaderKind == k, onClick = {
                            loaderKind = k
                            loaderList = emptyList()
                            loaderVersion = null
                        }, label = { Text(k) })
                    }
                }
                if (selected.isBlank()) {
                    Text("请先选中一个版本", style = MaterialTheme.typography.bodySmall)
                } else {
                    Button(onClick = {
                        scope.launch(Dispatchers.IO) {
                            try {
                                val mc = baseMc(selected)
                                loaderList = when (loaderKind) {
                                    "Forge" -> LoaderInstaller.forgeList(mc)
                                    "NeoForge" -> LoaderInstaller.neoforgeList(mc)
                                    "Fabric" -> LoaderInstaller.fabricList(mc)
                                    else -> LoaderInstaller.quiltList(mc)
                                }
                                withContext(Dispatchers.Main) { status = "${loaderKind} 共 ${loaderList.size} 个版本" }
                            } catch (e: Exception) {
                                withContext(Dispatchers.Main) { status = "获取失败：${e.message}" }
                            }
                        }
                    }) { Text("获取版本列表") }

                    var expanded by remember { mutableStateOf(false) }
                    OutlinedButton(onClick = { expanded = true }, modifier = Modifier.fillMaxWidth()) {
                        Text(loaderVersion?.display ?: "选择加载器版本")
                    }
                    DropdownMenu(expanded, { expanded = false }) {
                        if (loaderList.isEmpty()) {
                            DropdownMenuItem(text = { Text("（先获取列表）") }, onClick = { expanded = false })
                        }
                        loaderList.forEach { l ->
                            DropdownMenuItem(text = { Text(l.display) }, onClick = { loaderVersion = l; expanded = false })
                        }
                    }

                    Button(onClick = {
                        val lv = loaderVersion ?: run { status = "请选择加载器版本"; return@Button }
                        scope.launch(Dispatchers.IO) {
                            try {
                                val mc = baseMc(selected)
                                when (loaderKind) {
                                    "Forge" -> LoaderInstaller.installForge(mc, lv.version)
                                    "NeoForge" -> LoaderInstaller.installNeoForge(mc, lv.version)
                                    "Fabric" -> LoaderInstaller.installFabric(mc, lv.version)
                                    "Quilt" -> LoaderInstaller.installQuilt(mc, lv.version)
                                }
                                withContext(Dispatchers.Main) { status = "${loaderKind} ${lv.version} 安装完成"; reload() }
                            } catch (e: Exception) {
                                LogBus.log("安装失败：${e.message}", "E")
                                withContext(Dispatchers.Main) { status = "安装失败：${e.message}" }
                            }
                        }
                    }, modifier = Modifier.fillMaxWidth()) { Text("安装加载器") }
                }
            }
        }

        Card(Modifier.fillMaxWidth()) {
            Column(Modifier.padding(12.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
                SectionTitle("已安装模组（${selected}）")
                if (mods.isEmpty()) Text("暂无模组", style = MaterialTheme.typography.bodySmall)
                mods.forEach { m ->
                    Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
                        Text(m, Modifier.weight(1f), style = MaterialTheme.typography.bodySmall)
                        OutlinedButton(onClick = {
                            GameLauncher.toggleMod(selected, m)
                            mods = GameLauncher.installedMods(selected)
                        }) { Text(if (m.endsWith(".disabled")) "启用" else "禁用") }
                    }
                }
            }
        }

        Text(status, style = MaterialTheme.typography.bodySmall)
    }
}

/** 从任意版本名推断其 Minecraft 原版号 */
private fun baseMc(version: String): String {
    return try {
        var json: JSONObject = VersionManager.versionJson(version)
        var guard = 0
        while (json.has("inheritsFrom") && guard++ < 8) {
            val p = json.getString("inheritsFrom")
            json = VersionManager.versionJson(p)
        }
        json.optString("id", version)
    } catch (e: Exception) {
        version
    }
}
