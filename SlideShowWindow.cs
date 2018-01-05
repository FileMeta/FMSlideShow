using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Interop;


namespace SlideDiscWPF
{
	class SlideShowWindow : Window
	{
		private const int WM_SYSCOMMAND = 0x112;
		private const int SC_SCREENSAVE = 0xF140;
		private const int SC_MONITORPOWER = 0xF170;

        // Tag Strings
        private static readonly string sFavorite = "Favorite";
        private static readonly string sToShare = "ToShare";
        private static readonly string sNeedsWork = "NeedsWork";

		SlideShow fSlideShow;

		public SlideShowWindow()
		{
			BeginInit();
			fSlideShow = new SlideShow();
			Content = fSlideShow;
            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Maximized;
            ResizeMode = ResizeMode.NoResize;
            //Topmost = true; // Required to hide the taskbar on Windows 10
			WindowStartupLocation = WindowStartupLocation.CenterScreen;
			EndInit();
		}

		public string RootPath
		{
			get { return fSlideShow.RootPath; }
			set { fSlideShow.RootPath = value; }
		}

		public string RegistryPath
		{
			get { return fSlideShow.RegistryPath; }
			set { fSlideShow.RegistryPath = value; }
		}

		public bool RegistryUseLocalMachine
		{
			get { return fSlideShow.RegistryUseLocalMachine; }
			set { fSlideShow.RegistryUseLocalMachine = value; }
		}

		public string[] SelectedDirectories
		{
			get { return fSlideShow.SelectedDirectories; }
			set { fSlideShow.SelectedDirectories = value; }
		}

		public void LoadStateFromRegistry()
		{
			fSlideShow.LoadStateFromRegistry();
		}

		public void Start()
		{
			fSlideShow.Start(1);
		}

        /* Old version
		public void ShowConfigWindow()
		{
			ConfigWindow config = new ConfigWindow();
			config.Owner = this;
			config.SlideShow = fSlideShow;
			config.Show();
		}
        */


        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (KeyCommand(e.Key))
            {
                e.Handled = true;
            }
            else
            {
                base.OnKeyDown(e);
            }
        }

        public bool KeyCommand(Key key)
        {
            switch (key)
            { 
				case Key.Down:
				case Key.Right:
				case Key.NumPad9:
				case Key.D9:
				case Key.NumPad6:
				case Key.D6:
				case Key.NumPad3:
				case Key.D3:
                case Key.PageDown:
                case Key.OemPeriod: // period and greater-than
					fSlideShow.Next(true);
					break;

				case Key.Left:
				case Key.Up:
				case Key.NumPad7:
				case Key.D7:
				case Key.NumPad4:
				case Key.D4:
				case Key.NumPad1:
				case Key.D1:
                case Key.PageUp:
                case Key.OemComma: // comma and less-than
					fSlideShow.Prev(true);
					break;

				case Key.Space:
				case Key.P:
				case Key.NumPad8:
				case Key.D8:
				case Key.NumPad5:
				case Key.D5:
				case Key.NumPad2:
				case Key.D2:
					fSlideShow.TogglePause();
					break;

				case Key.M:
					fSlideShow.ToggleMuted();
					break;

                case Key.T:
                    fSlideShow.ToggleTruncateVideo();
                    break;

                case Key.S: // "Share"
                    fSlideShow.ToggleTag(sToShare);
                    break;

                case Key.F: // "Favorite"
                    fSlideShow.ToggleTag(sFavorite);
                    break;

                case Key.N: // "Needs Work"
                    fSlideShow.ToggleTag(sNeedsWork);
                    break;

				case Key.Escape:
					Close();
					break;

                case Key.Enter:
                    DoConfig();
                    break;
                    
				default:
                    return false; // Avoid e.Handled = true;
			}
            return true;
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
		{
			if (e.ChangedButton == MouseButton.Right)
			{
                DoContextMenu();
                //DoConfig();
			}
			else
			{
                fSlideShow.Next(true);
			}
		}

        private void DoContextMenu()
        {
            MainContextMenu menu = new MainContextMenu();
            menu.Owner = this;
            menu.ShowDialog();
        }

		protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
		{
			fSlideShow.SaveSelectionsToRegistry();
			fSlideShow.SaveCurrentSlideToRegistry();
			base.OnClosing(e);
		}

		protected override void OnSourceInitialized(EventArgs e)
		{
			base.OnSourceInitialized(e);
			HwndSource source = PresentationSource.FromVisual(this) as HwndSource;
			if (source != null)
			{
				source.AddHook(SuppressScreenSaverHook);
			}
		}

        SlideShowSelectorWindow fConfigWindow = null;
        private void DoConfig()
        {
            if (fConfigWindow != null)
            {
                if (fConfigWindow.IsVisible) return;    // Config window is already up
                fConfigWindow = null; // Let the old one go and create a new one
            }
            fConfigWindow = new SlideShowSelectorWindow();
            fConfigWindow.Owner = this;
            fConfigWindow.SlideShow = fSlideShow;
            fConfigWindow.Top = Height - fConfigWindow.Height-20;
            fConfigWindow.Left = 0;
            fConfigWindow.Show();
        }

		private static IntPtr SuppressScreenSaverHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
		{
			if (msg == WM_SYSCOMMAND)
			{
				long cmd = wParam.ToInt64() & 0xFFF0;
				if (cmd == SC_SCREENSAVE)
				{
					handled = true;
#if DEBUG
					System.Diagnostics.Trace.WriteLine("Screensaver.");
#endif
				}

				/* Uncomment to suppress monitor powerdown
				if (cmd == SC_MONITORPOWER)
				{
					handled = true;
				}
				*/
			}
			return IntPtr.Zero;
		}
	}
}
