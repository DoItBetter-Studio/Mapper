using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Glyphborn.Mapper.Dialogs
{
	public class ShortcutsDialog : Form
	{
		private readonly ListView _list;

		public ShortcutsDialog(MenuStrip menu)
		{
			Text = "Keyboard Shortcuts";
			Width = 420;
			Height = 500;
			StartPosition = FormStartPosition.CenterParent;
			FormBorderStyle = FormBorderStyle.FixedDialog;
			MaximizeBox = false;
			MinimizeBox = false;

			_list = new ListView
			{
				Dock = DockStyle.Fill,
				View = View.Details,
				FullRowSelect = true,
				GridLines = false,
				HeaderStyle = ColumnHeaderStyle.Nonclickable
			};

			_list.Columns.Add("Command", 250);
			_list.Columns.Add("Shortcut", 120);

			Controls.Add(_list);

			LoadShortcuts(menu);
		}

		private void LoadShortcuts(MenuStrip menu)
		{
			// Keyboard shortcuts
			foreach (var (path, shortcut) in GetAllShortcuts(menu))
			{
				var item = new ListViewItem(path);
				item.SubItems.Add(FormatShortcut(shortcut));
				_list.Items.Add(item);
			}

			// Spacer row
			_list.Items.Add(new ListViewItem(""));

			// Mouse Controls header
			var header = new ListViewItem("Mouse Controls");
			header.Font = new Font(_list.Font, FontStyle.Bold);
			_list.Items.Add(header);

			// Mouse controls
			AddMouse("Left Click", "Paint tile / Select");
			AddMouse("Right Click", "Erase tile");
			AddMouse("Middle Click", "Bucket fill");
			AddMouse("Scroll Wheel", "Scroll layers / tileset");
			AddMouse("Left Drag", "Paint continuously");
			AddMouse("Right Drag", "Erase continuously");
			AddMouse("3D View", "Orbit / Pan / Zoom");
		}

		private void AddMouse(string action, string description)
		{
			var item = new ListViewItem(action);
			item.SubItems.Add(description);
			_list.Items.Add(item);
		}

		private static string FormatShortcut(Keys keys)
		{
			return keys.ToString().Replace(", ", " + ");
		}

		private static IEnumerable<(string Path, Keys Shortcut)> GetAllShortcuts(MenuStrip menu)
		{
			foreach (ToolStripMenuItem item in menu.Items)
				foreach (var entry in Walk(item, item.Text))
					yield return entry;
		}

		private static IEnumerable<(string Path, Keys Shortcut)> Walk(ToolStripMenuItem item, string path)
		{
			if (item.ShortcutKeys != Keys.None)
				yield return (path, item.ShortcutKeys);

			foreach (var child in item.DropDownItems.OfType<ToolStripMenuItem>())
				foreach (var entry in Walk(child, $"{path} → {child.Text}"))
					yield return entry;
		}
	}
}
