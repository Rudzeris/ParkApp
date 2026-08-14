using System.Collections.Generic;

namespace ParkApp.components.Application
{
    /// <summary>Настраиваемые пути к папкам и файлам.</summary>
    public enum PathSetting
    {
        /// <summary>Папка своих документов (штрафы и их сканы).</summary>
        DataRoot,

        /// <summary>Папка общих справочников (машины, люди).</summary>
        SharedRoot,

        /// <summary>Файл машин целиком, в обход папки справочников.</summary>
        CarsFile,

        /// <summary>Файл людей целиком.</summary>
        PeopleFile,

        /// <summary>Файл штрафов целиком.</summary>
        FinesFile
    }

    /// <summary>Путь: как называется, папка это или файл, и какую таблицу проверять при выборе.</summary>
    public class PathSettingDescription
    {
        public PathSettingDescription(PathSetting setting, string title, string hint, bool isFolder, TableFileKind? table)
        {
            Setting = setting;
            Title = title;
            Hint = hint;
            IsFolder = isFolder;
            Table = table;
        }

        public PathSetting Setting { get; private set; }
        public string Title { get; private set; }

        /// <summary>Что будет, если оставить пустым.</summary>
        public string Hint { get; private set; }

        public bool IsFolder { get; private set; }

        /// <summary>Какую таблицу проверять по столбцам. null — путь к папке.</summary>
        public TableFileKind? Table { get; private set; }
    }

    public static class PathSettingCatalog
    {
        private static readonly List<PathSettingDescription> Items = new List<PathSettingDescription>
        {
            new PathSettingDescription(PathSetting.SharedRoot, "Папка общих справочников",
                "пусто — там же, где свои документы", true, null),
            new PathSettingDescription(PathSetting.DataRoot, "Папка своих документов",
                "пусто — рядом с приложением", true, null),
            new PathSettingDescription(PathSetting.CarsFile, "Файл машин",
                "пусто — «Машины\\Машины.xlsx» в папке справочников", false, TableFileKind.Cars),
            new PathSettingDescription(PathSetting.PeopleFile, "Файл людей",
                "пусто — «Люди\\Люди.xlsx» в папке справочников", false, TableFileKind.People),
            new PathSettingDescription(PathSetting.FinesFile, "Файл штрафов",
                "пусто — «Штрафы\\Штрафы.xlsx» в папке документов", false, TableFileKind.Fines)
        };

        public static IReadOnlyList<PathSettingDescription> All
        {
            get { return Items; }
        }
    }
}
