using System.Windows;
using System.Windows.Controls;

namespace FileSync.View
{
    /// <summary>
    /// Interaction logic for EditRuleView.xaml
    /// </summary>
    public partial class EditRuleView : UserControl
    {
        public EditRuleView()
        {
            InitializeComponent();
        }

        private void EditRuleView_OnLoaded(object sender, RoutedEventArgs e)
        {
            SourceTextBox.Focus();
        }
    }
}
