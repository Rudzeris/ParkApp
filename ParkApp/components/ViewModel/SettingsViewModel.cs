using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
    /// Настройки рабочего места: какие разделы нужны и где лежат таблицы.
    /// Выбранный файл проверяется по столбцам сразу — пользователь должен узнать
    /// «это не тот файл» при выборе, а не при следующем открытии окна.
    /// </summary>
    public class SettingsViewModel : ViewModelBase
    {
        private const string ExcelFilter = "Таблицы Excel (*.xlsx)|*.xlsx";

        private readonly IAppSettings _settings;
        private readonly IFileDialogService _fileDialogs;
        private readonly ITableFileValidator _validator;
        private readonly IMessageService _messages;

        public SettingsViewModel(
            IAppSettings settings,
            IFileDialogService fileDialogs,
            ITableFileValidator validator,
            IMessageService messages,
            Func<PathSetting, string> defaultPath)
        {
            _settings = settings;
            _fileDialogs = fileDialogs;
            _validator = validator;
            _messages = messages;

            Sections = new ObservableCollection<SectionToggleViewModel>();
            foreach (var description in SectionCatalog.All)
                Sections.Add(new SectionToggleViewModel(description, settings.IsSectionEnabled(description.Section)));

            Paths = new ObservableCollection<PathSettingViewModel>();
            foreach (var description in PathSettingCatalog.All)
            {
                Paths.Add(new PathSettingViewModel(
                    description,
                    settings.GetPath(description.Setting),
                    defaultPath(description.Setting)));
            }

            BrowsePathCommand = new RelayCommand(Browse);
            ResetPathCommand = new RelayCommand(Reset);
            SaveCommand = new RelayCommand(o => Save());
            CancelCommand = new RelayCommand(o => Close(false));
        }

        /// <summary>Закрыть окно. true — настройки сохранены.</summary>
        public event EventHandler<bool> RequestClose;

        public ObservableCollection<SectionToggleViewModel> Sections { get; private set; }
        public ObservableCollection<PathSettingViewModel> Paths { get; private set; }

        public ICommand BrowsePathCommand { get; private set; }
        public ICommand ResetPathCommand { get; private set; }
        public ICommand SaveCommand { get; private set; }
        public ICommand CancelCommand { get; private set; }

        private void Browse(object parameter)
        {
            var row = parameter as PathSettingViewModel;
            if (row == null)
                return;

            if (row.IsFolder)
            {
                var folder = _fileDialogs.PickFolder("Выберите папку: " + row.Title, row.EffectivePath);
                if (folder != null)
                    row.Value = folder;

                return;
            }

            PickTableFile(row);
        }

        /// <summary>
        /// Просит выбрать файл до тех пор, пока файл не подойдёт по столбцам
        /// или пользователь не откажется.
        /// </summary>
        private void PickTableFile(PathSettingViewModel row)
        {
            while (true)
            {
                var file = _fileDialogs.PickFile("Выберите файл: " + row.Title, ExcelFilter);
                if (file == null)
                    return;

                var check = _validator.Check(row.Description.Table.Value, file);

                if (!check.IsValid)
                {
                    _messages.ShowWarning(string.Format(
                        "Файл не подходит: {0}{1}{1}{2}{1}{1}Выберите другой файл.",
                        check.Problem, Environment.NewLine, file));
                    continue;
                }

                row.Value = file;
                row.Note = DescribeMissing(check);
                return;
            }
        }

        private static string DescribeMissing(TableFileCheck check)
        {
            if (check.MissingColumns.Count == 0)
                return null;

            return "В файле нет столбцов: " + string.Join(", ", check.MissingColumns)
                   + ". Эти данные читаться не будут.";
        }

        private void Reset(object parameter)
        {
            var row = parameter as PathSettingViewModel;
            if (row == null)
                return;

            row.Value = string.Empty;
            row.Note = null;
        }

        /// <summary>
        /// Переносит введённые пути в настройки. Вызывается только при сохранении:
        /// до этого настройки трогать нельзя, иначе отмена всё равно изменила бы
        /// пути, по которым работает приложение.
        /// </summary>
        private void ApplyPaths()
        {
            foreach (var row in Paths)
                _settings.SetPath(row.Description.Setting, row.Value);
        }

        private void Save()
        {
            // путь мог быть вписан руками, минуя проверку при выборе
            var problems = CheckTypedPaths();
            if (problems.Count > 0)
            {
                _messages.ShowWarning(
                    "Настройки не сохранены." + Environment.NewLine + Environment.NewLine
                    + string.Join(Environment.NewLine, problems) + Environment.NewLine + Environment.NewLine
                    + "Выберите другой файл или очистите поле.");
                return;
            }

            foreach (var toggle in Sections)
            {
                if (toggle.IsAvailable)
                    _settings.SetSectionEnabled(toggle.Description.Section, toggle.IsEnabled);
            }

            ApplyPaths();

            try
            {
                _settings.Save();
            }
            catch (Exception ex)
            {
                _messages.ShowError("Не удалось сохранить настройки: " + ex.Message);
                return;
            }

            Close(true);
        }

        private List<string> CheckTypedPaths()
        {
            var problems = new List<string>();

            foreach (var row in Paths.Where(r => !r.IsFolder && !string.IsNullOrWhiteSpace(r.Value)))
            {
                var check = _validator.Check(row.Description.Table.Value, row.Value.Trim());
                if (!check.IsValid)
                    problems.Add(string.Format("{0}: {1}", row.Title, check.Problem));
            }

            return problems;
        }

        private void Close(bool saved)
        {
            var handler = RequestClose;
            if (handler != null)
                handler(this, saved);
        }
    }
}
