using System;
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
        public void OpenEditor(FineEditViewModel viewModel)
        {
            // у каждого редактора свой ключ: записей можно открыть сколько угодно,
            // в отличие от разделов, которые показываются одной вкладкой
            var document = DocumentHost.Open(viewModel, viewModel.Title, Guid.NewGuid().ToString());
            if (document == null)
                return;

            viewModel.RequestClose += (sender, saved) => document.RequestClose();
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
