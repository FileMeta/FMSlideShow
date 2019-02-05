using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Diagnostics;
using System.IO;

namespace SlideDiscWPF
{
    /// <summary>
    /// Interaction logic for SlideShowSelectorWindow.xaml
    /// </summary>
    public partial class SlideShowSelectorWindow : Window
    {
        public SlideShowSelectorWindow()
        {
            InitializeComponent();
        }

        SlideShow fSlideShow;
        string fBookmark;
        internal SlideShow SlideShow
        {
            get { return fSlideShow; }
            set
            {
                fSlideShow = value;
                fFolderView.RootDirectory = fSlideShow.RootPath;
                fHeader.Text = fSlideShow.RootPath;
                fFolderView.SetPathsChecked(fSlideShow.SelectedDirectories);
                fBookmark = fSlideShow.CurrentSlidePath;
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            //Debug.WriteLine("SelectorWindow OnKeyDown({0})", e.Key, 0);
            switch (e.Key)
            {
                // If these made it here, the TreeView doesn't have focus
                case Key.Up:
                case Key.Down:
                case Key.Right:
                case Key.Left:
                    fFolderView.KbActivate();
                    e.Handled = true;
                    break;

                case Key.Enter:
                    SaveSelections();
                    Close();
                    e.Handled = true;
                    break;

                case Key.Escape:
                case Key.Back:
                    Close();
                    e.Handled = true;
                    break;

                default:
                    base.OnKeyDown(e);
                    break;
            }
        }

        private void fHeader_MouseDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void fExit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void fOk_Click(object sender, RoutedEventArgs e)
        {
            SaveSelections();
            Close();
        }

        private void SaveSelections()
        {
            fSlideShow.Pause();
            fSlideShow.SelectedDirectories = fFolderView.GetCheckedPaths();
            fSlideShow.CurrentSlidePath = fBookmark;
            Close();

            SlideShowWindow window = Parent as SlideShowWindow;
            if (window != null) window.SaveState();
            fSlideShow.Start(1);
        }
    }

    /*
     * Keyboard navigation has some serious issues in TreeView. Switching to an MVVM model
     * would help some of this (based on articles by Josh Smith on CodeProject.com) but
     * it would generate others. The fix is to make the checkbox non-focusable (so focus
     * stays with the TreeViewItem) and handle keyboard setting and clearing of the checkbox
     * in the OnKeyDown of the TreeViewItem.
     */ 
    public class FolderTreeView : TreeView
    {
        FolderTreeViewItem fRootItem;

        public FolderTreeView()
        {
            BeginInit();
            fRootItem = new FolderTreeViewItem();
            SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
            SetValue(KeyboardNavigation.DirectionalNavigationProperty, KeyboardNavigationMode.Cycle);
            Items.Add(fRootItem);
            EndInit();
        }

        public string RootDirectory
        {
            get { return fRootItem.FullPath; }
            set
            {
                fRootItem.FullPath = value;
                fRootItem.IsExpanded = true;
            }
        }

        public void SetPathsChecked(string[] paths)
        {
            foreach (string path in paths)
            {
                fRootItem.SetPathChecked(path);
            }
        }

        public void SetPathChecked(string path)
        {
            fRootItem.SetPathChecked(path);
        }

        public string[] GetCheckedPaths()
        {
            List<string> checkedPaths = new List<string>();
            fRootItem.LoadCheckedPaths(checkedPaths);
            return checkedPaths.ToArray();
        }
        
        public void KbActivate()
        {
            Focus();
            if (Items.Count > 0)
            {
                TreeViewItem tvm = Items[0] as TreeViewItem;
                if (tvm != null)
                {
                    tvm.IsSelected = true;
                }
            }
        }

        private class FolderTreeViewItem : TreeViewItem
        {
            private bool fPropogating = false;
            private bool fHasLoadedChildren = false;
            private string fFullPath;
            private CheckBox fCheckBox;

            public FolderTreeViewItem()
            {
                BeginInit();
                VerticalContentAlignment = System.Windows.VerticalAlignment.Center;
                fCheckBox = new CheckBox();
                fCheckBox.VerticalContentAlignment = System.Windows.VerticalAlignment.Center;
                fCheckBox.Focusable = false;
                Header = fCheckBox;
                fCheckBox.Checked += fCheckBox_CheckChanged;
                fCheckBox.Unchecked += fCheckBox_CheckChanged;
                EndInit();
            }

            public string FullPath
            {
                get { return fFullPath; }
                set
                {
                    if (fFullPath != null)
                    {
                        throw new ArgumentException("Cannot set Path more than once.");
                    }
                    IsExpanded = false;
                    fHasLoadedChildren = false;
                    fFullPath = value;
                    fCheckBox.Content = System.IO.Path.GetFileName(fFullPath);

                    // If this has a subfolder, add a placeholder item so that there's an expand control
                    if (HasSubfolder(fFullPath))
                    {
                        TreeViewItem placeholderItem = new TreeViewItem();
                        placeholderItem.IsEnabled = false;
                        Items.Add(placeholderItem);
                    }
                }
            }

            public bool SetPathChecked(string fullPath)
            {
                if (fullPath.Equals(fFullPath, StringComparison.OrdinalIgnoreCase))
                {
                    IsChecked = true;
                    return true;
                }
                if (fullPath.StartsWith(fFullPath, StringComparison.OrdinalIgnoreCase))
                {
                    if (!fHasLoadedChildren) LoadChildren();
                    foreach (object item in Items)
                    {
                        FolderTreeViewItem tvm = item as FolderTreeViewItem;
                        if (tvm != null)
                        {
                            if (tvm.SetPathChecked(fullPath)) return true;
                        }
                    }
                }
                return false;
            }

