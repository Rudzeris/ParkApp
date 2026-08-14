using ParkApp.components.Application;
using ParkApp.components.Domain;

namespace ParkApp.components.ViewModel
{
    /// <summary>
    /// Строка списка машин: машина плюс должностное лицо, подтянутое по номеру
    /// из справочника людей.
    /// </summary>
    public class CarRowViewModel
    {
        public CarRowViewModel(Car car, Person official)
        {
            Car = car;
            Official = official;
        }

        public Car Car { get; private set; }
        public Person Official { get; private set; }

        public string Model { get { return CarOption.ModelText(Car); } }
        public string Plates { get { return CarOption.Plates(Car.Numbers); } }
        public string Vin { get { return Car.Vin; } }
        public string LocationText { get { return CarOption.LocationText(Car.Location); } }

        public string YearText
        {
            get { return Car.Year.HasValue ? Car.Year.Value.ToString() : "—"; }
        }

        public string Affiliation
        {
            get { return string.IsNullOrWhiteSpace(Car.Affiliation) ? "—" : Car.Affiliation; }
        }

        /// <summary>Ответственный: звание и ФИО. Показывает, если ссылка «повисла».</summary>
        public string OfficialText
        {
            get
            {
                var described = PersonService.Describe(Official);
                if (described != null)
                    return described;

                return Car.OfficialId.HasValue
                    ? string.Format("не найден (№ {0})", Car.OfficialId.Value)
                    : "—";
            }
        }
    }
}
