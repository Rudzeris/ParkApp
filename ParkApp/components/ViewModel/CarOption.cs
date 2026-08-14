using System.Collections.Generic;
using System.Linq;
using ParkApp.components.Domain;

namespace ParkApp.components.ViewModel
{
    /// <summary>
    /// Машина в выпадающем списке: VIN для связи, строка — для человека.
    /// </summary>
    public class CarOption
    {
        public CarOption(string vin, string display)
        {
            Vin = vin;
            Display = display;
        }

        /// <summary>VIN машины. null — «все машины» в фильтре.</summary>
        public string Vin { get; private set; }

        public string Display { get; private set; }

        public static CarOption ForCar(Car car)
        {
            return new CarOption(car.Vin, Describe(car));
        }

        /// <summary>«BMW · A4444EC, 5555CE · VIN2»</summary>
        public static string Describe(Car car)
        {
            if (car == null)
                return "машина не найдена";

            var plates = Plates(car.Numbers);
            return plates.Length > 0
                ? string.Format("{0} · {1} · {2}", car.Model, plates, car.Vin)
                : string.Format("{0} · {1}", car.Model, car.Vin);
        }

        /// <summary>Гос. номера через запятую.</summary>
        public static string Plates(IEnumerable<CarNumber> numbers)
        {
            if (numbers == null)
                return string.Empty;

            return string.Join(", ", numbers
                .Where(n => n != null && !string.IsNullOrWhiteSpace(n.Text))
                .Select(n => n.Text.Trim()));
        }
    }
}
