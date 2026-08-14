using ParkApp.components.Application;

namespace ParkApp.components.ViewModel
{
    /// <summary>Одна строка настроек: путь к папке или файлу.</summary>
    public class PathSettingViewModel : ViewModelBase
    {
        private string _value;
        private string _note;

        public PathSettingViewModel(PathSettingDescription description, string value, string defaultPath)
        {
            Description = description;
            DefaultPath = defaultPath;
            _value = value ?? string.Empty;
        }

        public PathSettingDescription Description { get; private set; }

        /// <summary>Путь, который используется, если поле оставить пустым.</summary>
        public string DefaultPath { get; private set; }

        public string Title
        {
            get { return Description.Title; }
        }

        public string Hint
        {
            get { return Description.Hint; }
        }

        public bool IsFolder
        {
            get { return Description.IsFolder; }
        }

        /// <summary>Заданный путь. Пусто — используется значение по умолчанию.</summary>
        public string Value
        {
            get { return _value; }
            set
            {
                if (SetProperty(ref _value, value ?? string.Empty))
                    OnPropertyChanged("EffectivePath");
            }
        }

        /// <summary>Путь, который приложение возьмёт на самом деле.</summary>
        public string EffectivePath
        {
            get { return string.IsNullOrWhiteSpace(_value) ? DefaultPath : _value.Trim(); }
        }

        /// <summary>Замечание по выбранному файлу — например, каких столбцов в нём нет.</summary>
        public string Note
        {
            get { return _note; }
            set
            {
                if (SetProperty(ref _note, value))
                    OnPropertyChanged("HasNote");
            }
        }

        public bool HasNote
        {
            get { return !string.IsNullOrEmpty(_note); }
        }
    }
}
