using System;
using System.Configuration;
using System.IO;

namespace ParkApp.components.Infrastructure
{
    /// <summary>
    /// Раскладка данных на диске:
    /// <code>
    /// &lt;общие справочники&gt;/          SharedRoot — одинаковы на всех рабочих местах
    /// ├── Машины/Машины.xlsx
    /// └── Люди/Люди.xlsx
    ///
    /// &lt;свои документы&gt;/             DataRoot — у каждого своё
    /// └── Штрафы/
    ///     ├── Штрафы.xlsx
    ///     └── Сканы/
    /// </code>
    /// Оба корня задаются в App.config. Пустой DataRoot — каталог приложения,
    /// пустой SharedRoot — тот же каталог, что и DataRoot.
    ///
    /// Разделение нужно потому, что справочники (машины, люди) одинаковы у всех,
    /// а документы каждый ведёт свои. Справочники можно положить на общую папку
    /// и не рассылать копии: приложение их только читает, а параллельное чтение
    /// одного файла безопасно.
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

        /// <summary>Корень своих документов.</summary>
        public static string DataRoot
        {
            get { return Resolve("DataRoot", AppDomain.CurrentDomain.BaseDirectory); }
        }

        /// <summary>Корень общих справочников. По умолчанию совпадает с <see cref="DataRoot"/>.</summary>
        public static string SharedRoot
        {
            get { return Resolve("SharedRoot", DataRoot); }
        }

        public static string CarsFile
        {
            get { return Path.Combine(SharedRoot, CarsFolderName, CarsFileName); }
        }

        public static string PeopleFile
        {
            get { return Path.Combine(SharedRoot, PeopleFolderName, PeopleFileName); }
        }

        public static string FinesFile
        {
            get { return Path.Combine(DataRoot, FinesFolderName, FinesFileName); }
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

        /// <summary>Значение из App.config; относительный путь считается от каталога приложения.</summary>
        private static string Resolve(string settingKey, string fallback)
        {
            var configured = ConfigurationManager.AppSettings[settingKey];

            if (string.IsNullOrWhiteSpace(configured))
                return fallback;

            configured = configured.Trim();
            return Path.IsPathRooted(configured)
                ? configured
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configured);
        }
    }
}
