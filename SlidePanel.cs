using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;
using System.Diagnostics;
using System.Threading;
using System.Windows.Threading;
using System.Globalization;
using System.IO;

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

        // Property keys retrieved from https://msdn.microsoft.com/en-us/library/windows/desktop/dd561977(v=vs.85).aspx
        static WinShell.PROPERTYKEY s_pkTitle = new WinShell.PROPERTYKEY("F29F85E0-4FF9-1068-AB91-08002B27B3D9", 2); // System.Title
        static WinShell.PROPERTYKEY s_pkKeywords = new WinShell.PROPERTYKEY("F29F85E0-4FF9-1068-AB91-08002B27B3D9", 5); // System.Keywords
        static WinShell.PROPERTYKEY s_pkDateTaken = new WinShell.PROPERTYKEY("14B81DA1-0135-4D31-96D9-6CBFC9671A99", 36867); // System.Photo.DateTaken (used on .jpg)
        static WinShell.PROPERTYKEY s_pkDateEncoded = new WinShell.PROPERTYKEY("2E4B640D-5019-46D8-8881-55414CC5CAA0", 100); // System.Media.DateEncoded (used on .mp4)

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

        protected DateTime fDateTaken = DateTime.MinValue;
		protected Uri fUri;
		private PanelState fPanelState = PanelState.Init;
        private List<string> fTags;
        private bool fTagsChanged = false;
        private TextBlock fFlagsBlock;

        public SlidePanel(Uri uri)
        {
            fUri = uri;

            // Queue up the metadata load and the content load operations
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new EmptyDelegate(LoadMetadata));
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new EmptyDelegate(LoadContent));
        }

        protected virtual void LoadMetadata()
        {
            if (fUri == null) return;

            try
            {
                using (var ps = WinShell.PropertyStore.Open(fUri.LocalPath, false))
                {

                    // Get the date the photo or video was taken.
                    {
                        object dt = ps.GetValue(s_pkDateTaken);
                        if (dt == null)
                        {
                            dt = ps.GetValue(s_pkDateEncoded);
                        }
                        if (dt != null && dt is DateTime)
                        {
                            fDateTaken = ((DateTime)dt).ToLocalTime();
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

        public PanelState PanelState
		{
			get { return fPanelState; }
		}

		protected void SetPanelState(PanelState newState)
		{
			if (fPanelState == newState)
			{
				return;
			}
			PanelState oldState = fPanelState;
			fPanelState = newState;
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

        protected IEnumerable<string> Tags
        {
            get
            {
                return fTags;
            }
            set
            {
                fTags = new List<string>(value);
                fTagsChanged = false;
            }
        }

        public void ToggleTag(string tag)
        {
            if (fTags == null)
            {
                fTags = new List<string>();
            }
            if (!fTags.Remove(tag))
            {
                fTags.Add(tag);
            }
            fTagsChanged = true;
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

            // Save any tags
            if (fTagsChanged)
            {
                SaveTags();
                fTagsChanged = false;
            }
		}

		public Uri Uri
		{
			get { return fUri; }
		}

		protected void AddMetadata()
		{
			StringBuilder builder = new StringBuilder();

			if (fUri != null)
			{
				string path = fUri.OriginalString;

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

			if (fDateTaken != DateTime.MinValue)
			{
				builder.Append("\r\n");
				builder.Append(fDateTaken.ToString("ddd, d MMM yyyy, h:mm tt"));
			}

			if (builder.Length > 0)
			{
                TextBlock txtblk = new TextBlock();
				txtblk.BeginInit();
				txtblk.Text = builder.ToString();
				txtblk.FontSize = cMetadataFontSize;
				txtblk.Foreground = cMetadataColor;
                txtblk.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
                txtblk.VerticalAlignment = System.Windows.VerticalAlignment.Bottom;
				txtblk.EndInit();
				Children.Add(txtblk);
			}

            UpdateTagDisplay();
		}

		protected void UpdateTagDisplay()
		{
			if (fFlagsBlock != null)
            {
                Children.Remove(fFlagsBlock);
                fFlagsBlock = null;
            }

			if (fTags != null && fTags.Count > 0)
			{
				fFlagsBlock = new TextBlock();
                fFlagsBlock.BeginInit();
                fFlagsBlock.Text = string.Join("\r\n", fTags);
                fFlagsBlock.FontSize = cTagsFontSize;
                fFlagsBlock.Foreground = cTagsColor;
                fFlagsBlock.FontWeight = FontWeights.Bold;
                fFlagsBlock.HorizontalAlignment = HorizontalAlignment.Right;
                fFlagsBlock.VerticalAlignment = VerticalAlignment.Top;
                fFlagsBlock.EndInit();
                Children.Add(fFlagsBlock);
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

		BitmapSource fBitmap;
		Exception fLoadError;

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
                image.UriSource = fUri;
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
                fBitmap = image;
			}
			catch (Exception err)
			{
				Debug.WriteLine(err.ToString());
				fLoadError = err;
				fDateTaken = DateTime.MinValue;
				fBitmap = null;
			}

			Dispatcher.BeginInvoke(DispatcherPriority.Input, new EmptyDelegate(OnLoadComplete));
		}

		private void OnLoadComplete()
		{
			try
			{
				if (fBitmap != null)
				{
					Image image = new Image();
					image.BeginInit();
					image.Source = fBitmap;
					image.Stretch = Stretch.Uniform;
					image.EndInit();
					Children.Add(image);
				}
				else
				{
					string message = (fLoadError != null)
						? string.Concat("Failed to load image:\r\n", fLoadError.Message)
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

				AddMetadata();

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

        public override void SaveTags()
        {
            // Save the tags on the background thread (depending on image size and I/O performance it can take a bit)
			sDecoderDispatcher.BeginInvoke(DispatcherPriority.Normal, new EmptyDelegate(BackgroundSaveTags));
        }

        // Size of metadata padding to add
        const uint cMetadataPadding = 1024;

        private void BackgroundSaveTags()
        {
            /*
            try
            {
                if (!fUri.IsFile) throw new ApplicationException("Attempt to save tags to bitmap identified by a non-file URL");
                Debug.WriteLine("Saving metadata to {0}", fUri.LocalPath, 0);
                using (FileStream bitmapStream = new FileStream(fUri.LocalPath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
                {
                    BitmapDecoder decoder = BitmapDecoder.Create(bitmapStream, BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnDemand);
                    if (!decoder.CodecInfo.FileExtensions.Contains("jpg")) throw new ApplicationException("Saving metadata: Not a JPEG file.");
                    if (decoder.Frames.Count > 0 && decoder.Frames[0].Metadata != null)
                    {
                        // Attempt to save in-place, if that fails then make space
                        fBitmap = decoder.Frames[0];
                        InPlaceBitmapMetadataWriter metadataWriter = fBitmap.CreateInPlaceBitmapMetadataWriter();
                        string[] tags = new List<string>(Tags).ToArray(); // Not terribly efficient but it does the job.
                        metadataWriter.SetQuery("System.Keywords", tags);
                        if (metadataWriter.TrySave())
                        {
                            Debug.WriteLine("In-place metadata update succeeded.");
                            return;
                        }
                    }
                }

                // In-place write failed. We'll have to do it the hard way
                string outputFilename = string.Concat(
                    Path.Combine(Path.GetDirectoryName(fUri.LocalPath), Path.GetFileNameWithoutExtension(fUri.LocalPath)),
                    " (Tagged ZZYZX)", Path.GetExtension(fUri.LocalPath));
                using (FileStream bitmapStream = new FileStream(fUri.LocalPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    BitmapDecoder input = BitmapDecoder.Create(bitmapStream, BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);
                    JpegBitmapEncoder output = new JpegBitmapEncoder();

                    // Clone any existing metadata. Else, create anew
                    BitmapMetadata metadata = null;
                    if (input.Frames[0].Metadata != null)
                    {
                        metadata = input.Frames[0].Metadata.Clone() as BitmapMetadata;
                    }
                    else
                    {
                        metadata = new BitmapMetadata("jpg");
                    }

                    // Add padding of three types (seems that all are required)
                    metadata.SetQuery("/app1/ifd/PaddingSchema:Padding", cMetadataPadding);
                    metadata.SetQuery("/app1/ifd/exif/PaddingSchema:Padding", cMetadataPadding);
                    metadata.SetQuery("/xmp/PaddingSchema:Padding", cMetadataPadding);

                    // Set the tags
                    string[] tags = new List<string>(Tags).ToArray(); // Not terribly efficient but it does the job.
                    metadata.SetQuery("System.Keywords", tags);

                    // Create the new JPEG
                    output.Frames.Add(BitmapFrame.Create(input.Frames[0], input.Frames[0].Thumbnail, metadata, input.Frames[0].ColorContexts));

                    // Write the new JPEG to a temporary file
                    using (FileStream outputStream = new FileStream(outputFilename, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                    {
                        output.Save(outputStream);
                    }
                } // Close input stream

                // Verify that file sizes are different, delete the old and rename the new.
                {
                    // Strange that the File class doesn't have a GetLength method
                    FileInfo inputInfo = new FileInfo(fUri.LocalPath);
                    FileInfo outputInfo = new FileInfo(outputFilename);
                    if (outputInfo.Length - inputInfo.Length < 3 * cMetadataPadding)
                    {
                        outputInfo.Delete();    // Delete the changed file
                        throw new ApplicationException("Padded file isn't long enough.");
                    }

                    inputInfo.Delete(); // Delete the original
                    File.Move(outputFilename, fUri.LocalPath);                       
                }

                Debug.WriteLine("Padded metadata and added tags.");

            }
            catch (Exception err)
            {
                Debug.WriteLine(err.ToString());
            }
            */
        }

	} // BitmapPanel

	class VideoPanel : SlidePanel
	{
		MediaElement fMedia;

		public VideoPanel(Uri videoUri)
			: base(videoUri)
		{
            Background = Brushes.Black;
        }

        protected override void LoadContent()
        {
			fMedia = new MediaElement();
			fMedia.BeginInit();
			fMedia.LoadedBehavior = MediaState.Manual;
			fMedia.Source = fUri;
			fMedia.Stretch = Stretch.Uniform;
			fMedia.IsMuted = true;
			fMedia.Pause();
            fMedia.MediaOpened += new RoutedEventHandler(fMedia_MediaOpened);
            fMedia.MediaFailed += new EventHandler<ExceptionRoutedEventArgs>(fMedia_MediaFailed);
			fMedia.BufferingEnded += new RoutedEventHandler(fMedia_BufferingEnded);
			fMedia.MediaEnded +=new RoutedEventHandler(fMedia_MediaEnded);
			fMedia.EndInit();

			Children.Add(fMedia);
			AddMetadata();
		}

        void fMedia_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            // Could add error report here.
            SetPanelState(PanelState.Completed);
        }

        void fMedia_MediaOpened(object sender, RoutedEventArgs e)
        {
            if (PanelState == PanelState.Init)
            {
                SetPanelState(PanelState.Ready);
            }
        }

		void fMedia_BufferingEnded(object sender, RoutedEventArgs e)
		{
			if (PanelState == PanelState.Init)
			{
				SetPanelState(PanelState.Ready);
			}
		}

		void fMedia_MediaEnded(object sender, RoutedEventArgs e)
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
                return (fMedia.IsLoaded) ? fMedia.NaturalDuration : base.Duration;
            }
        }

		public override void Play()
		{
			fMedia.Play();
			SetPanelState(fMedia.HasVideo ? PanelState.Playing : PanelState.Still);
		}

		public override void Stop()
		{
			fMedia.Stop();
            base.Stop();
		}

		public override void Pause()
		{
			if (fMedia.CanPause)
			{
				fMedia.Pause();
			}
			SetPanelState(PanelState.Paused);
		}

		public override void UnPause()
		{
			if (fMedia.CanPause)
			{
				fMedia.Play();

			}
			SetPanelState(PanelState.Playing);
		}

		public override bool IsMuted
		{
			get { return fMedia.IsMuted; }
			set { fMedia.IsMuted = value; }
		}
	}
			
} // Namespace
