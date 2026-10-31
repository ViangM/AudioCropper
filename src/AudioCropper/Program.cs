using AudioCropper.Bootstrap;
using AudioCropper.Install;
using AudioCropper.Safety;
using AudioCropper.UI;

namespace AudioCropper;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var mode = AppModeDetector.Detect(args);

        switch (mode)
        {
            case AppMode.ShowHelp:
                Console.WriteLine(AppModeDetector.Usage);
                return 0;
            case AppMode.ShowVersion:
                Console.WriteLine($"{AppPaths.ProductName} {AppEnvironment.VersionText} ({AppEnvironment.ArchitectureTag})");
                return 0;
        }

        ApplicationConfiguration.Initialize();
        AppLogger.Initialize();
        AppLogger.Info($"启动：mode={mode}，{AppEnvironment.Describe()}");
        HookGlobalExceptionHandlers();

        // 上次升级遗留的旧文件在本进程不是运行中的镜像，可以直接清理。
        UpgradeService.TryCleanupOldBinaries();

        try
        {
            return mode switch
            {
                AppMode.Install => RunInstaller(),
                AppMode.Uninstall => RunUninstall(),
                _ => RunApplication(CollectPathArguments(args)),
            };
        }
        catch (Exception ex)
        {
            AppLogger.Error("未处理的启动异常。", ex);
            ShowFatalError("程序遇到无法恢复的错误，已记录日志。" + Environment.NewLine + ex.Message);
            return 1;
        }
    }

    /// <summary>
    /// 收集命令行里的文件/文件夹参数，支持"把文件夹拖到 EXE 图标上"直接导入。
    /// 只接受真实存在的路径，忽略各种开关参数。
    /// </summary>
    private static IReadOnlyList<string> CollectPathArguments(string[] args)
    {
        var paths = new List<string>();
        foreach (var argument in args)
        {
            if (string.IsNullOrWhiteSpace(argument) || argument.StartsWith('-') || argument.StartsWith('/'))
            {
                continue;
            }

            var value = argument.Trim().Trim('"');
            if (Directory.Exists(value) || File.Exists(value))
            {
                paths.Add(value);
            }
        }

        return paths;
    }

    private static int RunApplication(IReadOnlyList<string> initialPaths)
    {
        var notice = AppEnvironment.GetArchitectureNotice();
        using var form = new MainForm(initialPaths);
        if (notice is not null)
        {
            form.Shown += (_, _) => MessageBox.Show(
                form, notice, AppPaths.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        Application.Run(form);
        return 0;
    }

    private static int RunInstaller()
    {
        using var form = new InstallForm();
        return form.ShowDialog() == DialogResult.OK ? 0 : 1;
    }

    private static int RunUninstall()
    {
        var confirm = MessageBox.Show(
            "确定要卸载 AudioCropper 吗？" + Environment.NewLine + Environment.NewLine +
            "用户设置和已导出的音频文件不会被删除。",
            $"{AppPaths.ProductName} 卸载",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);

        if (confirm != DialogResult.Yes)
        {
            return 1;
        }

        var removeSettings = MessageBox.Show(
            "是否同时删除用户设置和日志？" + Environment.NewLine + "选择“否”将保留这些数据。",
            "保留用户数据",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2) == DialogResult.Yes;

        var result = new UninstallService().Uninstall(removeSettings);

        var message = result.Message;
        if (result.Leftovers.Count > 0)
        {
            message += Environment.NewLine + Environment.NewLine +
                       "以下项目未能删除，请手动检查：" + Environment.NewLine +
                       string.Join(Environment.NewLine, result.Leftovers);
        }

        MessageBox.Show(message, $"{AppPaths.ProductName} 卸载", MessageBoxButtons.OK, MessageBoxIcon.Information);
        return 0;
    }

    private static void HookGlobalExceptionHandlers()
    {
        Application.ThreadException += (_, e) =>
        {
            AppLogger.Error("界面线程未处理异常。", e.Exception);
            ShowFatalError("操作失败：" + e.Exception.Message);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception exception)
            {
                AppLogger.Error("未处理异常。", exception);
            }
        };
    }

    private static void ShowFatalError(string message)
    {
        MessageBox.Show(message, AppPaths.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
