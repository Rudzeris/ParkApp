using System.Windows;

namespace ParkApp
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            AppServices.Initialize();

            var window = new MainWindow
            {
                DataContext = AppServices.CreateCarListViewModel()
            };

            window.Show();
        }
    }
}
