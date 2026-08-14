using System;

namespace ParkApp.components.Application
{
    /// <summary>
    /// Условия отбора штрафов. Пустые поля означают «без ограничения».
    /// </summary>
    public class FineFilter
    {
        /// <summary>VIN машины.</summary>
        public string CarVin { get; set; }

        /// <summary>Водитель: ссылка на Person.Id.</summary>
        public int? DriverId { get; set; }

        /// <summary>Начало периода по дате нарушения (включительно).</summary>
        public DateTime? From { get; set; }

        /// <summary>Конец периода по дате нарушения (включительно).</summary>
        public DateTime? To { get; set; }

        /// <summary>Поиск по номеру постановления и месту нарушения.</summary>
        public string Text { get; set; }
    }
}
