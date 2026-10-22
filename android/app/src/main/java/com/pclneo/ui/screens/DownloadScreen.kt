package com.pclneo.ui.screens

import android.net.Uri
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
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
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Tab
import androidx.compose.material3.TabRow
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import com.pclneo.core.CurseForge
import com.pclneo.core.Dirs
import com.pclneo.core.GameLauncher
import com.pclneo.core.LogBus
import com.pclneo.core.ModpackInstaller
import com.pclneo.core.Modrinth
import com.pclneo.core.VersionManager
import com.pclneo.data.ModFile
import com.pclneo.data.ModItem
import com.pclneo.data.VersionEntry
import com.pclneo.ui.SectionTitle
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import java.io.File

@Composable
fun DownloadScreen() {
    var tab by remember { mutableStateOf(0) }
    Column(Modifier.fillMaxSize()) {
        TabRow(selectedTabIndex = tab) {
            listOf("版本下载", "Mod 下载", "整合包").forEachIndexed { i, t ->
                Tab(selected = tab == i, onClick = { tab = i }, text = { Text(t) })
            }
        }
        when (tab) {
            0 -> VersionDownloadTab()
            1 -> ModDownloadTab()
            2 -> ModpackTab()
        }
    }
}

// ---------------- 版本下载 ----------------
@Composable
private fun VersionDownloadTab() {
    val scope = rememberCoroutineScope()
    var list by remember { mutableStateOf(listOf<VersionEntry>()) }
    var showSnapshot by remember { mutableStateOf(false) }
    var showOld by remember { mutableStateOf(false) }
    var filter by remember { mutableStateOf("") }
    var status by remember { mutableStateOf("点击刷新获取版本列表") }

    fun load() {
        scope.launch(Dispatchers.IO) {
            try {
                val types = mutableSetOf("release")
                if (showSnapshot) types += "snapshot"
                if (showOld) types += listOf("old_beta", "old_alpha")
                list = VersionManager.remoteVersions(types)
                withContext(Dispatchers.Main) { status = "共 ${list.size} 个版本" }
            } catch (e: Exception) {
                withContext(Dispatchers.Main) { status = "获取失败：${e.message}" }
            }
        }
    }
    LaunchedEffect(Unit) { load() }

    Column(Modifier.fillMaxSize().padding(12.dp).verticalScroll(rememberScrollState()),
        verticalArrangement = Arrangement.spacedBy(8.dp)) {
        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            FilterChip(showSnapshot, { showSnapshot = it; load() }, label = { Text("快照") })
            FilterChip(showOld, { showOld = it; load() }, label = { Text("远古版") })
            Button(onClick = { load() }) { Text("刷新") }
        }
        OutlinedTextField(filter, { filter = it }, label = { Text("过滤版本名") }, modifier = Modifier.fillMaxWidth(), singleLine = true)
        Text(status, style = MaterialTheme.typography.bodySmall)

        val shown = list.filter { it.id.contains(filter, true) }.take(80)
        shown.forEach { e ->
            Card(Modifier.fillMaxWidth()) {
                Row(Modifier.padding(12.dp).fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
                    Column {
                        Text(e.id, style = MaterialTheme.typography.titleSmall)
                        Text("${e.type} · ${e.releaseTime.take(10)}", style = MaterialTheme.typography.bodySmall)
                    }
                    OutlinedButton(onClick = {
                        scope.launch(Dispatchers.IO) {
                            try {
                                VersionManager.installVersion(e)
                                withContext(Dispatchers.Main) { status = "${e.id} 安装完成" }
                            } catch (ex: Exception) {
                                LogBus.log("安装失败：${ex.message}", "E")
                                withContext(Dispatchers.Main) { status = "安装失败：${ex.message}" }
                            }
                        }
                    }) { Text("安装") }
                }
            }
        }
    }
}

