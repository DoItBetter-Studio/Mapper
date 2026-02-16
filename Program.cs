using System;
using System.Windows.Forms;

namespace Glyphborn.Mapper
{
    internal static class Program
    {
		/// <summary>
		///  The main entry point for the application.
		/// </summary>
		[STAThread]
        static void Main()
        {
			VersionChecker.CheckForUpdatesAsync().GetAwaiter().GetResult();

			// To customize application configuration such as set high DPI settings or default font,
			// see https://aka.ms/applicationconfiguration.
			ApplicationConfiguration.Initialize();
            Application.Run(new MapperForm());
        }
	}
}