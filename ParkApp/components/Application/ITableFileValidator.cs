using System.Collections.Generic;

namespace ParkApp.components.Application
{
    /// <summary>Какая таблица ожидается в файле.</summary>
    public enum TableFileKind
    {
        Cars,
        People,
        Fines
    }

    /// <summary>Результат проверки выбранного файла.</summary>
    public class TableFileCheck
    {
        public TableFileCheck(bool isValid, string problem, IReadOnlyList<string> missingColumns)
        {
            IsValid = isValid;
            Problem = problem;
            MissingColumns = missingColumns ?? new List<string>();
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
    }

    /// <summary>
    /// Проверка файла до того, как его назначат рабочим: пользователь должен
    /// узнать «это не тот файл» при выборе, а не при следующем открытии окна.
    /// </summary>
    public interface ITableFileValidator
    {
        TableFileCheck Check(TableFileKind kind, string path);
    }
}
