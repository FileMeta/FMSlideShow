using System;
using System.Windows;
using System.Data;
using System.Xml;
using System.Configuration;
using System.Collections;
using System.Diagnostics;
using System.Threading;
using Microsoft.Win32;

namespace SlideDiscWPF
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		private const string cSyntax =
@"Syntax:
   SlideDiscWpf: [rootPath] [options]
   Options:
     /c              Configure screensaver (puts up message box) for compability with .scr screensavers.
     /s              Show screensaver for compability with .scr screensavers.
     /reg:<keyname>  Load configuration from the registry under the specified keyname
     /mreg:<keyname> Load configuration from the Local Machine portion of the registry under the specified keyname.";

		private const string cDefaultRegistryPath = @"Software\BCR\SlideDisc";
		private const string cRegMapDrive = @"MapDrive";
		private const string cMutexName = "SlideDiscWPF";

		private bool fIsScreenSaver;
		private Mutex fAppMutex;

		public App()
		{
			bool firstInstance;
			fAppMutex = new Mutex(true, cMutexName, out firstInstance);
			if (!firstInstance)
			{
				Debug.WriteLine("Instance already running.");
				Shutdown();
				return;
			}

			//InitializeComponent();

#if DEBUG && false
			FolderTreeEnumerator.Test();
#endif
			bool noShow = false;
			bool syntaxError = false;
			string rootPath = null;
			string registryPath = null;
			bool registryUseLocalMachine = false;
			{
				string[] commandLineArgs = Environment.GetCommandLineArgs();

				// Get options hidden in the name (This is the only way when using it as a screen saver)
				if (commandLineArgs.Length > 0)
				{
					string[] parts = commandLineArgs[0].Split(new char[] {'.'}, StringSplitOptions.RemoveEmptyEntries);
					foreach (string part in parts)
					{
						switch (part.ToLower())
						{
							case "lm":
								registryUseLocalMachine = true;
								break;
						}
					}
				}
				
				fIsScreenSaver = System.IO.Path.GetExtension(commandLineArgs[0]).Equals(".scr", StringComparison.OrdinalIgnoreCase);

				IEnumerator p = commandLineArgs.GetEnumerator();
				p.MoveNext(); // Skip exe name
				while (p.MoveNext())
				{
					string arg = p.Current as string;
					if (arg[0] == '-' || arg[0] == '/')
					{
						int colon = arg.IndexOf(':');

						string option;
						string value;
						if (colon >= 0)
						{
							option = arg.Substring(1, colon - 1).ToLower();
							value = arg.Substring(colon + 1);
						}
						else
						{
							option = arg.Substring(1).ToLower();
							value = null;
						}

						switch (option)
						{
							case "c": // Screen saver configuration
								MessageBox.Show("Right-click on the screen saver while it's running to change configuration.");
								noShow = true;
								break;

							case "s": // Screen saver show
								break;	// do nothing

							case "reg":
								registryPath = value;
								registryUseLocalMachine = false;
								break;

							case "mreg":
								registryPath = value;
								registryUseLocalMachine = true;
								break;

							default:
								syntaxError = true;
								noShow = true;
								break;
						}
					}
					else if (rootPath == null)
					{
						rootPath = arg;
					}
					else
					{
						syntaxError = true;
						noShow = true;
					}
				}
			}

			if (syntaxError)
			{
				MessageBox.Show(cSyntax);
			}
			if (noShow)
			{
				return;
			}

			if (registryPath != null)
			{
				registryPath = registryPath.Trim('/', '\\');
				registryPath = registryPath.Replace('/', '_');
				registryPath = registryPath.Replace('\\', '_');
				registryPath = string.Concat(cDefaultRegistryPath, "\\", registryPath);
			}
			else
			{
				registryPath = cDefaultRegistryPath;
			}

			// Map a drive using the specified credentials if needed
			// Example MapDrive value in registry: \\axis\share&Y:&username&password
			try
			{
				RegistryKey key = (registryUseLocalMachine ? Registry.LocalMachine : Registry.CurrentUser).OpenSubKey(registryPath);
				if (key != null)
				{
					string fMapDrive = key.GetValue(cRegMapDrive, null) as string;
					if (fMapDrive != null)
					{
						string[] parts = fMapDrive.Split('&');
						string share = (parts.Length > 0) ? Uri.UnescapeDataString(parts[0]) : null;
						string drive = (parts.Length > 1) ? Uri.UnescapeDataString(parts[1]) : null;
						string username = (parts.Length > 2) ? Uri.UnescapeDataString(parts[2]) : null;
						string password = (parts.Length > 3) ? Uri.UnescapeDataString(parts[3]) : null;
						NetworkDriveMapper.MapDrive(share, drive, username, password);
					}
				}
			}
			catch (Exception err)
			{
				Debug.WriteLine(err);
			}

			SlideShowWindow mainWindow = new SlideShowWindow();
			mainWindow.Height = 800;	// QQQ Maximize the window here
			mainWindow.Width = 800;
			mainWindow.RegistryUseLocalMachine = registryUseLocalMachine;
			mainWindow.RegistryPath = registryPath;
			mainWindow.LoadStateFromRegistry();
			if (rootPath != null)
			{
				if (!string.Equals(mainWindow.RootPath, rootPath, StringComparison.OrdinalIgnoreCase))
				{
					mainWindow.SelectedDirectories = new string[] { rootPath };
				}
				mainWindow.RootPath = rootPath;
			}
			mainWindow.Show();
			mainWindow.Start();

			//Window otherWindow = new Window1();
			//otherWindow.Show();
		}

		protected override void OnExit(ExitEventArgs e)
		{
			base.OnExit(e);
			if (fAppMutex != null)
			{
				fAppMutex.Close();
				fAppMutex = null;
			}
		}

	}
}