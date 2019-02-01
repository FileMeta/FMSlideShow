using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Diagnostics;
using System.Threading;
using System.Windows.Threading;
using System.Globalization;
using FileMeta;
namespace SlideDiscWPF
{
	internal delegate void EmptyDelegate();

	enum PanelState : int
	{
		/// <summary>
		/// Initializing
		/// </summary>
		Init = 0,

		/// <summary>
		/// Ready to play or display
		/// </summary>
		Ready = 1,

		/// <summary>
		/// Displaying a still picture
		/// </summary>
		Still = 2,

		/// <summary>
		/// Playing a video or animation
		/// </summary>
		Playing = 3,

		/// <summary>
		/// Paused (still or video)
		/// </summary>
		Paused = 4,

		/// <summary>
		/// Video or animation completed
		/// </summary>
		Completed = 5
	}

	abstract class SlidePanel : Grid
	{

        #region Static Elements

        const string c_usCultureName = "en-US";
        const string c_usOverrideDateFormat = "ddd, d MMM yyyy, h:mm tt";

        // Property keys retrieved from https://msdn.microsoft.com/en-us/library/windows/desktop/dd561977(v=vs.85).aspx
        static WinShell.PROPERTYKEY s_pkTitle = new WinShell.PROPERTYKEY("F29F85E0-4FF9-1068-AB91-08002B27B3D9", 2); // System.Title
        static WinShell.PROPERTYKEY s_pkKeywords = new WinShell.PROPERTYKEY("F29F85E0-4FF9-1068-AB91-08002B27B3D9", 5); // System.Keywords
        static WinShell.PROPERTYKEY s_pkDateTaken = new WinShell.PROPERTYKEY("14B81DA1-0135-4D31-96D9-6CBFC9671A99", 36867); // System.Photo.DateTaken (used on .jpg)
        static WinShell.PROPERTYKEY s_pkDateEncoded = new WinShell.PROPERTYKEY("2E4B640D-5019-46D8-8881-55414CC5CAA0", 100); // System.Media.DateEncoded (used on .mp4)
        static WinShell.PROPERTYKEY s_pkComment = new WinShell.PROPERTYKEY("F29F85E0-4FF9-1068-AB91-08002B27B3D9", 6);

        protected const double cMetadataFontSize = 20;
        protected static readonly Brush cMetadataColor = Brushes.Chartreuse;
        protected const double cTagsFontSize = 28;
        protected static readonly Brush cTagsColor = Brushes.Crimson;

		static char[] sPathSeparators = new char[] { '\\', '/' };
        private static readonly Duration sZeroDuration = new Duration(TimeSpan.Zero);

		protected static Thread sDecoderThread;
		protected static Dispatcher sDecoderDispatcher;

		static SlidePanel()
		{
			Dispatcher.CurrentDispatcher.ShutdownStarted += new EventHandler(OnShutdownStarted);

			Debug.WriteLine("Starting decoder dispatcher.");
			sDecoderThread = new Thread(new ThreadStart(DecoderThreadMain));
			sDecoderThread.Start();
			/*
			while (sDecoderDispatcher == null)
			{
				Thread.Sleep(100);
			}
			Debug.WriteLine("Decoder thread is ready.");
			*/
		}

		static void OnShutdownStarted(object sender, EventArgs e)
		{
			Debug.WriteLine("Shutting down decoder dispatcher.");
			if (sDecoderDispatcher != null)
			{
				sDecoderDispatcher.BeginInvokeShutdown(DispatcherPriority.Normal);
			}
		}

		static void DecoderThreadMain()
		{
			sDecoderDispatcher = Dispatcher.CurrentDispatcher;
			Debug.WriteLine("Decoder dispatcher started.");
			Dispatcher.Run();
			Debug.WriteLine("Decoder dispatcher ended.");
		}

