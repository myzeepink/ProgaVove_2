using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using ProgaVove2.Converters;
// на английский переводи и начнешь понимать 70% кода

namespace ProgaVove2
{   // основное окно с наследованием методов и свойств из Window - нечего объяснять
    public partial class MainWindow : Window
    {
        public ICommand RefreshCommand => new RelayCommand(_ => CheckExpired_Click(null, null)); // refresh on F5
        private CancellationTokenSource _loadingCancellationToken;
        public class Advertisement // класс для создания объектов обьявлений
        {
            public string FolderName { get; set; }  // где хранится, по совместительству название
            public string TextContent { get; set; } // описание
            public List<string> ImagePaths { get; set; } = new(); // пути до картинок к объявлению
            public List<BitmapImage> OptimizedImages => // список этих самых картинок
                ImagePaths.Select(path => LoadOptimizedImage(path)).ToList(); // оптимизируется ниже
            public DateTime? ExpiryDate { get; set; } // срок годности
            public BitmapImage LoadOptimizedImage(string path, int targetSize = 50)
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(path);
                bitmap.DecodePixelWidth = targetSize; // фактически уменьшает ширину до размера на экране ( 50 пикселей )
                bitmap.CacheOption = BitmapCacheOption.OnLoad; // кэширование
                bitmap.EndInit();
                return bitmap;
            }
        }
        public ObservableCollection<Advertisement> Advertisements { get; private set; } = new();
        private string _baseDirectory; // основная папка в которой происходит весь fun
        private string RootFolder => Path.Combine(_baseDirectory, "Используемые"); // пути от корневой _baseDirectory
        private string ReadyFolder => Path.Combine(_baseDirectory, "Готовые к использованию");
        private bool _isLoading; // флаг чтоб рекурсии не было
        private void SelectFolder_Click(object sender, RoutedEventArgs e)
        {
            // выбор корневой папки
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Выберите ЛЮБОЙ файл в нужной папке",
                CheckFileExists = false,
                FileName = "SelectFolder"
            };
            // логика по сохранению этой папки в переменную
            if (dialog.ShowDialog() == true)
            {
                _baseDirectory = Path.GetDirectoryName(dialog.FileName);
                Directory.CreateDirectory(RootFolder);
                Directory.CreateDirectory(ReadyFolder);
                LoadAdvertisements();
            }
        }
        public MainWindow()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); // хз
            var encoding = Encoding.GetEncoding(1251); // костыль
            InitializeComponent();
            DataContext = this;

            string baseDir = SelectFolderWithWpfDialog(); // очередная переменная для папки я ебу нахуя?
            if (baseDir == null)
            { // ловилка exception'а если ты долбоёб
                MessageBox.Show("Папка не выбрана! Программа закроется.");
                Close();
                return;
            }

            _baseDirectory = baseDir; // опа нихуя
            Directory.CreateDirectory(Path.Combine(_baseDirectory, "Используемые")); // создать если нету
            Directory.CreateDirectory(Path.Combine(_baseDirectory, "Готовые к использованию"));

            var timer = new DispatcherTimer { Interval = TimeSpan.FromHours(1) }; // таймер для автообновления списка
            timer.Tick += (s, e) => SafeCheckExpired();
            timer.Start();

            SortAdvertisements(); // сортировка по дате с приоритетом того, у чего не выставлена дата
            Loaded += (s, e) => LoadAdvertisements(); // автозагрузка на старте

        }
        private void SaveExpiryDate(Advertisement ad) // сохраняет дату просрочки в отдельный файл
        {
            string expiryFile = Path.Combine(RootFolder, ad.FolderName, "expiry_date.txt");
            if (ad.ExpiryDate.HasValue)
            {
                File.WriteAllText(expiryFile, ad.ExpiryDate.Value.ToString("o"));
            }
            else if (File.Exists(expiryFile))
            {
                File.Delete(expiryFile);
            }
        }
        private Advertisement LoadSingleAd(string folderPath) // загрузка ОДНОГО объявления
        {
            var ad = new Advertisement // новый объект с такими-то возможными расширениями
            {
                FolderName = Path.GetFileName(folderPath),
                ImagePaths = Directory.GetFiles(folderPath)
                    .Where(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                               f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                               f.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase) ||
                               f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
                    .ToList()
            };
            // загрузка описания
            var textFile = Directory.GetFiles(folderPath, "*.txt")
                .FirstOrDefault(f => !f.EndsWith("expiry_date.txt"));
            if (textFile != null)
            {
                ad.TextContent = File.ReadAllText(textFile, Encoding.GetEncoding(1251));
            }
            // загрузка даты
            var expiryFile = Path.Combine(folderPath, "expiry_date.txt");
            if (File.Exists(expiryFile))
            {
                if (DateTime.TryParse(File.ReadAllText(expiryFile), out var date))
                {
                    ad.ExpiryDate = date;
                }
            }
            return ad;
        }
        private void SafeCheckExpired() // неебаться какая безопасная проверялка сроков годности
        {
            try
            {
                foreach (var folder in Directory.GetDirectories(RootFolder))
                {
                    var expiryFile = Path.Combine(folder, "expiry_date.txt");
                    if (File.Exists(expiryFile) && // если вот эта вся хуйня происходит...
                        DateTime.TryParse(File.ReadAllText(expiryFile), out var expiryDate) &&
                        expiryDate < DateTime.Now)
                    {
                        MoveToReady(folder); // ...то перекидываем в готовые к использованию
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка проверки: {ex.Message}"); // если не получилось выключай компьютер
            }
        }
        private async void LoadAdvertisements() // осоторожно тут, пиздец, асинхрон, загрузка всех объявлений
        {
            if (_isLoading) return;
            _isLoading = true;
            // показываем прогресс-бар
            LoadingProgressBar.Visibility = Visibility.Visible;
            LoadingProgressBar.IsIndeterminate = true;
            try
            {
                // не спрашивай не ебу, лучше нахуй убрать этот прогрессбар, пиздец все сложно я нешарю
                await Application.Current.Dispatcher.InvokeAsync(() => Advertisements.Clear());
                var folders = Directory.GetDirectories(RootFolder);
                int total = folders.Length;
                int processed = 0;
                foreach (var folder in folders)
                {
                    var ad = await Task.Run(() => LoadSingleAd(folder));
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        Advertisements.Add(ad);
                        LoadingProgressBar.Value = (++processed * 100) / total;
                    });
                }
                await Application.Current.Dispatcher.InvokeAsync(SortAdvertisements);
            }
            finally
            {
                _isLoading = false;
                LoadingProgressBar.Visibility = Visibility.Collapsed;
            }
        }
        private void MoveToReady(string folderPath) // перемещение в ГИ
        {
            try
            { // это локальные переменные, они только для этого метода
                string folderName = Path.GetFileName(folderPath); 
                string destPath = Path.Combine(ReadyFolder, folderName);
                if (Directory.Exists(destPath))
                    Directory.Delete(destPath, true);
                Directory.Move(folderPath, destPath);
                File.Delete(Path.Combine(destPath, "expiry_date.txt"));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка переноса: {ex.Message}");
            }
        }
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) // поисковик
        {
            if (string.IsNullOrWhiteSpace(SearchBox.Text)) // если в строке поиска пусто
            {
                // ReadyListBox - название списка обьявлений в разметке интерфейса
                // отображаем без фильтров
                ReadyListBox.Items.Filter = null;
            }
            else
            {
                // фильтруем по соответствию введенного в поле поиска с названиями
                ReadyListBox.Items.Filter = item =>
                ((Advertisement)item).FolderName.Contains(SearchBox.Text, StringComparison.OrdinalIgnoreCase);
            }
        }
        private void AddDays_Click(object sender, RoutedEventArgs e) // добавлялка дней
        { // добавлялка по тегам 30/60, они в xaml расписаны
            if (sender is Button button && button.Tag is string daysStr && 
                int.TryParse(daysStr, out int days) &&
                button.DataContext is Advertisement ad)
            {
                ad.ExpiryDate = DateTime.Now.AddDays(days);
                SaveExpiryDate(ad);
                SortAdvertisements();
            }
        }
        private void SelectDate_Click(object sender, RoutedEventArgs e) // ручной ввод даты
        {
            if (sender is Button button && button.DataContext is Advertisement ad)
            {
                var dialog = new DatePickerWindow { Owner = this };
                if (dialog.ShowDialog() == true)
                {
                    ad.ExpiryDate = dialog.SelectedDate;
                    SaveExpiryDate(ad);
                    ReadyListBox.Items.Refresh();
                }
            }
        }
        private void CheckExpired_Click(object sender, RoutedEventArgs e) // кнопка обновления списка
        {
            SafeCheckExpired();
            LoadAdvertisements(); // перезагружает список с 0!!!
        }
        private void SortAdvertisements() // сортировка
        {
            var sorted = Advertisements // дубликат коллекции
                .OrderBy(ad => ad.ExpiryDate.HasValue)  // сначала без даты...
                .ThenBy(ad => ad.ExpiryDate)            // ...затем по дате (ближайшие сверху)
                .ToList();

            Advertisements.Clear();
            foreach (var ad in sorted)
            {
                Advertisements.Add(ad);  // перезаполнение коллекции
            }
        }
        private string SelectFolderWithWpfDialog() // это какой-то иной метод для выбора папки, тоже работает
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Выберите корневую папку для объявлений",
                Multiselect = false
            };

            if (dialog.ShowDialog() == true)
            {
                return dialog.FolderName;
            }
            return null;
        }
    }
    public class RelayCommand : ICommand // обработчик хоткея
    {
        private readonly Action<object> _execute;
        public RelayCommand(Action<object> execute) => _execute = execute;
        public bool CanExecute(object parameter) => true;
        public void Execute(object parameter) => _execute(parameter);
        public event EventHandler CanExecuteChanged;
    }
}
