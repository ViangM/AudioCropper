using System.Diagnostics;
using AudioCropper.Audio;
using AudioCropper.Bootstrap;
using AudioCropper.Install;
using AudioCropper.Safety;

namespace AudioCropper.UI;

public partial class MainForm : Form
{
    private readonly BatchSplitService _batchService = new();
    private readonly HashSet<string> _paths = new(StringComparer.OrdinalIgnoreCase);
    private readonly IReadOnlyList<string> _initialPaths;

    private CancellationTokenSource? _cancellation;
    private bool _running;

    public MainForm() : this([])
    {
    }

    /// <summary>
    /// <paramref name="initialPaths"/> 来自命令行（把文件夹拖到 EXE 图标上时由系统传入），
    /// 窗口首次显示后自动导入。
    /// </summary>
    public MainForm(IReadOnlyList<string> initialPaths)
    {
        _initialPaths = initialPaths;
        InitializeComponent();
        PopulateFormats();
        WireEvents();
        UpdateOutputPreview();
        AppendStatus("添加文件或整个文件夹（支持多级子目录），设置分段方式后开始。程序完全离线运行。");

        if (_initialPaths.Count > 0)
        {
            Shown += OnFirstShown;
        }
    }

    private void OnFirstShown(object? sender, EventArgs e)
    {
        Shown -= OnFirstShown;
        AddPaths(_initialPaths);
    }

