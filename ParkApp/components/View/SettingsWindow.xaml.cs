using System.Windows;
using ParkApp.components.ViewModel;

namespace ParkApp.components.View
{
    public partial class SettingsWindow : Window
    {
        private bool _closing;

        public SettingsWindow(SettingsViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            viewModel.RequestClose += OnRequestClose;
        }

        private void OnRequestClose(object sender, bool saved)
        {
            if (_closing)
                return;

            _closing = true;
            DialogResult = saved;
        }
    }
}
