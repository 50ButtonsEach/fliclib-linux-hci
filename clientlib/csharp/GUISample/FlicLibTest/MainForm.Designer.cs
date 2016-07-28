namespace FlicLibTest
{
    partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.buttonsList = new System.Windows.Forms.FlowLayoutPanel();
            this.lblConnectionStatus = new System.Windows.Forms.Label();
            this.lblBluetoothStatus = new System.Windows.Forms.Label();
            this.txtServer = new System.Windows.Forms.TextBox();
            this.lblServer = new System.Windows.Forms.Label();
            this.lblPort = new System.Windows.Forms.Label();
            this.txtPort = new System.Windows.Forms.TextBox();
            this.btnConnectDisconnect = new System.Windows.Forms.Button();
            this.btnAddNewFlic = new System.Windows.Forms.Button();
            this.lblScanWizardStatus = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // buttonsList
            // 
            this.buttonsList.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this.buttonsList.AutoScroll = true;
            this.buttonsList.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.buttonsList.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.buttonsList.Location = new System.Drawing.Point(13, 13);
            this.buttonsList.Name = "buttonsList";
            this.buttonsList.Size = new System.Drawing.Size(225, 358);
            this.buttonsList.TabIndex = 0;
            this.buttonsList.WrapContents = false;
            // 
            // lblConnectionStatus
            // 
            this.lblConnectionStatus.AutoSize = true;
            this.lblConnectionStatus.Location = new System.Drawing.Point(274, 13);
            this.lblConnectionStatus.Name = "lblConnectionStatus";
            this.lblConnectionStatus.Size = new System.Drawing.Size(164, 13);
            this.lblConnectionStatus.TabIndex = 1;
            this.lblConnectionStatus.Text = "Connection status: Disconnected";
            // 
            // lblBluetoothStatus
            // 
            this.lblBluetoothStatus.AutoSize = true;
            this.lblBluetoothStatus.Location = new System.Drawing.Point(273, 26);
            this.lblBluetoothStatus.Name = "lblBluetoothStatus";
            this.lblBluetoothStatus.Size = new System.Drawing.Size(132, 13);
            this.lblBluetoothStatus.TabIndex = 2;
            this.lblBluetoothStatus.Text = "Bluetooth controller status:";
            // 
            // txtServer
            // 
            this.txtServer.Location = new System.Drawing.Point(276, 101);
            this.txtServer.Name = "txtServer";
            this.txtServer.Size = new System.Drawing.Size(100, 20);
            this.txtServer.TabIndex = 3;
            this.txtServer.Text = "localhost";
            // 
            // lblServer
            // 
            this.lblServer.AutoSize = true;
            this.lblServer.Location = new System.Drawing.Point(273, 85);
            this.lblServer.Name = "lblServer";
            this.lblServer.Size = new System.Drawing.Size(41, 13);
            this.lblServer.TabIndex = 4;
            this.lblServer.Text = "Server:";
            // 
            // lblPort
            // 
            this.lblPort.AutoSize = true;
            this.lblPort.Location = new System.Drawing.Point(273, 124);
            this.lblPort.Name = "lblPort";
            this.lblPort.Size = new System.Drawing.Size(29, 13);
            this.lblPort.TabIndex = 5;
            this.lblPort.Text = "Port:";
            // 
            // txtPort
            // 
            this.txtPort.Location = new System.Drawing.Point(276, 140);
            this.txtPort.Name = "txtPort";
            this.txtPort.Size = new System.Drawing.Size(100, 20);
            this.txtPort.TabIndex = 6;
            this.txtPort.Text = "5551";
            // 
            // btnConnectDisconnect
            // 
            this.btnConnectDisconnect.Location = new System.Drawing.Point(275, 167);
            this.btnConnectDisconnect.Name = "btnConnectDisconnect";
            this.btnConnectDisconnect.Size = new System.Drawing.Size(101, 23);
            this.btnConnectDisconnect.TabIndex = 7;
            this.btnConnectDisconnect.Text = "Connect";
            this.btnConnectDisconnect.UseVisualStyleBackColor = true;
            this.btnConnectDisconnect.Click += new System.EventHandler(this.btnConnectDisconnect_Click);
            // 
            // btnAddNewFlic
            // 
            this.btnAddNewFlic.Enabled = false;
            this.btnAddNewFlic.Location = new System.Drawing.Point(277, 226);
            this.btnAddNewFlic.Name = "btnAddNewFlic";
            this.btnAddNewFlic.Size = new System.Drawing.Size(99, 23);
            this.btnAddNewFlic.TabIndex = 8;
            this.btnAddNewFlic.Text = "Add new Flic";
            this.btnAddNewFlic.UseVisualStyleBackColor = true;
            this.btnAddNewFlic.Click += new System.EventHandler(this.btnAddNewFlic_Click);
            // 
            // lblScanWizardStatus
            // 
            this.lblScanWizardStatus.AutoSize = true;
            this.lblScanWizardStatus.Location = new System.Drawing.Point(277, 256);
            this.lblScanWizardStatus.Name = "lblScanWizardStatus";
            this.lblScanWizardStatus.Size = new System.Drawing.Size(105, 13);
            this.lblScanWizardStatus.TabIndex = 9;
            this.lblScanWizardStatus.Text = "lblScanWizardStatus";
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(636, 383);
            this.Controls.Add(this.lblScanWizardStatus);
            this.Controls.Add(this.btnAddNewFlic);
            this.Controls.Add(this.btnConnectDisconnect);
            this.Controls.Add(this.txtPort);
            this.Controls.Add(this.lblPort);
            this.Controls.Add(this.lblServer);
            this.Controls.Add(this.txtServer);
            this.Controls.Add(this.lblBluetoothStatus);
            this.Controls.Add(this.lblConnectionStatus);
            this.Controls.Add(this.buttonsList);
            this.Name = "MainForm";
            this.Text = "Flic Sample";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.FlowLayoutPanel buttonsList;
        private System.Windows.Forms.Label lblConnectionStatus;
        private System.Windows.Forms.Label lblBluetoothStatus;
        private System.Windows.Forms.TextBox txtServer;
        private System.Windows.Forms.Label lblServer;
        private System.Windows.Forms.Label lblPort;
        private System.Windows.Forms.TextBox txtPort;
        private System.Windows.Forms.Button btnConnectDisconnect;
        private System.Windows.Forms.Button btnAddNewFlic;
        private System.Windows.Forms.Label lblScanWizardStatus;
    }
}

