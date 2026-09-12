namespace shuangkai
{
    partial class Home
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Home));
            this.headerPanel = new System.Windows.Forms.Panel();
            this.versionLabel = new System.Windows.Forms.Label();
            this.subtitleLabel = new System.Windows.Forms.Label();
            this.titleLabel = new System.Windows.Forms.Label();
            this.wechatPanel = new System.Windows.Forms.Panel();
            this.wechatStartButton = new System.Windows.Forms.Button();
            this.wechatPathLabel = new System.Windows.Forms.Label();
            this.wechatCountLabel = new System.Windows.Forms.Label();
            this.wechatNameLabel = new System.Windows.Forms.Label();
            this.wechatIcon = new System.Windows.Forms.PictureBox();
            this.wecomPanel = new System.Windows.Forms.Panel();
            this.wecomStartButton = new System.Windows.Forms.Button();
            this.wecomPathLabel = new System.Windows.Forms.Label();
            this.wecomCountLabel = new System.Windows.Forms.Label();
            this.wecomNameLabel = new System.Windows.Forms.Label();
            this.wecomIcon = new System.Windows.Forms.PictureBox();
            this.quantityPanel = new System.Windows.Forms.Panel();
            this.targetHintLabel = new System.Windows.Forms.Label();
            this.targetCountInput = new System.Windows.Forms.NumericUpDown();
            this.targetLabel = new System.Windows.Forms.Label();
            this.footerPanel = new System.Windows.Forms.Panel();
            this.sourceLink = new System.Windows.Forms.LinkLabel();
            this.statusLabel = new System.Windows.Forms.Label();
            this.statusTimer = new System.Windows.Forms.Timer(this.components);
            this.pathToolTip = new System.Windows.Forms.ToolTip(this.components);
            this.headerPanel.SuspendLayout();
            this.wechatPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.wechatIcon)).BeginInit();
            this.wecomPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.wecomIcon)).BeginInit();
            this.quantityPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.targetCountInput)).BeginInit();
            this.footerPanel.SuspendLayout();
            this.SuspendLayout();
            //
            // headerPanel
            //
            this.headerPanel.BackColor = System.Drawing.Color.FromArgb(35, 43, 55);
            this.headerPanel.Controls.Add(this.versionLabel);
            this.headerPanel.Controls.Add(this.subtitleLabel);
            this.headerPanel.Controls.Add(this.titleLabel);
            this.headerPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.headerPanel.Location = new System.Drawing.Point(0, 0);
            this.headerPanel.Name = "headerPanel";
            this.headerPanel.Size = new System.Drawing.Size(664, 68);
            this.headerPanel.TabIndex = 0;
            //
            // versionLabel
            //
            this.versionLabel.BackColor = System.Drawing.Color.FromArgb(65, 78, 96);
            this.versionLabel.Font = new System.Drawing.Font("微软雅黑", 8F);
            this.versionLabel.ForeColor = System.Drawing.Color.White;
            this.versionLabel.Location = new System.Drawing.Point(574, 20);
            this.versionLabel.Name = "versionLabel";
            this.versionLabel.Size = new System.Drawing.Size(70, 26);
            this.versionLabel.TabIndex = 2;
            this.versionLabel.Text = "V1.0.0";
            this.versionLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // subtitleLabel
            //
            this.subtitleLabel.AutoSize = true;
            this.subtitleLabel.Font = new System.Drawing.Font("微软雅黑", 8.5F);
            this.subtitleLabel.ForeColor = System.Drawing.Color.FromArgb(185, 195, 208);
            this.subtitleLabel.Location = new System.Drawing.Point(22, 40);
            this.subtitleLabel.Name = "subtitleLabel";
            this.subtitleLabel.Size = new System.Drawing.Size(233, 17);
            this.subtitleLabel.TabIndex = 1;
            this.subtitleLabel.Text = "智能检测当前实例，只补开缺少的窗口";
            //
            // titleLabel
            //
            this.titleLabel.AutoSize = true;
            this.titleLabel.Font = new System.Drawing.Font("微软雅黑", 15F, System.Drawing.FontStyle.Bold);
            this.titleLabel.ForeColor = System.Drawing.Color.White;
            this.titleLabel.Location = new System.Drawing.Point(20, 10);
            this.titleLabel.Name = "titleLabel";
            this.titleLabel.Size = new System.Drawing.Size(252, 27);
            this.titleLabel.TabIndex = 0;
            this.titleLabel.Text = "微信 · 企业微信多开助手";
            //
            // wechatPanel
            //
            this.wechatPanel.BackColor = System.Drawing.Color.White;
            this.wechatPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.wechatPanel.Controls.Add(this.wechatStartButton);
            this.wechatPanel.Controls.Add(this.wechatPathLabel);
            this.wechatPanel.Controls.Add(this.wechatCountLabel);
            this.wechatPanel.Controls.Add(this.wechatNameLabel);
            this.wechatPanel.Controls.Add(this.wechatIcon);
            this.wechatPanel.Location = new System.Drawing.Point(18, 84);
            this.wechatPanel.Name = "wechatPanel";
            this.wechatPanel.Size = new System.Drawing.Size(444, 94);
            this.wechatPanel.TabIndex = 1;
            //
            // wechatStartButton
            //
            this.wechatStartButton.BackColor = System.Drawing.Color.FromArgb(42, 179, 94);
            this.wechatStartButton.Cursor = System.Windows.Forms.Cursors.Hand;
            this.wechatStartButton.FlatAppearance.BorderSize = 0;
            this.wechatStartButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.wechatStartButton.Font = new System.Drawing.Font("微软雅黑", 9F, System.Drawing.FontStyle.Bold);
            this.wechatStartButton.ForeColor = System.Drawing.Color.White;
            this.wechatStartButton.Location = new System.Drawing.Point(341, 25);
            this.wechatStartButton.Name = "wechatStartButton";
            this.wechatStartButton.Size = new System.Drawing.Size(86, 42);
            this.wechatStartButton.TabIndex = 4;
            this.wechatStartButton.Text = "启动 / 补开";
            this.wechatStartButton.UseVisualStyleBackColor = false;
            this.wechatStartButton.Click += new System.EventHandler(this.wechatStartButton_Click);
            //
            // wechatPathLabel
            //
            this.wechatPathLabel.AutoEllipsis = true;
            this.wechatPathLabel.Font = new System.Drawing.Font("微软雅黑", 8F);
            this.wechatPathLabel.ForeColor = System.Drawing.Color.FromArgb(132, 139, 147);
            this.wechatPathLabel.Location = new System.Drawing.Point(82, 64);
            this.wechatPathLabel.Name = "wechatPathLabel";
            this.wechatPathLabel.Size = new System.Drawing.Size(250, 18);
            this.wechatPathLabel.TabIndex = 3;
            this.wechatPathLabel.Text = "正在检测安装路径…";
            //
            // wechatCountLabel
            //
            this.wechatCountLabel.AutoSize = true;
            this.wechatCountLabel.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.wechatCountLabel.ForeColor = System.Drawing.Color.FromArgb(105, 113, 122);
            this.wechatCountLabel.Location = new System.Drawing.Point(82, 40);
            this.wechatCountLabel.Name = "wechatCountLabel";
            this.wechatCountLabel.Size = new System.Drawing.Size(80, 17);
            this.wechatCountLabel.TabIndex = 2;
            this.wechatCountLabel.Text = "当前未运行";
            //
            // wechatNameLabel
            //
            this.wechatNameLabel.AutoSize = true;
            this.wechatNameLabel.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Bold);
            this.wechatNameLabel.ForeColor = System.Drawing.Color.FromArgb(38, 45, 54);
            this.wechatNameLabel.Location = new System.Drawing.Point(80, 12);
            this.wechatNameLabel.Name = "wechatNameLabel";
            this.wechatNameLabel.Size = new System.Drawing.Size(42, 22);
            this.wechatNameLabel.TabIndex = 1;
            this.wechatNameLabel.Text = "微信";
            //
            // wechatIcon
            //
            this.wechatIcon.Cursor = System.Windows.Forms.Cursors.Hand;
            this.wechatIcon.Image = global::duokai.Properties.Resources.WeChat;
            this.wechatIcon.Location = new System.Drawing.Point(13, 16);
            this.wechatIcon.Name = "wechatIcon";
            this.wechatIcon.Size = new System.Drawing.Size(56, 56);
            this.wechatIcon.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.wechatIcon.TabIndex = 0;
            this.wechatIcon.TabStop = false;
            this.wechatIcon.Click += new System.EventHandler(this.wechatIcon_Click);
            //
            // wecomPanel
            //
            this.wecomPanel.BackColor = System.Drawing.Color.White;
            this.wecomPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.wecomPanel.Controls.Add(this.wecomStartButton);
            this.wecomPanel.Controls.Add(this.wecomPathLabel);
            this.wecomPanel.Controls.Add(this.wecomCountLabel);
            this.wecomPanel.Controls.Add(this.wecomNameLabel);
            this.wecomPanel.Controls.Add(this.wecomIcon);
            this.wecomPanel.Location = new System.Drawing.Point(18, 188);
            this.wecomPanel.Name = "wecomPanel";
            this.wecomPanel.Size = new System.Drawing.Size(444, 94);
            this.wecomPanel.TabIndex = 2;
            //
            // wecomStartButton
            //
            this.wecomStartButton.BackColor = System.Drawing.Color.FromArgb(45, 105, 197);
            this.wecomStartButton.Cursor = System.Windows.Forms.Cursors.Hand;
            this.wecomStartButton.FlatAppearance.BorderSize = 0;
            this.wecomStartButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.wecomStartButton.Font = new System.Drawing.Font("微软雅黑", 9F, System.Drawing.FontStyle.Bold);
            this.wecomStartButton.ForeColor = System.Drawing.Color.White;
            this.wecomStartButton.Location = new System.Drawing.Point(341, 25);
            this.wecomStartButton.Name = "wecomStartButton";
            this.wecomStartButton.Size = new System.Drawing.Size(86, 42);
            this.wecomStartButton.TabIndex = 4;
            this.wecomStartButton.Text = "启动 / 补开";
            this.wecomStartButton.UseVisualStyleBackColor = false;
            this.wecomStartButton.Click += new System.EventHandler(this.wecomStartButton_Click);
            //
            // wecomPathLabel
            //
            this.wecomPathLabel.AutoEllipsis = true;
            this.wecomPathLabel.Font = new System.Drawing.Font("微软雅黑", 8F);
            this.wecomPathLabel.ForeColor = System.Drawing.Color.FromArgb(132, 139, 147);
            this.wecomPathLabel.Location = new System.Drawing.Point(82, 64);
            this.wecomPathLabel.Name = "wecomPathLabel";
            this.wecomPathLabel.Size = new System.Drawing.Size(250, 18);
            this.wecomPathLabel.TabIndex = 3;
            this.wecomPathLabel.Text = "正在检测安装路径…";
            //
            // wecomCountLabel
            //
            this.wecomCountLabel.AutoSize = true;
            this.wecomCountLabel.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.wecomCountLabel.ForeColor = System.Drawing.Color.FromArgb(105, 113, 122);
            this.wecomCountLabel.Location = new System.Drawing.Point(82, 40);
            this.wecomCountLabel.Name = "wecomCountLabel";
            this.wecomCountLabel.Size = new System.Drawing.Size(80, 17);
            this.wecomCountLabel.TabIndex = 2;
            this.wecomCountLabel.Text = "当前未运行";
            //
            // wecomNameLabel
            //
            this.wecomNameLabel.AutoSize = true;
            this.wecomNameLabel.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Bold);
            this.wecomNameLabel.ForeColor = System.Drawing.Color.FromArgb(38, 45, 54);
            this.wecomNameLabel.Location = new System.Drawing.Point(80, 12);
            this.wecomNameLabel.Name = "wecomNameLabel";
            this.wecomNameLabel.Size = new System.Drawing.Size(74, 22);
            this.wecomNameLabel.TabIndex = 1;
            this.wecomNameLabel.Text = "企业微信";
            //
            // wecomIcon
            //
            this.wecomIcon.Cursor = System.Windows.Forms.Cursors.Hand;
            this.wecomIcon.Image = global::duokai.Properties.Resources.WXWork;
            this.wecomIcon.Location = new System.Drawing.Point(13, 16);
            this.wecomIcon.Name = "wecomIcon";
            this.wecomIcon.Size = new System.Drawing.Size(56, 56);
            this.wecomIcon.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.wecomIcon.TabIndex = 0;
            this.wecomIcon.TabStop = false;
            this.wecomIcon.Click += new System.EventHandler(this.wecomIcon_Click);
            //
            // quantityPanel
            //
            this.quantityPanel.BackColor = System.Drawing.Color.White;
            this.quantityPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.quantityPanel.Controls.Add(this.targetHintLabel);
            this.quantityPanel.Controls.Add(this.targetCountInput);
            this.quantityPanel.Controls.Add(this.targetLabel);
            this.quantityPanel.Location = new System.Drawing.Point(478, 84);
            this.quantityPanel.Name = "quantityPanel";
            this.quantityPanel.Size = new System.Drawing.Size(168, 198);
            this.quantityPanel.TabIndex = 3;
            //
            // targetHintLabel
            //
            this.targetHintLabel.Font = new System.Drawing.Font("微软雅黑", 8.5F);
            this.targetHintLabel.ForeColor = System.Drawing.Color.FromArgb(117, 125, 134);
            this.targetHintLabel.Location = new System.Drawing.Point(17, 126);
            this.targetHintLabel.Name = "targetHintLabel";
            this.targetHintLabel.Size = new System.Drawing.Size(132, 54);
            this.targetHintLabel.TabIndex = 2;
            this.targetHintLabel.Text = "会自动记住设置。再次点击时，只补足已关闭的实例。";
            this.targetHintLabel.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            //
            // targetCountInput
            //
            this.targetCountInput.Font = new System.Drawing.Font("微软雅黑", 26F, System.Drawing.FontStyle.Bold);
            this.targetCountInput.Location = new System.Drawing.Point(20, 58);
            this.targetCountInput.Maximum = new decimal(new int[] { 10, 0, 0, 0 });
            this.targetCountInput.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.targetCountInput.Name = "targetCountInput";
            this.targetCountInput.Size = new System.Drawing.Size(126, 53);
            this.targetCountInput.TabIndex = 1;
            this.targetCountInput.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.targetCountInput.Value = new decimal(new int[] { 2, 0, 0, 0 });
            this.targetCountInput.ValueChanged += new System.EventHandler(this.targetCountInput_ValueChanged);
            //
            // targetLabel
            //
            this.targetLabel.Font = new System.Drawing.Font("微软雅黑", 11F, System.Drawing.FontStyle.Bold);
            this.targetLabel.ForeColor = System.Drawing.Color.FromArgb(55, 63, 73);
            this.targetLabel.Location = new System.Drawing.Point(18, 22);
            this.targetLabel.Name = "targetLabel";
            this.targetLabel.Size = new System.Drawing.Size(130, 24);
            this.targetLabel.TabIndex = 0;
            this.targetLabel.Text = "双开数量";
            this.targetLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // footerPanel
            //
            this.footerPanel.BackColor = System.Drawing.Color.FromArgb(244, 246, 248);
            this.footerPanel.Controls.Add(this.sourceLink);
            this.footerPanel.Controls.Add(this.statusLabel);
            this.footerPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.footerPanel.Location = new System.Drawing.Point(0, 298);
            this.footerPanel.Name = "footerPanel";
            this.footerPanel.Size = new System.Drawing.Size(664, 48);
            this.footerPanel.TabIndex = 4;
            //
            // sourceLink
            //
            this.sourceLink.AutoSize = true;
            this.sourceLink.Font = new System.Drawing.Font("微软雅黑", 8F);
            this.sourceLink.LinkBehavior = System.Windows.Forms.LinkBehavior.HoverUnderline;
            this.sourceLink.LinkColor = System.Drawing.Color.FromArgb(87, 97, 109);
            this.sourceLink.Location = new System.Drawing.Point(573, 16);
            this.sourceLink.Name = "sourceLink";
            this.sourceLink.Size = new System.Drawing.Size(63, 16);
            this.sourceLink.TabIndex = 1;
            this.sourceLink.TabStop = true;
            this.sourceLink.Text = "开源项目";
            this.sourceLink.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.sourceLink_LinkClicked);
            //
            // statusLabel
            //
            this.statusLabel.AutoEllipsis = true;
            this.statusLabel.Font = new System.Drawing.Font("微软雅黑", 8.5F);
            this.statusLabel.ForeColor = System.Drawing.Color.FromArgb(87, 97, 109);
            this.statusLabel.Location = new System.Drawing.Point(19, 13);
            this.statusLabel.Name = "statusLabel";
            this.statusLabel.Size = new System.Drawing.Size(536, 23);
            this.statusLabel.TabIndex = 0;
            this.statusLabel.Text = "就绪：选择目标数量，然后点击左侧图标或“启动 / 补开”。";
            this.statusLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // statusTimer
            //
            this.statusTimer.Interval = 2000;
            this.statusTimer.Tick += new System.EventHandler(this.statusTimer_Tick);
            //
            // pathToolTip
            //
            this.pathToolTip.AutoPopDelay = 8000;
            this.pathToolTip.InitialDelay = 350;
            this.pathToolTip.ReshowDelay = 100;
            //
            // Home
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 17F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(236, 239, 243);
            this.ClientSize = new System.Drawing.Size(664, 346);
            this.Controls.Add(this.footerPanel);
            this.Controls.Add(this.quantityPanel);
            this.Controls.Add(this.wecomPanel);
            this.Controls.Add(this.wechatPanel);
            this.Controls.Add(this.headerPanel);
            this.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "Home";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "微信 · 企业微信多开助手 V1.0.0";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Home_FormClosing);
            this.Load += new System.EventHandler(this.Home_Load);
            this.headerPanel.ResumeLayout(false);
            this.headerPanel.PerformLayout();
            this.wechatPanel.ResumeLayout(false);
            this.wechatPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.wechatIcon)).EndInit();
            this.wecomPanel.ResumeLayout(false);
            this.wecomPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.wecomIcon)).EndInit();
            this.quantityPanel.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.targetCountInput)).EndInit();
            this.footerPanel.ResumeLayout(false);
            this.footerPanel.PerformLayout();
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Panel headerPanel;
        private System.Windows.Forms.Label versionLabel;
        private System.Windows.Forms.Label subtitleLabel;
        private System.Windows.Forms.Label titleLabel;
        private System.Windows.Forms.Panel wechatPanel;
        private System.Windows.Forms.Button wechatStartButton;
        private System.Windows.Forms.Label wechatPathLabel;
        private System.Windows.Forms.Label wechatCountLabel;
        private System.Windows.Forms.Label wechatNameLabel;
        private System.Windows.Forms.PictureBox wechatIcon;
        private System.Windows.Forms.Panel wecomPanel;
        private System.Windows.Forms.Button wecomStartButton;
        private System.Windows.Forms.Label wecomPathLabel;
        private System.Windows.Forms.Label wecomCountLabel;
        private System.Windows.Forms.Label wecomNameLabel;
        private System.Windows.Forms.PictureBox wecomIcon;
        private System.Windows.Forms.Panel quantityPanel;
        private System.Windows.Forms.Label targetHintLabel;
        private System.Windows.Forms.NumericUpDown targetCountInput;
        private System.Windows.Forms.Label targetLabel;
        private System.Windows.Forms.Panel footerPanel;
        private System.Windows.Forms.LinkLabel sourceLink;
        private System.Windows.Forms.Label statusLabel;
        private System.Windows.Forms.Timer statusTimer;
        private System.Windows.Forms.ToolTip pathToolTip;
    }
}
