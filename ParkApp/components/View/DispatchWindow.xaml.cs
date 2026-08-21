using System.Windows;
using ParkApp.components.ViewModel;

namespace ParkApp.components.View
{
    public partial class DispatchWindow : Window
    {
        public DispatchWindow(DispatchViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            // данные тянем после показа окна, чтобы конструктор не ждал файловых операций
            Loaded += async (sender, args) => await viewModel.InitializeAsync();
        }
    }
}
