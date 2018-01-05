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

namespace SlideDiscWPF
{
    /// <summary>
    /// Interaction logic for MainContextMenu.xaml
    /// </summary>
    public partial class MainContextMenu : Window
    {
        public MainContextMenu()
        {
            InitializeComponent();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            switch(e.Key)
            {
                case Key.Escape:
                    Close();
                    break;

                // If these made it here, the ListBox doesn't have focus
                case Key.Up:
                case Key.Down:
                case Key.Right:
                case Key.Left:
                    ((ListBoxItem)fListBox.Items[0]).Focus();
                    e.Handled = true;
                    break;

                case Key.Enter:
                    if (fListBox.SelectedItems.Count > 0)
                    {
                        ListBoxItem item = fListBox.SelectedItems[0] as ListBoxItem;
                        if (item != null)
                        {
                            DoCommand(item);
                        }
                    }
                    e.Handled = true;
                    break;

                default:
                    {
                        SlideShowWindow owner = Owner as SlideShowWindow;
                        if (owner != null)
                        {
                            if (owner.KeyCommand(e.Key))
                            {
                                e.Handled = true;
                                Close();
                            }
                        }
                    }
                    base.OnKeyDown(e);
                    break;
            }
        }

        // This is a total Kludge. But it looks and works well. Someday I'll change it all over
        // to a proper context menu, routed commands and so forth. But that method would require
        // styles to change font size and a bunch of other complexity I don't want to tackle
        // right now.
        bool fMouseDown = false;
        protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
        {
            fMouseDown = true;
            base.OnPreviewMouseDown(e);
        }

        protected override void OnPreviewMouseUp(MouseButtonEventArgs e)
        {
            fMouseDown = false;
            base.OnPreviewMouseUp(e);
        }

        private void fListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (fMouseDown && e.AddedItems.Count > 0)
            {
                ListBoxItem item = e.AddedItems[0] as ListBoxItem;
                if (item != null)
                {
                    DoCommand(item);
                }
            }
        }

        private void DoCommand(ListBoxItem item)
        {
            Debug.WriteLine("DoCommand({0})", item.Content, 0);
            CommandKey ck = item.Resources["Cmd"] as CommandKey;
            if (ck != null)
            {
                SlideShowWindow owner = Owner as SlideShowWindow;
                if (owner != null)
                {
                    owner.KeyCommand((Key)ck.KbKey);
                }
            }

            Close();
        }
    }

    public class CommandKey
    {
       int fKey;
        public CommandKey() {}

        public int KbKey
        {
            get { return fKey; }
            set {fKey = value;}
        }
    }
}
