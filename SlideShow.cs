using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Input;
using System.Diagnostics;
using Microsoft.Win32;

namespace SlideDiscWPF
{
	class SlideShow : Grid
	{
        /// <summary>
        /// Slideshow Settings
        /// </summary>
        public class Settings
        {
            List<string> m_paths = new List<string>();

            public string RootPath { get; set; }
            public IList<string> Paths { get { return m_paths; } }
            public int? AdvanceTime { get; set; } // In milliseconds
            public int? FadeTime { get; set; } // In milliseconds
            public bool? ShowMetadata { get; set; }
        }

        const string cAtBeginning = "Beginning of collection.";
		const string cNoImagesFound = "No Photos or Videos found!";
		static string[] sIncludeExtensions = new string[] { ".jpg", ".jpeg", ".avi", ".wmv", ".mpg", ".mpeg", ".mp4", ".mov" };

		static readonly TimeSpan cMinFadeTime = TimeSpan.FromSeconds(0.25);
        const int cMaxWaitForLoad = 30000; // In milliseconds
        const int cMessageDisplayPeriod = 1000; // In milliseconds
        const int cHideCursorAfter = 3000; // In milliseconds;
        static readonly Duration sLongVideoThreshold = new Duration(new TimeSpan(0, 0, 40)); // 40 Seconds
        const int cLongVideoLimit = 30000; // In milliseconds

		private delegate void TickHandler();

		System.Threading.Timer fTimer;
		TickHandler fTickHandler;
		int fTimeOfLastTick;
		bool fInTick;
		int fAutoAdvanceAt = 0;
        int fAutoAdvanceNextPeriod = 0;
        int fCursorDisplayedAt = 0;
        Point fLastMousePosition; // = 0,0
		bool fDelayed = false;	// AutoAdvanceIn set to a large value but not paused.
		bool fDirectionIsForward = true;
        int fClearMessageAt = 0;

		private SlidePanel fFormerPanel;
		private SlidePanel fActivePanel;
		private SlidePanel fNextPanel;
        private TextBlock fMessage;

		string fRootPath = string.Empty;
		private FolderTreeEnumerator fEnumerator = new FolderTreeEnumerator();

		#region Construction

		public SlideShow()
		{
			fEnumerator.IncludeExtensions = sIncludeExtensions;

			BeginInit();

			fFormerPanel = new TextPanel();
			fActivePanel = new TextPanel();
			fNextPanel = new TextPanel();
			fNextPanel.Opacity = 0;
			Children.Add(fFormerPanel);
			Children.Add(fActivePanel);
			Children.Add(fNextPanel);

			fTickHandler = new TickHandler(Tick);
			fTimer = new System.Threading.Timer(RawTick, null, 500, 500);

            EndInit();
		}

		#endregion // Construction

		#region Properties

		public string RootPath
		{
			get { return fRootPath; }
			set { fRootPath = value; }
		}

		int fAdvanceTime = 3000;
		public int AdvanceTime
		{
			get { return fAdvanceTime; }
			set { fAdvanceTime = value; }
		}

		int fDelayAdvanceTime = 15000;
		public int DelayAdvanceTime
		{
			get { return fDelayAdvanceTime; }
			set { fDelayAdvanceTime = value; }
		}

		// fFadeTime is a TimeSpan while FadeTime is in milliseconds
		TimeSpan fFadeTime = TimeSpan.FromSeconds(0.75);
		public int FadeTime
		{
			get { return (int)fFadeTime.TotalMilliseconds; }
			set { fFadeTime = TimeSpan.FromMilliseconds(value); }
		}

		bool fMuted = true;
		public bool IsMuted
		{
			get { return fMuted; }
			set
			{
				if (value != fMuted)
				{
					fMuted = value;
					fActivePanel.IsMuted = value;
				}
			}
		}

		public bool ToggleMuted()
		{
			IsMuted = !fMuted;
            Message(fMuted ? "Mute" : "Unmute");
			return fMuted;
		}

        bool fTruncateVideo = true;
        public bool TruncateVideo
        {
            get { return fTruncateVideo; }
            set
            {
                if (value != fTruncateVideo)
                {
                    fTruncateVideo = value;
                    if (fActivePanel.PanelState == PanelState.Playing)
                    {
                        if (fTruncateVideo)
                        {
                            AutoAdvanceIn(cLongVideoLimit);
                            Debug.WriteLine("Truncating video to {0} seconds.", cLongVideoLimit / 1000);
                        }
                        else
                        {
                            AutoAdvanceIn(-1);  // Allow to play to the end.
                            Debug.WriteLine("Allowing {0} second video to play to end.", fActivePanel.Duration.TimeSpan.TotalSeconds);
                        }
                    }
                }
            }
        }

