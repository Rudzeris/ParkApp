using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using ParkApp.components.Application;

namespace ParkApp.components.ViewModel
{
    /// <summary>Кнопка раздела на панели главного окна.</summary>
    public class SectionCommandViewModel
    {
        public SectionCommandViewModel(string title, ICommand open)
        {
            Title = title;
            Open = open;
        }

        public string Title { get; private set; }
        public ICommand Open { get; private set; }
    }

    /// <summary>
    /// Главное окно: список машин плюс панель разделов. Состав панели зависит
    /// от настроек рабочего места — человек, который не ведёт штрафы, их кнопку не видит.
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        private readonly IAppSettings _settings;
        private readonly IMainDialogService _dialogs;
        private readonly Func<SettingsViewModel> _settingsViewModelFactory;

        public MainViewModel(
            CarListViewModel cars,
            IAppSettings settings,
            IMainDialogService dialogs,
            Func<SettingsViewModel> settingsViewModelFactory)
        {
            Cars = cars;
            _settings = settings;
            _dialogs = dialogs;
            _settingsViewModelFactory = settingsViewModelFactory;

            Sections = new ObservableCollection<SectionCommandViewModel>();
            OpenSettingsCommand = new RelayCommand(o => OpenSettings());

            RefreshSections();
        }

        public CarListViewModel Cars { get; private set; }

        public ObservableCollection<SectionCommandViewModel> Sections { get; private set; }

        public ICommand OpenSettingsCommand { get; private set; }

        private void RefreshSections()
        {
            Sections.Clear();

            foreach (var description in SectionCatalog.All)
            {
                if (!description.IsAvailable || !_settings.IsSectionEnabled(description.Section))
                    continue;

                var section = description.Section;
                Sections.Add(new SectionCommandViewModel(
                    description.Title,
                    new RelayCommand(o => OpenSection(section))));
            }
        }

        private void OpenSection(AppSection section)
        {
            try
            {
                _dialogs.OpenSection(section);
            }
            catch (Exception ex)
            {
                _dialogs.ShowError("Не удалось открыть раздел: " + ex.Message);
            }
        }

        private void OpenSettings()
        {
            if (!_dialogs.ShowSettings(_settingsViewModelFactory()))
                return;

            RefreshSections();

            // пути к таблицам могли поменяться — перечитываем данные
            Cars.Reload();
        }
    }
}
