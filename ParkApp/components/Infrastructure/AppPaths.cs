using System;
using System.Configuration;
using System.IO;
using ParkApp.components.Application;

namespace ParkApp.components.Infrastructure
{
    /// <summary>
    /// Раскладка данных на диске:
    /// <code>
    /// &lt;общие справочники&gt;/          SharedRoot — одинаковы на всех рабочих местах
    /// ├── Машины/Машины.xlsx
    /// └── Люди/Люди.xlsx
    ///
    /// &lt;свои документы&gt;/             DataRoot — у каждого свои
    /// └── Штрафы/
    ///     ├── Штрафы.xlsx
    ///     └── Сканы/
    /// </code>
    ///
    /// Путь берётся в три шага: настройки приложения → App.config → значение по умолчанию.
    /// Настройки читаются при каждом обращении, поэтому смена папки в окне настроек
    /// действует сразу, без перезапуска.
    /// </summary>
    public static class AppPaths
    {
        public const string CarsFolderName = "Машины";
        public const string PeopleFolderName = "Люди";
        public const string FinesFolderName = "Штрафы";
        public const string InsurancesFolderName = "Страховки";
        public const string MaintenanceFolderName = "ТО";
        public const string ScansFolderName = "Сканы";

        public const string CarsFileName = "Машины.xlsx";
        public const string PeopleFileName = "Люди.xlsx";
        public const string FinesFileName = "Штрафы.xlsx";

        private static IAppSettings _settings;

        /// <summary>Подключает настройки. Вызывается один раз при запуске.</summary>
        public static void UseSettings(IAppSettings settings)
        {
            _settings = settings;
        }

        /// <summary>Каталог приложения. От него считаются относительные пути.</summary>
        public static string AppFolder
        {
            get { return AppDomain.CurrentDomain.BaseDirectory; }
        }

        /// <summary>Корень своих документов.</summary>
        public static string DataRoot
        {
            get { return FromSettings(PathSetting.DataRoot) ?? FromConfig("DataRoot") ?? AppFolder; }
        }

        /// <summary>Корень общих справочников. По умолчанию совпадает с <see cref="DataRoot"/>.</summary>
        public static string SharedRoot
        {
            get { return FromSettings(PathSetting.SharedRoot) ?? FromConfig("SharedRoot") ?? DataRoot; }
        }

        public static string CarsFile
        {
            get
            {
                return FromSettings(PathSetting.CarsFile)
                       ?? Path.Combine(SharedRoot, CarsFolderName, CarsFileName);
            }
        }

        public static string PeopleFile
        {
            get
            {
                return FromSettings(PathSetting.PeopleFile)
                       ?? Path.Combine(SharedRoot, PeopleFolderName, PeopleFileName);
            }
        }

        public static string FinesFile
        {
            get
            {
                return FromSettings(PathSetting.FinesFile)
                       ?? Path.Combine(DataRoot, FinesFolderName, FinesFileName);
            }
        }

        /// <summary>
        /// Путь задан пользователем, а не выведен по умолчанию.
        /// Для такого пути отсутствующий файл — ошибка: создавать вместо него
        /// файл-образец нельзя, пользователь ждёт свои данные.
        /// </summary>
        public static bool IsConfigured(PathSetting setting)
        {
            return _settings != null && !string.IsNullOrWhiteSpace(_settings.GetPath(setting));
        }

        /// <summary>Путь, который получится при пустой настройке — показывается в окне настроек.</summary>
        public static string DefaultPath(PathSetting setting)
        {
            switch (setting)
            {
                case PathSetting.SharedRoot:
                    return FromConfig("SharedRoot") ?? DataRoot;
                case PathSetting.CarsFile:
                    return Path.Combine(SharedRoot, CarsFolderName, CarsFileName);
                case PathSetting.PeopleFile:
                    return Path.Combine(SharedRoot, PeopleFolderName, PeopleFileName);
                case PathSetting.FinesFile:
                    return Path.Combine(DataRoot, FinesFolderName, FinesFileName);
                default:
                    return FromConfig("DataRoot") ?? AppFolder;
            }
        }

        /// <summary>Папка сканов раздела относительно корня документов, например «Штрафы\Сканы».</summary>
        public static string ScansRelativeFolder(string sectionFolderName)
        {
            return Path.Combine(sectionFolderName, ScansFolderName);
        }

        /// <summary>Создаёт каталог для файла, если его ещё нет.</summary>
        public static void EnsureFolderFor(string filePath)
        {
            var folder = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(folder))
                Directory.CreateDirectory(folder);
        }

        private static string FromSettings(PathSetting setting)
        {
            if (_settings == null)
                return null;

            return MakeAbsolute(_settings.GetPath(setting));
        }

        private static string FromConfig(string key)
        {
            return MakeAbsolute(ConfigurationManager.AppSettings[key]);
        }

        private static string MakeAbsolute(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            value = value.Trim();
            return Path.IsPathRooted(value) ? value : Path.Combine(AppFolder, value);
        }
    }
}
