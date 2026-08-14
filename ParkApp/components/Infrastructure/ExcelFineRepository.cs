using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ParkApp.components.Application;
using ParkApp.components.Domain;
using ParkApp.components.Infrastructure.Excel;

namespace ParkApp.components.Infrastructure
{
    /// <summary>
    /// Штрафы хранятся в «Штрафы\Штрафы.xlsx» — той же таблице, которую можно открыть в Excel.
    /// Файл перезаписывается целиком: на ожидаемых объёмах (тысячи строк) это доли секунды,
    /// а частичная правка xlsx потребовала бы полноценной библиотеки.
    /// </summary>
    public class ExcelFineRepository : IFineRepository
    {
        public static string SheetName
        {
            get { return AppSheets.Name(SheetKind.Fines); }
        }

        public static readonly string[] NumberNames = { "№ постановления", "Номер постановления", "Постановление" };
        public static readonly string[] ResolutionDateNames = { "Дата постановления" };
        public static readonly string[] ViolationDateNames = { "Дата нарушения" };
        public static readonly string[] VinNames = { "VIN машины", "VIN", "Машина", "ВИН" };
        public static readonly string[] DriverNames = { "Водитель (№ из таблицы Люди)", "Водитель", "Водитель №" };
        public static readonly string[] AmountNames = { "Сумма, руб.", "Сумма" };
        private static readonly object Sync = new object();

        private static readonly string[] Headers =
        {
            "№ постановления",
            "Дата постановления",
            "Дата нарушения",
            "VIN машины",
            "Водитель (№ из таблицы Люди)",
            "Место нарушения",
            "Сумма, руб.",
            "Оплачен",
            "Дата оплаты",
            "Скан постановления"
        };

        public Task<IReadOnlyList<Fine>> GetAllAsync()
        {
            lock (Sync)
            {
                IReadOnlyList<Fine> fines = Load();
                return Task.FromResult(fines);
            }
        }

        public Task<Fine> GetByNumberAsync(string resolutionNumber)
        {
            lock (Sync)
            {
                var fine = Load().FirstOrDefault(f => SameNumber(f.ResolutionNumber, resolutionNumber));
                return Task.FromResult(fine);
            }
        }

        public Task AddAsync(Fine fine)
        {
            if (fine == null)
                throw new ArgumentNullException("fine");

            lock (Sync)
            {
                var fines = Load();
                if (fines.Any(f => SameNumber(f.ResolutionNumber, fine.ResolutionNumber)))
                    throw new InvalidOperationException(
                        string.Format("Постановление № {0} уже есть в таблице.", fine.ResolutionNumber));

                fines.Add(fine);
                Save(fines);
            }

            return Task.CompletedTask;
        }

        public Task UpdateAsync(Fine fine)
        {
            if (fine == null)
                throw new ArgumentNullException("fine");

            lock (Sync)
            {
                var fines = Load();
                var index = fines.FindIndex(f => SameNumber(f.ResolutionNumber, fine.ResolutionNumber));
                if (index < 0)
                    throw new InvalidOperationException(
                        string.Format("Постановление № {0} не найдено.", fine.ResolutionNumber));

                fines[index] = fine;
                Save(fines);
            }

            return Task.CompletedTask;
        }

        public Task DeleteAsync(string resolutionNumber)
        {
            lock (Sync)
            {
                var fines = Load();
                fines.RemoveAll(f => SameNumber(f.ResolutionNumber, resolutionNumber));
                Save(fines);
            }

            return Task.CompletedTask;
        }

