using AudioCropper.Install;

namespace AudioCropper.UI;

public partial class InstallForm : Form
{
    private readonly InstallerService _installer = new();
    private bool _working;

    public InstallForm()
    {
        InitializeComponent();
        txtDir.Text = InstallerService.ResolveDefaultInstallDirectory();

        btnBrowse.Click += OnBrowseClicked;
        btnInstall.Click += async (_, _) => await InstallAsync().ConfigureAwait(true);
        btnClose.Click += (_, _) => Close();
    }

    private void OnBrowseClicked(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择安装目录",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true,
            SelectedPath = Directory.Exists(txtDir.Text) ? txtDir.Text : string.Empty,
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            txtDir.Text = dialog.SelectedPath;
        }
    }

    private async Task InstallAsync()
    {
        if (_working)
        {
            return;
        }

        var directory = txtDir.Text.Trim();
        if (directory.Length == 0)
        {
            MessageBox.Show(this, "请填写安装目录。", "安装", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        SetWorking(true);

        try
        {
            var options = new InstallOptions
            {
                InstallDirectory = directory,
                CreateDesktopShortcut = chkDesktop.Checked,
                CreateStartMenuShortcut = chkStartMenu.Checked,
            };

            var progress = new Progress<string>(text => lblStatus.Text = text);
            var result = await _installer.InstallAsync(options, progress).ConfigureAwait(true);

            if (!result.Success)
            {
                var failure = result.Message;
                if (result.Warnings.Count > 0)
                {
                    failure += Environment.NewLine + string.Join(Environment.NewLine, result.Warnings);
                }

                MessageBox.Show(this, failure, "安装失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var message = result.Message;
            if (result.InstalledExecutable is not null)
            {
                message += Environment.NewLine + Environment.NewLine + result.InstalledExecutable;
            }

            if (result.Warnings.Count > 0)
            {
                message += Environment.NewLine + Environment.NewLine + "提示：" + Environment.NewLine +
                           string.Join(Environment.NewLine, result.Warnings);
            }

            var launch = MessageBox.Show(
                this,
                message + Environment.NewLine + Environment.NewLine + "现在启动 AudioCropper 吗？",
                "安装完成",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);

            DialogResult = DialogResult.OK;

            if (launch == DialogResult.Yes && result.InstalledExecutable is not null)
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(result.InstalledExecutable)
                    {
                        UseShellExecute = true,
                        WorkingDirectory = Path.GetDirectoryName(result.InstalledExecutable) ?? string.Empty,
                    });
                }
                catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception)
                {
                    MessageBox.Show(this, $"无法启动程序：{ex.Message}", "启动失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }

            Close();
        }
        finally
        {
            SetWorking(false);
        }
    }

    private void SetWorking(bool working)
    {
        _working = working;
        btnInstall.Enabled = !working;
        btnBrowse.Enabled = !working;
        txtDir.ReadOnly = working;
        chkDesktop.Enabled = !working;
        chkStartMenu.Enabled = !working;
        btnClose.Enabled = !working;
        progressBar1.Style = working ? ProgressBarStyle.Marquee : ProgressBarStyle.Blocks;
        progressBar1.MarqueeAnimationSpeed = working ? 30 : 0;
        UseWaitCursor = working;
    }
}
