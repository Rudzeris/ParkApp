using System;
using System.Collections.Generic;
using System.Linq;

namespace ParkApp.components.Application
{
    /// <summary>
    /// Значения, которые можно выбрать в наряде. Руками в таблице наряда
    /// ничего не набирают: набранное с клавиатуры расходится опечатками
    /// («Перевозка л/с», «перевозка ЛС», «Перевозка личного состава»), а потом
    /// не сходится ни в наряде, ни в путевом листе.
    ///
    /// Списки ведёт пользователь в Excel, на листе «Списки» файла нарядов —
    /// по столбцу на список. Приложение их только читает и дополняет теми
    /// значениями, которые уже встречаются в данных: иначе выбор из списка
    /// затёр бы то, что было записано раньше.
    /// </summary>
    public class DispatchChoices
    {
        public const string GroupColumn = "Группа";
        public const string OperationGroupColumn = "Группа эксплуатации";
        public const string PurposeColumn = "Для каких целей назначается";
        public const string RouteColumn = "Маршрут движения";
        public const string AssignmentColumn = "В чьё распоряжение";
        public const string NotesColumn = "Примечание";

        /// <summary>Столбцы листа «Списки» — они же заголовки колонок в окне наряда.</summary>
        public static readonly string[] Columns =
        {
            GroupColumn, OperationGroupColumn, PurposeColumn,
            RouteColumn, AssignmentColumn, NotesColumn
        };

        public DispatchChoices()
        {
            Groups = new List<string>();
            OperationGroups = new List<string>();
            Purposes = new List<string>();
            Routes = new List<string>();
            Assignments = new List<string>();
            Notes = new List<string>();
        }

        public IList<string> Groups { get; set; }
        public IList<string> OperationGroups { get; set; }
        public IList<string> Purposes { get; set; }
        public IList<string> Routes { get; set; }
        public IList<string> Assignments { get; set; }
        public IList<string> Notes { get; set; }

        public IList<string> ByColumn(string column)
        {
            switch (column)
            {
                case GroupColumn: return Groups;
                case OperationGroupColumn: return OperationGroups;
                case PurposeColumn: return Purposes;
                case RouteColumn: return Routes;
                case AssignmentColumn: return Assignments;
                case NotesColumn: return Notes;
                default: return new List<string>();
            }
        }

        public void SetByColumn(string column, IList<string> values)
        {
            switch (column)
            {
                case GroupColumn: Groups = values; break;
                case OperationGroupColumn: OperationGroups = values; break;
                case PurposeColumn: Purposes = values; break;
                case RouteColumn: Routes = values; break;
                case AssignmentColumn: Assignments = values; break;
                case NotesColumn: Notes = values; break;
            }
        }

        /// <summary>
        /// Добавляет к списку значения, которые уже стоят в данных.
        /// Без этого выбор «только из списка» молча потерял бы всё,
        /// что записали до появления списков или правкой файла в Excel.
        /// </summary>
        public void Include(string column, IEnumerable<string> used)
        {
            if (used == null)
                return;

            var values = ByColumn(column);
            var known = new HashSet<string>(values, StringComparer.CurrentCultureIgnoreCase);

            foreach (var value in used)
            {
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                var text = value.Trim();
                if (known.Add(text))
                    values.Add(text);
            }
        }

        /// <summary>Списки-заготовка при первом запуске: их правят в Excel.</summary>
        public static DispatchChoices Default()
        {
            return new DispatchChoices
            {
                Groups = new List<string> { "Группа боевых машин", "Группа машин обеспечения" },
                OperationGroups = new List<string> { "тр.", "стр.", "уч.-бо." },
                Purposes = new List<string>
                {
                    "Перевозка личного состава",
                    "Перевозка материальных средств",
                    "Обеспечение занятий",
                    "Хозяйственные нужды"
                },
                Routes = new List<string> { "ППД — полигон — ППД", "ППД — город — ППД" },
                Assignments = new List<string>(),
                Notes = new List<string>()
            };
        }

        public IEnumerable<string> AllOf(string column)
        {
            return ByColumn(column).Where(v => !string.IsNullOrWhiteSpace(v));
        }
    }
}
