using System.Windows;
using ParkApp.components.ViewModel;

namespace ParkApp.components.View
{
    public partial class FineEditWindow : Window
    {
        private bool _closing;

        public FineEditWindow(FineEditViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            viewModel.RequestClose += OnRequestClose;
            Loaded += async (sender, args) => await viewModel.InitializeAsync();
        }

        private void OnRequestClose(object sender, bool saved)
        {
            // DialogResult закрывает окно, повторная установка после закрытия бросает исключение
            if (_closing)
                return;

            _closing = true;
            DialogResult = saved;
        }
    }
}
