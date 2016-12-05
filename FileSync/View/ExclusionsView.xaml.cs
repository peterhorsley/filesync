using System.Windows;
using System.Windows.Controls;

namespace FileSync.View
{

    /// <summary>
    /// Interaction logic for ExclusionsView.xaml
    /// </summary>
    public partial class ExclusionsView : UserControl
    {
        public ExclusionsView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            FileNameExclusionsTextBox.Focus();
        }
    }
}
