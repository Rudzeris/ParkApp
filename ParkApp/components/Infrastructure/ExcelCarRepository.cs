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
    /// Столбцы ищутся по названиям с синонимами: точное совпадение, иначе по началу названия
    /// (шапка вида «Местонахождение(ППД и ВО - enum)» тоже опознаётся). Лишние столбцы не мешают.
    ///
    /// Репозиторий пока только на чтение: редактора машин в приложении нет, а перезапись
    /// файла целиком потеряла бы столбцы, которые приложение ещё не знает. Запись появится
    /// на этапе 1 — с сохранением неизвестных столбцов как есть.
    /// </summary>
    public class ExcelCarRepository : ICarRepository
    {
        public const string SheetName = "Машины";

        // имена столбцов вынесены сюда, потому что по ним же проверяется файл,
        // который пользователь выбирает в настройках
        public static readonly string[] VinNames = { "VIN", "ВИН" };
        public static readonly string[] ModelNames = { "Марка автомобиля", "Марка", "Марка/модель", "Модель автомобиля" };
        public static readonly string[] YearNames = { "Год выпуска", "Год" };
        public static readonly string[] LocationNames = { "Местонахождение", "Место" };
        public static readonly string[] AffiliationNames = { "Куда относится", "Принадлежность" };
        public static readonly string[] OfficialNames = { "Должностное лицо", "Ответственный" };
        public static readonly string[] VehicleTypeNames = { "Тип машины", "Тип" };
        public static readonly string[] PlatePrefixes =
        {
            "Гос. рег. знак", "Гос. номер", "Госномер", "ГРЗ",
            "Регистрационный знак", "Рег. знак", "Номер машины"
        };
        private static readonly object Sync = new object();

        /// <summary>Войсковой знак: четыре цифры и две буквы, например 0123АВ.</summary>
        private static readonly Regex ArmyPlate = new Regex(@"^\d{4}[A-ZА-Я]{2}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>Мощность вида «220/299» — кВт и л.с.</summary>
        private static readonly Regex PowerPair = new Regex(@"(\d+)\s*/\s*(\d+)", RegexOptions.Compiled);

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

            var vinColumn = sheet.Column(VinNames);
            if (vinColumn < 0)
                throw new InvalidOperationException(
                    "В файле «Машины.xlsx» не найден столбец «VIN». " +
                    "VIN — ключ машины, без него документы не с чем связывать.");

            var modelColumn = sheet.Column(ModelNames);
            var yearColumn = sheet.Column(YearNames);
            var locationColumn = sheet.Column(LocationNames);
            var engineColumn = sheet.Column("Модель и № двигателя", "Модель двигателя");
            var powerColumn = sheet.Column("Мощность двигателя КВт/Л.С.", "Мощность двигателя", "Мощность");
            var chassisColumn = sheet.Column("№ шасси (рама)", "№ шасси", "Шасси", "Рама");
            var bodyColumn = sheet.Column("№ кузова", "Кузов");
            var pfmColumn = sheet.Column("ПФМ");
            var ptsColumn = sheet.Column("ПТС");
            var regCertificateColumn = sheet.Column("Свид. о регистрации", "Свидетельство о регистрации", "СРТС", "СТС");
            var policyColumn = sheet.Column("Страховой полис", "Полис", "ОСАГО");
            var diagnosticColumn = sheet.Column("Диагностическая карта", "Диагностическая");
            var affiliationColumn = sheet.Column(AffiliationNames);
            var officialColumn = sheet.Column(OfficialNames);
            var staffColumn = sheet.Column("Штатная", "Штат");
            var confiscatedColumn = sheet.Column("Конфискат");
            var capacityColumn = sheet.Column("Вместимость");
            var typeColumn = sheet.Column(VehicleTypeNames);
            var colorColumn = sheet.Column("Цвет");
            var volumeColumn = sheet.Column("Объем двигателя", "Объём двигателя");
            var maxMassColumn = sheet.Column("max m (масса)", "max m", "Максимальная масса");
            var massColumn = sheet.Column("m (масса)", "Масса", "m");
            var notesColumn = sheet.Column("Особые отметки", "Примечание");
            var vaiColumn = sheet.Column("ВАИ");
            var receivedColumn = sheet.Column("Получили машину", "Получили");
            var handedOverColumn = sheet.Column("Отдали машину", "Отдали");

            // «m» — слишком короткое название: если точного совпадения не нашлось,
            // поиск по началу мог зацепить тот же столбец, что и «max m»
            if (massColumn >= 0 && massColumn == maxMassColumn)
                massColumn = -1;

            var plateColumns = sheet.ColumnsStartingWith(PlatePrefixes).ToList();

            var cars = new List<Car>();
            var seenVins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in sheet.Rows)
            {
                var rawVin = SheetTable.GetString(row, vinColumn);
                if (rawVin == null)
                    continue;

                var vin = NormalizeVin(rawVin);

                // дубль VIN внутри файла: берём первую строку, остальные пропускаем
                if (!seenVins.Add(vin))
                    continue;

                var car = new Car
                {
                    Id = Guid.NewGuid(),
                    Vin = vin,
                    Model = SheetTable.GetString(row, modelColumn),
                    Year = SheetTable.GetInt(row, yearColumn),
                    Location = ParseLocation(SheetTable.GetString(row, locationColumn)),
                    Numbers = ReadNumbers(row, plateColumns),
                    EngineModel = SheetTable.GetString(row, engineColumn),
                    Chassis = SheetTable.GetString(row, chassisColumn),
                    BodyNumber = SheetTable.GetString(row, bodyColumn),
                    Pfm = SheetTable.GetString(row, pfmColumn),
                    Pts = SheetTable.GetString(row, ptsColumn),
                    RegCertificate = SheetTable.GetString(row, regCertificateColumn),
                    InsurancePolicy = SheetTable.GetString(row, policyColumn),
                    DiagnosticCard = SheetTable.GetString(row, diagnosticColumn),
                    Affiliation = SheetTable.GetString(row, affiliationColumn),
                    OfficialId = SheetTable.GetInt(row, officialColumn),
                    IsStaff = ParseStaff(SheetTable.GetString(row, staffColumn)),
                    IsConfiscated = ParseConfiscated(row, confiscatedColumn),
                    Capacity = SheetTable.GetInt(row, capacityColumn),
                    VehicleType = SheetTable.GetString(row, typeColumn),
                    Color = SheetTable.GetString(row, colorColumn),
                    EngineVolume = SheetTable.GetInt(row, volumeColumn),
                    MaxMass = SheetTable.GetDecimal(row, maxMassColumn),
                    Mass = SheetTable.GetDecimal(row, massColumn),
                    Notes = SheetTable.GetString(row, notesColumn),
                    Vai = SheetTable.GetString(row, vaiColumn),
                    ReceivedAt = SheetTable.GetDate(row, receivedColumn),
                    HandedOverAt = SheetTable.GetDate(row, handedOverColumn)
                };

                ApplyPower(car, SheetTable.GetString(row, powerColumn));

                cars.Add(car);
            }

            return cars;
        }

        /// <summary>«220/299» → 220 кВт и 299 л.с.</summary>
        private static void ApplyPower(Car car, string value)
        {
            if (value == null)
                return;

            var match = PowerPair.Match(value);
            if (match.Success)
            {
                int kw, hp;
                if (int.TryParse(match.Groups[1].Value, out kw))
                    car.PowerKw = kw;
                if (int.TryParse(match.Groups[2].Value, out hp))
                    car.PowerHp = hp;
                return;
            }

            // одно число без дроби: считаем его лошадиными силами — так пишут чаще
            var digits = new string(value.Where(char.IsDigit).ToArray());
            int single;
            if (digits.Length > 0 && int.TryParse(digits, out single))
                car.PowerHp = single;
        }

        private static List<CarNumber> ReadNumbers(string[] row, IEnumerable<int> plateColumns)
        {
            var numbers = new List<CarNumber>();

            foreach (var column in plateColumns)
                AddNumbers(numbers, SheetTable.GetString(row, column));

            return numbers;
        }

        /// <summary>В одной ячейке может быть несколько знаков через запятую или слэш.</summary>
        private static void AddNumbers(List<CarNumber> numbers, string value)
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

                numbers.Add(new CarNumber { Text = text, Type = GuessType(text) });
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

            var result = vin.Trim().ToUpperInvariant().ToCharArray();

            for (var i = 0; i < result.Length; i++)
            {
                var index = cyrillic.IndexOf(result[i]);
                if (index >= 0)
                    result[i] = latin[index];
            }

            return new string(result);
        }

        private static Location? ParseLocation(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var normalized = value.Trim().ToLowerInvariant();

            if (normalized.StartsWith("ппд", StringComparison.Ordinal))
                return Domain.Location.Ppd;

            if (normalized.StartsWith("во", StringComparison.Ordinal))
                return Domain.Location.Vo;

            return null;
        }

        /// <summary>«штатная» / «вне штата».</summary>
        private static bool? ParseStaff(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            return value.Trim().ToLowerInvariant().Contains("вне") ? false : true;
        }

        /// <summary>«да/нет» или «конфискат/пусто».</summary>
        private static bool ParseConfiscated(string[] row, int column)
        {
            var yesNo = SheetTable.GetYesNo(row, column);
            if (yesNo.HasValue)
                return yesNo.Value;

            // любое непустое значение («конфискат») считаем отметкой
            return SheetTable.GetString(row, column) != null;
        }

        /// <summary>
        /// Создаёт файл-образец с ожидаемой шапкой, чтобы приложение запускалось
        /// на чистом ПК и было видно, какие столбцы оно понимает.
        /// Реальную таблицу достаточно положить на его место.
        /// </summary>
        private static void CreateDemoFile(string path)
        {
            var headers = new[]
            {
                "№ п/п", "Марка автомобиля", "Гос. рег. знак", "Год выпуска", "VIN",
                "Модель и № двигателя", "Мощность двигателя КВт/Л.С.", "№ шасси (рама)", "№ кузова",
                "ПФМ", "ПТС", "Свид. о регистрации", "Страховой полис", "Диагностическая карта",
                "Куда относится", "Местонахождение", "Должностное лицо", "Штатная", "Конфискат",
                "Вместимость", "Тип машины", "Цвет", "Объем двигателя", "max m (масса)", "m (масса)",
                "Особые отметки", "ВАИ", "Получили машину", "Отдали машину"
            };

            var rows = new List<IList<XlsxCell>>
            {
                new List<XlsxCell>
                {
                    XlsxCell.Number(1), XlsxCell.Text("УАЗ-3163"), XlsxCell.Text("0123АВ, А123ВС16"),
                    XlsxCell.Number(2014), XlsxCell.Text("XTT316300E0012345"), XlsxCell.Text("409051 / 12345"),
                    XlsxCell.Text("94/128"), XlsxCell.Text("316300E0012345"), XlsxCell.Text("316300E0012345"),
                    XlsxCell.Text("ПФМ-001"), XlsxCell.Text("16 ОР 123456"), XlsxCell.Text("9902 123456"),
                    XlsxCell.Text("ХХХ0123456789"), XlsxCell.Text("DK-2026-001"),
                    XlsxCell.Text("Гараж"), XlsxCell.Text("ППД"), XlsxCell.Number(1),
                    XlsxCell.Text("штатная"), XlsxCell.Empty, XlsxCell.Number(5),
                    XlsxCell.Text("легковой универсал"), XlsxCell.Text("зелёный"), XlsxCell.Number(2693),
                    XlsxCell.Number(2650), XlsxCell.Number(2070), XlsxCell.Empty, XlsxCell.Text("ВАИ-77"),
                    XlsxCell.Date(new DateTime(2020, 3, 12)), XlsxCell.Empty
                },
                new List<XlsxCell>
                {
                    XlsxCell.Number(2), XlsxCell.Text("КамАЗ-5350"), XlsxCell.Text("5555СЕ"),
                    XlsxCell.Number(2018), XlsxCell.Text("X1F53500J0000123"), XlsxCell.Text("740.622 / 55123"),
                    XlsxCell.Text("191/260"), XlsxCell.Text("53500J0000123"), XlsxCell.Empty,
                    XlsxCell.Text("ПФМ-002"), XlsxCell.Empty, XlsxCell.Text("9903 654321"),
                    XlsxCell.Empty, XlsxCell.Empty,
                    XlsxCell.Text("Обеспечение"), XlsxCell.Text("ВО"), XlsxCell.Number(2),
                    XlsxCell.Text("штатная"), XlsxCell.Empty, XlsxCell.Number(3),
                    XlsxCell.Text("грузовой"), XlsxCell.Text("хаки"), XlsxCell.Number(11760),
                    XlsxCell.Number(15850), XlsxCell.Number(9200), XlsxCell.Text("тент"), XlsxCell.Empty,
                    XlsxCell.Date(new DateTime(2021, 9, 1)), XlsxCell.Empty
                },
                // машина с неполными данными — так бывает в реальной таблице
                new List<XlsxCell>
                {
                    XlsxCell.Number(3), XlsxCell.Text("ГАЗ-3221"), XlsxCell.Text("0001СВ"),
                    XlsxCell.Empty, XlsxCell.Text("X9632210081234567"), XlsxCell.Empty,
                    XlsxCell.Empty, XlsxCell.Empty, XlsxCell.Empty,
                    XlsxCell.Empty, XlsxCell.Empty, XlsxCell.Empty,
                    XlsxCell.Empty, XlsxCell.Empty,
                    XlsxCell.Text("Гараж"), XlsxCell.Text("ППД"), XlsxCell.Number(3),
                    XlsxCell.Text("вне штата"), XlsxCell.Text("да"), XlsxCell.Number(13),
                    XlsxCell.Text("автобус"), XlsxCell.Empty, XlsxCell.Empty,
                    XlsxCell.Empty, XlsxCell.Empty, XlsxCell.Empty, XlsxCell.Empty,
                    XlsxCell.Empty, XlsxCell.Empty
                }
            };

            // рядом с данными — справочные листы: заказчик ведёт их в том же файле
            XlsxWriter.Write(path, new List<XlsxSheet>
            {
                new XlsxSheet(SheetName, headers, rows),
                XlsxSheet.Lookup(ExcelLookupRepository.AffiliationSheetName,
                    new[] { "Гараж", "Обеспечение" }),
                XlsxSheet.Lookup(ExcelLookupRepository.VehicleTypeSheetName,
                    new[] { "легковой седан", "легковой универсал", "автобус", "грузовой" })
            });
        }
    }
}
