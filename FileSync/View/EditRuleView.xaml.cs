using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

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