        private static List<Fine> Load()
        {
            var path = AppPaths.FinesFile;

            if (!File.Exists(path))
            {
                if (AppPaths.IsConfigured(PathSetting.FinesFile) || AppPaths.IsConfigured(PathSetting.DataRoot))
                    throw new FileNotFoundException(string.Format(
                        "Файл штрафов не найден:{0}{1}{0}{0}Проверьте путь в настройках — возможно, файл переместили.",
                        Environment.NewLine, path));

                var seed = CreateSeed();
                Save(seed);
                return seed;
            }

            // строго по имени: имя листа настраивается, и молча прочитать
            // вместо него первый лист книги было бы хуже, чем сказать об ошибке
            var sheet = XlsxReader.ReadOrNull(path, SheetName);
            if (sheet == null)
                throw new InvalidOperationException(string.Format(
                    "В файле «{0}» нет листа «{1}».{2}{2}Укажите имя листа в настройках.",
                    Path.GetFileName(path), SheetName, Environment.NewLine));

            var numberColumn = sheet.Column(NumberNames);
            if (numberColumn < 0)
                throw new InvalidOperationException(
                    "В файле «Штрафы.xlsx» не найден столбец «№ постановления». " +
                    "Проверьте, что шапка таблицы на первой заполненной строке листа.");

            var resolutionDateColumn = sheet.Column(ResolutionDateNames);
            var violationDateColumn = sheet.Column(ViolationDateNames);
            var vinColumn = sheet.Column(VinNames);
            var driverColumn = sheet.Column(DriverNames);
            var placeColumn = sheet.Column("Место нарушения", "Место");
            var amountColumn = sheet.Column(AmountNames);
            var paidColumn = sheet.Column("Оплачен", "Оплата");
            var paidDateColumn = sheet.Column("Дата оплаты");
            var scanColumn = sheet.Column("Скан постановления", "Скан", "Файл");

            var fines = new List<Fine>();

            foreach (var row in sheet.Rows)
            {
                var number = SheetTable.GetString(row, numberColumn);

                // строка без номера постановления — это ключ записи, без него она бессмысленна
                if (number == null)
                    continue;

                fines.Add(new Fine
                {
                    ResolutionNumber = number,
                    ResolutionDate = SheetTable.GetDate(row, resolutionDateColumn) ?? default(DateTime),
                    ViolationDate = SheetTable.GetDate(row, violationDateColumn) ?? default(DateTime),
                    CarVin = SheetTable.GetString(row, vinColumn),
                    DriverId = SheetTable.GetInt(row, driverColumn),
                    ViolationPlace = SheetTable.GetString(row, placeColumn),
                    Amount = SheetTable.GetDecimal(row, amountColumn) ?? 0m,
                    IsPaid = ParsePaid(row, paidColumn, paidDateColumn),
                    PaidDate = SheetTable.GetDate(row, paidDateColumn),
                    ScanPath = SheetTable.GetString(row, scanColumn)
                });
            }

            return fines;
        }

        private static void Save(List<Fine> fines)
        {
            var rows = fines
                .OrderByDescending(f => f.ViolationDate)
                .ThenBy(f => f.ResolutionNumber)
                .Select(f => (IList<XlsxCell>)new List<XlsxCell>
                {
                    XlsxCell.Text(f.ResolutionNumber),
                    XlsxCell.Date(f.ResolutionDate),
                    XlsxCell.Date(f.ViolationDate),
                    XlsxCell.Text(f.CarVin),
                    XlsxCell.Number(f.DriverId),
                    XlsxCell.Text(f.ViolationPlace),
                    XlsxCell.Money(f.Amount),
                    XlsxCell.Text(f.IsPaid ? "да" : "нет"),
                    XlsxCell.Date(f.PaidDate),
                    XlsxCell.Text(f.ScanPath)
                })
                .ToList();

            XlsxWriter.Write(AppPaths.FinesFile, SheetName, Headers, rows);
        }

        /// <summary>
        /// Оплачен ли штраф. Заполненная дата оплаты сама по себе означает «да»,
        /// даже если отметку в столбце «Оплачен» поставить забыли.
        /// </summary>
        private static bool ParsePaid(string[] row, int paidColumn, int paidDateColumn)
        {
            var flag = SheetTable.GetYesNo(row, paidColumn);
            if (flag.HasValue)
                return flag.Value;

            return SheetTable.GetDate(row, paidDateColumn).HasValue;
        }

        private static bool SameNumber(string left, string right)
        {
            return string.Equals(
                (left ?? string.Empty).Trim(),
                (right ?? string.Empty).Trim(),
                StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Демо-штрафы на машины из демо-файла «Машины.xlsx». Заменяются реальными данными.</summary>
        private static List<Fine> CreateSeed()
        {
            var today = DateTime.Today;

            return new List<Fine>
            {
                new Fine
                {
                    ResolutionNumber = "18810516250401234567",
                    ResolutionDate = today.AddDays(-20),
                    ViolationDate = today.AddDays(-25),
                    ViolationPlace = "г. Казань, пр. Победы, 12",
                    CarVin = "XTT316300E0012345",
                    DriverId = 2,
                    Amount = 500m,
                    IsPaid = true,
                    PaidDate = today.AddDays(-18)
                },
                new Fine
                {
                    ResolutionNumber = "18810516250405554443",
                    ResolutionDate = today.AddDays(-8),
                    ViolationDate = today.AddDays(-10),
                    ViolationPlace = "трасса М-7, 812 км",
                    CarVin = "X1F53500J0000123",
                    DriverId = 3,
                    Amount = 1500m
                },
                new Fine
                {
                    ResolutionNumber = "18810516250409876543",
                    ResolutionDate = today.AddDays(-3),
                    ViolationDate = today.AddDays(-3),
                    CarVin = "X1F53500J0000123",
                    Amount = 800m
                }
            };
        }
    }
}
