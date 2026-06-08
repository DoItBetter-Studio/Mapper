using Glyphborn.Mapper.Editor;
using Glyphborn.Mapper.Tiles;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Glyphborn.Mapper.Controls
{
	public sealed class RoomPane : UserControl
	{
		private readonly EditorState _state;
		private readonly Action _onChanged;

		private RoomControl _roomControl;
		private TextBox _nameBox;
		private Panel _colorSwatch;
		private Button _deleteBtn;

		private bool _suppressNameUpdate;

		public RoomPane(EditorState state, Action onChanged)
		{
			_state = state;
			_onChanged = onChanged;

			Dock = DockStyle.Fill;
			BackColor = Color.FromArgb(30, 30, 30);

			Build();
			RefreshList();
		}

		private void Build()
		{
			// ── Header (mirrors TilesetPane exactly) ──────────────────────────────
			var header = new Panel
			{
				Dock = DockStyle.Top,
				Height = 60,
				BackColor = Color.FromArgb(20, 20, 20)
			};

			var titleLabel = new Label
			{
				Text = "Rooms",
				Dock = DockStyle.Top,
				Height = 30,
				Padding = new Padding(6),
				BackColor = Color.FromArgb(20, 20, 20),
				ForeColor = Color.White,
				Font = new Font("Segoe UI", 10, FontStyle.Bold)
			};

			var newBtn = new Button
			{
				Text = "+ New Room",
				Dock = DockStyle.Top,
				Height = 30,
				BackColor = Color.FromArgb(40, 120, 40),
				ForeColor = Color.White,
				FlatStyle = FlatStyle.Flat,
				Cursor = Cursors.Hand
			};
			newBtn.FlatAppearance.BorderSize = 0;
			newBtn.Click += OnNewRoom;

			header.Controls.Add(newBtn);
			header.Controls.Add(titleLabel);

			// ── Properties strip at bottom ────────────────────────────────────────
			_deleteBtn = new Button
			{
				Text = "Delete Room",
				Dock = DockStyle.Bottom,
				Height = 30,
				BackColor = Color.FromArgb(100, 30, 30),
				ForeColor = Color.White,
				FlatStyle = FlatStyle.Flat,
				Cursor = Cursors.Hand,
				Enabled = false
			};
			_deleteBtn.FlatAppearance.BorderSize = 0;
			_deleteBtn.Click += OnDeleteRoom;

			// Color row
			var colorRow = new Panel
			{
				Dock = DockStyle.Bottom,
				Height = 30,
				BackColor = Color.FromArgb(20, 20, 20)
			};

			var colorLabel = new Label
			{
				Text = "Color",
				Dock = DockStyle.Left,
				Width = 50,
				Padding = new Padding(6, 0, 0, 0),
				ForeColor = Color.FromArgb(180, 180, 180),
				Font = new Font("Segoe UI", 8f),
				TextAlign = ContentAlignment.MiddleLeft
			};

			_colorSwatch = new Panel
			{
				Dock = DockStyle.Fill,
				Cursor = Cursors.Hand,
				Enabled = false
			};
			_colorSwatch.Click += OnColorSwatchClick;

			colorRow.Controls.Add(_colorSwatch);
			colorRow.Controls.Add(colorLabel);

			// Name row
			var nameRow = new Panel
			{
				Dock = DockStyle.Bottom,
				Height = 30,
				BackColor = Color.FromArgb(20, 20, 20)
			};

			var nameLabel = new Label
			{
				Text = "Name",
				Dock = DockStyle.Left,
				Width = 50,
				Padding = new Padding(6, 0, 0, 0),
				ForeColor = Color.FromArgb(180, 180, 180),
				Font = new Font("Segoe UI", 8f),
				TextAlign = ContentAlignment.MiddleLeft
			};

			_nameBox = new TextBox
			{
				Dock = DockStyle.Fill,
				BackColor = Color.FromArgb(45, 45, 45),
				ForeColor = Color.White,
				BorderStyle = BorderStyle.None,
				Enabled = false
			};
			_nameBox.TextChanged += OnNameChanged;

			nameRow.Controls.Add(_nameBox);
			nameRow.Controls.Add(nameLabel);

			// ── Room list (fills remaining space) ────────────────────────────────
			var scroll = new Panel
			{
				Dock = DockStyle.Fill,
				AutoScroll = true,
				BackColor = Color.FromArgb(30, 30, 30)
			};

			_roomControl = new RoomControl { Dock = DockStyle.Top };
			_roomControl.RoomSelected += OnRoomSelected;
			scroll.Controls.Add(_roomControl);

			// ── Assemble — Bottom first, then Top, then Fill ──────────────────────
			Controls.Add(scroll);
			Controls.Add(_deleteBtn);
			Controls.Add(colorRow);
			Controls.Add(nameRow);
			Controls.Add(header);
		}

		// ── Room list ─────────────────────────────────────────────────────────────

		private void RefreshList()
		{
			var rooms = _state.Area?.Rooms;
			if (rooms == null) return;

			_roomControl.Refresh(rooms);
			_roomControl.Height = Math.Max(36, rooms.Count * 36);

			if (_state.CurrentRoomID != 0)
				_roomControl.SelectById(_state.CurrentRoomID);
		}

		// ── Events ────────────────────────────────────────────────────────────────

		private void OnRoomSelected(RoomDefinition? room)
		{
			bool has = room != null;

			_deleteBtn.Enabled = has;
			_nameBox.Enabled = has;
			_colorSwatch.Enabled = has;

			if (!has) return;

			_state.CurrentRoomID = room!.Id;

			_suppressNameUpdate = true;
			_nameBox.Text = room.Name;
			_suppressNameUpdate = false;

			_colorSwatch.BackColor = room.Color;

			_onChanged?.Invoke();
		}

		private void OnNameChanged(object? sender, EventArgs e)
		{
			if (_suppressNameUpdate) return;

			var room = _state.Area?.GetRoom(_state.CurrentRoomID);
			if (room == null) return;

			room.Name = _nameBox.Text;
			_roomControl.Refresh(_state.Area!.Rooms);
			_onChanged?.Invoke();
		}

		private void OnColorSwatchClick(object? sender, EventArgs e)
		{
			var room = _state.Area?.GetRoom(_state.CurrentRoomID);
			if (room == null) return;

			using var cd = new ColorDialog { Color = room.Color };
			if (cd.ShowDialog() != DialogResult.OK) return;

			room.Color = cd.Color;
			_colorSwatch.BackColor = cd.Color;
			_roomControl.Refresh(_state.Area!.Rooms);
			_onChanged?.Invoke();
		}

		private void OnNewRoom(object? sender, EventArgs e)
		{
			if (_state.Area == null) return;

			var room = _state.Area.CreateRoom();
			_state.CurrentRoomID = room.Id;

			RefreshList();
			_roomControl.SelectById(room.Id);
			_onChanged?.Invoke();

			_nameBox.Focus();
			_nameBox.SelectAll();
		}

		private void OnDeleteRoom(object? sender, EventArgs e)
		{
			var room = _state.Area?.GetRoom(_state.CurrentRoomID);
			if (room == null) return;

			var result = MessageBox.Show(
				$"Delete \"{room.Name}\" (#{room.Id})?\n\nTiles painted with this room ID will remain tagged but the room will no longer be named.",
				"Delete Room",
				MessageBoxButtons.YesNo,
				MessageBoxIcon.Warning);

			if (result != DialogResult.Yes) return;

			_state.Area!.DeleteRoom(_state.CurrentRoomID);
			_state.CurrentRoomID = 0;

			_nameBox.Text = string.Empty;
			_nameBox.Enabled = false;
			_colorSwatch.BackColor = Color.FromArgb(45, 45, 45);
			_colorSwatch.Enabled = false;
			_deleteBtn.Enabled = false;

			RefreshList();
			_onChanged?.Invoke();
		}
	}
}