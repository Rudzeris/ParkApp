using System.Linq;
using System.Windows;
using ParkApp.components.ViewModel;

namespace ParkApp.components.View
{
    /// <summary>
    /// Реализация окон для списка штрафов. Единственное место, где ViewModel
    /// соприкасается с WPF-окнами.
    /// </summary>
    public class WpfFineDialogService : IFineDialogService
    {
        public bool ShowEditor(FineEditViewModel viewModel)
        {
            var window = new FineEditWindow(viewModel);

            var owner = FindActiveWindow();
            if (owner != null && !ReferenceEquals(owner, window))
                window.Owner = owner;

            return window.ShowDialog() == true;
        }

        public bool Confirm(string message, string caption)
        {
            return MessageBox.Show(message, caption, MessageBoxButton.YesNo, MessageBoxImage.Question)
                   == MessageBoxResult.Yes;
        }

        public void ShowError(string message)
        {
            MessageBox.Show(message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private static Window FindActiveWindow()
        {
            // полное имя обязательно: рядом лежит пространство имён ParkApp.components.Application
            var application = System.Windows.Application.Current;
            if (application == null)
                return null;

            return application.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
        }
    }
}
