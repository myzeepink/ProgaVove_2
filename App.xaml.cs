using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ProgaVove2
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Логирование всех необработанных ошибок
            this.DispatcherUnhandledException += (s, ex) =>
            {
                try // пытаемся залогировать что-нибудь, если что-то есть при НЕзапуске программы
                {
                    File.WriteAllText("error.log", $"Произошла ошибка:\n{ex.Exception}");
                    MessageBox.Show(
                        $"Критическая ошибка:\n{ex.Exception.Message}\n\n" +
                        "Подробности записаны в error.log",
                        "Ошибка",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                catch
                {
                    // если почему-то не удалось записать лог
                    MessageBox.Show(
                        $"Критическая ошибка:\n{ex.Exception}",
                        "Ошибка",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                ex.Handled = true;
            };
        }
    }
}