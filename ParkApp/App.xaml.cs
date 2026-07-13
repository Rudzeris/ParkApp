using System.Windows;
using ParkApp.components.Application;
using ParkApp.components.Infrastructure;
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

            ICarRepository repo = new JsonCarRepository();

            CarService carService = new CarService(repo);

            var vm = new CarListViewModel(carService);

            var window = new MainWindow()
            {
                DataContext = vm
            };

            window.Show();
        }
    }
}