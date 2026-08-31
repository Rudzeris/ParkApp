using System.Windows;
using ParkApp.components.Application;
using ParkApp.components.Infrastructure;
using ParkApp.components.ViewModel;
using ParkApp.Windows;

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

            var repo = new JsonFineRecordRepository();

            var service = new FineService(repo);

            var vm = new FineListViewModel(service);

            var window = new FineWindow()
            {
                DataContext = vm
            };

            window.Show();
        }
    }
}