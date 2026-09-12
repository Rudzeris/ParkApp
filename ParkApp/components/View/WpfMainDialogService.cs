using System;
using System.Linq;
using System.Windows;
using ParkApp.components.Application;
using ParkApp.components.ViewModel;

namespace ParkApp.components.View
{
    /// <summary>
    /// Открывает разделы и настройки. Какой экран соответствует разделу —
    /// знает только слой View.
    ///
    /// Разделы открываются вкладками в том окне, с которым сейчас работают;
    /// настройки остаются модальным окном — это диалог, а не документ, и
    /// держать его открытым рядом незачем.
    /// </summary>
    public class WpfMainDialogService : IMainDialogService
    {
        private readonly Func<FineListViewModel> _fineListFactory;
        private readonly Func<DispatchViewModel> _dispatchFactory;

        public WpfMainDialogService(
            Func<FineListViewModel> fineListFactory,
            Func<DispatchViewModel> dispatchFactory)
        {
            _fineListFactory = fineListFactory;
            _dispatchFactory = dispatchFactory;
        }

        public void OpenSection(AppSection section)
        {
            switch (section)
            {
                case AppSection.Fines:
                    DocumentHost.Open(_fineListFactory(), "Книга постановлений", "Штрафы");
                    break;

                case AppSection.DispatchOrders:
                    DocumentHost.Open(_dispatchFactory(), "Наряд на выход", "Наряд");
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
