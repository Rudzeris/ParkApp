using System;

namespace ParkApp.components.Application
{
    /// <summary>
    /// Даты по-русски для печатных форм: «На «22» августа 2026 г.».
    /// Своя таблица месяцев, а не культура ОС: у рабочих мест она может быть любой,
    /// а документ должен выглядеть одинаково.
    /// </summary>
    public static class RussianDate
    {
        private static readonly string[] Months =
        {
            "января", "февраля", "марта", "апреля", "мая", "июня",
            "июля", "августа", "сентября", "октября", "ноября", "декабря"
        };

        public static string Day(DateTime date)
        {
            return date.Day.ToString("00");
        }

        public static string Month(DateTime date)
        {
            return Months[date.Month - 1];
        }

        public static string Year(DateTime date)
        {
            return date.Year.ToString("0000");
        }

        /// <summary>«22.08.26» — как в таблице наряда.</summary>
        public static string ShortDate(DateTime date)
        {
            return date.ToString("dd.MM.yy");
        }

        /// <summary>«06:00».</summary>
        public static string Time(DateTime date)
        {
            return date.ToString("HH:mm");
        }
    }
}
