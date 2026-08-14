using System;

namespace ParkApp.components.Domain
{
    /// <summary>
    /// Штраф (постановление об административном правонарушении).
    /// Первичный ключ — номер постановления.
    /// </summary>
    public class Fine
    {
        /// <summary>Номер постановления. Уникален, после создания не меняется.</summary>
        public string ResolutionNumber { get; set; }

        /// <summary>Дата постановления.</summary>
        public DateTime ResolutionDate { get; set; }

        /// <summary>Дата нарушения.</summary>
        public DateTime ViolationDate { get; set; }

        /// <summary>Место нарушения. Опционально.</summary>
        public string ViolationPlace { get; set; }

        /// <summary>VIN машины. Ссылка на <see cref="Car.Vin"/>.</summary>
        public string CarVin { get; set; }

        /// <summary>ФИО водителя. Может быть пустым: постановление приходит на владельца.</summary>
        public string DriverName { get; set; }

        /// <summary>Сумма штрафа, руб.</summary>
        public decimal Amount { get; set; }

        /// <summary>Относительный путь к скану постановления внутри каталога данных.</summary>
        public string ScanPath { get; set; }
    }
}
