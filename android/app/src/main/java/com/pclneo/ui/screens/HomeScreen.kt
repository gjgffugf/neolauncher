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
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
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
import com.pclneo.core.Auth
import com.pclneo.core.GameLauncher
import com.pclneo.core.LogBus
import com.pclneo.core.Settings
import com.pclneo.core.VersionManager
import com.pclneo.data.Account
import com.pclneo.ui.SectionTitle
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

@Composable
fun HomeScreen() {
    val scope = rememberCoroutineScope()
    val context = LocalContext.current

    var versions by remember { mutableStateOf(listOf<String>()) }
    var accounts by remember { mutableStateOf(listOf<Account>()) }
    var version by remember { mutableStateOf(Settings.lastVersion) }
    var account by remember { mutableStateOf<Account?>(null) }
    var status by remember { mutableStateOf("就绪") }
    var busy by remember { mutableStateOf(false) }

    fun reload() {
        scope.launch(Dispatchers.IO) {
            versions = VersionManager.localVersions()
            accounts = Auth.getAccounts()
            withContext(Dispatchers.Main) {
                if (version !in versions) version = versions.firstOrNull() ?: ""
                account = accounts.firstOrNull()
            }
        }
    }
    LaunchedEffect(Unit) { reload() }

    Column(
        Modifier.fillMaxSize().padding(14.dp).verticalScroll(rememberScrollState()),
        verticalArrangement = Arrangement.spacedBy(12.dp)
    ) {
        Card(Modifier.fillMaxWidth()) {
            Column(Modifier.padding(14.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                SectionTitle("启动游戏")

                Text("游戏版本")
                Spinner(items = versions, selected = version) { version = it }

                Text("游戏账号")
                val names = accounts.map { it.display() }
                Spinner(items = names, selected = account?.display() ?: "", onSelect = { sel ->
                    account = accounts.firstOrNull { it.display() == sel }
                })

                Button(
                    onClick = {
                        if (version.isBlank()) { status = "请先下载一个版本"; return@Button }
                        val acc = account ?: Auth.loginOffline("Player")
                        Settings.lastVersion = version
                        busy = true
                        status = "正在启动…"
                        scope.launch(Dispatchers.IO) {
                            try {
                                GameLauncher.launch(version, acc)
                                withContext(Dispatchers.Main) { status = "游戏已启动" }
                            } catch (e: Exception) {
                                LogBus.log("启动失败：${e.message}", "E")
                                withContext(Dispatchers.Main) { status = "启动失败：${e.message}" }
                            } finally {
                                withContext(Dispatchers.Main) { busy = false }
                            }
                        }
                    },
                    enabled = !busy,
                    modifier = Modifier.fillMaxWidth()
                ) { Text(if (busy) "启动中…" else "启动游戏") }

                Text(status, style = MaterialTheme.typography.bodySmall)
            }
        }

        LoginCard(onChanged = { reload() })

        Card(Modifier.fillMaxWidth()) {
            Column(Modifier.padding(14.dp)) {
                SectionTitle("运行环境")
                Text(
                    if (com.pclneo.core.JavaRuntime.isInstalled()) "Java 运行时：已安装"
                    else "Java 运行时：未安装，请在「设置」中下载安装 JRE",
                    style = MaterialTheme.typography.bodySmall
                )
                Text("最大内存：${Settings.memoryMax} MB", style = MaterialTheme.typography.bodySmall)
                Text("游戏目录：${Settings.gameDir}", style = MaterialTheme.typography.bodySmall)
            }
        }
    }
}

@Composable
private fun Spinner(items: List<String>, selected: String, onSelect: (String) -> Unit) {
    var expanded by remember { mutableStateOf(false) }
    OutlinedButton(onClick = { expanded = true }, modifier = Modifier.fillMaxWidth()) {
        Text(if (selected.isBlank()) "（未选择）" else selected)
    }
    DropdownMenu(expanded = expanded, onDismissRequest = { expanded = false }) {
        if (items.isEmpty()) {
            DropdownMenuItem(text = { Text("（空）") }, onClick = { expanded = false })
        }
        items.forEach {
            DropdownMenuItem(text = { Text(it) }, onClick = { onSelect(it); expanded = false })
        }
    }
}

@Composable
private fun LoginCard(onChanged: () -> Unit) {
    val scope = rememberCoroutineScope()
    var user by remember { mutableStateOf("") }
    var pwd by remember { mutableStateOf("") }
    var server by remember { mutableStateOf(Settings.yggServer) }
    var hint by remember { mutableStateOf("") }
    var deviceCode by remember { mutableStateOf("") }
    var msInfo by remember { mutableStateOf("") }

    Card(Modifier.fillMaxWidth()) {
        Column(Modifier.padding(14.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
            SectionTitle("账号管理")
            OutlinedTextField(user, { user = it }, label = { Text("用户名") }, modifier = Modifier.fillMaxWidth(), singleLine = true)
            OutlinedTextField(pwd, { pwd = it }, label = { Text("密码（外置 / 无需为正版）") }, modifier = Modifier.fillMaxWidth(), singleLine = true)
            OutlinedTextField(server, { server = it }, label = { Text("外置登录服务器地址") }, modifier = Modifier.fillMaxWidth(), singleLine = true)

            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                Button(onClick = {
                    if (user.isBlank()) { hint = "请输入用户名"; return@Button }
                    Auth.add(Auth.loginOffline(user))
                    Settings.yggServer = server
                    hint = "离线登录成功：$user"
                    onChanged()
                }) { Text("离线登录") }

                OutlinedButton(onClick = {
                    if (user.isBlank() || pwd.isBlank() || server.isBlank()) { hint = "请填写完整"; return@OutlinedButton }
                    scope.launch(Dispatchers.IO) {
                        try {
                            val acc = Auth.loginYggdrasil(user, pwd, server)
                            Auth.add(acc)
                            Settings.yggServer = server
                            withContext(Dispatchers.Main) { hint = "外置登录成功：${acc.name}"; onChanged() }
                        } catch (e: Exception) {
                            withContext(Dispatchers.Main) { hint = "外置登录失败：${e.message}" }
                        }
                    }
                }) { Text("外置登录") }
            }

            OutlinedButton(onClick = {
                scope.launch(Dispatchers.IO) {
                    try {
                        val r = Auth.msDeviceCode()
                        deviceCode = r.getString("device_code")
                        withContext(Dispatchers.Main) {
                            msInfo = "请打开 ${r.getString("verification_uri")} 并输入代码 ${r.getString("user_code")}，然后点击下方按钮"
                        }
                    } catch (e: Exception) {
                        withContext(Dispatchers.Main) { hint = "微软登录失败：${e.message}" }
                    }
                }
            }) { Text("微软正版登录") }

            if (msInfo.isNotBlank()) {
                Text(msInfo, style = MaterialTheme.typography.bodySmall)
                Button(onClick = {
                    val dc = deviceCode
                    scope.launch(Dispatchers.IO) {
                        try {
                            val acc = Auth.msLogin(dc)
                            Auth.add(acc)
                            withContext(Dispatchers.Main) { hint = "微软登录成功：${acc.name}"; msInfo = ""; onChanged() }
                        } catch (e: Exception) {
                            withContext(Dispatchers.Main) { hint = "等待授权失败：${e.message}" }
                        }
                    }
                }) { Text("我已完成授权，开始等待") }
            }

            if (hint.isNotBlank()) Text(hint, style = MaterialTheme.typography.bodySmall)
        }
    }
}
