using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Globalization;

namespace SlideDiscWPF
{
	[Flags]
	public enum AdvanceFlags
	{
		None = 0,
		NextFolder = 1,
		Wrap = 2
	}

	public class FolderTreeEnumerator
	{
		public FolderTreeEnumerator()
		{
		}

		#region Properties

		public string[] RootDirectories
		{
			get
			{
				return (string[])fRootDirectories.Clone();
			}
			set
			{
                List<string> rootDirectories = new List<string>();
				for (int i = 0; i < value.Length; ++i)
				{
                    if (!string.IsNullOrEmpty(value[i]))
                    {
                        if (value[i][value[i].Length - 1] == '\\')
                        {
                            rootDirectories.Add(value[i].Substring(0, fRootDirectories[i].Length - 1));
                        }
                        else
                        {
                            rootDirectories.Add(value[i]);
                        }
                    }
				}
                fRootDirectories = rootDirectories.ToArray();
				Reset();
			}
		}

		public string[] IncludeExtensions
		{
			get
			{
				int count = fIncludeExtensions.Count;
				string[] extensions = new string[count];
				int i=0;
				foreach(string ext in fIncludeExtensions.Keys)
				{
					extensions[i] = ext.ToLower();
					++i;
				}
				return extensions;
			}
			set
			{
				fIncludeExtensions.Clear();
				if (value == null) return;
				foreach(string ext in value)
				{
					fIncludeExtensions[ext] = true;
				}
			}
		}

		public string Current
		{
			get { return fCurrent; }
		}

		#endregion

		#region Methods

		public void Reset()
		{
			fCurrentFileIndex = -1;
			fCurrentFiles = null;
			fCurrentDirectoryList = null;
			fDirectoryStack.Clear();
			fCurrent = null;
		}

		public bool MoveNext()
		{
			return MoveNext(AdvanceFlags.None);
		}

		public bool MoveNext(AdvanceFlags flags)
		{
			// If before beginning or after end
			if (fCurrentDirectoryList == null)
			{
				if (fRootDirectories == null || fRootDirectories.Length == 0)
				{
					return false;
				}

				if (fCurrentFileIndex >= 0 && 0 != (flags & AdvanceFlags.Wrap))
				{
					Reset();
				}

				// if before beginning, load up the first lists
				if (fCurrentFileIndex < 0)
				{
					fDirectoryStack.Clear();
					fCurrentDirectoryList = new DirectoryList(fRootDirectories, fIncludeExtensions.Keys);
					fCurrentFiles = fCurrentDirectoryList.GetCurrentFiles();
					fCurrentFileIndex = 0;
				}

				// Else, after end
				else
				{
					return false;
				}
			}

			// Else, if Advance to next folder
			else if ((flags & AdvanceFlags.NextFolder) != 0)
			{
				fCurrentFileIndex = int.MaxValue;
			}

			// Else, advance image
			else
			{
				++fCurrentFileIndex;
			}

			// Keep trying to load up an image until success or end is reached
			for (; ; )
			{
				// Get next file if available
				if (fCurrentFiles != null && fCurrentFileIndex < fCurrentFiles.Length)
				{
					fCurrent = fCurrentFiles[fCurrentFileIndex];
					return true;
				}

				// Advance folder
				else
				{
					fCurrentFiles = null;
					fCurrentFileIndex = 0;

					// Load up the subFolders
					DirectoryList directoryList = fCurrentDirectoryList.GetSubDirectoryList();
					if (directoryList.Directories.Length != 0)
					{
						fDirectoryStack.Push(fCurrentDirectoryList);
						fCurrentDirectoryList = directoryList;
					}

					// Else, move to the next peer folder
					else
					{
						++fCurrentDirectoryList.CurrentIndex;

						// If all folders are used up, pop levels off the stack
						if (fCurrentDirectoryList.CurrentIndex >= fCurrentDirectoryList.Directories.Length)
						{
							fCurrentDirectoryList = null;
							while (fDirectoryStack.Count > 0)
							{
								fCurrentDirectoryList = fDirectoryStack.Pop();
								++fCurrentDirectoryList.CurrentIndex;
								if (fCurrentDirectoryList.CurrentIndex < fCurrentDirectoryList.Directories.Length) break;
								fCurrentDirectoryList = null;
							}
						}
					}

					// We've popped the entire stack. At the end.
					if (fCurrentDirectoryList == null)
					{
						if (0 == (flags & AdvanceFlags.Wrap))
						{
							fCurrentFileIndex = 0;
							fCurrentFiles = null;
							fCurrentDirectoryList = null;
							fDirectoryStack.Clear();
							fCurrent = null;
							return false;
						}
						else
						{
							// Wrap back to the beginning
							fCurrentDirectoryList = new DirectoryList(fRootDirectories, fIncludeExtensions.Keys);
							fCurrentFileIndex = 0;
						}
					}

					// Get the files from the current folder
					fCurrentFiles = fCurrentDirectoryList.GetCurrentFiles();
				}
			} // End of retry loop
		}

