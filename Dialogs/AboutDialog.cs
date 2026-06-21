using System.Windows.Forms;

namespace Glyphborn.Mapper.Dialogs
{
	public class AboutDialog : Form
	{
		public AboutDialog()
		{
			Text = "About";
			Width = 400;
			Height = 300;
			StartPosition = FormStartPosition.CenterParent;
			FormBorderStyle = FormBorderStyle.FixedDialog;
			MaximizeBox = false;
			MinimizeBox = false;
			var label = new Label
			{
				Text = $"Mapper {VersionChecker.LOCAL_VERSION}\r\nWorld Authoring Tool\r\n\r\nDoItBetter Studio\r\nStarted: December 2025\r\n\r\nProprietary Software\r\nAll Rights Reserved\r\n",
				Dock = DockStyle.Fill,
				TextAlign = System.Drawing.ContentAlignment.MiddleCenter
			};
			Controls.Add(label);
		}
	}
}
