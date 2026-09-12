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
            : this(vin, null, display)
        {
        }

        public CarOption(string vin, string plate, string display)
            : this(vin, plate, display, null)
        {
        }

        public CarOption(string vin, string plate, string display, string[] searchFields)
        {
            Vin = vin;
            Plate = plate;
            Display = display;
            SearchFields = searchFields ?? new[] { display };
        }

        /// <summary>
        /// По чему ищется машина: все её номера, VIN и оба написания марки.
        /// Искать по строке списка нельзя — в ней значения склеены разделителями,
        /// и «точное совпадение» перестало бы быть точным.
        /// </summary>
        public string[] SearchFields { get; private set; }

        /// <summary>VIN машины. null — «все машины» в фильтре.</summary>
        public string Vin { get; private set; }

        /// <summary>Первый гос. номер — по нему книга постановлений связывается с машиной.</summary>
        public string Plate { get; private set; }

        public string Display { get; private set; }

        public static CarOption ForCar(Car car)
        {
            return new CarOption(car.Vin, FirstPlate(car), Describe(car), SearchFieldsOf(car));
        }

        private static string[] SearchFieldsOf(Car car)
        {
            if (car == null)
                return new string[0];

            var fields = new List<string> { car.Model, car.ModelLatin, car.Vin };

            if (car.Numbers != null)
                fields.AddRange(car.Numbers.Where(n => n != null).Select(n => n.Text));

            return fields.Where(f => !string.IsNullOrWhiteSpace(f)).ToArray();
        }

        /// <summary>Первый гос. номер машины.</summary>
        public static string FirstPlate(Car car)
        {
            if (car == null || car.Numbers == null)
                return null;

            var number = car.Numbers.FirstOrDefault(n => n != null && !string.IsNullOrWhiteSpace(n.Text));
            return number == null ? null : number.Text.Trim();
        }

        /// <summary>«УАЗ-3163 · 0123АВ, А123ВС16 · XTT316300E0012345»</summary>
        public static string Describe(Car car)
        {
            if (car == null)
                return "машина не найдена";

            var model = ModelText(car);
            var plates = Plates(car.Numbers);

            return plates.Length > 0
                ? string.Format("{0} · {1} · {2}", model, plates, car.Vin)
                : string.Format("{0} · {1}", model, car.Vin);
        }

        /// <summary>Марка машины; в таблице она может быть не заполнена.</summary>
        public static string ModelText(Car car)
        {
            if (car == null || string.IsNullOrWhiteSpace(car.Model))
                return "без марки";

            return car.Model.Trim();
        }

        /// <summary>Местонахождение словами.</summary>
        public static string LocationText(Location? location)
        {
            if (!location.HasValue)
                return "—";

            return location.Value == Domain.Location.Ppd ? "ППД" : "ВО";
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