		public bool MovePrev()
		{
			return MovePrev(AdvanceFlags.None);
		}

		public bool MovePrev(AdvanceFlags flags)
		{
			// If before beginning or after end
			if (fCurrentDirectoryList == null)
			{
				if (fRootDirectories == null || fRootDirectories.Length == 0)
				{
					return false;
				}

				// if before beginning, load up the first lists
				if (fCurrentFileIndex < 0)
				{
					return false;
				}

				// Else, after end
				else
				{
					// Load up the stack
					fDirectoryStack.Clear();
					fCurrentDirectoryList = new DirectoryList(fRootDirectories, fIncludeExtensions.Keys);
					for (;;)
					{
						DirectoryList directoryList = fCurrentDirectoryList.GetSubDirectoryList();
						if (directoryList.Directories.Length == 0) break;
						fDirectoryStack.Push(fCurrentDirectoryList);
						fCurrentDirectoryList = directoryList;
						fCurrentDirectoryList.CurrentIndex = fCurrentDirectoryList.Directories.Length - 1;
					}
					fCurrentFiles = fCurrentDirectoryList.GetCurrentFiles();
					fCurrentFileIndex = fCurrentFiles.Length-1;
				}
			}

			// Else, if advance to previous folder
			else if ((flags & AdvanceFlags.NextFolder) != 0)
			{
				fCurrentFileIndex = -1;
			}

			// Else, advance to previous image
			else
			{
				--fCurrentFileIndex;
			}

			// Keep trying to load up an image until success or beginning is reached
			for (; ; )
			{
				// Get current file if available
				if (fCurrentFiles != null && fCurrentFiles.Length > 0 && fCurrentFileIndex >= 0)
				{
					fCurrent = fCurrentFiles[fCurrentFileIndex];
					return true;
				}

				// Move to previous folder
				else
				{
					fCurrentFiles = null;
					fCurrentFileIndex = 0;

					// Pop the stack if necessary
					if (fCurrentDirectoryList.CurrentIndex == 0)
					{
						// We're at the very beginning. Cannot go further.
						if (fDirectoryStack.Count <= 0)
						{
							fCurrentFileIndex = -1;
							fCurrentFiles = null;
							fCurrentDirectoryList = null;
							fCurrent = null;
							return false;
						}

						// Pop one directory
						fCurrentDirectoryList = fDirectoryStack.Pop();
					}

					else
					{
						// Move to the previous peer folder
						--fCurrentDirectoryList.CurrentIndex;

						// Load up all subfolders
						for (; ; )
						{
							DirectoryList directoryList = fCurrentDirectoryList.GetSubDirectoryList();
							if (directoryList.Directories.Length == 0) break;
							fDirectoryStack.Push(fCurrentDirectoryList);
							fCurrentDirectoryList = directoryList;
							fCurrentDirectoryList.CurrentIndex = fCurrentDirectoryList.Directories.Length - 1;
						}
					}

					// Load up all files
					fCurrentFiles = fCurrentDirectoryList.GetCurrentFiles();
					fCurrentFileIndex = fCurrentFiles.Length - 1;
				}
			} // End of retry loop
		}

