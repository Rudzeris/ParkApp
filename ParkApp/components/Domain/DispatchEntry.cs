using System;

namespace ParkApp.components.Domain
{
    /// <summary>
    /// Строка наряда на выход: одна машина, которой разрешён выезд в этот день.
    /// Марка и ГРЗ сохраняются снимком — наряд подписан, и задним числом
    /// он меняться не должен, даже если машину переименовали в реестре.
    /// </summary>
    public class DispatchEntry
    {
        /// <summary>Дата наряда.</summary>
        public DateTime Date { get; set; }

        /// <summary>Номер наряда, одинаковый для всех строк одной даты.</summary>
        public string OrderNumber { get; set; }

        public string CarVin { get; set; }
        public string CarBrand { get; set; }
        public string CarPlate { get; set; }

        /// <summary>Заголовок группы в наряде.</summary>
        public string GroupName { get; set; }

        public string OperationGroup { get; set; }
        public string Purpose { get; set; }
        public string Route { get; set; }
        public string Assignment { get; set; }

        public DateTime DepartureAt { get; set; }
        public DateTime ReturnAt { get; set; }

        public string Notes { get; set; }
    }
}
