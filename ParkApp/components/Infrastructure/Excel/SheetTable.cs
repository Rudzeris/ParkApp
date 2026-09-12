using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ParkApp.components.Infrastructure.Excel
{
    /// <summary>
    /// Ячейка как она лежит в файле. Нужна, чтобы вернуть на место содержимое
    /// столбцов, которых приложение не знает: иначе дата в чужом столбце
    /// при перезаписи превратится в число.
    /// </summary>
    public class RawCell
    {
        public RawCell(string value, bool isText, int style, string formula = null)
        {
            Value = value;
            IsText = isText;
            Style = style;
            Formula = formula;
        }

        public string Value { get; private set; }
        public bool IsText { get; private set; }
        public int Style { get; private set; }

        /// <summary>
        /// Формула ячейки без «=», если она была. В рабочих книгах заказчика
        /// графы заполняют ссылками на другой лист («Люди!A$1»), и при
        /// перезаписи ссылка обязана вернуться на место: иначе живая связь
        /// молча превратится в разовый текст.
        /// </summary>
        public string Formula { get; private set; }
    }

    /// <summary>
    /// Лист как «шапка + строки». Столбцы ищутся по названию, а не по номеру:
    /// в живых таблицах столбцы переставляют и переименовывают.
    /// </summary>
    public class SheetTable
    {
        private readonly List<string> _headers;
        private readonly List<string[]> _rows;
        private readonly List<RawCell[]> _rawRows;

        private SheetTable(List<string> headers, List<string[]> rows, List<RawCell[]> rawRows)
        {
            _headers = headers;
            _rows = rows;
            _rawRows = rawRows;
        }

        public IList<string> Headers
        {
            get { return _headers; }
        }

        public IList<string[]> Rows
        {
            get { return _rows; }
        }

        /// <summary>Те же строки, но с типом и оформлением ячеек. Порядок совпадает с <see cref="Rows"/>.</summary>
        public IList<RawCell[]> RawRows
        {
            get { return _rawRows; }
        }

        /// <summary>Таблица без строк — только шапка. Нужна, чтобы найти столбцы по списку названий.</summary>
        public static SheetTable FromHeaders(IList<string> headers)
        {
            return new SheetTable(
                headers.Select(h => (h ?? string.Empty).Trim()).ToList(),
                new List<string[]>(),
                new List<RawCell[]>());
        }

        /// <summary>Первая непустая строка считается шапкой, остальные — данными.</summary>
        public static SheetTable FromRows(IList<RawCell[]> sourceRows)
        {
            var headers = new List<string>();
            var rows = new List<string[]>();
            var rawRows = new List<RawCell[]>();
            var headerFound = false;

            foreach (var row in sourceRows)
            {
                var text = row.Select(cell => cell == null ? null : cell.Value).ToArray();
                var isEmpty = text.All(string.IsNullOrWhiteSpace);

                if (!headerFound)
                {
                    if (isEmpty)
                        continue;

                    headers.AddRange(text.Select(value => (value ?? string.Empty).Trim()));
                    headerFound = true;
                    continue;
                }

                if (isEmpty)
                    continue;

                rows.Add(text);
                rawRows.Add(row);
            }

            return new SheetTable(headers, rows, rawRows);
        }

        /// <summary>
        /// Номер столбца по любому из названий-синонимов. -1, если ни одного нет.
        ///
        /// Сначала ищется точное совпадение по всем синонимам, и только потом —
        /// совпадение по началу названия. Порядок важен: в рабочих таблицах шапка
        /// часто содержит пояснение в скобках («Местонахождение(ППД и ВО - enum)»),
        /// но при этом рядом может стоять похожий столбец («Модель и № двигателя»),
        /// который не должен перехватить поиск раньше точного совпадения.
        /// </summary>
        /// <summary>
        /// Все столбцы с таким названием, слева направо. В рабочей таблице
        /// «Марка автомобиля» встречается дважды — по-русски и латиницей.
        /// </summary>
        public IEnumerable<int> Columns(params string[] names)
        {
            var wanted = names.Select(Normalize).Where(n => n.Length > 0).ToList();

            for (var i = 0; i < _headers.Count; i++)
            {
                if (wanted.Contains(Normalize(_headers[i])))
                    yield return i;
            }
        }

        public int Column(params string[] names)
        {
            foreach (var name in names)
            {
                var wanted = Normalize(name);
                if (wanted.Length == 0)
                    continue;

                for (var i = 0; i < _headers.Count; i++)
                {
                    if (Normalize(_headers[i]) == wanted)
                        return i;
                }
            }

            foreach (var name in names)
            {
                var wanted = Normalize(name);
                if (wanted.Length == 0)
                    continue;

                for (var i = 0; i < _headers.Count; i++)
                {
                    if (Normalize(_headers[i]).StartsWith(wanted, StringComparison.Ordinal))
                        return i;
                }
            }

            return -1;
        }

        /// <summary>Номера всех столбцов, чьё название начинается с одного из префиксов («Гос. номер 1», «Гос. номер 2»).</summary>
        public IList<int> ColumnsStartingWith(params string[] prefixes)
        {
            var result = new List<int>();

            for (var i = 0; i < _headers.Count; i++)
            {
                var header = Normalize(_headers[i]);
                if (header.Length == 0)
                    continue;

                if (prefixes.Any(prefix => header.StartsWith(Normalize(prefix), StringComparison.Ordinal)))
                    result.Add(i);
            }

            return result;
        }

        public static string GetString(string[] row, int column)
        {
            if (column < 0 || row == null || column >= row.Length)
                return null;

            var value = row[column];
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        /// <summary>
        /// Дата из ячейки. Excel хранит даты числом, но выгрузки часто содержат текст,
        /// поэтому пробуем оба варианта.
        /// </summary>
        public static DateTime? GetDate(string[] row, int column)
        {
            var value = GetString(row, column);
            if (value == null)
                return null;

            double serial;
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out serial) && serial > 0)
                return XlsxCell.FromSerial(serial).Date;

            DateTime parsed;
            var formats = new[] { "dd.MM.yyyy", "d.M.yyyy", "yyyy-MM-dd", "dd/MM/yyyy" };

            if (DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
                return parsed.Date;

            if (DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out parsed))
                return parsed.Date;

            return null;
        }

        /// <summary>Число из ячейки: терпит пробелы, неразрывные пробелы и запятую как разделитель.</summary>
        public static decimal? GetDecimal(string[] row, int column)
        {
            var value = GetString(row, column);
            if (value == null)
                return null;

            var normalized = new string(value
                .Replace(',', '.')
                .Where(c => char.IsDigit(c) || c == '.' || c == '-')
                .ToArray());

            decimal result;
            return decimal.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out result)
                ? result
                : (decimal?)null;
        }

        /// <summary>
        /// «Да/нет» из ячейки. Понимает «да», «нет», «+», «-», «1», «0», «истина», «ложь».
        /// null — ячейка пустая или значение непонятное.
        /// </summary>
        public static bool? GetYesNo(string[] row, int column)
        {
            var value = GetString(row, column);
            if (value == null)
                return null;

            var normalized = value.Trim().ToLowerInvariant();

            if (normalized == "да" || normalized == "+" || normalized == "1"
                || normalized == "истина" || normalized == "yes" || normalized == "true")
                return true;

            if (normalized == "нет" || normalized == "-" || normalized == "0"
                || normalized == "ложь" || normalized == "no" || normalized == "false")
                return false;

            return null;
        }

        /// <summary>Целое из ячейки: «2010 г.» → 2010, «12 500 км» → 12500.</summary>
        public static int? GetInt(string[] row, int column)
        {
            var value = GetString(row, column);
            if (value == null)
                return null;

            var digits = new string(value.Where(char.IsDigit).ToArray());
            if (digits.Length == 0)
                return null;

            int result;
            return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out result)
                ? result
                : (int?)null;
        }

        /// <summary>Сравнение названий столбцов без учёта регистра, пробелов, точек, «№» и ё/е.</summary>
        private static string Normalize(string header)
        {
            if (string.IsNullOrWhiteSpace(header))
                return string.Empty;

            var cleaned = header
                .ToLowerInvariant()
                .Replace("ё", "е")
                .Where(c => !char.IsWhiteSpace(c) && c != '.' && c != '№' && c != '_' && c != '-')
                .ToArray();

            return new string(cleaned);
        }
    }
}
