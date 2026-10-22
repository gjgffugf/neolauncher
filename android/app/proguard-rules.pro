# PCL Neo Android 混淆规则
-keep class com.pclneo.data.** { *; }
-keep class com.pclneo.core.Settings { *; }
-dontwarn okhttp3.**
-dontwarn okio.**
-keepattributes Signature, InnerClasses, EnclosingMethod
