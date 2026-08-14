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
    /// Настройки рабочего места: какие разделы нужны, где лежат таблицы
    /// и как называются листы.
    ///
    /// Выбранный файл проверяется сразу: пользователь должен узнать «это не тот файл»
    /// при выборе, а не при следующем открытии окна. Если файл тот, а лист называется
    /// иначе — предлагается выбрать лист, и выбор сохраняется вместе с остальными.
    /// </summary>
    public class SettingsViewModel : ViewModelBase
    {
        private const string ExcelFilter = "Таблицы Excel (*.xlsx)|*.xlsx";

        private readonly IAppSettings _settings;
        private readonly IFileDialogService _fileDialogs;
        private readonly ITableFileValidator _validator;
        private readonly IMessageService _messages;
        private readonly ISheetPicker _sheetPicker;

        public SettingsViewModel(
            IAppSettings settings,
            IFileDialogService fileDialogs,
            ITableFileValidator validator,
            IMessageService messages,
            ISheetPicker sheetPicker,
            Func<PathSetting, string> defaultPath)
        {
            _settings = settings;
            _fileDialogs = fileDialogs;
            _validator = validator;
            _messages = messages;
            _sheetPicker = sheetPicker;

            Sections = new ObservableCollection<SectionToggleViewModel>();
            foreach (var description in SectionCatalog.All)
                Sections.Add(new SectionToggleViewModel(description, settings.IsSectionEnabled(description.Section)));

            Paths = new ObservableCollection<PathSettingViewModel>();
            foreach (var description in PathSettingCatalog.All)
            {
                Paths.Add(new PathSettingViewModel(
                    description,
                    settings.GetPath(description.Setting),
                    defaultPath(description.Setting),
                    description.Table.HasValue ? SheetNameOf(SheetCatalog.ForTable(description.Table.Value)) : null));
            }

            // справочные листы лежат в файле машин, отдельного пути у них нет
            Sheets = new ObservableCollection<SheetSettingViewModel>();
            foreach (var description in SheetCatalog.All.Where(IsLookupSheet))
                Sheets.Add(new SheetSettingViewModel(description, SheetNameOf(description.Kind)));

            BrowsePathCommand = new RelayCommand(Browse);
            ResetPathCommand = new RelayCommand(Reset);
            PickSheetCommand = new RelayCommand(PickSheet);
            SaveCommand = new RelayCommand(o => Save());
            CancelCommand = new RelayCommand(o => Close(false));
        }

        /// <summary>Закрыть окно. true — настройки сохранены.</summary>
        public event EventHandler<bool> RequestClose;

        public ObservableCollection<SectionToggleViewModel> Sections { get; private set; }
        public ObservableCollection<PathSettingViewModel> Paths { get; private set; }
        public ObservableCollection<SheetSettingViewModel> Sheets { get; private set; }

        public ICommand BrowsePathCommand { get; private set; }
        public ICommand ResetPathCommand { get; private set; }
        public ICommand PickSheetCommand { get; private set; }
        public ICommand SaveCommand { get; private set; }
        public ICommand CancelCommand { get; private set; }

        private static bool IsLookupSheet(SheetDescription description)
        {
            return description.Kind == SheetKind.Affiliation || description.Kind == SheetKind.VehicleType;
        }

        private string SheetNameOf(SheetKind kind)
        {
            var configured = _settings.GetSheetName(kind);
            return string.IsNullOrWhiteSpace(configured) ? SheetCatalog.DefaultName(kind) : configured;
        }

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
        /// Просит выбрать файл, пока файл не подойдёт или пользователь не откажется.
        /// Если файл тот, а листа с нужным именем нет — сначала предлагает выбрать лист.
        /// </summary>
        private void PickTableFile(PathSettingViewModel row)
        {
            var table = row.Description.Table.Value;

            while (true)
            {
                var file = _fileDialogs.PickFile("Выберите файл: " + row.Title, ExcelFilter);
                if (file == null)
                    return;

                var sheetName = row.SheetName;
                var check = _validator.Check(table, file, sheetName);

                if (check.SheetMissing)
                {
                    var chosen = AskSheet(file, check, sheetName);
                    if (chosen == null)
                        continue; // от выбора листа отказались — предлагаем выбрать другой файл

                    sheetName = chosen;
                    check = _validator.Check(table, file, sheetName);
                }

                if (!check.IsValid)
                {
                    _messages.ShowWarning(string.Format(
                        "Файл не подходит: {0}.{1}{1}{2}{1}{1}Выберите другой файл.",
                        check.Problem, Environment.NewLine, file));
                    continue;
                }

                row.Value = file;
                row.SheetName = sheetName;
                row.Note = DescribeMissing(check);
                return;
            }
        }

        private string AskSheet(string file, TableFileCheck check, string currentName)
        {
            var message = string.Format(
                "В книге нет листа «{0}».{1}{1}Файл: {2}{1}{1}Укажите, какой лист использовать.",
                currentName, Environment.NewLine, file);

            return _sheetPicker.PickSheet(message, check.AvailableSheets, currentName);
        }

        /// <summary>Сменить лист у уже выбранного файла.</summary>
        private void PickSheet(object parameter)
        {
            var row = parameter as PathSettingViewModel;
            if (row == null || !row.HasSheet)
                return;

            var table = row.Description.Table.Value;
            var path = row.EffectivePath;

            var available = _validator.GetSheetNames(path);
            var message = available.Count > 0
                ? string.Format("Листы книги: {0}", string.Join(", ", available))
                : string.Format("Не удалось прочитать книгу «{0}» — впишите имя листа вручную.", path);

            var chosen = _sheetPicker.PickSheet(message, available, row.SheetName);
            if (chosen == null)
                return;

            var check = _validator.Check(table, path, chosen);
            if (!check.IsValid)
            {
                _messages.ShowWarning(string.Format(
                    "Лист не подходит: {0}.{1}{1}Имя листа не изменено.", check.Problem, Environment.NewLine));
                return;
            }

            row.SheetName = chosen;
            row.Note = DescribeMissing(check);
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

            if (row.HasSheet)
                row.SheetName = SheetCatalog.DefaultName(SheetCatalog.ForTable(row.Description.Table.Value));
        }

        private void Save()
        {
            // путь и имя листа могли быть вписаны руками, минуя проверку при выборе
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

            foreach (var row in Paths)
            {
                _settings.SetPath(row.Description.Setting, row.Value);

                if (row.HasSheet)
                    _settings.SetSheetName(SheetCatalog.ForTable(row.Description.Table.Value), row.SheetName);
            }

            foreach (var sheet in Sheets)
                _settings.SetSheetName(sheet.Description.Kind, sheet.Name);

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
                var check = _validator.Check(row.Description.Table.Value, row.Value.Trim(), row.SheetName);
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
