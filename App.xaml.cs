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
";

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
			{
				string[] commandLineArgs = Environment.GetCommandLineArgs();

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

			SlideShowWindow mainWindow = new SlideShowWindow();
			mainWindow.Height = 800;	// QQQ Maximize the window here
			mainWindow.Width = 800;
			mainWindow.LoadState();
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