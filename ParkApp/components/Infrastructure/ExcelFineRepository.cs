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
        private const string SheetName = "Штрафы";
        private static readonly object Sync = new object();

        private static readonly string[] Headers =
        {
            "№ постановления",
            "Дата постановления",
            "Дата нарушения",
            "VIN машины",
            "Водитель",
            "Место нарушения",
            "Сумма, руб.",
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
                var seed = CreateSeed();
                Save(seed);
                return seed;
            }

            var sheet = XlsxReader.Read(path, SheetName);

            var numberColumn = sheet.Column("№ постановления", "Номер постановления", "Постановление");
            if (numberColumn < 0)
                throw new InvalidOperationException(
                    "В файле «Штрафы.xlsx» не найден столбец «№ постановления». " +
                    "Проверьте, что шапка таблицы на первой заполненной строке листа.");

            var resolutionDateColumn = sheet.Column("Дата постановления");
            var violationDateColumn = sheet.Column("Дата нарушения");
            var vinColumn = sheet.Column("VIN машины", "VIN", "Машина", "ВИН");
            var driverColumn = sheet.Column("Водитель");
            var placeColumn = sheet.Column("Место нарушения", "Место");
            var amountColumn = sheet.Column("Сумма, руб.", "Сумма");
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
                    DriverName = SheetTable.GetString(row, driverColumn),
                    ViolationPlace = SheetTable.GetString(row, placeColumn),
                    Amount = SheetTable.GetDecimal(row, amountColumn) ?? 0m,
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
                    XlsxCell.Text(f.DriverName),
                    XlsxCell.Text(f.ViolationPlace),
                    XlsxCell.Money(f.Amount),
                    XlsxCell.Text(f.ScanPath)
                })
                .ToList();

            XlsxWriter.Write(AppPaths.FinesFile, SheetName, Headers, rows);
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
                    CarVin = "VIN1",
                    DriverName = "Иванов И.И.",
                    Amount = 500m
                },
                new Fine
                {
                    ResolutionNumber = "18810516250405554443",
                    ResolutionDate = today.AddDays(-8),
                    ViolationDate = today.AddDays(-10),
                    ViolationPlace = "трасса М-7, 812 км",
                    CarVin = "VIN2",
                    DriverName = "Петров П.П.",
                    Amount = 1500m
                },
                new Fine
                {
                    ResolutionNumber = "18810516250409876543",
                    ResolutionDate = today.AddDays(-3),
                    ViolationDate = today.AddDays(-3),
                    CarVin = "VIN2",
                    Amount = 800m
                }
            };
        }
    }
}
