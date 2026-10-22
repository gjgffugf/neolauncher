Imports System.Windows

Namespace PCLNeo
    ''' <summary>应用程序入口对象（资源字典见 Application.xaml）</summary>
    Partial Public Class App
        Inherits Application

        Protected Overrides Sub OnStartup(e As StartupEventArgs)
            MyBase.OnStartup(e)
            ModBase.Log("PCL Neo " & ModBase.AppVersion & " 启动，数据目录：" & ModBase.PathData)
        End Sub

        Private Sub App_DispatcherUnhandledException(sender As Object, e As Threading.DispatcherUnhandledExceptionEventArgs) _
            Handles Me.DispatcherUnhandledException
            ModBase.Log("[崩溃] " & e.Exception.ToString(), ModBase.LogLevel.Critical)
            MessageBox.Show(e.Exception.Message, "PCL Neo 发生错误", MessageBoxButton.OK, MessageBoxImage.Error)
            e.Handled = True
        End Sub

    End Class
End Namespace
