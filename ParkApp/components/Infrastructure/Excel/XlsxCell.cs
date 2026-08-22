using System;
using System.Globalization;

namespace ParkApp.components.Infrastructure.Excel
{
    public enum XlsxCellKind
    {
        Empty,
        Text,
        Number,
        Money,
        Date,
        DateTime,

        /// <summary>Значение и оформление берутся из исходного файла как есть.</summary>
        Raw
    }

    /// <summary>
    /// Значение ячейки для записи. Тип нужен, чтобы в Excel числа складывались,
    /// а даты показывались датами, а не «45900».
    /// </summary>
    public class XlsxCell
    {
        /// <summary>Excel считает дни от 30.12.1899 (с учётом его же ошибки с 1900 годом).</summary>
        private static readonly DateTime SerialEpoch = new DateTime(1899, 12, 30);

        private XlsxCell(string value, XlsxCellKind kind, bool isText = false, int style = 0, string formula = null)
        {
            Value = value;
            Kind = kind;
            IsText = isText;
            Style = style;
            Formula = formula;
        }

        public string Value { get; private set; }
        public XlsxCellKind Kind { get; private set; }

        /// <summary>Для Raw: строка это или число.</summary>
        public bool IsText { get; private set; }

        /// <summary>Для Raw: индекс стиля из исходного файла — им держится формат даты.</summary>
        public int Style { get; private set; }

        /// <summary>Для Raw: формула ячейки, если она была. Возвращается на место как есть.</summary>
        public string Formula { get; private set; }

        /// <summary>
        /// Ячейка чужого столбца: приложение её не понимает, но обязано вернуть
        /// на место в том же виде — иначе дата в ней превратится в число.
        /// </summary>
        public static XlsxCell Raw(string value, bool isText, int style, string formula = null)
        {
            return string.IsNullOrEmpty(value) && string.IsNullOrEmpty(formula)
                ? Empty
                : new XlsxCell(value, XlsxCellKind.Raw, isText, style, formula);
        }

        public static readonly XlsxCell Empty = new XlsxCell(null, XlsxCellKind.Empty);

        public static XlsxCell Text(string value)
        {
            return string.IsNullOrEmpty(value) ? Empty : new XlsxCell(value, XlsxCellKind.Text);
        }

        public static XlsxCell Number(decimal? value)
        {
            return value.HasValue
                ? new XlsxCell(value.Value.ToString(CultureInfo.InvariantCulture), XlsxCellKind.Number)
                : Empty;
        }

        public static XlsxCell Money(decimal? value)
        {
            return value.HasValue
                ? new XlsxCell(value.Value.ToString(CultureInfo.InvariantCulture), XlsxCellKind.Money)
                : Empty;
        }

        public static XlsxCell Date(DateTime? value)
        {
            if (!value.HasValue || value.Value == default(DateTime))
                return Empty;

            var serial = (int)(value.Value.Date - SerialEpoch).TotalDays;
            return new XlsxCell(serial.ToString(CultureInfo.InvariantCulture), XlsxCellKind.Date);
        }

        /// <summary>Дата со временем — например, время выезда по наряду.</summary>
        public static XlsxCell DateAndTime(DateTime? value)
        {
            if (!value.HasValue || value.Value == default(DateTime))
                return Empty;

            var serial = (value.Value - SerialEpoch).TotalDays;
            return new XlsxCell(serial.ToString("0.######", CultureInfo.InvariantCulture), XlsxCellKind.DateTime);
        }

        /// <summary>Обратное преобразование: номер дня Excel в дату.</summary>
        public static DateTime FromSerial(double serial)
        {
            return SerialEpoch.AddDays(serial);
        }
    }
}
