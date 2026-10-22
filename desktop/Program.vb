Namespace PCLNeo
    ''' <summary>程序入口（vbproj 中 StartupObject 指向此处）</summary>
    Public Module Program

        <STAThread()>
        Public Sub Main()
            Dim app As New App()
            ' InitializeComponent 会加载 Application.xaml 中的全局资源（配色、卡片与按钮样式）
            ' 并按 StartupUri 打开 MainWindow
            app.InitializeComponent()
            app.Run()
        End Sub

    End Module
End Namespace
