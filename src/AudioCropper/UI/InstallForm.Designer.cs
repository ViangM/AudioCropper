#nullable enable

namespace AudioCropper.UI;

partial class InstallForm
{
    private System.ComponentModel.IContainer? components = null;

    private Label lblIntro = null!;
    private Label lblDir = null!;
    private TextBox txtDir = null!;
    private Button btnBrowse = null!;
    private CheckBox chkDesktop = null!;
    private CheckBox chkStartMenu = null!;
    private Label lblStatus = null!;
    private ProgressBar progressBar1 = null!;
    private Button btnInstall = null!;
    private Button btnClose = null!;

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
        lblIntro = new Label();
        lblDir = new Label();
        txtDir = new TextBox();
        btnBrowse = new Button();
        chkDesktop = new CheckBox();
        chkStartMenu = new CheckBox();
        lblStatus = new Label();
        progressBar1 = new ProgressBar();
        btnInstall = new Button();
        btnClose = new Button();
        SuspendLayout();
        //
        // lblIntro
        //
        lblIntro.Location = new Point(14, 14);
        lblIntro.Size = new Size(530, 38);
        lblIntro.Text = "将 AudioCropper 安装到当前用户，无需管理员权限。安装后可创建快捷方式，" +
                        "并可在“应用和功能”中卸载。";
        //
        // lblDir
        //
        lblDir.AutoSize = true;
        lblDir.Location = new Point(14, 62);
        lblDir.Text = "安装目录";
        //
        // txtDir
        //
        txtDir.Location = new Point(14, 82);
        txtDir.Name = "txtDir";
        txtDir.Size = new Size(420, 23);
        txtDir.TabIndex = 0;
        //
        // btnBrowse
        //
        btnBrowse.Location = new Point(444, 81);
        btnBrowse.Name = "btnBrowse";
        btnBrowse.Size = new Size(100, 25);
        btnBrowse.TabIndex = 1;
        btnBrowse.Text = "浏览…";
        btnBrowse.UseVisualStyleBackColor = true;
        //
        // chkDesktop
        //
        chkDesktop.AutoSize = true;
        chkDesktop.Checked = true;
        chkDesktop.CheckState = CheckState.Checked;
        chkDesktop.Location = new Point(14, 118);
        chkDesktop.Text = "创建桌面快捷方式";
        //
        // chkStartMenu
        //
        chkStartMenu.AutoSize = true;
        chkStartMenu.Checked = true;
        chkStartMenu.CheckState = CheckState.Checked;
        chkStartMenu.Location = new Point(14, 144);
        chkStartMenu.Text = "创建开始菜单快捷方式";
        //
        // lblStatus
        //
        lblStatus.AutoEllipsis = true;
        lblStatus.Location = new Point(14, 178);
        lblStatus.Name = "lblStatus";
        lblStatus.Size = new Size(530, 20);
        lblStatus.Text = "";
        //
        // progressBar1
        //
        progressBar1.Location = new Point(14, 202);
        progressBar1.Name = "progressBar1";
        progressBar1.Size = new Size(530, 16);
        //
        // btnInstall
        //
        btnInstall.Location = new Point(330, 230);
        btnInstall.Name = "btnInstall";
        btnInstall.Size = new Size(110, 32);
        btnInstall.TabIndex = 2;
        btnInstall.Text = "安装";
        btnInstall.UseVisualStyleBackColor = true;
        //
        // btnClose
        //
        btnClose.Location = new Point(444, 230);
        btnClose.Name = "btnClose";
        btnClose.Size = new Size(100, 32);
        btnClose.TabIndex = 3;
        btnClose.Text = "关闭";
        btnClose.UseVisualStyleBackColor = true;
        //
        // InstallForm
        //
        AcceptButton = btnInstall;
        CancelButton = btnClose;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(558, 278);
        Controls.Add(lblIntro);
        Controls.Add(lblDir);
        Controls.Add(txtDir);
        Controls.Add(btnBrowse);
        Controls.Add(chkDesktop);
        Controls.Add(chkStartMenu);
        Controls.Add(lblStatus);
        Controls.Add(progressBar1);
        Controls.Add(btnInstall);
        Controls.Add(btnClose);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "InstallForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = "安装 AudioCropper";
        ResumeLayout(false);
        PerformLayout();
    }
}
