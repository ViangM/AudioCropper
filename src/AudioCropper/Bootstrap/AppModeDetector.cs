namespace AudioCropper.Bootstrap;

/// <summary>同一个 EXE 的运行模式（设计文档第 5 章）。</summary>
public enum AppMode
{
    /// <summary>直接运行：进入主界面。</summary>
    Run,

    /// <summary>内置安装向导。</summary>
    Install,

    /// <summary>卸载流程。</summary>
    Uninstall,

    /// <summary>只打印版本信息。</summary>
    ShowVersion,

    /// <summary>只打印用法。</summary>
    ShowHelp,
}

/// <summary>命令行到运行模式的映射。</summary>
public static class AppModeDetector
{
    public const string Usage = """
        AudioCropper —— 离线音频分割工具

        用法:
          AudioCropper.exe                 直接运行（无需安装）
          AudioCropper.exe --install       启动安装向导
          AudioCropper.exe --uninstall     卸载本机安装
          AudioCropper.exe --version       显示版本
          AudioCropper.exe --help          显示本帮助
        """;

    public static AppMode Detect(IReadOnlyList<string> args)
    {
        foreach (var raw in args)
        {
            switch (raw.Trim().TrimStart('-', '/').ToLowerInvariant())
            {
                case "install":
                    return AppMode.Install;
                case "uninstall":
                    return AppMode.Uninstall;
                case "version":
                case "v":
                    return AppMode.ShowVersion;
                case "help":
                case "h":
                case "?":
                    return AppMode.ShowHelp;
            }
        }

        return AppMode.Run;
    }
}