		public static SlidePanel Load(Uri mediaUri)
		{
			string ext = System.IO.Path.GetExtension(mediaUri.OriginalString);
			if (!ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
				&& !ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))	// Commented out for testing purposes
			{
				return new VideoPanel(mediaUri);
			}
			else
			{
				return new BitmapPanel(mediaUri);
			}

		}

        #endregion Static Elements

        #region Member Variables and Constructor

        protected Uri m_uri;
        protected bool m_showMetadata = true;
        private PanelState m_panelState = PanelState.Init;
        private TextBlock m_metadataBlock;
        private TextBlock m_flagsBlock;
        protected string m_dateTaken = null;
        private string m_title;
        private List<string> m_tags;
        private bool m_tagsChanged = false;

        public SlidePanel(Uri uri)
        {
            m_uri = uri;

            // Queue up the metadata load and the content load operations
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new EmptyDelegate(LoadMetadata));
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new EmptyDelegate(LoadContent));
        }

        #endregion Member Variables and Constructor

        protected virtual void LoadMetadata()
        {
            if (m_uri == null) return;

            try
            {
                using (var ps = WinShell.PropertyStore.Open(m_uri.LocalPath, false))
                {
                    // See if timezone and precision info are included
                    int precision = 0;
                    TimeZoneTag tz = null;
                    foreach(var pair in MetaTag.Extract(ps.GetValue(s_pkComment) as string))
                    {
                        if (pair.Key.Equals("datePrecision", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!int.TryParse(pair.Value, out precision))
                                precision = 0;
                        }

                        if (pair.Key.Equals("timezone", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!TimeZoneTag.TryParse(pair.Value, out tz))
                                tz = null;
                        }
                    }

                    // Get the date the photo or video was taken.
                    {
                        object date = ps.GetValue(s_pkDateTaken);
                        if (date == null)
                        {
                            date = ps.GetValue(s_pkDateEncoded);
                        }
                        if (date != null && date is DateTime)
                        {
                            var dt = new DateTag((DateTime)date, tz, precision);
                            dt = dt.ResolveTimeZone(TimeZoneInfo.Local);
                            var ci = CultureInfo.CurrentCulture;

                            // Use my preferred format if US English
                            string format = ci.Name.Equals(c_usCultureName, StringComparison.Ordinal)
                                ? c_usOverrideDateFormat
                                : ci.DateTimeFormat.FullDateTimePattern;

                            m_dateTaken = dt.ToString(format, ci);
                        }
                    }

                    var tags = new List<string>();

                    // Get title
                    string title = ps.GetValue(s_pkTitle) as string;
                    if (!string.IsNullOrEmpty(title))
                    {
                        tags.Add(title);
                    }

                    // Get keywords
                    var keywords = ps.GetValue(s_pkKeywords) as IEnumerable<string>;
                    if (keywords != null)
                    {
                        tags.AddRange(keywords);
                    }

                    if (tags.Count > 0)
                    {
                        Tags = tags;
                    }

                }
            }
            catch (Exception err)
            {
                Debug.WriteLine(err.ToString());
            }
        }

        protected virtual void LoadContent()
        {
            Debug.WriteLine("Default SlidePanel.LoadContent (not overrided)");
        }

        protected virtual void BackgroundSaveMetadata()
        {
            Debug.Assert(m_tagsChanged);
            if (m_uri == null) return;

            try
            {
                using (var ps = WinShell.PropertyStore.Open(m_uri.LocalPath, true))
                {
                    string[] keywords = (m_tags != null) ? m_tags.ToArray() : new string[0];
                    ps.SetValue(s_pkKeywords, keywords);
                    ps.Commit();
                }
            }
            catch (Exception err)
            {
                Debug.WriteLine(err.ToString());
            }
        }

        public PanelState PanelState
		{
			get { return m_panelState; }
		}

		protected void SetPanelState(PanelState newState)
		{
			if (m_panelState == newState)
			{
				return;
			}
			PanelState oldState = m_panelState;
			m_panelState = newState;
			SlideShow slideShow = Parent as SlideShow;
			if (slideShow != null)
			{
				slideShow.PanelStateChanged(this, oldState, newState);
			}
		}

		public virtual bool IsStill
		{
			get { return true; }
		}

		public virtual bool IsMuted
		{
			get { return true; }
			set { }
		}

        public bool ShowMetadata
        {
            get { return m_showMetadata; }
            set
            {
                if (m_showMetadata != value)
                {
                    m_showMetadata = value;
                    UpdateMetadata();
                }
            }
        }

        protected string Title
        {
            get { return m_title; }
            set { m_title = value; }
        }

        protected IEnumerable<string> Tags
        {
            get
            {
                return m_tags;
            }
            set
            {
                m_tags = new List<string>(value);
                m_tagsChanged = false;
            }
        }

        public void ToggleTag(string tag)
        {
            if (m_tags == null)
            {
                m_tags = new List<string>();
            }
            if (!m_tags.Remove(tag))
            {
                m_tags.Add(tag);
            }
            m_tagsChanged = true;
            UpdateTagDisplay();
        }

        public virtual void SaveTags()
        {
            // default is to do nothing
        }

        public virtual Duration Duration
        {
            get { return sZeroDuration; }
        }

		public virtual void Play()
		{
			// Default is to set state to still.
			SetPanelState(PanelState.Still);
		}

		public virtual void Pause()
		{
			// Default is to set state to pause.
			SetPanelState(PanelState.Paused);
		}

		public virtual void UnPause()
		{
			// Default is to set state to still
			SetPanelState(PanelState.Still);
		}

		public virtual void Stop()
		{
            // Default is to set state to ready
            SetPanelState(PanelState.Ready);
		}

        public virtual void Done()
        {
            // Save any tags on the background thread
            if (m_tagsChanged)
            {
                sDecoderDispatcher.BeginInvoke(DispatcherPriority.Background, new EmptyDelegate(BackgroundSaveMetadata));
            }
        }

        public Uri Uri
        {
            get { return m_uri; }
        }

		protected void UpdateMetadata()
		{
            // Remove any existing metadata block
            if (m_metadataBlock != null)
            {
                Children.Remove(m_metadataBlock);
                m_metadataBlock = null;
            }

            if (!m_showMetadata) return; // No metadata to be shown

            StringBuilder builder = new StringBuilder();

			if (m_uri != null)
			{
				string path = m_uri.OriginalString;

				SlideShow slideShow = Parent as SlideShow;
				if (slideShow != null)
				{
					string rootPath = slideShow.RootPath;
					if (path.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
					{
						path = path.Substring(rootPath.Length);
					}
				}

				string[] pathParts = path.Split(sPathSeparators, StringSplitOptions.RemoveEmptyEntries);

				foreach (string part in pathParts)
				{
					builder.Append("\r\n");
					builder.Append(part);
				}
			}

			if (m_dateTaken != null)
			{
				builder.Append("\r\n");
				builder.Append(m_dateTaken);
			}

			if (builder.Length > 0)
			{
                m_metadataBlock = new TextBlock();
                m_metadataBlock.BeginInit();
                m_metadataBlock.Text = builder.ToString();
                m_metadataBlock.FontSize = cMetadataFontSize;
                m_metadataBlock.Foreground = cMetadataColor;
                m_metadataBlock.HorizontalAlignment = HorizontalAlignment.Right;
                m_metadataBlock.VerticalAlignment = VerticalAlignment.Bottom;
                m_metadataBlock.TextAlignment = TextAlignment.Right;
                m_metadataBlock.EndInit();
				Children.Add(m_metadataBlock);
			}

            UpdateTagDisplay();
		}

		protected void UpdateTagDisplay()
		{
			if (m_flagsBlock != null)
            {
                Children.Remove(m_flagsBlock);
                m_flagsBlock = null;
            }

            if (!m_showMetadata) return; // No metadata to be shown

            string flagsText = m_title ?? string.Empty;
            if (m_tags != null && m_tags.Count > 0)
            {
                flagsText = string.Concat(flagsText, "\r\n", string.Join("\r\n", m_tags));
            }

            if (!string.IsNullOrEmpty(flagsText))
			{
				m_flagsBlock = new TextBlock();
                m_flagsBlock.BeginInit();
                m_flagsBlock.Text = flagsText;
                m_flagsBlock.FontSize = cTagsFontSize;
                m_flagsBlock.Foreground = cTagsColor;
                m_flagsBlock.FontWeight = FontWeights.Bold;
                m_flagsBlock.HorizontalAlignment = HorizontalAlignment.Right;
                m_flagsBlock.VerticalAlignment = VerticalAlignment.Top;
                m_flagsBlock.EndInit();
                Children.Add(m_flagsBlock);
			}
		}
	}

    class TextPanel : SlidePanel
	{
		public TextPanel()
			: base(null)
		{
			Background = Brushes.Black;
		}

		public TextPanel(string text)
			: base(null)
		{
			BeginInit();

			if (!string.IsNullOrEmpty(text))
			{
				Label label = new Label();
				label.BeginInit();
				label.Content = text;
				label.HorizontalContentAlignment = HorizontalAlignment.Center;
				label.VerticalContentAlignment = VerticalAlignment.Center;
				label.FontSize = 20;
				label.Foreground = Brushes.White;
				label.EndInit();
				Children.Add(label);
			}

			Background = Brushes.Black;
			EndInit();
            SetPanelState(PanelState.Ready);
		}
	}

	class BitmapPanel : SlidePanel
	{
		static System.Net.Cache.RequestCachePolicy sCachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);

        // Logical to pixel scaling factors
        static bool s_hasSf = false;
        static double s_sfX = 1.0;
        static double s_sfY = 1.0;

		BitmapSource m_bitmap;
		Exception m_loadError;

		public BitmapPanel(Uri bitmapUri)
			: base(bitmapUri)
		{
			Background = Brushes.Black;
		}

        protected override void LoadContent()
        {
            // Get scaling factor (must happen on the foreground thread)
            if (!s_hasSf)
            {
                PresentationSource ps = PresentationSource.FromVisual(this);
                if (ps != null)
                {
                    s_sfX = ps.CompositionTarget.TransformToDevice.M11;
                    s_sfY = ps.CompositionTarget.TransformToDevice.M22;
                    Debug.WriteLine("ScalingFactors: {0}, {1}", s_sfX, s_sfY);
                    s_hasSf = true;
                }
            }

			// Make the load occur on the background thread.
			sDecoderDispatcher.BeginInvoke(DispatcherPriority.Normal, new EmptyDelegate(BackgroundLoadBitmap));
        }

        private void BackgroundLoadBitmap()
		{
			try
			{
                Debug.WriteLine("ActualSize: {0} x {1}", ActualWidth, ActualHeight);
                Debug.WriteLine("PixelSize: {0} x {1}", ActualWidth * s_sfX, ActualHeight * s_sfY);

                // Load and decode the bitmap (this is where most of the time goes)
                // Earlier version used BitmapDecoder but that class leaves the file open and resources locked
                // BitmapImage frees the resources on EndInit if the decode size has been set.
                BitmapImage image = new BitmapImage();
                image.BeginInit();
                image.UriSource = m_uri;
                image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriCachePolicy = sCachePolicy;

                // A majority of the time we are rendering 4:3 aspect ratio images
                // on a 16:9 display. Regardless of whether the image is portrait or
                // landscape, setting the decode height will result in the ideal
                // resolution. On the rare occasion this isn't true such as a 
                // portrait-oriented display or a panoramic photo, we will get a
                // higher-resolution bitmap than necessary which will be scaled down
                // for the display. It will impact performance slightly (unnoticeably)
                // but display quality will be maintained.
                image.DecodePixelHeight = (int)(ActualHeight * s_sfY);

                image.EndInit();
                image.Freeze();
                m_bitmap = image;
			}
			catch (Exception err)
			{
				Debug.WriteLine(err.ToString());
				m_loadError = err;
				m_dateTaken = null;
				m_bitmap = null;
			}

			Dispatcher.BeginInvoke(DispatcherPriority.Input, new EmptyDelegate(OnLoadComplete));
		}

		private void OnLoadComplete()
		{
			try
			{
				if (m_bitmap != null)
				{
					Image image = new Image();
					image.BeginInit();
					image.Source = m_bitmap;
					image.Stretch = Stretch.Uniform;
					image.EndInit();
					Children.Add(image);
				}
				else
				{
					string message = (m_loadError != null)
						? string.Concat("Failed to load image:\r\n", m_loadError.Message)
						: "Failed to load image.";
					TextBlock label = new TextBlock();
					label.BeginInit();
					//label.Content = message;
					//label.HorizontalContentAlignment = HorizontalAlignment.Center;
					//label.VerticalContentAlignment = VerticalAlignment.Center;
					label.Text = message;
					label.FontSize = 20;
					label.Foreground = Brushes.White;
					label.TextWrapping = TextWrapping.Wrap;
					label.EndInit();
					Children.Add(label);
				}

				UpdateMetadata();

				// Don't change the state if already displayed.
				if (PanelState == PanelState.Init)
				{
					SetPanelState(PanelState.Ready);
				}
			}
			catch (Exception err)
			{
				Debug.WriteLine(err.ToString());
			}
		}

	} // BitmapPanel

	class VideoPanel : SlidePanel
	{
		MediaElement m_media;

		public VideoPanel(Uri videoUri)
			: base(videoUri)
		{
            Background = Brushes.Black;
        }

        protected override void LoadContent()
        {
            m_media = new MediaElement();
			m_media.BeginInit();
			m_media.LoadedBehavior = MediaState.Manual;
			m_media.Source = m_uri;
			m_media.Stretch = Stretch.Uniform;
			m_media.IsMuted = true;
			m_media.Pause();
            m_media.MediaOpened += new RoutedEventHandler(Media_MediaOpened);
            m_media.MediaFailed += new EventHandler<ExceptionRoutedEventArgs>(Media_MediaFailed);
			m_media.BufferingEnded += new RoutedEventHandler(Media_BufferingEnded);
			m_media.MediaEnded +=new RoutedEventHandler(Media_MediaEnded);
			m_media.EndInit();

			Children.Add(m_media);
			UpdateMetadata();
		}

        void Media_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            // Could add error report here.
            SetPanelState(PanelState.Completed);
        }

        void Media_MediaOpened(object sender, RoutedEventArgs e)
        {
            if (PanelState == PanelState.Init)
            {
                SetPanelState(PanelState.Ready);
            }
        }

		void Media_BufferingEnded(object sender, RoutedEventArgs e)
		{
			if (PanelState == PanelState.Init)
			{
				SetPanelState(PanelState.Ready);
			}
		}

		void Media_MediaEnded(object sender, RoutedEventArgs e)
		{
			SetPanelState(PanelState.Completed);
		}

		public override bool IsStill
		{
			get { return false; }
		}

        public override Duration Duration
        {
            get
            {
                return (m_media.IsLoaded) ? m_media.NaturalDuration : base.Duration;
            }
        }

		public override void Play()
		{
			m_media.Play();
			SetPanelState(m_media.HasVideo ? PanelState.Playing : PanelState.Still);
		}

		public override void Stop()
		{
			m_media.Stop();
            SetPanelState(PanelState.Ready);
        }

        public override void Pause()
		{
			if (m_media.CanPause)
			{
				m_media.Pause();
			}
			SetPanelState(PanelState.Paused);
		}

		public override void UnPause()
		{
			if (m_media.CanPause)
			{
				m_media.Play();

			}
			SetPanelState(PanelState.Playing);
		}

		public override bool IsMuted
		{
			get { return m_media.IsMuted; }
			set { m_media.IsMuted = value; }
		}
	}
			
} // Namespace
