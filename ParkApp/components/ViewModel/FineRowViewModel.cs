using System;
using ParkApp.components.Domain;

namespace ParkApp.components.ViewModel
{
    /// <summary>
    /// Строка списка штрафов: сам штраф плюс данные машины, подтянутые по VIN.
    /// </summary>
    public class FineRowViewModel
    {
        public FineRowViewModel(Fine fine, Car car, bool hasScan)
        {
            Fine = fine;
            Car = car;
            HasScan = hasScan;

            // ссылка может «повиснуть», если машину удалили — показываем это вместо падения
            CarModel = car != null ? car.Model : "машина не найдена";
            CarPlates = car != null ? CarOption.Plates(car.Numbers) : fine.CarVin;
        }

        public Fine Fine { get; private set; }
        public Car Car { get; private set; }
        public bool HasScan { get; private set; }

        public string CarModel { get; private set; }
        public string CarPlates { get; private set; }

        public string ResolutionNumber { get { return Fine.ResolutionNumber; } }
        public DateTime ResolutionDate { get { return Fine.ResolutionDate; } }
        public DateTime ViolationDate { get { return Fine.ViolationDate; } }
        public decimal Amount { get { return Fine.Amount; } }

        public string ViolationPlace
        {
            get { return string.IsNullOrWhiteSpace(Fine.ViolationPlace) ? "—" : Fine.ViolationPlace; }
        }

        public string DriverName
        {
            get { return string.IsNullOrWhiteSpace(Fine.DriverName) ? "не установлен" : Fine.DriverName; }
        }

        /// <summary>Отметка о вложенном скане постановления.</summary>
        public string ScanMark { get { return HasScan ? "есть" : "—"; } }
    }
}
