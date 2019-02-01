using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace FileSync.View
{
    /// <summary>
    /// Interaction logic for SyncView.xaml
    /// </summary>
    public partial class SyncView : UserControl
    {
        public SyncView()
        {
            InitializeComponent();
        }

        private void LogScrollViewer_OnLoaded(object sender, RoutedEventArgs e)
        {
            var childCount = VisualTreeHelper.GetChildrenCount(LogScrollViewer);
            if (childCount == 0)
            {
                return;
            }

            var grid = VisualTreeHelper.GetChild(LogScrollViewer, 0);
            var rect = VisualTreeHelper.GetChild(grid, 0) as Rectangle;
            if (rect == null)
            {
                return;
            }

            rect.Fill = new SolidColorBrush(Colors.Transparent);
        }
    }
}
