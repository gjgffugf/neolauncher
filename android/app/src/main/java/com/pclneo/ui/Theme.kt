package com.pclneo.ui

import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Typography
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color

// PCL 风格蓝白配色
val PclBlue = Color(0xFF3B8EFA)
val PclBlueDark = Color(0xFF2C77DA)
val PclBg = Color(0xFFF7F9FC)
val PclGray = Color(0xFF8A8A8A)

private val LightColors = lightColorScheme(
    primary = PclBlue,
    onPrimary = Color.White,
    primaryContainer = Color(0xFFE3F0FF),
    secondary = PclBlueDark,
    background = PclBg,
    surface = Color.White,
    onSurface = Color(0xFF222222)
)

private val DarkColors = darkColorScheme(
    primary = Color(0xFF6FB0FF),
    onPrimary = Color(0xFF00233F),
    secondary = Color(0xFF9CCBFF),
    background = Color(0xFF101418),
    surface = Color(0xFF171B21),
    onSurface = Color(0xFFE8EAEE)
)

@Composable
fun PclNeoTheme(
    dark: Boolean = isSystemInDarkTheme(),
    content: @Composable () -> Unit
) {
    MaterialTheme(
        colorScheme = if (dark) DarkColors else LightColors,
        typography = Typography(),
        content = content
    )
}
