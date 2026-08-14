using System;
using System.Linq;
using System.Windows;
using ParkApp.components.Application;
using ParkApp.components.ViewModel;

namespace ParkApp.components.View
{
    /// <summary>
    /// Открывает окна разделов и настроек. Какое окно соответствует разделу —
    /// знает только слой View.
    /// </summary>
    public class WpfMainDialogService : IMainDialogService
    {
        private readonly Func<FineListViewModel> _fineListFactory;

        public WpfMainDialogService(Func<FineListViewModel> fineListFactory)
        {
            _fineListFactory = fineListFactory;
        }

        public void OpenSection(AppSection section)
        {
            switch (section)
            {
                case AppSection.Fines:
                    Show(new FinesWindow(_fineListFactory()));
                    break;

                default:
                    throw new NotSupportedException("Раздел ещё не реализован.");
            }
        }

        public bool ShowSettings(SettingsViewModel viewModel)
        {
            return Show(new SettingsWindow(viewModel)) == true;
        }

        public void ShowError(string message)
        {
            MessageBox.Show(message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private static bool? Show(Window window)
        {
            var owner = FindActiveWindow();
            if (owner != null && !ReferenceEquals(owner, window))
                window.Owner = owner;

            return window.ShowDialog();
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
