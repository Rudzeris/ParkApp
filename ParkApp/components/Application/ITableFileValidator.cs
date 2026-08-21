using System.Collections.Generic;

namespace ParkApp.components.Application
{
    /// <summary>Какая таблица ожидается в файле.</summary>
    public enum TableFileKind
    {
        Cars,
        People,
        Fines,
        Dispatch
    }

    /// <summary>Результат проверки выбранного файла.</summary>
    public class TableFileCheck
    {
        public TableFileCheck(bool isValid, string problem, IReadOnlyList<string> missingColumns,
            bool sheetMissing, IReadOnlyList<string> availableSheets)
        {
            IsValid = isValid;
            Problem = problem;
            MissingColumns = missingColumns ?? new List<string>();
            SheetMissing = sheetMissing;
            AvailableSheets = availableSheets ?? new List<string>();
        }

        /// <summary>Файл пригоден: лист и обязательные столбцы на месте.</summary>
        public bool IsValid { get; private set; }

        /// <summary>Почему файл не подходит. Пусто, если подходит.</summary>
        public string Problem { get; private set; }

        /// <summary>
        /// Известные, но не найденные столбцы. Не мешают работать —
        /// эти данные просто не будут читаться.
        /// </summary>
        public IReadOnlyList<string> MissingColumns { get; private set; }

        /// <summary>
        /// Файл открылся, но листа с ожидаемым именем в нём нет.
        /// Это поправимо: лист можно выбрать из <see cref="AvailableSheets"/>.
        /// </summary>
        public bool SheetMissing { get; private set; }

        /// <summary>Листы, которые есть в книге.</summary>
        public IReadOnlyList<string> AvailableSheets { get; private set; }
    }

    /// <summary>
    /// Проверка файла до того, как его назначат рабочим: пользователь должен
    /// узнать «это не тот файл» при выборе, а не при следующем открытии окна.
    /// </summary>
    public interface ITableFileValidator
    {
        /// <param name="sheetName">Ожидаемое имя листа. Пусто — имя по умолчанию.</param>
        TableFileCheck Check(TableFileKind kind, string path, string sheetName);

        /// <summary>Имена листов книги. Пустой список, если файл не читается.</summary>
        IReadOnlyList<string> GetSheetNames(string path);
    }
}
