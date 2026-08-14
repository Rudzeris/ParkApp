using System;

namespace ParkApp.components.Application
{
    /// <summary>
    /// Условия отбора штрафов. Пустые поля означают «без ограничения».
    /// </summary>
    public class FineFilter
    {
        /// <summary>Гос. рег. знак машины.</summary>
        public string CarPlate { get; set; }

        /// <summary>Часть Ф.И.О. правонарушителя.</summary>
        public string OffenderName { get; set; }

        /// <summary>Начало периода по дате правонарушения (включительно).</summary>
        public DateTime? From { get; set; }

        /// <summary>Конец периода по дате правонарушения (включительно).</summary>
        public DateTime? To { get; set; }

        /// <summary>Поиск по номеру постановления, марке и примечанию.</summary>
        public string Text { get; set; }

        /// <summary>Оплата: true — оплаченные, false — неоплаченные, null — все.</summary>
        public bool? IsPaid { get; set; }
    }
}
