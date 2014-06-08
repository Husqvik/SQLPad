using System.Windows;
using System.Windows.Input;

namespace SqlPad.FindReplace
{
    public partial class FindReplaceDialog
    {
	    readonly FindReplaceManager _findReplaceManager;

        public FindReplaceDialog(FindReplaceManager findReplaceManager)
        {            
            DataContext = _findReplaceManager = findReplaceManager;  
            InitializeComponent();
        }

        private void FindNextClick(object sender, RoutedEventArgs e)
        {
            _findReplaceManager.FindNext();
        }

        private void ReplaceClick(object sender, RoutedEventArgs e)
        {
            _findReplaceManager.Replace();
        }

        private void ReplaceAllClick(object sender, RoutedEventArgs e)
        {
            _findReplaceManager.ReplaceAll();
        }
  
        private void KeyDownHandler(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
                Close();
        }
    }
}
