package com.pclneo.core

import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow

/**
 * 全局日志与进度总线：UI 通过 StateFlow 订阅，后台线程直接写入。
 */
object LogBus {

    private const val MAX_LINES = 2000

    private val _logs = MutableStateFlow(listOf<String>())
    val logs: StateFlow<List<String>> = _logs.asStateFlow()

    /** 当前任务名与进度（0~1，负数表示不确定） */
    private val _progress = MutableStateFlow(Progress("空闲", 0f, "就绪"))
    val progress = _progress.asStateFlow()

    data class Progress(val task: String, val value: Float, val status: String)

    @Synchronized
    fun log(message: String, level: String = "I") {
        val line = "[${level}] $message"
        val list = (_logs.value + line).takeLast(MAX_LINES)
        _logs.value = list
    }

    fun progress(task: String, value: Float, status: String) {
        _progress.value = Progress(task, value, status)
    }

    @Synchronized
    fun clear() {
        _logs.value = emptyList()
    }

    fun text(): String = _logs.value.joinToString("\n")
}
