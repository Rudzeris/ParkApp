using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ParkApp.components.Application;
using ParkApp.components.Domain;
using ParkApp.components.Infrastructure.Excel;

namespace ParkApp.components.Infrastructure
{
    /// <summary>
    /// Машины читаются из «Машины\Машины.xlsx» — той таблицы, которую уже ведут в Excel.
    /// Столбцы ищутся по названиям с синонимами, лишние столбцы просто не читаются.
    ///
    /// Репозиторий пока только на чтение: редактора машин в приложении нет, а перезапись
    /// файла целиком потеряла бы столбцы, которые приложение ещё не знает (ПТС, ПФМ, мощность).
    /// Запись появится на этапе 1 вместе с карточкой машины — тогда неизвестные столбцы
    /// нужно будет сохранять как есть.
    /// </summary>
    public class ExcelCarRepository : ICarRepository
    {
        private const string SheetName = "Машины";
        private static readonly object Sync = new object();

        /// <summary>Войсковой знак: четыре цифры и две буквы, например 0123АВ.</summary>
        private static readonly Regex ArmyPlate = new Regex(@"^\d{4}[A-ZА-Я]{2}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly char[] NumberSeparators = { ',', ';', '/', '|', '\n', '\r' };

        public Task<IReadOnlyList<Car>> GetAllAsync()
        {
            lock (Sync)
            {
                IReadOnlyList<Car> cars = Load();
                return Task.FromResult(cars);
            }
        }

        public Task AddAsync(Car car)
        {
            throw ReadOnly();
        }

        public Task UpdateAsync(Car car)
        {
            throw ReadOnly();
        }

        public Task DeleteAsync(Guid id)
        {
            throw ReadOnly();
        }

        private static NotSupportedException ReadOnly()
        {
            return new NotSupportedException(
                "Редактирование машин из приложения появится на этапе 1. " +
                "Пока правьте файл «Машины\\Машины.xlsx» в Excel.");
        }

        private static List<Car> Load()
        {
            var path = AppPaths.CarsFile;

            if (!File.Exists(path))
                CreateDemoFile(path);

            var sheet = XlsxReader.Read(path, SheetName);

            var vinColumn = sheet.Column("VIN", "ВИН", "Номер кузова");
            if (vinColumn < 0)
                throw new InvalidOperationException(
                    "В файле «Машины.xlsx» не найден столбец «VIN». " +
                    "VIN — ключ машины, без него связать штрафы не с чем.");

            var modelColumn = sheet.Column("Модель", "Марка", "Марка, модель", "Марка/модель");
            var yearColumn = sheet.Column("Год выпуска", "Год");
            var locationColumn = sheet.Column("Местонахождение", "Место", "Расположение");
            var armyColumn = sheet.Column("Войсковой номер", "Военный номер", "Войсковой");
            var civilColumn = sheet.Column("Гражданский номер", "Гражданский");

            // на случай, если номера лежат в общих столбцах «Гос. номер 1», «Гос. номер 2»
            var plateColumns = sheet
                .ColumnsStartingWith("Гос. номер", "Госномер", "ГРЗ", "Номер машины", "Рег. знак", "Регистрационный знак")
                .Where(index => index != armyColumn && index != civilColumn)
                .ToList();

            var cars = new List<Car>();

            foreach (var row in sheet.Rows)
            {
                var vin = SheetTable.GetString(row, vinColumn);
                if (vin == null)
                    continue;

                var car = new Car
                {
                    Id = Guid.NewGuid(),
                    Vin = NormalizeVin(vin),
                    Model = SheetTable.GetString(row, modelColumn) ?? "не указана",
                    Year = SheetTable.GetInt(row, yearColumn) ?? 0,
                    Location = ParseLocation(SheetTable.GetString(row, locationColumn)),
                    Numbers = ReadNumbers(row, armyColumn, civilColumn, plateColumns)
                };

                cars.Add(car);
            }

            return cars;
        }

        private static List<CarNumber> ReadNumbers(string[] row, int armyColumn, int civilColumn, IEnumerable<int> plateColumns)
        {
            var numbers = new List<CarNumber>();

            AddNumbers(numbers, SheetTable.GetString(row, armyColumn), NumberType.Army);
            AddNumbers(numbers, SheetTable.GetString(row, civilColumn), NumberType.NoArmy);

            foreach (var column in plateColumns)
            {
                var value = SheetTable.GetString(row, column);
                AddNumbers(numbers, value, null);
            }

            return numbers;
        }

        /// <summary>В одной ячейке может быть несколько знаков через запятую или слэш.</summary>
        private static void AddNumbers(List<CarNumber> numbers, string value, NumberType? type)
        {
            if (value == null)
                return;

            foreach (var part in value.Split(NumberSeparators, StringSplitOptions.RemoveEmptyEntries))
            {
                var text = part.Trim();
                if (text.Length == 0)
                    continue;

                if (numbers.Any(n => string.Equals(n.Text, text, StringComparison.CurrentCultureIgnoreCase)))
                    continue;

                numbers.Add(new CarNumber
                {
                    Text = text,
                    Type = type ?? GuessType(text)
                });
            }
        }

        private static NumberType GuessType(string plate)
        {
            var cleaned = new string(plate.Where(char.IsLetterOrDigit).ToArray());
            return ArmyPlate.IsMatch(cleaned) ? NumberType.Army : NumberType.NoArmy;
        }

        /// <summary>
        /// VIN приводится к верхнему регистру, кириллические двойники латинских букв
        /// заменяются — иначе одна и та же машина в двух таблицах не совпадёт.
        /// </summary>
        private static string NormalizeVin(string vin)
        {
            const string cyrillic = "АВЕКМНОРСТУХ";
            const string latin = "ABEKMHOPCTYX";

            var upper = vin.Trim().ToUpperInvariant();
            var result = upper.ToCharArray();

            for (var i = 0; i < result.Length; i++)
            {
                var index = cyrillic.IndexOf(result[i]);
                if (index >= 0)
                    result[i] = latin[index];
            }

            return new string(result);
        }

        private static Location ParseLocation(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return Location.Park;

            var normalized = value.Trim().ToLowerInvariant();

            if (normalized.Contains("сво") || normalized.Contains("2"))
                return Location.SVO;

            return Location.Park;
        }

        /// <summary>
        /// Создаёт файл-образец, чтобы приложение запускалось на чистой машине.
        /// Заменяется реальной таблицей парка.
        /// </summary>
        private static void CreateDemoFile(string path)
        {
            var headers = new[]
            {
                "VIN", "Модель", "Войсковой номер", "Гражданский номер", "Год выпуска", "Местонахождение"
            };

            var rows = new List<IList<XlsxCell>>
            {
                new List<XlsxCell>
                {
                    XlsxCell.Text("VIN1"), XlsxCell.Text("Lada"), XlsxCell.Text("0123АВ"),
                    XlsxCell.Text("А0123ВС"), XlsxCell.Number(2000), XlsxCell.Text("Парк")
                },
                new List<XlsxCell>
                {
                    XlsxCell.Text("VIN2"), XlsxCell.Text("BMW"), XlsxCell.Text("5555СЕ"),
                    XlsxCell.Text("А4444ЕС"), XlsxCell.Number(2010), XlsxCell.Text("Парк")
                },
                new List<XlsxCell>
                {
                    XlsxCell.Text("VIN3"), XlsxCell.Text("Haval"), XlsxCell.Text("0001СВ"),
                    XlsxCell.Empty, XlsxCell.Number(2020), XlsxCell.Text("СВО")
                }
            };

            XlsxWriter.Write(path, SheetName, headers, rows);
        }
    }
}