// ---------------- Mod 下载 ----------------
@Composable
private fun ModDownloadTab() {
    val scope = rememberCoroutineScope()
    var keyword by remember { mutableStateOf("") }
    var useCf by remember { mutableStateOf(false) }
    var mc by remember { mutableStateOf(Settings.lastVersion) }
    var loader by remember { mutableStateOf("Any") }
    var results by remember { mutableStateOf(listOf<ModItem>()) }
    var files by remember { mutableStateOf(listOf<ModFile>()) }
    var selectedMod by remember { mutableStateOf<ModItem?>(null) }
    var status by remember { mutableStateOf("") }
    var versions by remember { mutableStateOf(listOf<String>()) }

    LaunchedEffect(Unit) { scope.launch(Dispatchers.IO) { versions = VersionManager.localVersions() } }

    Column(Modifier.fillMaxSize().padding(12.dp).verticalScroll(rememberScrollState()),
        verticalArrangement = Arrangement.spacedBy(8.dp)) {
        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            FilterChip(!useCf, { useCf = !it }, label = { Text("Modrinth") })
            FilterChip(useCf, { useCf = it }, label = { Text("CurseForge") })
        }
        OutlinedTextField(keyword, { keyword = it }, label = { Text("搜索关键词") }, modifier = Modifier.fillMaxWidth(), singleLine = true)
        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            SimpleSpinner("MC 版本", versions, mc) { mc = it }
            SimpleSpinner("加载器", listOf("Any", "fabric", "forge", "neoforge", "quilt"), loader) { loader = it }
        }
        Button(onClick = {
            scope.launch(Dispatchers.IO) {
                try {
                    results = if (useCf) CurseForge.search(keyword, mc, loader) else Modrinth.search(keyword, mc, loader)
                    withContext(Dispatchers.Main) { status = "找到 ${results.size} 个结果" }
                } catch (e: Exception) {
                    withContext(Dispatchers.Main) { status = "搜索失败：${e.message}" }
                }
            }
        }) { Text("搜索") }

        results.forEach { m ->
            Card(Modifier.fillMaxWidth()) {
                Row(Modifier.padding(12.dp).fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
                    Column(Modifier.weight(1f)) {
                        Text(m.title, style = MaterialTheme.typography.titleSmall)
                        Text(m.description.take(80), style = MaterialTheme.typography.bodySmall, maxLines = 2)
                    }
                    OutlinedButton(onClick = {
                        selectedMod = m
                        scope.launch(Dispatchers.IO) {
                            try {
                                files = if (m.source == com.pclneo.data.ModSource.CURSEFORGE)
                                    CurseForge.files(m.id, mc) else Modrinth.files(m.id, mc, loader)
                                withContext(Dispatchers.Main) { status = "${m.title}：${files.size} 个版本" }
                            } catch (e: Exception) {
                                withContext(Dispatchers.Main) { status = "获取失败：${e.message}" }
                            }
                        }
                    }) { Text("版本") }
                }
            }
        }

        if (selectedMod != null && files.isNotEmpty()) {
            Card(Modifier.fillMaxWidth()) {
                Column(Modifier.padding(12.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
                    SectionTitle("${selectedMod!!.title} - 选择版本并安装到 $mc")
                    files.take(10).forEach { f ->
                        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
                            Text(f.name, Modifier.weight(1f), style = MaterialTheme.typography.bodySmall)
                            OutlinedButton(onClick = {
                                if (mc.isBlank()) { status = "请先选择 MC 版本"; return@OutlinedButton }
                                scope.launch(Dispatchers.IO) {
                                    try {
                                        GameLauncher.installMod(f.url, f.fileName, mc)
                                        withContext(Dispatchers.Main) { status = "${f.name} 安装完成" }
                                    } catch (e: Exception) {
                                        withContext(Dispatchers.Main) { status = "安装失败：${e.message}" }
                                    }
                                }
                            }) { Text("安装") }
                        }
                    }
                }
            }
        }
        Text(status, style = MaterialTheme.typography.bodySmall)
    }
}

@Composable
private fun SimpleSpinner(label: String, items: List<String>, selected: String, onSelect: (String) -> Unit) {
    var expanded by remember { mutableStateOf(false) }
    OutlinedButton(onClick = { expanded = true }) { Text("$label: ${selected.ifBlank { "未选" }}") }
    DropdownMenu(expanded, { expanded = false }) {
        if (items.isEmpty()) DropdownMenuItem(text = { Text("（空）") }, onClick = { expanded = false })
        items.forEach {
            DropdownMenuItem(text = { Text(it) }, onClick = { onSelect(it); expanded = false })
        }
    }
}

// ---------------- 整合包 ----------------
@Composable
private fun ModpackTab() {
    val scope = rememberCoroutineScope()
    val context = LocalContext.current
    var instances by remember { mutableStateOf(com.pclneo.core.ModpackInstaller.instances()) }
    var status by remember { mutableStateOf("选择 .mrpack 或 CurseForge .zip 导入") }
    var picked by remember { mutableStateOf<Uri?>(null) }

    val launcher = rememberLauncherForActivityResult(ActivityResultContracts.GetContent()) { uri ->
        picked = uri
        if (uri != null) {
            scope.launch(Dispatchers.IO) {
                try {
                    val tmp = File(Dirs.temp, "import-${System.currentTimeMillis()}")
                    tmp.parentFile?.mkdirs()
                    context.contentResolver.openInputStream(uri)?.use { it.copyTo(tmp.outputStream()) }
                    val info = if (tmp.name.endsWith(".mrpack")) ModpackInstaller.installMrpack(tmp)
                    else ModpackInstaller.installCurseForge(tmp)
                    tmp.delete()
                    withContext(Dispatchers.Main) {
                        status = "「${info.name}」安装完成：${info.dir}"
                        instances = ModpackInstaller.instances()
                    }
                } catch (e: Exception) {
                    LogBus.log("导入失败：${e.message}", "E")
                    withContext(Dispatchers.Main) { status = "导入失败：${e.message}" }
                }
            }
        }
    }

    Column(Modifier.fillMaxSize().padding(12.dp).verticalScroll(rememberScrollState()),
        verticalArrangement = Arrangement.spacedBy(8.dp)) {
        Button(onClick = { launcher.launch("*/*") }) { Text("选择整合包文件") }
        OutlinedButton(onClick = { instances = ModpackInstaller.instances() }) { Text("刷新实例列表") }
        Text(status, style = MaterialTheme.typography.bodySmall)

        instances.forEach { info ->
            Card(Modifier.fillMaxWidth()) {
                Row(Modifier.padding(12.dp).fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
                    Column(Modifier.weight(1f)) {
                        Text(info.name, style = MaterialTheme.typography.titleSmall)
                        Text("${info.mcVersion} ${info.loader}", style = MaterialTheme.typography.bodySmall)
                    }
                    OutlinedButton(onClick = {
                        ModpackInstaller.switchInstance(info)
                        status = "已切换到 ${info.name}"
                    }) { Text("切换") }
                }
            }
        }
    }
}