            public void LoadCheckedPaths(List<string> checkedPaths)
            {
                if (fCheckBox.IsChecked == true)
                {
                    checkedPaths.Add(fFullPath);
                    return;
                }
                else
                {
                    foreach (object item in Items)
                    {
                        FolderTreeViewItem tvm = item as FolderTreeViewItem;
                        if (tvm != null)
                        {
                            tvm.LoadCheckedPaths(checkedPaths);
                        }
                    }
                }
            }

            protected Nullable<bool> IsChecked
            {
                get { return fCheckBox.IsChecked; }
                set
                {
                    // Avoid propogation if no material change
                    if (fCheckBox.IsChecked != value)
                    {
                        fCheckBox.IsChecked = value;
                    }
                }
            }

            void fCheckBox_CheckChanged(object sender, RoutedEventArgs e)
            {
                FolderTreeViewItem tvm;
                if (!fPropogating)
                {
                    foreach (object item in Items)
                    {
                        tvm = item as FolderTreeViewItem;
                        if (tvm != null)
                        {
                            tvm.PropogateToLeaves(fCheckBox.IsChecked == true);
                        }
                    }

                    tvm = Parent as FolderTreeViewItem;
                    if (tvm != null)
                    {
                        tvm.PropogateToRoot(fCheckBox.IsChecked);
                    }
                }
            }

            protected void PropogateToLeaves(bool isChecked)
            {
                if (fCheckBox.IsChecked != isChecked)
                {
                    fPropogating = true;
                    fCheckBox.IsChecked = isChecked;
                    foreach (object item in Items)
                    {
                        FolderTreeViewItem tvm = item as FolderTreeViewItem;
                        if (tvm != null && tvm.IsChecked != isChecked)
                        {
                            tvm.PropogateToLeaves(isChecked);
                        }
                    }
                    fPropogating = false;
                }
            }

            protected void PropogateToRoot(Nullable<bool> isChecked)
            {
                // If isChecked is not null (some checked), find out what it's value should be
                if (isChecked != null)
                {
                    bool hasChecked = false;
                    bool hasUnchecked = false;
                    foreach (object item in Items)
                    {
                        FolderTreeViewItem tvm = item as FolderTreeViewItem;
                        if (tvm != null)
                        {
                            if (tvm.IsChecked == null)
                            {
                                hasChecked = true;
                                hasUnchecked = true;
                                break;
                            }
                            else if (tvm.IsChecked == true)
                            {
                                hasChecked = true;
                                if (hasUnchecked) break;
                            }
                            else
                            {
                                hasUnchecked = true;
                                if (hasChecked) break;
                            }
                        }
                    }

                    if (hasChecked && hasUnchecked)
                    {
                        isChecked = null;
                    }
                    else if (hasChecked)
                    {
                        isChecked = true;
                    }
                    else
                    {
                        isChecked = false;
                    }
                }

                // If isChecked is different from current value, update it and propogate
                if (fCheckBox.IsChecked != isChecked)
                {
                    fPropogating = true;
                    fCheckBox.IsChecked = isChecked;
                    FolderTreeViewItem tvm = Parent as FolderTreeViewItem;
                    if (tvm != null)
                    {
                        tvm.PropogateToRoot(isChecked);
                    }
                    fPropogating = false;
                }
            }

            protected override void OnExpanded(RoutedEventArgs e)
            {
                if (!fHasLoadedChildren) LoadChildren();
                base.OnExpanded(e);
            }

            private void LoadChildren()
            {
                if (fHasLoadedChildren) return;
                // Existing item count (for later removal).  Should be only one.
                int existingCount = Items.Count;

                // Fill in the subfolders
                DirectoryInfo di = new DirectoryInfo(fFullPath);
                foreach(DirectoryInfo subDi in di.EnumerateDirectories())
                {
                    if ((subDi.Attributes & (FileAttributes.Hidden|FileAttributes.System)) == 0)
                    {
                        FolderTreeViewItem newItem = new FolderTreeViewItem();
                        newItem.FullPath = subDi.FullName;
                        if (fCheckBox.IsChecked == true)
                        {
                            newItem.fPropogating = true;
                            newItem.IsChecked = IsSelected;
                            newItem.fPropogating = false;
                        }
                        Items.Add(newItem);
                    }
                }

                // Remove all placeholder items (paranoid code, there should only be one)
                for (; existingCount > 0; --existingCount)
                {
                    Items.RemoveAt(0);
                }
                fHasLoadedChildren = true;
            }

            private static bool HasSubfolder(string folderPath)
            {
                DirectoryInfo di = new DirectoryInfo(folderPath);
                foreach (DirectoryInfo subDi in di.EnumerateDirectories())
                {
                    if ((subDi.Attributes & (FileAttributes.Hidden|FileAttributes.System)) == 0)
                    {
                        return true;
                    }
                }
                return false;
            }

            protected override void OnKeyDown(KeyEventArgs e)
            {
                //Debug.WriteLine("TreeViewItem({0}).OnKeyDown({1})", fCheckBox.Content.ToString(), e.Key);
                switch (e.Key)
                {
                    case Key.Space:
                        fCheckBox.IsChecked = fCheckBox.IsChecked == false;
                        e.Handled = true;
                        break;

                    // Handle expand directly (it prevents some weird behavior from the default)
                    case Key.Right:
                        if (!IsExpanded)
                        {
                            IsExpanded = true;
                        }
                        e.Handled = true;
                        break;

                    // Handle contract directly (it prevents some weird behavior from the default)
                    case Key.Left:
                        if (IsExpanded)
                        {
                            IsExpanded = false;
                        }
                        e.Handled = true;
                        break;

                    default:
                        base.OnKeyDown(e);
                        break;
                }
            }


        } // Class FolderTreeViewItem


    } // Class FolderTreeView

}
