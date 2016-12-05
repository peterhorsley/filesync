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
            var grid = VisualTreeHelper.GetChild(LogScrollViewer, 0);
            if (grid == null)
                return;
            var rect = VisualTreeHelper.GetChild(grid, 0) as Rectangle;
            if (rect == null)
                return;

            rect.Fill = new SolidColorBrush(Colors.Transparent);
        }
    }
}
