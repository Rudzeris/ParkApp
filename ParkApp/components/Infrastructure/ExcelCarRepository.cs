using System;
using System.Collections.Generic;
using System.Globalization;
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
    /// Машины читаются из рабочей книги — той таблицы, которую уже ведут в Excel
    /// (лист «Список всех машин»). Столбцы ищутся по названиям с синонимами: точное
    /// совпадение, иначе по началу названия. Лишние столбцы не мешают.
    ///
    /// Особенности живой таблицы, из-за которых здесь больше кода, чем ожидаешь:
    ///   «отс.»                     — так пишут «нет данных», это не значение;
    ///   VIN заполнен не везде      — строку без VIN всё равно надо показать;
    ///   «Марка автомобиля» дважды  — по-русски и латиницей;
    ///   «102,7/139,7»              — мощность бывает дробной;
    ///   «ТТТ 7095971328⏎23.04.2027» — полис и срок в одной ячейке;
    ///   «№ Д л» и «Должностное лицо» — номер и подставленное по нему имя,
    ///                                  причём имя приходит формулой-ссылкой
    ///                                  на лист «Должностные лица».
    ///
    /// Репозиторий пока только на чтение: редактора машин в приложении нет, а перезапись
    /// файла целиком потеряла бы столбцы, которые приложение ещё не знает. Запись появится
    /// на этапе 1 — с сохранением неизвестных столбцов как есть.
    /// </summary>
    public class ExcelCarRepository : ICarRepository
    {
        /// <summary>Имя листа по умолчанию; пользователь может назначить своё в настройках.</summary>
        public static string SheetName
        {
            get { return AppSheets.Name(SheetKind.Cars); }
        }

        // имена столбцов вынесены сюда, потому что по ним же проверяется файл,
        // который пользователь выбирает в настройках
        public static readonly string[] VinNames = { "VIN", "ВИН" };
        public static readonly string[] ModelNames = { "Марка автомобиля", "Марка", "Марка/модель", "Модель автомобиля" };
        public static readonly string[] OfficialIdNames = { "№ Д л", "№ ДЛ", "№ должностного лица" };
        public static readonly string[] YearNames = { "Год выпуска", "Год" };
        public static readonly string[] LocationNames = { "Местонахождение", "Место" };
        public static readonly string[] InsuranceEndNames = { "Дата истечения страховки", "Страховка до" };
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
        private static readonly Regex PowerPair =
            new Regex(@"(\d+(?:[.,]\d+)?)\s*/\s*(\d+(?:[.,]\d+)?)", RegexOptions.Compiled);

        private static readonly char[] NumberSeparators = { ',', ';', '/', '|', '\n', '\r' };

        /// <summary>Так в таблице пишут «данных нет». Это не значение, а его отсутствие.</summary>
        private static readonly string[] Absent = { "отс.", "отс", "нет", "-", "—" };

        /// <summary>Дата в тексте: «23.04.2027» в хвосте ячейки полиса.</summary>
        private static readonly Regex DateInText =
            new Regex(@"(\d{1,2})[.,/](\d{1,2})[.,/](\d{2,4})", RegexOptions.Compiled);

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
            {
                // путь указал пользователь, а файла нет: скорее всего его перенесли.
                // Подсовывать вместо него образец нельзя — человек ждёт свои данные
                if (AppPaths.IsConfigured(PathSetting.CarsFile) || AppPaths.IsConfigured(PathSetting.SharedRoot))
                    throw new FileNotFoundException(string.Format(
                        "Файл машин не найден:{0}{1}{0}{0}Проверьте путь в настройках — возможно, файл переместили.",
                        Environment.NewLine, path));

                CreateDemoFile(path);
            }

            // строго по имени: имя листа настраивается, и молча прочитать
            // вместо него первый лист книги было бы хуже, чем сказать об ошибке
            var sheet = XlsxReader.ReadOrNull(path, SheetName);
            if (sheet == null)
                throw new InvalidOperationException(string.Format(
                    "В файле «{0}» нет листа «{1}».{2}{2}Укажите имя листа в настройках.",
                    Path.GetFileName(path), SheetName, Environment.NewLine));

            var vinColumn = sheet.Column(VinNames);
            // VIN не требуется: в рабочей таблице он заполнен у части машин,
            // и отбрасывать остальные строки нельзя — их там большинство

            var modelColumns = sheet.Columns(ModelNames).ToList();
            var modelColumn = modelColumns.Count > 0 ? modelColumns[0] : sheet.Column(ModelNames);
            var modelLatinColumn = modelColumns.Count > 1 ? modelColumns[1] : -1;
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
            var diagnosticColumn = sheet.Column("Диагностическая карта", "Д/ карта", "Д/карта", "Диагностическая");
            var insuranceEndColumn = sheet.Column(InsuranceEndNames);
            var officialIdColumn = sheet.Column(OfficialIdNames);
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
            var vaiColumn = sheet.Column("ВАИ №", "ВАИ");
            var receivedColumn = sheet.Column("Получили (дата)", "Получили машину", "Получили");
            var receivedFromColumn = sheet.Column("Получили (от кого)", "От кого");
            var handedOverColumn = sheet.Column("Отдали (дата)", "Отдали машину", "Отдали");
            var handedOverToColumn = sheet.Column("Отдали (кому)", "Кому");

            // «m» — слишком короткое название: если точного совпадения не нашлось,
            // поиск по началу мог зацепить тот же столбец, что и «max m»
            if (massColumn >= 0 && massColumn == maxMassColumn)
                massColumn = -1;

            var plateColumns = sheet.ColumnsStartingWith(PlatePrefixes).ToList();

            var cars = new List<Car>();
            var seenVins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < sheet.Rows.Count; i++)
            {
                var row = sheet.Rows[i];

                var model = Text(row, modelColumn);
                var modelLatin = Text(row, modelLatinColumn);
                var rawVin = Text(row, vinColumn);

                // совсем пустая строка — разделитель или хвост листа
                if (model == null && modelLatin == null && rawVin == null)
                    continue;

                var vin = rawVin == null ? null : NormalizeVin(rawVin);

                // дубль VIN внутри файла: берём первую строку, остальные пропускаем
                if (vin != null && !seenVins.Add(vin))
                    continue;

                var car = new Car
                {
                    Id = Guid.NewGuid(),
                    Vin = vin,
                    RowNumber = i + 1,
                    Model = model ?? modelLatin,
                    ModelLatin = model == null ? null : modelLatin,
                    Year = SheetTable.GetInt(row, yearColumn),
                    Location = ParseLocation(Text(row, locationColumn)),
                    Numbers = ReadNumbers(row, plateColumns),
                    EngineModel = Text(row, engineColumn),
                    Chassis = Text(row, chassisColumn),
                    BodyNumber = Text(row, bodyColumn),
                    Pfm = Text(row, pfmColumn),
                    Pts = Text(row, ptsColumn),
                    RegCertificate = Text(row, regCertificateColumn),
                    DiagnosticCard = Text(row, diagnosticColumn),
                    Affiliation = Text(row, affiliationColumn),
                    OfficialId = SheetTable.GetInt(row, officialIdColumn),
                    OfficialName = Reference(row, officialColumn),
                    IsStaff = ParseStaff(Text(row, staffColumn)),
                    IsConfiscated = ParseConfiscated(row, confiscatedColumn),
                    Capacity = SheetTable.GetInt(row, capacityColumn),
                    VehicleType = Text(row, typeColumn),
                    Color = Text(row, colorColumn),
                    EngineVolume = SheetTable.GetInt(row, volumeColumn),
                    MaxMass = SheetTable.GetDecimal(row, maxMassColumn),
                    Mass = SheetTable.GetDecimal(row, massColumn),
                    Notes = Text(row, notesColumn),
                    Vai = Text(row, vaiColumn),
                    ReceivedAt = SheetTable.GetDate(row, receivedColumn),
                    ReceivedFrom = Text(row, receivedFromColumn),
                    HandedOverAt = SheetTable.GetDate(row, handedOverColumn),
                    HandedOverTo = Text(row, handedOverToColumn)
                };

                // если номер должностного лица отдельным столбцом не ведут,
                // он мог оказаться в самой графе «Должностное лицо»
                if (!car.OfficialId.HasValue && officialIdColumn < 0)
                    car.OfficialId = SheetTable.GetInt(row, officialColumn);

                ApplyPower(car, Text(row, powerColumn));
                ApplyInsurance(car, Text(row, policyColumn), row, insuranceEndColumn);

                cars.Add(car);
            }

            return cars;
        }

        /// <summary>
        /// Ячейка «отс.» — это «данных нет», а не значение. Показывать её как
        /// текст было бы враньём: приложение решило бы, что марка машины
        /// называется «отс.».
        /// </summary>
        private static string Text(string[] row, int column)
        {
            var value = SheetTable.GetString(row, column);
            if (value == null)
                return null;

            var trimmed = value.Trim();
            return Absent.Any(a => string.Equals(a, trimmed, StringComparison.CurrentCultureIgnoreCase))
                ? null
                : trimmed;
        }

        /// <summary>
        /// Значение, которое подставляется ссылкой на другой лист. Если ячейка
        /// на том листе пустая, Excel возвращает ноль — печатать «0» вместо
        /// фамилии нельзя, поэтому такой ноль считается пустым значением.
        /// </summary>
        private static string Reference(string[] row, int column)
        {
            var value = Text(row, column);
            return value == "0" ? null : value;
        }

        /// <summary>
        /// Полис и срок его действия пишут в одну ячейку: «ТТТ 7095971328⏎23.04.2027».
        /// Отдельный столбец «Дата истечения страховки» в таблице считается формулой
        /// из этой же ячейки, поэтому он в приоритете, а разбор — запасной путь.
        /// </summary>
        private static void ApplyInsurance(Car car, string value, string[] row, int endColumn)
        {
            car.InsuranceEndsAt = SheetTable.GetDate(row, endColumn);

            if (value == null)
                return;

            var match = DateInText.Match(value);
            if (!match.Success)
            {
                car.InsurancePolicy = Clean(value);
                return;
            }

            if (!car.InsuranceEndsAt.HasValue)
            {
                int day, month, year;
                if (int.TryParse(match.Groups[1].Value, out day)
                    && int.TryParse(match.Groups[2].Value, out month)
                    && int.TryParse(match.Groups[3].Value, out year))
                {
                    if (year < 100)
                        year += 2000;

                    try
                    {
                        car.InsuranceEndsAt = new DateTime(year, month, day);
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        // в ячейке не дата, а что-то похожее — оставляем как есть
                    }
                }
            }

            car.InsurancePolicy = Clean(value.Remove(match.Index, match.Length));
        }

        private static string Clean(string value)
        {
            if (value == null)
                return null;

            var cleaned = value.Replace('\r', ' ').Replace('\n', ' ').Trim(' ', ',', ';');
            while (cleaned.Contains("  "))
                cleaned = cleaned.Replace("  ", " ");

            return cleaned.Length == 0 ? null : cleaned;
        }

        /// <summary>«220/299» → 220 кВт и 299 л.с. Бывает и дробным: «102,7/139,7».</summary>
        private static void ApplyPower(Car car, string value)
        {
            if (value == null)
                return;

            var match = PowerPair.Match(value);
            if (match.Success)
            {
                car.PowerKw = Round(match.Groups[1].Value);
                car.PowerHp = Round(match.Groups[2].Value);
                return;
            }

            // одно число без дроби: считаем его лошадиными силами — так пишут чаще
            var digits = new string(value.Where(char.IsDigit).ToArray());
            int single;
            if (digits.Length > 0 && int.TryParse(digits, out single))
                car.PowerHp = single;
        }

        /// <summary>Дробная мощность округляется: в документах её пишут целой.</summary>
        private static int? Round(string value)
        {
            decimal number;
            if (decimal.TryParse(value.Replace(',', '.'), NumberStyles.Number,
                    CultureInfo.InvariantCulture, out number))
                return (int)Math.Round(number, MidpointRounding.AwayFromZero);

            return null;
        }

        private static List<CarNumber> ReadNumbers(string[] row, IEnumerable<int> plateColumns)
        {
            var numbers = new List<CarNumber>();

            foreach (var column in plateColumns)
                AddNumbers(numbers, Text(row, column));

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

            // в таблице пишут «СВО», в требованиях было «ВО» — это одно и то же
            if (normalized.StartsWith("во", StringComparison.Ordinal)
                || normalized.StartsWith("сво", StringComparison.Ordinal))
                return Domain.Location.Vo;

            return null;
        }

        /// <summary>«штатная» / «вне штата».</summary>
        private static bool? ParseStaff(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            // «вне штата» и «за штатом» — одно и то же
            var normalized = value.Trim().ToLowerInvariant();
            return !(normalized.Contains("вне") || normalized.Contains("за штат"));
        }

        /// <summary>«да/нет» или «конфискат/пусто».</summary>
        private static bool ParseConfiscated(string[] row, int column)
        {
            var yesNo = SheetTable.GetYesNo(row, column);
            if (yesNo.HasValue)
                return yesNo.Value;

            // любое непустое значение («конфискат») считаем отметкой
            return Text(row, column) != null;
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
                    XlsxCell.Text(DispatchGroups.Service), XlsxCell.Text("ППД"), XlsxCell.Number(1),
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
                    XlsxCell.Text(DispatchGroups.Duty), XlsxCell.Text("ВО"), XlsxCell.Number(2),
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
                    XlsxCell.Text(DispatchGroups.Service), XlsxCell.Text("ППД"), XlsxCell.Number(3),
                    XlsxCell.Text("вне штата"), XlsxCell.Text("да"), XlsxCell.Number(13),
                    XlsxCell.Text("автобус"), XlsxCell.Empty, XlsxCell.Empty,
                    XlsxCell.Empty, XlsxCell.Empty, XlsxCell.Empty, XlsxCell.Empty,
                    XlsxCell.Empty, XlsxCell.Empty
                }
            };

            // рядом с данными — справочные листы и должностные лица:
            // в рабочей книге заказчик ведёт всё это в одном файле
            XlsxWriter.Write(path, new List<XlsxSheet>
            {
                new XlsxSheet(SheetName, headers, rows),
                ExcelPersonRepository.DemoSheet(),
                XlsxSheet.Lookup(ExcelLookupRepository.AffiliationSheetName,
                    new[] { DispatchGroups.Service, DispatchGroups.Duty }),
                XlsxSheet.Lookup(ExcelLookupRepository.VehicleTypeSheetName,
                    new[] { "легковой седан", "легковой универсал", "автобус", "грузовой" })
            });
        }

        /// <summary>
        /// Создаёт файл-образец, если его ещё нет. Нужен репозиторию людей:
        /// по умолчанию они лежат в этой же книге, и создавать её дважды нельзя.
        /// </summary>
        public static void EnsureDemoFile()
        {
            var path = AppPaths.CarsFile;
            if (!File.Exists(path))
                CreateDemoFile(path);
        }
    }
}
