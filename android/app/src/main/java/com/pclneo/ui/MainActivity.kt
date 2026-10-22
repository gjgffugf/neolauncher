package com.pclneo.ui

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Build
import androidx.compose.material.icons.filled.Download
import androidx.compose.material.icons.filled.Home
import androidx.compose.material.icons.filled.List
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material3.Icon
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.NavigationBar
import androidx.compose.material3.NavigationBarItem
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.pclneo.core.Dirs
import com.pclneo.core.LogBus
import com.pclneo.core.Settings
import com.pclneo.ui.screens.DownloadScreen
import com.pclneo.ui.screens.HomeScreen
import com.pclneo.ui.screens.LogScreen
import com.pclneo.ui.screens.SettingsScreen
import com.pclneo.ui.screens.VersionScreen

class MainActivity : ComponentActivity() {

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        Settings.init(this)
        Dirs.ensure()
        com.pclneo.core.LogBus.log("PCL Neo Android 启动，游戏目录：${Dirs.game.absolutePath}")
        setContent {
            PclNeoTheme {
                MainApp()
            }
        }
    }
}

@Composable
fun MainApp() {
    var tab by rememberSaveable { mutableStateOf(0) }
    val progress by LogBus.progress.collectAsState()

    Scaffold(
        bottomBar = {
            NavigationBar {
                listOf(
                    "启动" to Icons.Default.Home,
                    "下载" to Icons.Default.Download,
                    "版本" to Icons.Default.List,
                    "设置" to Icons.Default.Settings,
                    "日志" to Icons.Default.Build
                ).forEachIndexed { index, (label, icon) ->
                    NavigationBarItem(
                        selected = tab == index,
                        onClick = { tab = index },
                        icon = { Icon(icon, contentDescription = label) },
                        label = { Text(label) }
                    )
                }
            }
        }
    ) { padding ->
        Box(Modifier.fillMaxSize().padding(padding)) {
            when (tab) {
                0 -> HomeScreen()
                1 -> DownloadScreen()
                2 -> VersionScreen()
                3 -> SettingsScreen()
                4 -> LogScreen()
            }
            // 顶部全局进度条
            if (progress.value < 0f || progress.value in 0f..0.99f) {
                LinearProgressIndicator(
                    progress = { if (progress.value < 0f) 0.5f else progress.value },
                    modifier = Modifier.fillMaxWidth()
                )
            }
        }
    }
}

/** 全局统一的卡片标题样式 */
@Composable
fun SectionTitle(text: String) {
    Text(
        text,
        style = MaterialTheme.typography.titleMedium,
        color = MaterialTheme.colorScheme.primary
    )
}
