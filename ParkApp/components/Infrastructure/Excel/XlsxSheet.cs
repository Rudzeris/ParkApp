using System.Collections.Generic;

namespace ParkApp.components.Infrastructure.Excel
{
    /// <summary>Лист книги для записи: имя, шапка и строки.</summary>
    public class XlsxSheet
    {
        public XlsxSheet(string name, IList<string> headers, IEnumerable<IList<XlsxCell>> rows)
        {
            Name = name;
            Headers = headers ?? new List<string>();
            Rows = rows ?? new List<IList<XlsxCell>>();
        }

        public string Name { get; private set; }
        public IList<string> Headers { get; private set; }
        public IEnumerable<IList<XlsxCell>> Rows { get; private set; }

        /// <summary>Лист-справочник: один столбец «Название» и значения под ним.</summary>
        public static XlsxSheet Lookup(string name, IEnumerable<string> values)
        {
            var rows = new List<IList<XlsxCell>>();
            foreach (var value in values)
                rows.Add(new List<XlsxCell> { XlsxCell.Text(value) });

            return new XlsxSheet(name, new List<string> { "Название" }, rows);
        }
    }
}
