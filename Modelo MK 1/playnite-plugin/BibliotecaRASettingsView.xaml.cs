using System.Windows;
using System.Windows.Controls;

namespace BibliotecaRA
{
    public partial class BibliotecaRASettingsView : UserControl
    {
        public BibliotecaRASettingsView()
        {
            InitializeComponent();
        }

        private BibliotecaRASettingsViewModel ViewModel => DataContext as BibliotecaRASettingsViewModel;

        private void AddFolder_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.AddFolder();
        }

        private void RemoveFolder_Click(object sender, RoutedEventArgs e)
        {
            var selected = FoldersList.SelectedItem as string;
            ViewModel?.RemoveFolder(selected);
        }

        private void ImportFolders_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.ImportGameFolders();
        }

        private void Test_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.TestConnection();
        }

        private void Force_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.ForceLibraryUpdate();
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.ClearCache();
        }
    }
}
