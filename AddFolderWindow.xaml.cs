using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ProgaVove2
{
    public partial class AddFolderWindow : Window
    {
        public string FolderName => NameTextBox.Text;
        public string FolderPath => PathTextBox.Text;

        public AddFolderWindow()
        {
            InitializeComponent();
        }
        public void SetInitialValues(string path, string defaultName)
        {
            PathTextBox.Text = path;
            NameTextBox.Text = defaultName;
        }
        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Выберите папку с объявлениями"
            };

            if (dialog.ShowDialog() == true)
            {
                PathTextBox.Text = dialog.FolderName;
                if (string.IsNullOrEmpty(NameTextBox.Text))
                {
                    NameTextBox.Text = Path.GetFileName(dialog.FolderName);
                }
            }
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(FolderName))
            {
                MessageBox.Show("Введите название папки", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(FolderPath) || !Directory.Exists(FolderPath))
            {
                MessageBox.Show("Укажите существующий путь к папке", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