		public bool SetCurrent(string path)
		{
			// Local versions of tracking variables
			DirectoryList directoryList = new DirectoryList(fRootDirectories, fIncludeExtensions.Keys);
			Stack<DirectoryList> directoryStack = new Stack<DirectoryList>();
			string[] currentFiles = null;
			int currentFileIndex = 0;

			if (path == null || path.Length == 0)
			{
				return false;
			}

			int lastSlash = path.LastIndexOf('\\');
			int matchLength = -1;
			for (; ; )
			{
				if (directoryList.Directories.Length == 0)
				{
					return false;
				}

				while (directoryList.CurrentIndex < directoryList.Directories.Length)
				{
					string str = directoryList.Directories[directoryList.CurrentIndex];
					if (path.Length > str.Length && path.StartsWith(str, StringComparison.OrdinalIgnoreCase) && path[str.Length] == '\\')
					{
						matchLength = str.Length;
						break;
					}
					++directoryList.CurrentIndex;
				}

				if (directoryList.CurrentIndex >= directoryList.Directories.Length)
				{
					return false;
				}

				if (matchLength >= lastSlash)
				{
					break;
				}

				directoryStack.Push(directoryList);
				directoryList = directoryList.GetSubDirectoryList();
			}

			currentFiles = directoryList.GetCurrentFiles();
			currentFileIndex = Array.BinarySearch(currentFiles, path, FileNameComparer.Value);
			if (currentFileIndex < 0)
			{
				return false;
			}

			// Found, set the local variables
			fDirectoryStack = directoryStack;
			fCurrentDirectoryList = directoryList;
			fCurrentFiles = currentFiles;
			fCurrentFileIndex = currentFileIndex;
			fCurrent = fCurrentFiles[fCurrentFileIndex];
			return true;
		}

		#endregion

		#region Fields

		private Dictionary<string, bool> fIncludeExtensions = new Dictionary<string, bool>();
		private string[] fRootDirectories = new string[0];
		private string fCurrent;
		private Stack<DirectoryList> fDirectoryStack = new Stack<DirectoryList>();
		private DirectoryList fCurrentDirectoryList = null;
		private string[] fCurrentFiles = null;
		private int fCurrentFileIndex = 0;

		#endregion

		#region private methods

		private static string[] PruneAndSortFileInfo(FileSystemInfo[] fileInfos, ICollection<string> includeExtensions)
		{
			List<string> list = new List<string>();
			foreach (FileSystemInfo info in fileInfos)
			{
				if (0 == (info.Attributes & (FileAttributes.Hidden | FileAttributes.System))
					&& (includeExtensions == null || includeExtensions.Contains(info.Extension.ToLower())))
				{
					list.Add(info.FullName);
				}
			}
			list.Sort(FileNameComparer.Value);
			return list.ToArray();
		}

		private static string[] PruneAndSortDirectories(string path)
		{
			try
			{
				DirectoryInfo directoryInfo = new DirectoryInfo(path);
				return PruneAndSortFileInfo(directoryInfo.GetDirectories(), null);
			}
			catch (Exception err)
			{
				Debug.WriteLine(err.ToString());
				return new string[0];
			}
		}

		private static string[] PruneAndSortFiles(string path, ICollection<string> includeExtensions)
		{
			try
			{
				DirectoryInfo directoryInfo = new DirectoryInfo(path);
				return PruneAndSortFileInfo(directoryInfo.GetFiles(), includeExtensions);
			}
			catch (Exception err)
			{
				Debug.WriteLine(err.ToString());
				return new string[0];
			}
		}

		#endregion

		#region private classes

		private class FileNameComparer : IComparer<string>
		{
			#region IComparer Members

			public int Compare(string x, string y)
			{
				if (x == null)
				{
					if (y == null)
					{
						return 0;
					}
					else
					{
						return -1;
					}
				}
				else if (y == null)
				{
					return 1;
				}
				else
				{
					string strX = x.ToString().ToLower(CultureInfo.InvariantCulture);
					string strY = y.ToString().ToLower(CultureInfo.InvariantCulture);
					return string.CompareOrdinal(strX, strY);
				}
			}

			#endregion

			public static readonly FileNameComparer Value = new FileNameComparer();
		}

		// Used to manage directory traversal and pushed onto the stack
		private class DirectoryList
		{
			private string[] fDirectories;
			private int fCurrentIndex;
			private ICollection<string> fIncludeExtensions;

			public DirectoryList()
			{
				fDirectories = new string[0];
				CurrentIndex = 0;
				fIncludeExtensions = null;
			}

			public DirectoryList(string path, ICollection<string> includeExtensions)
			{
				fDirectories = PruneAndSortDirectories(path);
				CurrentIndex = 0;
				fIncludeExtensions = includeExtensions;
			}

			public DirectoryList(string[] paths, ICollection<string> includeExtensions)
			{
				fDirectories = (string[])paths.Clone();
				CurrentIndex = 0;
				fIncludeExtensions = includeExtensions;
			}

			public string[] GetCurrentFiles()
			{
				if (CurrentIndex >= 0 && CurrentIndex < fDirectories.Length)
				{
					return PruneAndSortFiles(fDirectories[CurrentIndex], fIncludeExtensions);
				}
				else
				{
					return new string[0];
				}
			}