    private void WireEvents()
    {
        btnAddFiles.Click += (_, _) => AddFiles();
        btnAddFolder.Click += (_, _) => AddFolder();
        addFilesMenuItem.Click += (_, _) => AddFiles();
        addFolderMenuItem.Click += (_, _) => AddFolder();
        btnRemoveSelected.Click += (_, _) => RemoveSelected();
        btnClear.Click += (_, _) => ClearFiles();
        exitMenuItem.Click += (_, _) => Close();
        installMenuItem.Click += (_, _) => ShowInstallDialog();
        upgradeMenuItem.Click += async (_, _) => await ApplyUpgradeAsync().ConfigureAwait(true);
        openLogsMenuItem.Click += (_, _) => OpenDirectory(AppPaths.LogsDirectory);
        aboutMenuItem.Click += (_, _) => ShowAbout();

        lvFiles.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Delete && !_running)
            {
                RemoveSelected();
                e.Handled = true;
            }
        };

        EnableFileDropRecursive(this);
    }

    /// <summary>
    /// 把拖放支持挂到窗口内的每个控件上。
    /// WinForms 只有设置了 AllowDrop 的控件才接收 WM_DROPFILES，而界面大部分面积被
    /// 按钮、分组框、列表占据；只给窗体和列表挂拖放会导致"拖到别处没反应"。
    /// </summary>
    private void EnableFileDropRecursive(Control control)
    {
        control.AllowDrop = true;
        control.DragEnter += OnDragEnter;
        control.DragDrop += OnDragDrop;

        foreach (Control child in control.Controls)
        {
            EnableFileDropRecursive(child);
        }
    }

    private void PopulateFormats()
    {
        cmbFormat.Items.Clear();
        foreach (var format in AudioFormat.All)
        {
            cmbFormat.Items.Add(format);
        }

        if (cmbFormat.Items.Count > 0)
        {
            cmbFormat.SelectedIndex = 0;
        }
    }

    private void AddFiles()
    {
        if (_running)
        {
            return;
        }

        using var dialog = new OpenFileDialog
        {
            Title = "选择音频文件（可多选）",
            Filter = AudioFileScanner.OpenFileFilter,
            CheckFileExists = true,
            Multiselect = true,
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            AddPaths(dialog.FileNames);
        }
    }

    private void AddFolder()
    {
        if (_running)
        {
            return;
        }

        using var dialog = new FolderBrowserDialog
        {
            Description = "选择要批量导入的文件夹" + (chkRecursive.Checked ? "（包含所有子文件夹）" : string.Empty),
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            AddPaths([dialog.SelectedPath]);
        }
    }

    private void AddPaths(IEnumerable<string> paths)
    {
        if (_running)
        {
            return;
        }

        var expanded = AudioFileScanner.Expand(paths, chkRecursive.Checked, out var truncated);
        if (expanded.Count == 0)
        {
            AppendStatus("没有找到可导入的音频文件。");
            MessageBox.Show(this, "所选位置没有找到可识别的音频文件。", "没有可导入的文件",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var added = 0;
        var duplicates = 0;
        foreach (var file in expanded)
        {
            if (AddToList(file))
            {
                added++;
            }
            else
            {
                duplicates++;
            }
        }

        Renumber();
        UpdateOutputPreview();
        UpdateStartState();

        var message = $"已添加 {added} 个文件";
        if (duplicates > 0)
        {
            message += $"，{duplicates} 个已在列表中";
        }

        if (truncated > 0)
        {
            message += $"，已达到单次导入上限 {AudioFileScanner.MaxFilesPerImport} 个";
        }

        message += $"；列表共 {lvFiles.Items.Count} 个。";
        AppendStatus(message);
    }

    private bool AddToList(string path)
    {
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

        if (!_paths.Add(fullPath))
        {
            return false;
        }

        var item = new ListViewItem(new[]
        {
            string.Empty,
            Path.GetFileName(fullPath),
            Path.GetDirectoryName(fullPath) ?? string.Empty,
            "待处理",
        })
        {
            Tag = fullPath,
        };

        lvFiles.Items.Add(item);
        return true;
    }

    private void RemoveSelected()
    {
        if (_running || lvFiles.SelectedItems.Count == 0)
        {
            return;
        }

        foreach (ListViewItem item in lvFiles.SelectedItems)
        {
            if (item.Tag is string path)
            {
                _paths.Remove(path);
            }

            lvFiles.Items.Remove(item);
        }

        Renumber();
        UpdateOutputPreview();
        UpdateStartState();
    }

    private void ClearFiles()
    {
        if (_running || lvFiles.Items.Count == 0)
        {
            return;
        }

        lvFiles.Items.Clear();
        _paths.Clear();
        Renumber();
        UpdateOutputPreview();
        UpdateStartState();
    }

    private void Renumber()
    {
        for (var index = 0; index < lvFiles.Items.Count; index++)
        {
            lvFiles.Items[index].SubItems[0].Text = (index + 1).ToString();
        }
    }

    private void UpdateStartState()
    {
        btnStart.Enabled = !_running && lvFiles.Items.Count > 0;
        btnRemoveSelected.Enabled = !_running && lvFiles.SelectedItems.Count > 0;
        btnClear.Enabled = !_running && lvFiles.Items.Count > 0;
        btnAddFiles.Enabled = !_running;
        btnAddFolder.Enabled = !_running;
        chkRecursive.Enabled = !_running;
        addFilesMenuItem.Enabled = !_running;
        addFolderMenuItem.Enabled = !_running;
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
        {
            e.Effect = DragDropEffects.Copy;
        }
    }

    private void OnDragDrop(object? sender, DragEventArgs e)
    {
        if (_running)
        {
            return;
        }

        if (e.Data?.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } paths)
        {
            AddPaths(paths);
        }
    }

    private void OnSplitModeChanged(object? sender, EventArgs e)
    {
        var byDuration = rbByDuration.Checked;
        numSeconds.Enabled = byDuration;
        numCount.Enabled = !byDuration;
        UpdateOutputPreview();
    }

    private void OnFormatChanged(object? sender, EventArgs e) => UpdateOutputPreview();

    private void UpdateOutputPreview()
    {
        var count = lvFiles.Items.Count;
        lblOutput.Text = count == 0
            ? "输出位置：每个源文件所在目录下的同名文件夹"
            : $"输出位置：每个源文件所在目录下的同名文件夹（已选 {count} 个文件）";

        var modeText = rbByDuration.Checked
            ? $"每个文件按 {numSeconds.Value:0} 秒分段"
            : $"每个文件均分为 {numCount.Value:0} 段";

        lblInfo.Text = $"{modeText}，输出 {SelectedFormat.DisplayName}。实际片段数取决于各文件时长。";
    }

    private AudioFormat SelectedFormat =>
        cmbFormat.SelectedItem as AudioFormat ?? AudioFormat.All[0];

    /// <summary>开始批量前的一次性检查：组件缺失时只提示一次，不再逐文件失败。</summary>
    private bool EnsureFfmpegAvailable()
    {
        try
        {
            _ = new FfmpegLocator().Resolve();
            return true;
        }
        catch (FfmpegNotFoundException ex)
        {
            AppendStatus("未找到 FFmpeg 组件，已取消本次处理。");
            MessageBox.Show(this, ex.Message, "缺少音频组件", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private async void OnStartClicked(object? sender, EventArgs e)
    {
        if (_running)
        {
            return;
        }

        var sources = lvFiles.Items
            .Cast<ListViewItem>()
            .Select(item => item.Tag as string)
            .Where(path => !string.IsNullOrEmpty(path))
            .Select(path => path!)
            .ToList();

        if (sources.Count == 0)
        {
            MessageBox.Show(this, "请先添加要处理的音频文件。", "缺少文件", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // 开始前先确认音频组件可用，避免每个文件都失败一遍。
        if (!EnsureFfmpegAvailable())
        {
            return;
        }

        var policy = BatchConflictPolicy.CreateNumberedFolder;
        var existing = sources
            .Select(ExistingOutputDirectory)
            .Where(path => path is not null)
            .Select(path => path!)
            .ToList();

        if (existing.Count > 0)
        {
            var chosen = AskBatchConflictPolicy(existing);
            if (chosen is null)
            {
                AppendStatus("已取消：目标文件夹已存在。");
                return;
            }

            policy = chosen.Value;
        }

        var template = new SplitRequest
        {
            SourcePath = sources[0],
            Mode = rbByDuration.Checked ? SplitMode.ByDuration : SplitMode.ByCount,
            SegmentLength = TimeSpan.FromSeconds((double)numSeconds.Value),
            SegmentCount = (int)numCount.Value,
            Format = SelectedFormat,
            ConflictPolicy = OutputConflictPolicy.Cancel,
        };

        _running = true;
        _cancellation = new CancellationTokenSource();
        SetBusy(true);
        progressBar1.Value = 0;

        foreach (ListViewItem item in lvFiles.Items)
        {
            item.SubItems[3].Text = "等待中";
            item.ForeColor = SystemColors.ControlText;
        }

        AppendStatus($"开始批量处理 {sources.Count} 个文件…");

        var progress = new Progress<BatchProgress>(OnBatchProgress);
        var completed = new Progress<BatchFileResult>(OnFileCompleted);

        try
        {
            var result = await _batchService
                .RunAsync(sources, template, policy, progress, completed, _cancellation.Token)
                .ConfigureAwait(true);

            ShowBatchSummary(result);
        }
        finally
        {
            _cancellation.Dispose();
            _cancellation = null;
            _running = false;
            SetBusy(false);
            UpdateStartState();
            UpdateOutputPreview();
        }
    }

    private void OnBatchProgress(BatchProgress progress)
    {
        progressBar1.Value = Math.Clamp((int)Math.Round(progress.Fraction * progressBar1.Maximum), 0, progressBar1.Maximum);
        lblOutput.Text = progress.Status;
    }

    private void OnFileCompleted(BatchFileResult result)
    {
        var item = lvFiles.Items
            .Cast<ListViewItem>()
            .FirstOrDefault(candidate => string.Equals(candidate.Tag as string, result.SourcePath, StringComparison.OrdinalIgnoreCase));

        if (item is null)
        {
            return;
        }

        item.SubItems[3].Text = result.StatusText;
        item.ForeColor = result.Success
            ? Color.ForestGreen
            : result.Skipped ? SystemColors.GrayText : Color.Firebrick;

        var detail = result.Success
            ? $"完成：{Path.GetFileName(result.SourcePath)} → {result.OutputDirectory}（{result.FileCount} 段）"
            : result.Skipped
                ? $"跳过：{Path.GetFileName(result.SourcePath)}（目标文件夹已存在）"
                : $"失败：{Path.GetFileName(result.SourcePath)} — {result.Message}";

        AppendStatus(detail);
    }

    private void ShowBatchSummary(BatchResult result)
    {
        var summary = $"成功 {result.SucceededCount} 个，失败 {result.FailedCount} 个，跳过 {result.SkippedCount} 个。";
        if (result.Canceled)
        {
            summary = "已取消。" + Environment.NewLine + summary;
        }

        progressBar1.Value = result.Canceled ? 0 : progressBar1.Maximum;
        AppendStatus(summary);

        if (result.FailedCount > 0)
        {
            MessageBox.Show(
                this,
                summary + Environment.NewLine + Environment.NewLine +
                "失败项可查看下方日志了解原因。未完成的片段不会保留。",
                "批量处理结束",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
        else
        {
            MessageBox.Show(this, summary, "批量处理结束", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private BatchConflictPolicy? AskBatchConflictPolicy(IReadOnlyList<string> existingDirectories)
    {
        var preview = string.Join(Environment.NewLine, existingDirectories.Take(5).Select(path => "  " + path));
        if (existingDirectories.Count > 5)
        {
            preview += Environment.NewLine + $"  …另有 {existingDirectories.Count - 5} 个";
        }

        var text =
            $"有 {existingDirectories.Count} 个目标文件夹已存在：" + Environment.NewLine +
            preview + Environment.NewLine + Environment.NewLine +
            "是：覆盖本次会生成的同名文件（其他文件不受影响）" + Environment.NewLine +
            "否：全部改用编号目录，例如“lesson (1)”" + Environment.NewLine +
            "取消：不做任何修改";

        var answer = MessageBox.Show(
            this, text, "目标文件夹已存在",
            MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);

        return answer switch
        {
            DialogResult.Yes => BatchConflictPolicy.OverwriteExisting,
            DialogResult.No => BatchConflictPolicy.CreateNumberedFolder,
            _ => null,
        };
    }

    private static string? ExistingOutputDirectory(string sourcePath)
    {
        try
        {
            var directory = OutputLayout.GetDefaultOutputDirectory(sourcePath);
            return Directory.Exists(directory) ? directory : null;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    private void OnCancelClicked(object? sender, EventArgs e)
    {
        if (_cancellation is null || _cancellation.IsCancellationRequested)
        {
            return;
        }

        AppendStatus("正在取消，等待当前文件停止…");
        _cancellation.Cancel();
    }

    private void SetBusy(bool busy)
    {
        _running = busy;
        btnStart.Enabled = !busy && lvFiles.Items.Count > 0;
        btnCancel.Enabled = busy;
        lvFiles.Enabled = !busy;
        rbByDuration.Enabled = !busy;
        rbByCount.Enabled = !busy;
        numSeconds.Enabled = !busy && rbByDuration.Checked;
        numCount.Enabled = !busy && rbByCount.Checked;
        cmbFormat.Enabled = !busy;
        UseWaitCursor = busy;
        UpdateStartState();
    }

    private void ShowInstallDialog()
    {
        using var form = new InstallForm();
        form.ShowDialog(this);
    }

    private async Task ApplyUpgradeAsync()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "选择新版本的 AudioCropper.exe",
            Filter = "可执行文件|*.exe",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        if (_running)
        {
            return;
        }

        SetBusy(true);
        try
        {
            var result = await new UpgradeService()
                .ApplyUpdateAsync(dialog.FileName, expectedSha256: null)
                .ConfigureAwait(true);

            var message = result.Message;
            if (result.Warnings.Count > 0)
            {
                message += Environment.NewLine + string.Join(Environment.NewLine, result.Warnings);
            }

            MessageBox.Show(
                this, message, "升级",
                MessageBoxButtons.OK,
                result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
        finally
        {
            SetBusy(false);
            UpdateStartState();
        }
    }

    private void ShowAbout()
    {
        var notice = AppEnvironment.GetArchitectureNotice();
        var text =
            $"{AppPaths.ProductName} {AppEnvironment.VersionText}" + Environment.NewLine +
            $"{AppEnvironment.Describe()}" + Environment.NewLine + Environment.NewLine +
            "轻量级离线音频分割工具，支持多级目录批量处理。所有处理都在本机完成，不上传任何文件。" +
            (notice is null ? string.Empty : Environment.NewLine + Environment.NewLine + notice);

        MessageBox.Show(this, text, "关于", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void OpenDirectory(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            // 用字符串参数而不是 ArgumentList：UseShellExecute=true 时不支持参数列表。
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{directory}\"") { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            MessageBox.Show(this, $"无法打开目录：{directory}" + Environment.NewLine + ex.Message,
                "打开目录失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void AppendStatus(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        txtStatus.AppendText(line + Environment.NewLine);
        AppLogger.Info(message);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_cancellation is not null)
        {
            var answer = MessageBox.Show(
                this, "正在处理音频，确定要退出吗？未完成的片段不会保留。", "确认退出",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);

            if (answer != DialogResult.Yes)
            {
                e.Cancel = true;
                return;
            }

            _cancellation.Cancel();
        }

        base.OnFormClosing(e);
    }
}
