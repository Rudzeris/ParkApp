using System.Windows;
using ParkApp.components.View;
using ParkApp.components.ViewModel;

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

            var main = AppServices.CreateMainViewModel();
            var shell = new ShellViewModel();

            // список машин открыт сразу: с него начинается работа,
            // и пустое окно при запуске выглядело бы поломкой
            shell.Open(main.Cars, "Машины", "Машины");

            var window = new ShellWindow(new ShellHostViewModel(main, shell));
            window.Show();
        }
    }
}
