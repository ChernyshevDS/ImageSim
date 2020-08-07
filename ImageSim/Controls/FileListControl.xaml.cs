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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ImageSim.Controls
{
    /// <summary>
    /// Логика взаимодействия для FileListControl.xaml
    /// </summary>
    public partial class FileListControl : UserControl
    {
        public FileListControl()
        {
            InitializeComponent();
        }

        private bool mSuppressRequestBringIntoView;

        private void TreeViewItem_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            // Ignore re-entrant calls
            if (mSuppressRequestBringIntoView)
                return;

            // Cancel the current scroll attempt
            e.Handled = true;

            // Call BringIntoView using a rectangle that extends into "negative space" to the left of our
            // actual control. This allows the vertical scrolling behaviour to operate without adversely
            // affecting the current horizontal scroll position.
            mSuppressRequestBringIntoView = true;

            if (sender is TreeViewItem tvi)
            {
                var newTargetRect = new Rect(int.MinValue, 0, tvi.ActualWidth + int.MaxValue, tvi.ActualHeight);
                tvi.BringIntoView(newTargetRect);
            }

            mSuppressRequestBringIntoView = false;
        }

        private void TreeViewItem_Selected(object sender, RoutedEventArgs e)
        {
            ((TreeViewItem)sender).BringIntoView();
            e.Handled = true;
        }
    }
}
