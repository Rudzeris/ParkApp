using System;
using System.IO;

namespace ParkApp.components.Infrastructure
{
    /// <summary>
    /// Единая точка, где приложение решает, куда класть данные и сканы.
    /// При переезде на сервер меняется только этот класс и реализации репозиториев.
    /// </summary>
    public static class AppPaths
    {
        /// <summary>
        /// Корень данных. Взят каталог приложения, а не текущий каталог процесса:
        /// текущий каталог может незаметно измениться после системных диалогов.
        /// </summary>
        public static string DataRoot
        {
            get { return AppDomain.CurrentDomain.BaseDirectory; }
        }

        /// <summary>Корень хранилища сканов.</summary>
        public static string ScansRoot
        {
            get { return Path.Combine(DataRoot, "Scans"); }
        }

        /// <summary>Полный путь к файлу данных в корне.</summary>
        public static string DataFile(string fileName)
        {
            return Path.Combine(DataRoot, fileName);
        }
    }
}
