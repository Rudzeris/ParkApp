using System;
using System.Configuration;
using System.IO;

namespace ParkApp.components.Infrastructure
{
    /// <summary>
    /// Раскладка данных на диске:
    /// <code>
    /// &lt;корень данных&gt;/
    /// ├── Машины/
    /// │   ├── Машины.xlsx
    /// │   └── Сканы/          СРТС, ПТС
    /// └── Штрафы/
    ///     ├── Штрафы.xlsx
    ///     └── Сканы/          постановления
    /// </code>
    /// Корень задаётся в App.config (ключ DataRoot). Пустое значение — каталог приложения.
    /// Отдельная настройка нужна, чтобы позже перевести данные на сетевую шару без пересборки.
    /// </summary>
    public static class AppPaths
    {
        public const string CarsFolderName = "Машины";
        public const string FinesFolderName = "Штрафы";
        public const string InsurancesFolderName = "Страховки";
        public const string MaintenanceFolderName = "ТО";
        public const string ScansFolderName = "Сканы";

        public const string CarsFileName = "Машины.xlsx";
        public const string FinesFileName = "Штрафы.xlsx";

        /// <summary>Корень данных. Относительный путь из настройки считается от каталога приложения.</summary>
        public static string DataRoot
        {
            get
            {
                var configured = ConfigurationManager.AppSettings["DataRoot"];
                var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

                if (string.IsNullOrWhiteSpace(configured))
                    return baseDirectory;

                configured = configured.Trim();
                return Path.IsPathRooted(configured)
                    ? configured
                    : Path.Combine(baseDirectory, configured);
            }
        }

        public static string CarsFile
        {
            get { return Path.Combine(DataRoot, CarsFolderName, CarsFileName); }
        }

        public static string FinesFile
        {
            get { return Path.Combine(DataRoot, FinesFolderName, FinesFileName); }
        }

        /// <summary>Папка сканов раздела относительно корня данных, например «Штрафы\Сканы».</summary>
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
    }
}
