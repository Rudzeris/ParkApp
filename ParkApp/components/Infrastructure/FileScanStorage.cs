using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ParkApp.components.Application;

namespace ParkApp.components.Infrastructure
{
    /// <summary>
    /// Сканы лежат рядом со своей таблицей: «Штрафы\Сканы\&lt;номер постановления&gt;.pdf».
    /// В записи хранится относительный путь — тогда всю папку данных можно перенести
    /// на другой ПК или на сетевую шару без правки ссылок.
    /// </summary>
    public class FileScanStorage : IScanStorage
    {
        public string Attach(ScanCategory category, string entityKey, string sourceFilePath)
        {
            if (string.IsNullOrWhiteSpace(sourceFilePath))
                throw new ArgumentException("Не указан файл скана.", "sourceFilePath");

            if (!File.Exists(sourceFilePath))
                throw new FileNotFoundException("Файл скана не найден.", sourceFilePath);

            var fileName = MakeSafe(entityKey) + Path.GetExtension(sourceFilePath);
            var relativePath = Path.Combine(AppPaths.ScansRelativeFolder(FolderFor(category)), fileName);
            var fullPath = Path.Combine(AppPaths.DataRoot, relativePath);

            AppPaths.EnsureFolderFor(fullPath);

            // именно копирование: исходник пользователя должен остаться на месте
            File.Copy(sourceFilePath, fullPath, true);

            return relativePath;
        }

        public string GetFullPath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return null;

            return Path.Combine(AppPaths.DataRoot, relativePath);
        }

        public bool Exists(string relativePath)
        {
            var fullPath = GetFullPath(relativePath);
            return fullPath != null && File.Exists(fullPath);
        }

        public void Open(string relativePath)
        {
            var fullPath = GetFullPath(relativePath);
            if (fullPath == null || !File.Exists(fullPath))
                throw new FileNotFoundException("Скан не найден. Возможно, файл удалили или переименовали.");

            Process.Start(new ProcessStartInfo(fullPath) { UseShellExecute = true });
        }

        public void Delete(string relativePath)
        {
            var fullPath = GetFullPath(relativePath);
            if (fullPath != null && File.Exists(fullPath))
                File.Delete(fullPath);
        }

        private static string FolderFor(ScanCategory category)
        {
            switch (category)
            {
                case ScanCategory.Car:
                    return AppPaths.CarsFolderName;
                case ScanCategory.Insurance:
                    return AppPaths.InsurancesFolderName;
                case ScanCategory.Maintenance:
                    return AppPaths.MaintenanceFolderName;
                default:
                    return AppPaths.FinesFolderName;
            }
        }

        /// <summary>Убирает из имени символы, недопустимые в файловой системе.</summary>
        private static string MakeSafe(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "_";

            var invalid = Path.GetInvalidFileNameChars();
            var safe = value.Trim().Select(c => invalid.Contains(c) ? '_' : c).ToArray();
            return new string(safe);
        }
    }
}