			public DirectoryList GetSubDirectoryList()
			{
				if (CurrentIndex >= 0 && CurrentIndex < fDirectories.Length)
				{
					return new DirectoryList(PruneAndSortDirectories(fDirectories[CurrentIndex]), fIncludeExtensions);
				}
				else
				{
					return new DirectoryList();
				}
			}

			public string[] Directories
			{
				get
				{
					return fDirectories;
				}
			}

			public int CurrentIndex
			{
				get { return fCurrentIndex; }
				set { fCurrentIndex = value; }
			}
		}

		#endregion

		#region Tests

#if DEBUG
		static int CompareFileInfo(FileInfo x, FileInfo y)
		{
			return string.CompareOrdinal(x.Name.ToLower(), y.Name.ToLower());
		}

		static void TestDirectory(DirectoryInfo dir, FolderTreeEnumerator enumerator)
		{
			List<FileInfo> files = new List<FileInfo>(dir.GetFiles());
			files.Sort(CompareFileInfo);
			foreach (FileInfo file in files)
			{
				if (0 == (file.Attributes & (FileAttributes.Hidden|FileAttributes.System)) && (file.Extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) || file.Extension.Equals(".doc", StringComparison.OrdinalIgnoreCase)))
				{
					Trace.WriteLine(enumerator.Current);
					if (file.FullName != enumerator.Current)
					{
						Trace.Fail(string.Format("{0} != {1}", file.FullName, enumerator.Current));
					}
					enumerator.MoveNext();
					enumerator.MovePrev();
					if (file.FullName != enumerator.Current)
					{
						Trace.Fail(string.Format("{0} != {1} (fwd/bck)", file.FullName, enumerator.Current));
					}
					enumerator.MoveNext();
				}
			}

			foreach(DirectoryInfo subdir in dir.GetDirectories())
			{
				if (0 == (subdir.Attributes & (FileAttributes.Hidden|FileAttributes.System)))
				{
					TestDirectory(subdir, enumerator);
				}
			}
		}

		static void TestDirectorySkip(DirectoryInfo dir, FolderTreeEnumerator enumerator)
		{
			FileInfo firstFile = null;
			FileInfo lastFile = null;
			List<FileInfo> files = new List<FileInfo>(dir.GetFiles());
			files.Sort(CompareFileInfo);
			foreach (FileInfo file in files)
			{
				if (0 == (file.Attributes & (FileAttributes.Hidden | FileAttributes.System)) && (file.Extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) || file.Extension.Equals(".doc", StringComparison.OrdinalIgnoreCase)))
				{
					if (firstFile == null)
					{
						firstFile = file;
					}
					lastFile = file;
				}
			}

			if (firstFile != null)
			{
				Trace.WriteLine(enumerator.Current);
				if (firstFile.FullName != enumerator.Current)
				{
					Trace.Fail(string.Format("{0} != {1}", firstFile.FullName, enumerator.Current));
				}
				enumerator.MoveNext(AdvanceFlags.NextFolder);
				enumerator.MovePrev(AdvanceFlags.NextFolder);
				if (lastFile.FullName != enumerator.Current)
				{
					Trace.Fail(string.Format("{0} != {1} (fwd/bck)", firstFile.FullName, enumerator.Current));
				}
				enumerator.MoveNext(AdvanceFlags.NextFolder);
			}

			foreach (DirectoryInfo subdir in dir.GetDirectories())
			{
				if (0 == (subdir.Attributes & (FileAttributes.Hidden | FileAttributes.System)))
				{
					TestDirectorySkip(subdir, enumerator);
				}
			}
		}

		public static void Test()
		{
			FolderTreeEnumerator enumerator = new FolderTreeEnumerator();
			enumerator.IncludeExtensions = new string[] {".jpg", ".doc"};
			enumerator.RootDirectories = new string[]
			{
				"C:\\Documents and Settings"
				//@"C:\Documents and Settings\brandt.redd\My Documents\Geocaches\Archive\TB Winnie's Wagon_files"
			};

			Trace.Assert(enumerator.MoveNext());
			TestDirectory(new DirectoryInfo(enumerator.RootDirectories[0]), enumerator);
			Trace.Assert(!enumerator.MoveNext());

			enumerator.Reset();
			Trace.Assert(enumerator.MoveNext());
			TestDirectorySkip(new DirectoryInfo(enumerator.RootDirectories[0]), enumerator);
			Trace.Assert(!enumerator.MoveNext());
		}

#endif

		#endregion
	}
}
