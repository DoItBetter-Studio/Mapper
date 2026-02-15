using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace Glyphborn.Mapper
{
    partial class MapperForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

		private const int menuItemSize = 60;

        /// <summary>
        ///  Clean up any resources being used.
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
		///  Required method for Designer support - do not modify
		///  the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			ComponentResourceManager resources = new ComponentResourceManager(typeof(MapperForm));
			menuStrip = new MenuStrip();
			fileToolStripMenuItem = new ToolStripMenuItem();
			newMapToolStripMenuItem = new ToolStripMenuItem();
			openMapToolStripMenuItem = new ToolStripMenuItem();
			saveMapToolStripMenuItem = new ToolStripMenuItem();
			saveMapAsToolStripMenuItem = new ToolStripMenuItem();
			exportMapToolStripMenuItem = new ToolStripMenuItem();
			toolStripSeparator1 = new ToolStripSeparator();
			exitToolStripMenuItem = new ToolStripMenuItem();
			editToolStripMenuItem = new ToolStripMenuItem();
			undoToolStripMenuItem = new ToolStripMenuItem();
			redoToolStripMenuItem = new ToolStripMenuItem();
			fillLayerToolStripMenuItem = new ToolStripMenuItem();
			clearLayerToolStripMenuItem = new ToolStripMenuItem();
			viewToolStripMenuItem = new ToolStripMenuItem();
			showGridToolStripMenuItem = new ToolStripMenuItem();
			showTilePropertiesToolStripMenuItem = new ToolStripMenuItem();
			layerOverlayToolStripMenuItem = new ToolStripMenuItem();
			helpToolStripMenuItem = new ToolStripMenuItem();
			shortcutsToolStripMenuItem = new ToolStripMenuItem();
			aboutToolStripMenuItem = new ToolStripMenuItem();
			dViewToolStripMenuItem = new ToolStripMenuItem();
			menuStrip.SuspendLayout();
			SuspendLayout();
			// 
			// menuStrip
			// 
			menuStrip.BackColor = SystemColors.ControlDarkDark;
			menuStrip.Items.AddRange(new ToolStripItem[] { fileToolStripMenuItem, editToolStripMenuItem, viewToolStripMenuItem, helpToolStripMenuItem });
			menuStrip.Location = new Point(0, 0);
			menuStrip.Name = "menuStrip";
			menuStrip.Size = new Size(1280, 24);
			menuStrip.TabIndex = 0;
			menuStrip.Text = "menuStrip1";
			// 
			// fileToolStripMenuItem
			// 
			fileToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { newMapToolStripMenuItem, openMapToolStripMenuItem, saveMapToolStripMenuItem, saveMapAsToolStripMenuItem, exportMapToolStripMenuItem, toolStripSeparator1, exitToolStripMenuItem });
			fileToolStripMenuItem.Name = "fileToolStripMenuItem";
			fileToolStripMenuItem.Size = new Size(37, 20);
			fileToolStripMenuItem.Text = "File";
			// 
			// newMapToolStripMenuItem
			// 
			newMapToolStripMenuItem.Name = "newMapToolStripMenuItem";
			newMapToolStripMenuItem.ShortcutKeyDisplayString = "";
			newMapToolStripMenuItem.ShortcutKeys =  Keys.Control | Keys.N;
			newMapToolStripMenuItem.Size = new Size(222, 22);
			newMapToolStripMenuItem.Text = "New Map";
			newMapToolStripMenuItem.Click += NewMap_Click;
			// 
			// openMapToolStripMenuItem
			// 
			openMapToolStripMenuItem.Name = "openMapToolStripMenuItem";
			openMapToolStripMenuItem.ShortcutKeys =  Keys.Control | Keys.O;
			openMapToolStripMenuItem.Size = new Size(222, 22);
			openMapToolStripMenuItem.Text = "Open Map...";
			openMapToolStripMenuItem.Click += LoadMap_Click;
			// 
			// saveMapToolStripMenuItem
			// 
			saveMapToolStripMenuItem.Enabled = false;
			saveMapToolStripMenuItem.Name = "saveMapToolStripMenuItem";
			saveMapToolStripMenuItem.ShortcutKeys =  Keys.Control | Keys.S;
			saveMapToolStripMenuItem.Size = new Size(222, 22);
			saveMapToolStripMenuItem.Text = "Save Map";
			saveMapToolStripMenuItem.Click += SaveMap_Click;
			// 
			// saveMapAsToolStripMenuItem
			// 
			saveMapAsToolStripMenuItem.Enabled = false;
			saveMapAsToolStripMenuItem.Name = "saveMapAsToolStripMenuItem";
			saveMapAsToolStripMenuItem.ShortcutKeys =  Keys.Control | Keys.Shift | Keys.S;
			saveMapAsToolStripMenuItem.Size = new Size(222, 22);
			saveMapAsToolStripMenuItem.Text = "Save Map As...";
			saveMapAsToolStripMenuItem.Click += SaveMapAs_Click;
			// 
			// exportMapToolStripMenuItem
			// 
			exportMapToolStripMenuItem.Enabled = false;
			exportMapToolStripMenuItem.Name = "exportMapToolStripMenuItem";
			exportMapToolStripMenuItem.ShortcutKeys =  Keys.Control | Keys.Shift | Keys.E;
			exportMapToolStripMenuItem.Size = new Size(222, 22);
			exportMapToolStripMenuItem.Text = "Export Map...";
			exportMapToolStripMenuItem.Click += Export_Click;
			// 
			// toolStripSeparator1
			// 
			toolStripSeparator1.Name = "toolStripSeparator1";
			toolStripSeparator1.Size = new Size(219, 6);
			// 
			// exitToolStripMenuItem
			// 
			exitToolStripMenuItem.Name = "exitToolStripMenuItem";
			exitToolStripMenuItem.Size = new Size(222, 22);
			exitToolStripMenuItem.Text = "Exit";
			exitToolStripMenuItem.Click += Exit_Click;
			// 
			// editToolStripMenuItem
			// 
			editToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { undoToolStripMenuItem, redoToolStripMenuItem, fillLayerToolStripMenuItem, clearLayerToolStripMenuItem });
			editToolStripMenuItem.Name = "editToolStripMenuItem";
			editToolStripMenuItem.Size = new Size(39, 20);
			editToolStripMenuItem.Text = "Edit";
			// 
			// undoToolStripMenuItem
			// 
			undoToolStripMenuItem.Name = "undoToolStripMenuItem";
			undoToolStripMenuItem.ShortcutKeys =  Keys.Control | Keys.Z;
			undoToolStripMenuItem.Size = new Size(144, 22);
			undoToolStripMenuItem.Text = "Undo";
			// 
			// redoToolStripMenuItem
			// 
			redoToolStripMenuItem.Name = "redoToolStripMenuItem";
			redoToolStripMenuItem.ShortcutKeys =  Keys.Control | Keys.Y;
			redoToolStripMenuItem.Size = new Size(144, 22);
			redoToolStripMenuItem.Text = "Redo";
			// 
			// fillLayerToolStripMenuItem
			// 
			fillLayerToolStripMenuItem.Name = "fillLayerToolStripMenuItem";
			fillLayerToolStripMenuItem.Size = new Size(144, 22);
			fillLayerToolStripMenuItem.Text = "Fill Layer";
			// 
			// clearLayerToolStripMenuItem
			// 
			clearLayerToolStripMenuItem.Name = "clearLayerToolStripMenuItem";
			clearLayerToolStripMenuItem.Size = new Size(144, 22);
			clearLayerToolStripMenuItem.Text = "Clear Layer";
			// 
			// viewToolStripMenuItem
			// 
			viewToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { showGridToolStripMenuItem, dViewToolStripMenuItem });
			viewToolStripMenuItem.Name = "viewToolStripMenuItem";
			viewToolStripMenuItem.Size = new Size(44, 20);
			viewToolStripMenuItem.Text = "View";
			// 
			// showGridToolStripMenuItem
			// 
			showGridToolStripMenuItem.CheckOnClick = true;
			showGridToolStripMenuItem.Name = "showGridToolStripMenuItem";
			showGridToolStripMenuItem.Size = new Size(181, 22);
			showGridToolStripMenuItem.Text = "Show Grid";
			showGridToolStripMenuItem.Click += ShowGrid_Click;
			// 
			// dViewToolStripMenuItem
			// 
			dViewToolStripMenuItem.Name = "dViewToolStripMenuItem";
			dViewToolStripMenuItem.Size = new Size(181, 22);
			dViewToolStripMenuItem.Text = "3D View";
			dViewToolStripMenuItem.Click += _3DView_Click;
			// 
			// helpToolStripMenuItem
			// 
			helpToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { shortcutsToolStripMenuItem, aboutToolStripMenuItem });
			helpToolStripMenuItem.Name = "helpToolStripMenuItem";
			helpToolStripMenuItem.Size = new Size(44, 20);
			helpToolStripMenuItem.Text = "Help";
			// 
			// shortcutsToolStripMenuItem
			// 
			shortcutsToolStripMenuItem.Name = "shortcutsToolStripMenuItem";
			shortcutsToolStripMenuItem.Size = new Size(124, 22);
			shortcutsToolStripMenuItem.Text = "Shortcuts";
			// 
			// aboutToolStripMenuItem
			// 
			aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
			aboutToolStripMenuItem.Size = new Size(124, 22);
			aboutToolStripMenuItem.Text = "About";
			// 
			// MapperForm
			// 
			AutoScaleDimensions = new SizeF(7F, 15F);
			AutoScaleMode = AutoScaleMode.Font;
			BackColor = SystemColors.ControlDarkDark;
			ClientSize = new Size(1280, 720);
			Controls.Add(menuStrip);
			Icon = (Icon) resources.GetObject("$this.Icon");
			MainMenuStrip = menuStrip;
			Name = "MapperForm";
			StartPosition = FormStartPosition.CenterScreen;
			Text = "Mapper";
			Load += MapperForm_Load;
			menuStrip.ResumeLayout(false);
			menuStrip.PerformLayout();
			ResumeLayout(false);
			PerformLayout();
		}
		#endregion

		private MenuStrip menuStrip;
		private ToolStripMenuItem fileToolStripMenuItem;
		private ToolStripMenuItem newMapToolStripMenuItem;
		private ToolStripMenuItem openMapToolStripMenuItem;
		private ToolStripMenuItem saveMapToolStripMenuItem;
		private ToolStripMenuItem saveMapAsToolStripMenuItem;
		private ToolStripMenuItem exportMapToolStripMenuItem;
		private ToolStripSeparator toolStripSeparator1;
		private ToolStripMenuItem exitToolStripMenuItem;
		private ToolStripMenuItem editToolStripMenuItem;
		private ToolStripMenuItem undoToolStripMenuItem;
		private ToolStripMenuItem redoToolStripMenuItem;
		private ToolStripMenuItem fillLayerToolStripMenuItem;
		private ToolStripMenuItem clearLayerToolStripMenuItem;
		private ToolStripMenuItem viewToolStripMenuItem;
		private ToolStripMenuItem showGridToolStripMenuItem;
		private ToolStripMenuItem showTilePropertiesToolStripMenuItem;
		private ToolStripMenuItem layerOverlayToolStripMenuItem;
		private ToolStripMenuItem helpToolStripMenuItem;
		private ToolStripMenuItem shortcutsToolStripMenuItem;
		private ToolStripMenuItem aboutToolStripMenuItem;
		private ToolStripMenuItem dViewToolStripMenuItem;
	}
}
