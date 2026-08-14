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
        Date
    }

    /// <summary>
    /// Значение ячейки для записи. Тип нужен, чтобы в Excel числа складывались,
    /// а даты показывались датами, а не «45900».
    /// </summary>
    public class XlsxCell
    {
        /// <summary>Excel считает дни от 30.12.1899 (с учётом его же ошибки с 1900 годом).</summary>
        private static readonly DateTime SerialEpoch = new DateTime(1899, 12, 30);

        private XlsxCell(string value, XlsxCellKind kind)
        {
            Value = value;
            Kind = kind;
        }

        public string Value { get; private set; }
        public XlsxCellKind Kind { get; private set; }

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

        /// <summary>Обратное преобразование: номер дня Excel в дату.</summary>
        public static DateTime FromSerial(double serial)
        {
            return SerialEpoch.AddDays(serial);
        }
    }
}
