using System.Windows;
using ParkApp.components.ViewModel;

namespace ParkApp.components.View
{
    public class WpfMessageService : IMessageService
    {
        public void ShowWarning(string message)
        {
            MessageBox.Show(message, "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        public void ShowError(string message)
        {
            MessageBox.Show(message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
