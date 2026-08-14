using System.Windows;
using ParkApp.components.View;

namespace ParkApp
{
    public partial class MainWindow
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void OnFinesClick(object sender, RoutedEventArgs e)
        {
            var window = new FinesWindow(AppServices.CreateFineListViewModel())
            {
                Owner = this
            };

            window.ShowDialog();
        }
    }
}
