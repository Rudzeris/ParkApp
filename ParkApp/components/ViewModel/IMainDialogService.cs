using ParkApp.components.Application;

namespace ParkApp.components.ViewModel
{
    /// <summary>Окна, которые открывает главное окно.</summary>
    public interface IMainDialogService
    {
        /// <summary>Открыть раздел.</summary>
        void OpenSection(AppSection section);

        /// <summary>Показать настройки. true — пользователь сохранил.</summary>
        bool ShowSettings(SettingsViewModel viewModel);

        void ShowError(string message);
    }
}
