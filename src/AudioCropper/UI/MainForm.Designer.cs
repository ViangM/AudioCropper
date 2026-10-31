#nullable enable

namespace AudioCropper.UI;

partial class MainForm
{
    private System.ComponentModel.IContainer? components = null;

    private MenuStrip menuStrip1 = null!;
    private ToolStripMenuItem fileMenuItem = null!;
    private ToolStripMenuItem addFilesMenuItem = null!;
    private ToolStripMenuItem addFolderMenuItem = null!;
    private ToolStripMenuItem exitMenuItem = null!;
    private ToolStripMenuItem toolsMenuItem = null!;
    private ToolStripMenuItem installMenuItem = null!;
    private ToolStripMenuItem upgradeMenuItem = null!;
    private ToolStripMenuItem openLogsMenuItem = null!;
    private ToolStripMenuItem helpMenuItem = null!;
    private ToolStripMenuItem aboutMenuItem = null!;

    private Button btnAddFiles = null!;
    private Button btnAddFolder = null!;
    private CheckBox chkRecursive = null!;
    private Button btnRemoveSelected = null!;
    private Button btnClear = null!;
    private ListView lvFiles = null!;
    private ColumnHeader colIndex = null!;
    private ColumnHeader colFile = null!;
    private ColumnHeader colDirectory = null!;
    private ColumnHeader colStatus = null!;
    private GroupBox grpMode = null!;
    private RadioButton rbByDuration = null!;
    private NumericUpDown numSeconds = null!;
    private Label lblSeconds = null!;
    private RadioButton rbByCount = null!;
    private NumericUpDown numCount = null!;
    private Label lblCount = null!;
    private Label lblFormat = null!;
    private ComboBox cmbFormat = null!;
    private Label lblInfo = null!;
    private Label lblOutput = null!;
    private Button btnStart = null!;
    private Button btnCancel = null!;
    private ProgressBar progressBar1 = null!;
    private TextBox txtStatus = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            components?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        menuStrip1 = new MenuStrip();
        fileMenuItem = new ToolStripMenuItem();
        addFilesMenuItem = new ToolStripMenuItem();
        addFolderMenuItem = new ToolStripMenuItem();
        exitMenuItem = new ToolStripMenuItem();
        toolsMenuItem = new ToolStripMenuItem();
        installMenuItem = new ToolStripMenuItem();
        upgradeMenuItem = new ToolStripMenuItem();
        openLogsMenuItem = new ToolStripMenuItem();
        helpMenuItem = new ToolStripMenuItem();
        aboutMenuItem = new ToolStripMenuItem();
        btnAddFiles = new Button();
        btnAddFolder = new Button();
        chkRecursive = new CheckBox();
        btnRemoveSelected = new Button();
        btnClear = new Button();
        lvFiles = new ListView();
        colIndex = new ColumnHeader();
        colFile = new ColumnHeader();
        colDirectory = new ColumnHeader();
        colStatus = new ColumnHeader();
        grpMode = new GroupBox();
        rbByDuration = new RadioButton();
        numSeconds = new NumericUpDown();
        lblSeconds = new Label();
        rbByCount = new RadioButton();
        numCount = new NumericUpDown();
        lblCount = new Label();
        lblFormat = new Label();
        cmbFormat = new ComboBox();
        lblInfo = new Label();
        lblOutput = new Label();
        btnStart = new Button();
        btnCancel = new Button();
        progressBar1 = new ProgressBar();
        txtStatus = new TextBox();
        menuStrip1.SuspendLayout();
        grpMode.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)numSeconds).BeginInit();
        ((System.ComponentModel.ISupportInitialize)numCount).BeginInit();
        SuspendLayout();
        //
        // menuStrip1
        //
        menuStrip1.Items.AddRange(new ToolStripItem[] { fileMenuItem, toolsMenuItem, helpMenuItem });
        menuStrip1.Location = new Point(0, 0);
        menuStrip1.Name = "menuStrip1";
        menuStrip1.Size = new Size(860, 24);
        menuStrip1.TabIndex = 0;
        //
        // fileMenuItem
        //
        fileMenuItem.DropDownItems.AddRange(new ToolStripItem[] { addFilesMenuItem, addFolderMenuItem, exitMenuItem });
        fileMenuItem.Text = "文件(&F)";
        //
        // addFilesMenuItem
        //
        addFilesMenuItem.Text = "添加文件…";
        addFilesMenuItem.ShortcutKeys = Keys.Control | Keys.O;
        //
        // addFolderMenuItem
        //
        addFolderMenuItem.Text = "添加文件夹…";
        addFolderMenuItem.ShortcutKeys = Keys.Control | Keys.Shift | Keys.O;
        //
        // exitMenuItem
        //
        exitMenuItem.Text = "退出";
        //
        // toolsMenuItem
        //
        toolsMenuItem.DropDownItems.AddRange(new ToolStripItem[] { installMenuItem, upgradeMenuItem, openLogsMenuItem });
        toolsMenuItem.Text = "工具(&T)";
        //
        // installMenuItem
        //
        installMenuItem.Text = "安装到本机…";
        //
        // upgradeMenuItem
        //
        upgradeMenuItem.Text = "从新版本 EXE 升级…";
        //
        // openLogsMenuItem
        //
        openLogsMenuItem.Text = "打开日志目录";
        //
        // helpMenuItem
        //
        helpMenuItem.DropDownItems.AddRange(new ToolStripItem[] { aboutMenuItem });
        helpMenuItem.Text = "帮助(&H)";
        //
        // aboutMenuItem
        //
        aboutMenuItem.Text = "关于 AudioCropper";
        //
        // btnAddFiles
        //
        btnAddFiles.Location = new Point(14, 32);
        btnAddFiles.Name = "btnAddFiles";
        btnAddFiles.Size = new Size(110, 27);
        btnAddFiles.TabIndex = 1;
        btnAddFiles.Text = "添加文件…";
        btnAddFiles.UseVisualStyleBackColor = true;
        //
        // btnAddFolder
        //
        btnAddFolder.Location = new Point(132, 32);
        btnAddFolder.Name = "btnAddFolder";
        btnAddFolder.Size = new Size(124, 27);
        btnAddFolder.TabIndex = 2;
        btnAddFolder.Text = "添加文件夹…";
        btnAddFolder.UseVisualStyleBackColor = true;
        //
        // chkRecursive
        //
        chkRecursive.AutoSize = true;
        chkRecursive.Checked = true;
        chkRecursive.CheckState = CheckState.Checked;
        chkRecursive.Location = new Point(266, 37);
        chkRecursive.Name = "chkRecursive";
        chkRecursive.Text = "包含子文件夹";
        //
        // btnRemoveSelected
        //
        btnRemoveSelected.Location = new Point(392, 32);
        btnRemoveSelected.Name = "btnRemoveSelected";
        btnRemoveSelected.Size = new Size(100, 27);
        btnRemoveSelected.TabIndex = 4;
        btnRemoveSelected.Text = "移除所选";
        btnRemoveSelected.UseVisualStyleBackColor = true;
        //
        // btnClear
        //
        btnClear.Location = new Point(500, 32);
        btnClear.Name = "btnClear";
        btnClear.Size = new Size(80, 27);
        btnClear.TabIndex = 5;
        btnClear.Text = "清空";
        btnClear.UseVisualStyleBackColor = true;
        //
        // lvFiles
        //
        lvFiles.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        lvFiles.Columns.AddRange(new ColumnHeader[] { colIndex, colFile, colDirectory, colStatus });
        lvFiles.FullRowSelect = true;
        lvFiles.GridLines = true;
        lvFiles.Location = new Point(14, 68);
        lvFiles.Name = "lvFiles";
        lvFiles.Size = new Size(832, 220);
        lvFiles.TabIndex = 6;
        lvFiles.UseCompatibleStateImageBehavior = false;
        lvFiles.View = View.Details;
        //
        // colIndex
        //
        colIndex.Text = "#";
        colIndex.Width = 44;
        //
        // colFile
        //
        colFile.Text = "文件";
        colFile.Width = 250;
        //
        // colDirectory
        //
        colDirectory.Text = "所在目录";
        colDirectory.Width = 400;
        //
        // colStatus
        //
        colStatus.Text = "状态";
        colStatus.Width = 120;
        //
        // grpMode
        //
        grpMode.Controls.Add(rbByDuration);
        grpMode.Controls.Add(numSeconds);
        grpMode.Controls.Add(lblSeconds);
        grpMode.Controls.Add(rbByCount);
        grpMode.Controls.Add(numCount);
        grpMode.Controls.Add(lblCount);
        grpMode.Location = new Point(14, 300);
        grpMode.Name = "grpMode";
        grpMode.Size = new Size(330, 100);
        grpMode.TabIndex = 7;
        grpMode.TabStop = false;
        grpMode.Text = "分段方式（应用于全部文件）";
        //
        // rbByDuration
        //
        rbByDuration.AutoSize = true;
        rbByDuration.Checked = true;
        rbByDuration.Location = new Point(16, 26);
        rbByDuration.Text = "按分段时长";
        rbByDuration.CheckedChanged += OnSplitModeChanged;
        //
        // numSeconds
        //
        numSeconds.Location = new Point(170, 24);
        numSeconds.Maximum = 86400;
        numSeconds.Minimum = 1;
        numSeconds.Name = "numSeconds";
        numSeconds.Size = new Size(90, 23);
        numSeconds.TabIndex = 1;
        numSeconds.TextAlign = HorizontalAlignment.Right;
        numSeconds.Value = 60;
        //
        // lblSeconds
        //
        lblSeconds.AutoSize = true;
        lblSeconds.Location = new Point(268, 28);
        lblSeconds.Text = "秒";
        //
        // rbByCount
        //
        rbByCount.AutoSize = true;
        rbByCount.Location = new Point(16, 62);
        rbByCount.Text = "按片段数量";
        rbByCount.CheckedChanged += OnSplitModeChanged;
        //
        // numCount
        //
        numCount.Enabled = false;
        numCount.Location = new Point(170, 60);
        numCount.Maximum = 1000;
        numCount.Minimum = 1;
        numCount.Name = "numCount";
        numCount.Size = new Size(90, 23);
        numCount.TabIndex = 3;
        numCount.TextAlign = HorizontalAlignment.Right;
        numCount.Value = 3;
        //
        // lblCount
        //
        lblCount.AutoSize = true;
        lblCount.Location = new Point(268, 64);
        lblCount.Text = "个";
        //
        // lblFormat
        //
        lblFormat.AutoSize = true;
        lblFormat.Location = new Point(364, 306);
        lblFormat.Text = "输出格式";
        //
        // cmbFormat
        //
        cmbFormat.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbFormat.Location = new Point(364, 326);
        cmbFormat.Name = "cmbFormat";
        cmbFormat.Size = new Size(200, 23);
        cmbFormat.TabIndex = 8;
        cmbFormat.SelectedIndexChanged += OnFormatChanged;
        //
        // lblInfo
        //
        lblInfo.Location = new Point(364, 358);
        lblInfo.Name = "lblInfo";
        lblInfo.Size = new Size(482, 34);
        lblInfo.ForeColor = SystemColors.GrayText;
        lblInfo.Text = "";
        //
        // lblOutput
        //
        lblOutput.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        lblOutput.AutoEllipsis = true;
        lblOutput.Location = new Point(14, 412);
        lblOutput.Name = "lblOutput";
        lblOutput.Size = new Size(832, 20);
        lblOutput.Text = "输出位置：每个源文件所在目录下的同名文件夹";
        //
        // btnStart
        //
        btnStart.Enabled = false;
        btnStart.Location = new Point(14, 440);
        btnStart.Name = "btnStart";
        btnStart.Size = new Size(130, 34);
        btnStart.TabIndex = 9;
        btnStart.Text = "开始分割";
        btnStart.UseVisualStyleBackColor = true;
        btnStart.Click += OnStartClicked;
        //
        // btnCancel
        //
        btnCancel.Enabled = false;
        btnCancel.Location = new Point(154, 440);
        btnCancel.Name = "btnCancel";
        btnCancel.Size = new Size(110, 34);
        btnCancel.TabIndex = 10;
        btnCancel.Text = "取消";
        btnCancel.UseVisualStyleBackColor = true;
        btnCancel.Click += OnCancelClicked;
        //
        // progressBar1
        //
        progressBar1.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        progressBar1.Location = new Point(278, 448);
        progressBar1.Maximum = 1000;
        progressBar1.Name = "progressBar1";
        progressBar1.Size = new Size(568, 18);
        progressBar1.TabIndex = 11;
        //
        // txtStatus
        //
        txtStatus.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        txtStatus.BackColor = SystemColors.Window;
        txtStatus.Font = new Font("Consolas", 9F);
        txtStatus.Location = new Point(14, 488);
        txtStatus.Multiline = true;
        txtStatus.Name = "txtStatus";
        txtStatus.ReadOnly = true;
        txtStatus.ScrollBars = ScrollBars.Vertical;
        txtStatus.Size = new Size(832, 178);
        txtStatus.TabIndex = 12;
        //
        // MainForm
        //
        AllowDrop = true;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(860, 680);
        Controls.Add(menuStrip1);
        Controls.Add(btnAddFiles);
        Controls.Add(btnAddFolder);
        Controls.Add(chkRecursive);
        Controls.Add(btnRemoveSelected);
        Controls.Add(btnClear);
        Controls.Add(lvFiles);
        Controls.Add(grpMode);
        Controls.Add(lblFormat);
        Controls.Add(cmbFormat);
        Controls.Add(lblInfo);
        Controls.Add(lblOutput);
        Controls.Add(btnStart);
        Controls.Add(btnCancel);
        Controls.Add(progressBar1);
        Controls.Add(txtStatus);
        MainMenuStrip = menuStrip1;
        MinimumSize = new Size(900, 620);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "AudioCropper — 离线音频分割";
        menuStrip1.ResumeLayout(false);
        menuStrip1.PerformLayout();
        grpMode.ResumeLayout(false);
        grpMode.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)numSeconds).EndInit();
        ((System.ComponentModel.ISupportInitialize)numCount).EndInit();
        ResumeLayout(false);
        PerformLayout();
    }
}
