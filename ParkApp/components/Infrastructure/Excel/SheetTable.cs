using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ParkApp.components.Infrastructure.Excel
{
    /// <summary>
    /// Лист как «шапка + строки». Столбцы ищутся по названию, а не по номеру:
    /// в живых таблицах столбцы переставляют и переименовывают.
    /// </summary>
    public class SheetTable
    {
        private readonly List<string> _headers;
        private readonly List<string[]> _rows;

        private SheetTable(List<string> headers, List<string[]> rows)
        {
            _headers = headers;
            _rows = rows;
        }

        public IList<string> Headers
        {
            get { return _headers; }
        }

        public IList<string[]> Rows
        {
            get { return _rows; }
        }

        /// <summary>Первая непустая строка считается шапкой, остальные — данными.</summary>
        public static SheetTable FromRows(IList<string[]> rawRows)
        {
            var headers = new List<string>();
            var rows = new List<string[]>();
            var headerFound = false;

            foreach (var row in rawRows)
            {
                var isEmpty = row.All(string.IsNullOrWhiteSpace);

                if (!headerFound)
                {
                    if (isEmpty)
                        continue;

                    headers.AddRange(row.Select(value => (value ?? string.Empty).Trim()));
                    headerFound = true;
                    continue;
                }

                if (isEmpty)
                    continue;

                rows.Add(row);
            }

            return new SheetTable(headers, rows);
        }

        /// <summary>
        /// Номер столбца по любому из названий-синонимов. -1, если ни одного нет.
        /// </summary>
        public int Column(params string[] names)
        {
            foreach (var name in names)
            {
                var wanted = Normalize(name);

                for (var i = 0; i < _headers.Count; i++)
                {
                    if (Normalize(_headers[i]) == wanted)
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
