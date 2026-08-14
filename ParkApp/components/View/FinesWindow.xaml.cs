using System.Windows;
using ParkApp.components.ViewModel;

namespace ParkApp.components.View
{
    public partial class FinesWindow : Window
    {
        public FinesWindow(FineListViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            // данные тянем после показа окна, чтобы конструктор не ждал файловых операций
            Loaded += async (sender, args) => await viewModel.InitializeAsync();
        }
    }
}
