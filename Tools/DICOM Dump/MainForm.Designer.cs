﻿namespace Dicom.Dump {
	partial class MainForm {
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing) {
			if (disposing && (components != null)) {
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent() {
            this.components = new System.ComponentModel.Container();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.menuItemSave = new System.Windows.Forms.ToolStripMenuItem();
            this.menuItemTools = new System.Windows.Forms.ToolStripMenuItem();
            this.menuItemView = new System.Windows.Forms.ToolStripMenuItem();
            this.menuItemSyntax = new System.Windows.Forms.ToolStripMenuItem();
            this.explicitVRLittleEndianToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.implicitVRLittleEndianToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.explicitVRBigEndianToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem2 = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem3 = new System.Windows.Forms.ToolStripMenuItem();
            this.menuItemJpegLossy = new System.Windows.Forms.ToolStripMenuItem();
            this.quality100ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.quality90ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.quality80ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.quality75ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.quality70ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.quality60ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.quality50ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.jPEG2000LosslessToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.jPEG2000Lossyrate80ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.rate5ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.rate10ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.rate20ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.rate40ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.rate80ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.jPEGLSLosslessToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.jPEGLSNearLosslessToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.error2ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.error3ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.error4ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.error5ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.error10ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.error20ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.rLELosslessToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exportPixelDataToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.lvDicom = new System.Windows.Forms.ListView();
            this.columnHeaderTag = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeaderVR = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeaderLength = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeaderValue = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeaderPos = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.cmDicom = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.copyValueToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.copyTagToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.menuStrip1.SuspendLayout();
            this.cmDicom.SuspendLayout();
            this.SuspendLayout();
            // 
            // menuStrip1
            // 
            this.menuStrip1.GripMargin = new System.Windows.Forms.Padding(2, 2, 0, 2);
            this.menuStrip1.ImageScalingSize = new System.Drawing.Size(32, 32);
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.menuItemTools});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(1584, 48);
            this.menuStrip1.TabIndex = 0;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.openToolStripMenuItem,
            this.menuItemSave});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(72, 40);
            this.fileToolStripMenuItem.Text = "&File";
            // 
            // openToolStripMenuItem
            // 
            this.openToolStripMenuItem.Name = "openToolStripMenuItem";
            this.openToolStripMenuItem.Size = new System.Drawing.Size(208, 44);
            this.openToolStripMenuItem.Text = "&Open";
            this.openToolStripMenuItem.Click += new System.EventHandler(this.OnClickOpen);
            // 
            // menuItemSave
            // 
            this.menuItemSave.Enabled = false;
            this.menuItemSave.Name = "menuItemSave";
            this.menuItemSave.Size = new System.Drawing.Size(208, 44);
            this.menuItemSave.Text = "&Save";
            this.menuItemSave.Click += new System.EventHandler(this.OnClickSave);
            // 
            // menuItemTools
            // 
            this.menuItemTools.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuItemView,
            this.menuItemSyntax,
            this.exportPixelDataToolStripMenuItem});
            this.menuItemTools.Name = "menuItemTools";
            this.menuItemTools.Size = new System.Drawing.Size(90, 40);
            this.menuItemTools.Text = "&Tools";
            // 
            // menuItemView
            // 
            this.menuItemView.Enabled = false;
            this.menuItemView.Name = "menuItemView";
            this.menuItemView.Size = new System.Drawing.Size(328, 44);
            this.menuItemView.Text = "&View";
            this.menuItemView.Click += new System.EventHandler(this.OnClickView);
            // 
            // menuItemSyntax
            // 
            this.menuItemSyntax.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.explicitVRLittleEndianToolStripMenuItem,
            this.implicitVRLittleEndianToolStripMenuItem,
            this.explicitVRBigEndianToolStripMenuItem,
            this.toolStripMenuItem2,
            this.toolStripMenuItem3,
            this.menuItemJpegLossy,
            this.jPEG2000LosslessToolStripMenuItem,
            this.jPEG2000Lossyrate80ToolStripMenuItem,
            this.jPEGLSLosslessToolStripMenuItem,
            this.jPEGLSNearLosslessToolStripMenuItem,
            this.rLELosslessToolStripMenuItem});
            this.menuItemSyntax.Enabled = false;
            this.menuItemSyntax.Name = "menuItemSyntax";
            this.menuItemSyntax.Size = new System.Drawing.Size(328, 44);
            this.menuItemSyntax.Text = "&Change Syntax";
            // 
            // explicitVRLittleEndianToolStripMenuItem
            // 
            this.explicitVRLittleEndianToolStripMenuItem.Name = "explicitVRLittleEndianToolStripMenuItem";
            this.explicitVRLittleEndianToolStripMenuItem.Size = new System.Drawing.Size(401, 44);
            this.explicitVRLittleEndianToolStripMenuItem.Text = "Explicit VR Little Endian";
            this.explicitVRLittleEndianToolStripMenuItem.Click += new System.EventHandler(this.OnClickExplicitVRLittleEndian);
            // 
            // implicitVRLittleEndianToolStripMenuItem
            // 
            this.implicitVRLittleEndianToolStripMenuItem.Name = "implicitVRLittleEndianToolStripMenuItem";
            this.implicitVRLittleEndianToolStripMenuItem.Size = new System.Drawing.Size(401, 44);
            this.implicitVRLittleEndianToolStripMenuItem.Text = "Implicit VR Little Endian";
            this.implicitVRLittleEndianToolStripMenuItem.Click += new System.EventHandler(this.OnClickImplicitVRLittleEndian);
            // 
            // explicitVRBigEndianToolStripMenuItem
            // 
            this.explicitVRBigEndianToolStripMenuItem.Name = "explicitVRBigEndianToolStripMenuItem";
            this.explicitVRBigEndianToolStripMenuItem.Size = new System.Drawing.Size(401, 44);
            this.explicitVRBigEndianToolStripMenuItem.Text = "Explicit VR Big Endian";
            this.explicitVRBigEndianToolStripMenuItem.Click += new System.EventHandler(this.OnClickExplicitVRBigEndian);
            // 
            // toolStripMenuItem2
            // 
            this.toolStripMenuItem2.Name = "toolStripMenuItem2";
            this.toolStripMenuItem2.Size = new System.Drawing.Size(401, 44);
            this.toolStripMenuItem2.Text = "JPEG Lossless P14";
            this.toolStripMenuItem2.Click += new System.EventHandler(this.OnClickJPEGLosslessP14);
            // 
            // toolStripMenuItem3
            // 
            this.toolStripMenuItem3.Name = "toolStripMenuItem3";
            this.toolStripMenuItem3.Size = new System.Drawing.Size(401, 44);
            this.toolStripMenuItem3.Text = "JPEG Lossless P14 SV1";
            this.toolStripMenuItem3.Click += new System.EventHandler(this.OnClickJPEGLosslessP14SV1);
            // 
            // menuItemJpegLossy
            // 
            this.menuItemJpegLossy.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.quality100ToolStripMenuItem,
            this.quality90ToolStripMenuItem,
            this.quality80ToolStripMenuItem,
            this.quality75ToolStripMenuItem,
            this.quality70ToolStripMenuItem,
            this.quality60ToolStripMenuItem,
            this.quality50ToolStripMenuItem});
            this.menuItemJpegLossy.Name = "menuItemJpegLossy";
            this.menuItemJpegLossy.Size = new System.Drawing.Size(401, 44);
            this.menuItemJpegLossy.Text = "JPEG Lossy P1 && P4";
            // 
            // quality100ToolStripMenuItem
            // 
            this.quality100ToolStripMenuItem.Name = "quality100ToolStripMenuItem";
            this.quality100ToolStripMenuItem.Size = new System.Drawing.Size(276, 44);
            this.quality100ToolStripMenuItem.Text = "Quality: 100";
            this.quality100ToolStripMenuItem.Click += new System.EventHandler(this.OnClickJPEGLossyQuality100);
            // 
            // quality90ToolStripMenuItem
            // 
            this.quality90ToolStripMenuItem.Name = "quality90ToolStripMenuItem";
            this.quality90ToolStripMenuItem.Size = new System.Drawing.Size(276, 44);
            this.quality90ToolStripMenuItem.Text = "Quality: 90";
            this.quality90ToolStripMenuItem.Click += new System.EventHandler(this.OnClickJPEGLossyQuality90);
            // 
            // quality80ToolStripMenuItem
            // 
            this.quality80ToolStripMenuItem.Name = "quality80ToolStripMenuItem";
            this.quality80ToolStripMenuItem.Size = new System.Drawing.Size(276, 44);
            this.quality80ToolStripMenuItem.Text = "Quality: 80";
            this.quality80ToolStripMenuItem.Click += new System.EventHandler(this.OnClickJPEGLossyQuality80);
            // 
            // quality75ToolStripMenuItem
            // 
            this.quality75ToolStripMenuItem.Name = "quality75ToolStripMenuItem";
            this.quality75ToolStripMenuItem.Size = new System.Drawing.Size(276, 44);
            this.quality75ToolStripMenuItem.Text = "Quality: 75";
            this.quality75ToolStripMenuItem.Click += new System.EventHandler(this.OnClickJPEGLossyQuality75);
            // 
            // quality70ToolStripMenuItem
            // 
            this.quality70ToolStripMenuItem.Name = "quality70ToolStripMenuItem";
            this.quality70ToolStripMenuItem.Size = new System.Drawing.Size(276, 44);
            this.quality70ToolStripMenuItem.Text = "Quality: 70";
            this.quality70ToolStripMenuItem.Click += new System.EventHandler(this.OnClickJPEGLossyQuality70);
            // 
            // quality60ToolStripMenuItem
            // 
            this.quality60ToolStripMenuItem.Name = "quality60ToolStripMenuItem";
            this.quality60ToolStripMenuItem.Size = new System.Drawing.Size(276, 44);
            this.quality60ToolStripMenuItem.Text = "Quality: 60";
            this.quality60ToolStripMenuItem.Click += new System.EventHandler(this.OnClickJPEGLossyQuality60);
            // 
            // quality50ToolStripMenuItem
            // 
            this.quality50ToolStripMenuItem.Name = "quality50ToolStripMenuItem";
            this.quality50ToolStripMenuItem.Size = new System.Drawing.Size(276, 44);
            this.quality50ToolStripMenuItem.Text = "Quality: 50";
            this.quality50ToolStripMenuItem.Click += new System.EventHandler(this.OnClickJPEGLossyQuality50);
            // 
            // jPEG2000LosslessToolStripMenuItem
            // 
            this.jPEG2000LosslessToolStripMenuItem.Name = "jPEG2000LosslessToolStripMenuItem";
            this.jPEG2000LosslessToolStripMenuItem.Size = new System.Drawing.Size(401, 44);
            this.jPEG2000LosslessToolStripMenuItem.Text = "JPEG 2000 Lossless";
            this.jPEG2000LosslessToolStripMenuItem.Click += new System.EventHandler(this.OnClickJPEG2000Lossless);
            // 
            // jPEG2000Lossyrate80ToolStripMenuItem
            // 
            this.jPEG2000Lossyrate80ToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.rate5ToolStripMenuItem,
            this.rate10ToolStripMenuItem,
            this.rate20ToolStripMenuItem,
            this.rate40ToolStripMenuItem,
            this.rate80ToolStripMenuItem});
            this.jPEG2000Lossyrate80ToolStripMenuItem.Name = "jPEG2000Lossyrate80ToolStripMenuItem";
            this.jPEG2000Lossyrate80ToolStripMenuItem.Size = new System.Drawing.Size(401, 44);
            this.jPEG2000Lossyrate80ToolStripMenuItem.Text = "JPEG 2000 Lossy";
            // 
            // rate5ToolStripMenuItem
            // 
            this.rate5ToolStripMenuItem.Name = "rate5ToolStripMenuItem";
            this.rate5ToolStripMenuItem.Size = new System.Drawing.Size(234, 44);
            this.rate5ToolStripMenuItem.Text = "Rate: 5";
            this.rate5ToolStripMenuItem.Click += new System.EventHandler(this.OnClickJPEG2000LossyRate5);
            // 
            // rate10ToolStripMenuItem
            // 
            this.rate10ToolStripMenuItem.Name = "rate10ToolStripMenuItem";
            this.rate10ToolStripMenuItem.Size = new System.Drawing.Size(234, 44);
            this.rate10ToolStripMenuItem.Text = "Rate: 10";
            this.rate10ToolStripMenuItem.Click += new System.EventHandler(this.OnClickJPEG2000LossyRate10);
            // 
            // rate20ToolStripMenuItem
            // 
            this.rate20ToolStripMenuItem.Name = "rate20ToolStripMenuItem";
            this.rate20ToolStripMenuItem.Size = new System.Drawing.Size(234, 44);
            this.rate20ToolStripMenuItem.Text = "Rate: 20";
            this.rate20ToolStripMenuItem.Click += new System.EventHandler(this.OnClickJPEG2000LossyRate20);
            // 
            // rate40ToolStripMenuItem
            // 
            this.rate40ToolStripMenuItem.Name = "rate40ToolStripMenuItem";
            this.rate40ToolStripMenuItem.Size = new System.Drawing.Size(234, 44);
            this.rate40ToolStripMenuItem.Text = "Rate: 40";
            this.rate40ToolStripMenuItem.Click += new System.EventHandler(this.OnClickJPEG2000LossyRate40);
            // 
            // rate80ToolStripMenuItem
            // 
            this.rate80ToolStripMenuItem.Name = "rate80ToolStripMenuItem";
            this.rate80ToolStripMenuItem.Size = new System.Drawing.Size(234, 44);
            this.rate80ToolStripMenuItem.Text = "Rate: 80";
            this.rate80ToolStripMenuItem.Click += new System.EventHandler(this.OnClickJPEG2000LossyRate80);
            // 
            // jPEGLSLosslessToolStripMenuItem
            // 
            this.jPEGLSLosslessToolStripMenuItem.Name = "jPEGLSLosslessToolStripMenuItem";
            this.jPEGLSLosslessToolStripMenuItem.Size = new System.Drawing.Size(401, 44);
            this.jPEGLSLosslessToolStripMenuItem.Text = "JPEG-LS Lossless";
            this.jPEGLSLosslessToolStripMenuItem.Click += new System.EventHandler(this.OnClickJPEGLSLossless);
            // 
            // jPEGLSNearLosslessToolStripMenuItem
            // 
            this.jPEGLSNearLosslessToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.error2ToolStripMenuItem,
            this.error3ToolStripMenuItem,
            this.error4ToolStripMenuItem,
            this.error5ToolStripMenuItem,
            this.error10ToolStripMenuItem,
            this.error20ToolStripMenuItem});
            this.jPEGLSNearLosslessToolStripMenuItem.Name = "jPEGLSNearLosslessToolStripMenuItem";
            this.jPEGLSNearLosslessToolStripMenuItem.Size = new System.Drawing.Size(401, 44);
            this.jPEGLSNearLosslessToolStripMenuItem.Text = "JPEG-LS Near Lossless";
            // 
            // error2ToolStripMenuItem
            // 
            this.error2ToolStripMenuItem.Name = "error2ToolStripMenuItem";
            this.error2ToolStripMenuItem.Size = new System.Drawing.Size(237, 44);
            this.error2ToolStripMenuItem.Text = "Error: 2";
            this.error2ToolStripMenuItem.Click += new System.EventHandler(this.OnClickJPEGLSNearLosslessError2);
            // 
            // error3ToolStripMenuItem
            // 
            this.error3ToolStripMenuItem.Name = "error3ToolStripMenuItem";
            this.error3ToolStripMenuItem.Size = new System.Drawing.Size(237, 44);
            this.error3ToolStripMenuItem.Text = "Error: 3";
            this.error3ToolStripMenuItem.Click += new System.EventHandler(this.OnClickJPEGLSNearLosslessError3);
            // 
            // error4ToolStripMenuItem
            // 
            this.error4ToolStripMenuItem.Name = "error4ToolStripMenuItem";
            this.error4ToolStripMenuItem.Size = new System.Drawing.Size(237, 44);
            this.error4ToolStripMenuItem.Text = "Error: 4";
            this.error4ToolStripMenuItem.Click += new System.EventHandler(this.OnClickJPEGLSNearLosslessError4);
            // 
            // error5ToolStripMenuItem
            // 
            this.error5ToolStripMenuItem.Name = "error5ToolStripMenuItem";
            this.error5ToolStripMenuItem.Size = new System.Drawing.Size(237, 44);
            this.error5ToolStripMenuItem.Text = "Error: 5";
            this.error5ToolStripMenuItem.Click += new System.EventHandler(this.OnClickJPEGLSNearLosslessError5);
            // 
            // error10ToolStripMenuItem
            // 
            this.error10ToolStripMenuItem.Name = "error10ToolStripMenuItem";
            this.error10ToolStripMenuItem.Size = new System.Drawing.Size(237, 44);
            this.error10ToolStripMenuItem.Text = "Error: 10";
            this.error10ToolStripMenuItem.Click += new System.EventHandler(this.OnClickJPEGLSNearLosslessError10);
            // 
            // error20ToolStripMenuItem
            // 
            this.error20ToolStripMenuItem.Name = "error20ToolStripMenuItem";
            this.error20ToolStripMenuItem.Size = new System.Drawing.Size(237, 44);
            this.error20ToolStripMenuItem.Text = "Error: 20";
            this.error20ToolStripMenuItem.Click += new System.EventHandler(this.error20ToolStripMenuItem_Click);
            // 
            // rLELosslessToolStripMenuItem
            // 
            this.rLELosslessToolStripMenuItem.Name = "rLELosslessToolStripMenuItem";
            this.rLELosslessToolStripMenuItem.Size = new System.Drawing.Size(401, 44);
            this.rLELosslessToolStripMenuItem.Text = "RLE Lossless";
            this.rLELosslessToolStripMenuItem.Click += new System.EventHandler(this.OnClickRLELossless);
            // 
            // exportPixelDataToolStripMenuItem
            // 
            this.exportPixelDataToolStripMenuItem.Name = "exportPixelDataToolStripMenuItem";
            this.exportPixelDataToolStripMenuItem.Size = new System.Drawing.Size(328, 44);
            this.exportPixelDataToolStripMenuItem.Text = "Export Pixel Data";
            this.exportPixelDataToolStripMenuItem.Click += new System.EventHandler(this.OnClickExportPixelData);
            // 
            // lvDicom
            // 
            this.lvDicom.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeaderTag,
            this.columnHeaderVR,
            this.columnHeaderLength,
            this.columnHeaderValue,
            this.columnHeaderPos});
            this.lvDicom.ContextMenuStrip = this.cmDicom;
            this.lvDicom.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lvDicom.FullRowSelect = true;
            this.lvDicom.HideSelection = false;
            this.lvDicom.Location = new System.Drawing.Point(0, 48);
            this.lvDicom.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.lvDicom.MultiSelect = false;
            this.lvDicom.Name = "lvDicom";
            this.lvDicom.Size = new System.Drawing.Size(1584, 1054);
            this.lvDicom.TabIndex = 1;
            this.lvDicom.UseCompatibleStateImageBehavior = false;
            this.lvDicom.View = System.Windows.Forms.View.Details;
            // 
            // columnHeaderTag
            // 
            this.columnHeaderTag.Text = "Tag";
            this.columnHeaderTag.Width = 230;
            // 
            // columnHeaderVR
            // 
            this.columnHeaderVR.Text = "VR";
            this.columnHeaderVR.Width = 40;
            // 
            // columnHeaderLength
            // 
            this.columnHeaderLength.Text = "Length";
            // 
            // columnHeaderValue
            // 
            this.columnHeaderValue.Text = "Value";
            this.columnHeaderValue.Width = 400;
            // 
            // columnHeaderPos
            // 
            this.columnHeaderPos.Text = "Position";
            // 
            // cmDicom
            // 
            this.cmDicom.ImageScalingSize = new System.Drawing.Size(32, 32);
            this.cmDicom.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.copyValueToolStripMenuItem,
            this.copyTagToolStripMenuItem});
            this.cmDicom.Name = "cmDicom";
            this.cmDicom.Size = new System.Drawing.Size(211, 80);
            this.cmDicom.Opening += new System.ComponentModel.CancelEventHandler(this.OnContextMenuOpening);
            // 
            // copyValueToolStripMenuItem
            // 
            this.copyValueToolStripMenuItem.Name = "copyValueToolStripMenuItem";
            this.copyValueToolStripMenuItem.Size = new System.Drawing.Size(210, 38);
            this.copyValueToolStripMenuItem.Text = "Copy &Value";
            this.copyValueToolStripMenuItem.Click += new System.EventHandler(this.OnClickContextMenuCopyValue);
            // 
            // copyTagToolStripMenuItem
            // 
            this.copyTagToolStripMenuItem.Name = "copyTagToolStripMenuItem";
            this.copyTagToolStripMenuItem.Size = new System.Drawing.Size(210, 38);
            this.copyTagToolStripMenuItem.Text = "Copy &Tag";
            this.copyTagToolStripMenuItem.Click += new System.EventHandler(this.OnClickContextMenuCopyTag);
            // 
            // MainForm
            // 
            this.AllowDrop = true;
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.ClientSize = new System.Drawing.Size(1584, 1102);
            this.Controls.Add(this.lvDicom);
            this.Controls.Add(this.menuStrip1);
            this.MainMenuStrip = this.menuStrip1;
            this.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.Name = "MainForm";
            this.Text = "DICOM Dump";
            this.DragDrop += new System.Windows.Forms.DragEventHandler(this.OnDragDrop);
            this.DragEnter += new System.Windows.Forms.DragEventHandler(this.OnDragEnter);
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.cmDicom.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.MenuStrip menuStrip1;
		private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem openToolStripMenuItem;
		private System.Windows.Forms.ListView lvDicom;
		private System.Windows.Forms.ColumnHeader columnHeaderTag;
		private System.Windows.Forms.ColumnHeader columnHeaderVR;
		private System.Windows.Forms.ColumnHeader columnHeaderLength;
		private System.Windows.Forms.ColumnHeader columnHeaderValue;
		private System.Windows.Forms.ContextMenuStrip cmDicom;
		private System.Windows.Forms.ToolStripMenuItem copyValueToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem menuItemSave;
		private System.Windows.Forms.ToolStripMenuItem menuItemTools;
		private System.Windows.Forms.ToolStripMenuItem menuItemView;
		private System.Windows.Forms.ToolStripMenuItem menuItemSyntax;
		private System.Windows.Forms.ToolStripMenuItem menuItemJpegLossy;
		private System.Windows.Forms.ToolStripMenuItem explicitVRLittleEndianToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem implicitVRLittleEndianToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem explicitVRBigEndianToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem jPEG2000LosslessToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem jPEGLSLosslessToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem1;
		private System.Windows.Forms.ToolStripMenuItem rLELosslessToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem jPEG2000Lossyrate80ToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem rate5ToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem rate10ToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem rate20ToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem rate40ToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem rate80ToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem jPEGLSNearLosslessToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem error2ToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem error3ToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem error4ToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem error5ToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem error10ToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem2;
		private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem3;
		private System.Windows.Forms.ToolStripMenuItem quality100ToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem quality90ToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem quality80ToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem quality75ToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem quality70ToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem quality60ToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem quality50ToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem copyTagToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem exportPixelDataToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem error20ToolStripMenuItem;
        private System.Windows.Forms.ColumnHeader columnHeaderPos;
    }
}

