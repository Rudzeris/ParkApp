using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using ParkApp.components.Application;

namespace ParkApp.components.ViewModel
{
    /// <summary>Галка «нужен ли раздел» в настройках.</summary>
    public class SectionToggleViewModel : ViewModelBase
    {
        private bool _isEnabled;

        public SectionToggleViewModel(SectionDescription description, bool isEnabled)
        {
            Description = description;
            _isEnabled = isEnabled;
        }

        public SectionDescription Description { get; private set; }

        public string Title
        {
            get { return Description.Title; }
        }

        /// <summary>Нереализованные разделы показываем, но включить нельзя.</summary>
        public bool IsAvailable
        {
            get { return Description.IsAvailable; }
        }

        public string Note
        {
            get { return Description.IsAvailable ? string.Empty : "в разработке"; }
        }

        public bool IsEnabled
        {
            get { return _isEnabled; }
            set { SetProperty(ref _isEnabled, value); }
        }
    }

    /// <summary>
    /// Настройки рабочего места: какие разделы нужны этому человеку.
    /// </summary>
    public class SettingsViewModel : ViewModelBase
    {
        private readonly IAppSettings _settings;

        public SettingsViewModel(IAppSettings settings)
        {
            _settings = settings;

            Sections = new ObservableCollection<SectionToggleViewModel>();
            foreach (var description in SectionCatalog.All)
                Sections.Add(new SectionToggleViewModel(description, settings.IsSectionEnabled(description.Section)));

            SaveCommand = new RelayCommand(o => Save());
            CancelCommand = new RelayCommand(o => Close(false));
        }

        /// <summary>Закрыть окно. true — настройки сохранены.</summary>
        public event EventHandler<bool> RequestClose;

        public ObservableCollection<SectionToggleViewModel> Sections { get; private set; }

        public ICommand SaveCommand { get; private set; }
        public ICommand CancelCommand { get; private set; }

        private void Save()
        {
            foreach (var toggle in Sections)
            {
                if (toggle.IsAvailable)
                    _settings.SetSectionEnabled(toggle.Description.Section, toggle.IsEnabled);
            }

            _settings.Save();
            Close(true);
        }

        private void Close(bool saved)
        {
            var handler = RequestClose;
            if (handler != null)
                handler(this, saved);
        }
    }
}
