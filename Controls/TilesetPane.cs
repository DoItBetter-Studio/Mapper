using System.Drawing;
using System.Windows.Forms;

using Glyphborn.Mapper.Editor;
using Glyphborn.Mapper.Tiles;

namespace Glyphborn.Mapper.Controls
{
	sealed class TilesetPane : Panel
	{
		private Tileset _tileset;
		private TilesetControl _tilesetControl;

		public TilesetPane(string name, byte tilesetIndex, Tileset tileset, EditorState state)
		{
			_tileset = tileset;

			Dock = DockStyle.Fill;
			BackColor = Color.FromArgb(30, 30, 30);

			// Header with tileset name
			var header = new Panel
			{
				Dock = DockStyle.Top,
				Height = 60,
				BackColor = Color.FromArgb(20, 20, 20)
			};

			var label = new Label
			{
				Text = name,
				Dock = DockStyle.Top,
				Height = 30,
				Padding = new Padding(6),
				BackColor = Color.FromArgb(20, 20, 20),
				ForeColor = Color.White,
				Font = new Font("Segoe UI", 10, FontStyle.Bold)
			};

			// Edit button
			var editButton = new Button
			{
				Text = "Edit Tileset",
				Dock = DockStyle.Top,
				Height = 30,
				BackColor = Color.FromArgb(40, 12, 180),
				ForeColor = Color.White,
				FlatStyle = FlatStyle.Flat,
				Cursor = Cursors.Hand
			};

			editButton.FlatAppearance.BorderSize = 0;
			editButton.Click += (s, e) => OpenTilesetEditor();

			header.Controls.Add(editButton);
			header.Controls.Add(label);

			var scroll = new Panel
			{
				Dock = DockStyle.Fill,
				AutoScroll = true
			};

			_tilesetControl = new TilesetControl
			{
				Tiles = tileset.Tiles.ToArray(),
				TilesetIndex = tilesetIndex,
				Width = 266,
				Dock = DockStyle.Top
			};

			_tilesetControl.TileSelected += sel =>
			{
				state.SelectedTile = sel;
			};

			_tilesetControl.Height = _tilesetControl.GetRequiredHeight();
			_tilesetControl.Invalidate();

			scroll.Controls.Add(_tilesetControl);
			Controls.Add(scroll);
			Controls.Add(header);
		}

		private void OpenTilesetEditor()
		{
			var dialog = new TilesetEditorDialog(_tileset);

			if (dialog.ShowDialog() == DialogResult.OK)
			{
				// Tileset was modifier, refresh the tile grid
				_tilesetControl.Tiles = _tileset.Tiles.ToArray();
				_tilesetControl.Height = _tilesetControl.GetRequiredHeight();
				_tilesetControl.Invalidate();
			}
		}
	}
}