        public bool ToggleTruncateVideo()
        {
            TruncateVideo = !fTruncateVideo;
            Message(fTruncateVideo ? "Truncate On" : "Truncate Off");
            return fTruncateVideo;
        }

        bool fShowMetadata = true;
        public bool ShowMetadata
        {
            get { return fShowMetadata; }
            set
            {
                if (fShowMetadata != value)
                {
                    fShowMetadata = value;
                    fFormerPanel.ShowMetadata = value;
                    fActivePanel.ShowMetadata = value;
                    fNextPanel.ShowMetadata = value;
                }
            }
        }

        public bool ToggleShowMetadata()
        {
            ShowMetadata = !fShowMetadata;
            Message(fShowMetadata ? "Metadata On" : "Metadata Off");
            return fShowMetadata;
        }

        public void ToggleTag(string tag)
        {
            if (fActivePanel != null)
            {
                fActivePanel.ToggleTag(tag);
            }
        }

		public string[] SelectedDirectories
		{
			get { return fEnumerator.RootDirectories; }
			set { fEnumerator.RootDirectories = value; }
		}

		#endregion // Properties

		#region // Public Methods

		public void PanelStateChanged(SlidePanel panel, PanelState oldState, PanelState newState)
		{
			Debug.WriteLine(string.Format("State changed from {0} to {1}", oldState, newState));
			// Must be the active panel to matter
			if (!Object.ReferenceEquals(panel, fActivePanel))
			{
				return;
			}

			switch(newState)
			{
				case PanelState.Still:
					if (oldState == PanelState.Paused)
					{
						Next();
					}
					else
					{
                        AutoAdvanceIn(fAutoAdvanceNextPeriod == 0 ? fAdvanceTime : fAutoAdvanceNextPeriod);
					}
					break;

				case PanelState.Playing:
                    if (fTruncateVideo && Duration.Compare(panel.Duration, sLongVideoThreshold) >= 0)
                    {
                        AutoAdvanceIn(cLongVideoLimit);
                        Debug.WriteLine("Truncating {0} second video.", panel.Duration.TimeSpan.TotalSeconds);
                    }
                    else
                    {
                        AutoAdvanceIn(-1);	// Clear auto-advance
                    }
					break;

				case PanelState.Paused:
					AutoAdvanceIn(-1);	// Clear auto-advance
					break;

				case PanelState.Completed:
					AutoAdvanceIn(fAdvanceTime);
					break;
			}
		}

		public void Next(bool delayAdvance)
		{
            if (fNextPanel == null || fNextPanel.PanelState == PanelState.Init)
            {
                AutoAdvanceIn(1, fDelayAdvanceTime);
            }
            else
            {
                Next();
                if (delayAdvance && fAutoAdvanceAt != 0)
                {
                    AutoAdvanceIn(fDelayAdvanceTime);
                }
            }
		}

		public void Prev(bool delayAdvance)
		{
			Prev();
			if (delayAdvance && fAutoAdvanceAt != 0)
			{
				AutoAdvanceIn(fDelayAdvanceTime);
			}
		}

		public void Pause()
		{
			fActivePanel.Pause();
		}

		public void UnPause()
		{
			fActivePanel.UnPause();
		}

		public void TogglePause()
		{
			if (fActivePanel.PanelState == PanelState.Paused)
			{
				fActivePanel.UnPause();
                Message("Play");
            }
			else if (fDelayed)
			{
				Next();
                Message("Play");
            }
			else
			{
				fActivePanel.Pause();
                Message("Pause");
			}
		}

