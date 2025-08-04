using System;
using System.Windows;

namespace ProgaVove2
{
    public partial class DatePickerWindow : Window
    { // окошко для выбора даты для объявлений
        public DateTime SelectedDate { get; set; } = DateTime.Now;
        public DatePickerWindow()
        {
            InitializeComponent();
            DataContext = this;
        }
        private void OK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}