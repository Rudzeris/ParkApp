using System;
using ParkApp.components.Application;
using ParkApp.components.Domain;

namespace ParkApp.components.ViewModel
{
    /// <summary>
    /// Строка списка штрафов: сам штраф плюс машина по VIN и водитель по номеру
    /// из справочника людей.
    /// </summary>
    public class FineRowViewModel
    {
        public FineRowViewModel(Fine fine, Car car, Person driver, bool hasScan)
        {
            Fine = fine;
            Car = car;
            Driver = driver;
            HasScan = hasScan;

            // ссылка может «повиснуть», если машину убрали из таблицы — показываем это вместо падения
            CarModel = car != null ? CarOption.ModelText(car) : "машина не найдена";
            CarPlates = car != null ? CarOption.Plates(car.Numbers) : fine.CarVin;
        }

        public Fine Fine { get; private set; }
        public Car Car { get; private set; }
        public Person Driver { get; private set; }
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
            get
            {
                var described = PersonService.Describe(Driver);
                if (described != null)
                    return described;

                return Fine.DriverId.HasValue
                    ? string.Format("не найден (№ {0})", Fine.DriverId.Value)
                    : "не установлен";
            }
        }

        public bool IsPaid { get { return Fine.IsPaid; } }

        /// <summary>«оплачен 12.08.2026», «оплачен» или «не оплачен».</summary>
        public string PaidText
        {
            get
            {
                if (!Fine.IsPaid)
                    return "не оплачен";

                return Fine.PaidDate.HasValue
                    ? string.Format("оплачен {0:dd.MM.yyyy}", Fine.PaidDate.Value)
                    : "оплачен";
            }
        }

        /// <summary>Отметка о вложенном скане постановления.</summary>
        public string ScanMark { get { return HasScan ? "есть" : "—"; } }
    }
}