		public void Start(int advanceCount)
		{
			if (advanceCount > 0)
			{
				EmptyDelegate nextDelegate = new EmptyDelegate(Next);
				while (advanceCount-- > 0)
				{
					Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, nextDelegate);
					Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, nextDelegate);
				}
			}
			else if (fActivePanel.PanelState == PanelState.Paused)
			{
				fActivePanel.UnPause();
			}
		}

		public void Start()
		{
			Start(2);
		}

        public string CurrentSlidePath
        {
            get
            {
                return fActivePanel.Uri?.OriginalString ?? string.Empty;
            }

            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    fEnumerator.SetCurrent(value);
                    fEnumerator.MovePrev();
                }

            }
        }

        public Settings GetSettings()
        {
            Settings value = new Settings();
            value.RootPath = fRootPath;
            foreach(var path in fEnumerator.RootDirectories)
            {
                value.Paths.Add(path);
            }

            // For now, we don't persist FadeTime, AdvanceTime, or ShowMetadata

            return value;
        }

        public void LoadSettings(Settings value)
        {
            fRootPath = value.RootPath;
            if (string.IsNullOrEmpty(fRootPath))
            {
                fRootPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            }

			if (value.Paths.Count > 0)
			{
				fEnumerator.RootDirectories = value.Paths.ToArray();
			}
			else
			{
				fEnumerator.RootDirectories = new string[] { fRootPath };
			}

            if (value.FadeTime.HasValue) FadeTime = value.FadeTime.Value;
            if (value.AdvanceTime.HasValue) AdvanceTime = value.AdvanceTime.Value;
            if (value.ShowMetadata.HasValue) ShowMetadata = value.ShowMetadata.Value;

		}

        public void Message(string msg)
        {
            if (fMessage != null)
            {
                Children.Remove(fMessage);
                fMessage = null;
            }
            if (!string.IsNullOrEmpty(msg))
            {
                TextBlock txtblk = new TextBlock();
                txtblk.BeginInit();
                txtblk.Text = msg;
                txtblk.FontSize = 28;
                txtblk.Foreground = Brushes.Crimson;
                txtblk.HorizontalAlignment = HorizontalAlignment.Left;
                txtblk.VerticalAlignment = VerticalAlignment.Bottom;
                txtblk.EndInit();
                Children.Add(txtblk);
                fMessage = txtblk;
                fClearMessageAt = Environment.TickCount + cMessageDisplayPeriod;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            Point fMousePos = e.GetPosition(this);
            double distance = Math.Abs(fMousePos.X - fLastMousePosition.X) + Math.Abs(fMousePos.Y - fLastMousePosition.Y);
            if (distance >= 2)
            {
                fLastMousePosition = fMousePos;
                if (fCursorDisplayedAt == 0)
                {
                    Cursor = null;
                }
                fCursorDisplayedAt = fTimeOfLastTick;
            }
            base.OnMouseMove(e);
        }

		#endregion

		#region Private Operations

		public void Next()
		{
            Children.Remove(fFormerPanel);
            fFormerPanel.BeginAnimation(Panel.OpacityProperty, null);	// Remove animation (not required but this assists the garbage collector)
            fFormerPanel.Done();    // Notify former panel that it is no longer in use.

			// Memory leak checking
			/*
			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect(5, GCCollectionMode.Forced);
			*/

        // Rotate the panels down
        fFormerPanel = fActivePanel;
			fActivePanel = fNextPanel;
			fNextPanel = null;

			// Stop the former panel
			fFormerPanel.Stop();

			// Animate the next panel in and play it
			if (fFadeTime >= cMinFadeTime)
			{
				DoubleAnimation animation = new DoubleAnimation(0.0, 1.0, fFadeTime);
				fActivePanel.BeginAnimation(Panel.OpacityProperty, animation, HandoffBehavior.SnapshotAndReplace);
			}
			else
			{
				fActivePanel.Opacity = 1.0;
			}
			fActivePanel.IsMuted = fMuted;
			fActivePanel.Play();

			if (!fDirectionIsForward)
			{
				fEnumerator.MoveNext();
				fEnumerator.MoveNext();
				fDirectionIsForward = true;
			}

			// Prep the next panel
			fEnumerator.MoveNext(AdvanceFlags.Wrap);
			string filename = fEnumerator.Current;
			if (string.IsNullOrEmpty(filename))
			{
				fNextPanel = new TextPanel(cNoImagesFound);
			}
			else
			{
				fNextPanel = SlidePanel.Load(new Uri(filename));
                fNextPanel.ShowMetadata = fShowMetadata;
            }
            fNextPanel.Opacity = 0.0;
			Children.Add(fNextPanel);
		}

		public void Prev()
		{
            if (fFormerPanel == null || fFormerPanel is TextPanel)
            {
                Message("At beginning.");
                return;
            }

            // Make sure we're ready for a reversal
            if (fFormerPanel.PanelState == PanelState.Init)
            {
                Message("Waiting for previous media to load...");
                return;
            }

			// Remove the prepped next panel
			Children.Remove(fNextPanel);
            fNextPanel.Done();    // Notify next panel that it is no longer in use.

            // Reverse-rotate the panels
            fNextPanel = fActivePanel;
			fActivePanel = fFormerPanel;
			fFormerPanel = null;

			// Stop the formerly active panel
			fNextPanel.Stop();

            // Make it transparent again (remove existing animation)
            fNextPanel.BeginAnimation(Panel.OpacityProperty, null, HandoffBehavior.SnapshotAndReplace);
            fNextPanel.Opacity = 0.0;
            Debug.Assert(fActivePanel.Opacity == 1.0); // Ensure that the new active panel is visible
            Debug.WriteLine("New Active Panel: " + fActivePanel.Uri);

			// Play the new active panel
			fActivePanel.IsMuted = fMuted;
			fActivePanel.Play();

			if (fDirectionIsForward)
			{
				fEnumerator.MovePrev();
				fEnumerator.MovePrev();
				fDirectionIsForward = false;
			}

			// Load up a replacement former panel
            if (!fEnumerator.MovePrev() || string.IsNullOrEmpty(fEnumerator.Current))
			{
                fFormerPanel = new TextPanel("At Beginning");
			}
			else
			{
				fFormerPanel = SlidePanel.Load(new Uri(fEnumerator.Current));
                fFormerPanel.ShowMetadata = fShowMetadata;
            }
            Debug.Assert(fFormerPanel.Opacity == 1.0);
			Children.Insert(0, fFormerPanel);
		}

        public void AutoAdvanceIn(int milliSeconds)
        {
            AutoAdvanceIn(milliSeconds, fAdvanceTime);
        }

		public void AutoAdvanceIn(int milliSeconds, int nextAdvancePeriod)
		{
			Debug.WriteLine(string.Format("AutoAdvanceIn: {0}", milliSeconds));
			fDelayed = (milliSeconds > fAdvanceTime);
			if (milliSeconds < 0)
			{
				fAutoAdvanceAt = 0;	// Clear auto-advance
				return;
			}
			unchecked
			{
				if (fInTick)
				{
					fAutoAdvanceAt = fTimeOfLastTick + milliSeconds;
				}
				else
				{
					fAutoAdvanceAt = Environment.TickCount + milliSeconds;
				}
				if (fAutoAdvanceAt == 0) fAutoAdvanceAt = 1;
			}
            fAutoAdvanceNextPeriod = nextAdvancePeriod;
		}

		private void RawTick(object state)
		{
            int tickCount = Environment.TickCount;
			fTimeOfLastTick = (tickCount != 0) ? tickCount : 1;
			Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, fTickHandler);
		}

		private void Tick()
		{
			fInTick = true;
			unchecked
			{
                // Hide the mouse cursor if it's been idle for a while
                if (fCursorDisplayedAt != 0 && fTimeOfLastTick - fCursorDisplayedAt > cHideCursorAfter)
                {
                    Cursor = Cursors.None;
                    fCursorDisplayedAt = 0;
                }

				if (fAutoAdvanceAt != 0)
				{
                    int timeSinceAutoAdvance = fTimeOfLastTick - fAutoAdvanceAt;
					// Subtract and compare to zero rather than just compare because of wrapping issues
                    if (timeSinceAutoAdvance > 0)
					{
                        if (fNextPanel.PanelState != PanelState.Init || timeSinceAutoAdvance > cMaxWaitForLoad)
                        {
                            fAutoAdvanceAt = 0;
                            if (fActivePanel.PanelState == PanelState.Paused)
                            {
                                Debug.WriteLine("AutoPlay");
                                fActivePanel.Play();
                            }
                            else
                            {
                                Debug.WriteLine("AutoAdvance");
                                Next();
                            }
                        }
                        else
                        {
                            Message("Waiting for media to load...");
                            Debug.WriteLine(string.Format("Waiting for next image to load: name={0}, tick={1}", fNextPanel.Uri, fTimeOfLastTick - fAutoAdvanceAt));
                        }
					}
				}

                if (fMessage != null && fTimeOfLastTick - fClearMessageAt > 0)
                {
                    Message(null);
                }
			}
			fInTick = false;
		}

#endregion // Private operations

    }



}
