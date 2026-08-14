using System;
using System.Collections.Generic;
using System.IO;
using ParkApp.components.Application;
using ParkApp.components.Infrastructure.Excel;

namespace ParkApp.components.Infrastructure
{
    /// <summary>
    /// Проверяет, что выбранный файл — действительно та таблица: есть нужный лист
    /// и обязательный столбец-ключ. Имена столбцов берутся из тех же констант,
    /// по которым файл потом читается, поэтому проверка не может разойтись с чтением.
    /// </summary>
    public class ExcelTableValidator : ITableFileValidator
    {
        public TableFileCheck Check(TableFileKind kind, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return Invalid("Путь к файлу не указан.");

            if (!File.Exists(path))
                return Invalid("Файл не найден.");

            var extension = (Path.GetExtension(path) ?? string.Empty).ToLowerInvariant();
            if (extension != ".xlsx")
                return Invalid("Нужен файл .xlsx. Старый .xls откройте в Excel и сохраните как «Книга Excel (*.xlsx)».");

            var sheetName = SheetNameFor(kind);

            SheetTable sheet;
            try
            {
                sheet = XlsxReader.ReadOrNull(path, sheetName);
            }
            catch (Exception ex)
            {
                return Invalid("Файл не читается: " + ex.Message);
            }

            if (sheet == null)
                return Invalid(string.Format("В книге нет листа «{0}».", sheetName));

            if (sheet.Headers.Count == 0)
                return Invalid(string.Format("Лист «{0}» пуст — не найдена строка заголовков.", sheetName));

            return CheckColumns(kind, sheet);
        }

        private static TableFileCheck CheckColumns(TableFileKind kind, SheetTable sheet)
        {
            var missing = new List<string>();

            switch (kind)
            {
                case TableFileKind.People:
                    if (sheet.Column(ExcelPersonRepository.FullNameNames) < 0)
                        return Invalid("Не найден обязательный столбец «ФИО».");

                    Optional(sheet, missing, "№", ExcelPersonRepository.IdNames);
                    Optional(sheet, missing, "Должность", ExcelPersonRepository.PositionNames);
                    Optional(sheet, missing, "Звание", ExcelPersonRepository.RankNames);
                    Optional(sheet, missing, "Телефон", ExcelPersonRepository.PhoneNames);
                    break;

                case TableFileKind.Fines:
                    if (sheet.Column(ExcelFineRepository.NumberNames) < 0)
                        return Invalid("Не найден обязательный столбец «№ постановления».");

                    Optional(sheet, missing, "Дата постановления", ExcelFineRepository.ResolutionDateNames);
                    Optional(sheet, missing, "Дата нарушения", ExcelFineRepository.ViolationDateNames);
                    Optional(sheet, missing, "VIN машины", ExcelFineRepository.VinNames);
                    Optional(sheet, missing, "Водитель", ExcelFineRepository.DriverNames);
                    Optional(sheet, missing, "Сумма", ExcelFineRepository.AmountNames);
                    break;

                default:
                    if (sheet.Column(ExcelCarRepository.VinNames) < 0)
                        return Invalid("Не найден обязательный столбец «VIN» — по нему документы связываются с машиной.");

                    Optional(sheet, missing, "Марка автомобиля", ExcelCarRepository.ModelNames);
                    Optional(sheet, missing, "Год выпуска", ExcelCarRepository.YearNames);
                    Optional(sheet, missing, "Местонахождение", ExcelCarRepository.LocationNames);
                    Optional(sheet, missing, "Куда относится", ExcelCarRepository.AffiliationNames);
                    Optional(sheet, missing, "Должностное лицо", ExcelCarRepository.OfficialNames);
                    Optional(sheet, missing, "Тип машины", ExcelCarRepository.VehicleTypeNames);

                    if (sheet.ColumnsStartingWith(ExcelCarRepository.PlatePrefixes).Count == 0)
                        missing.Add("Гос. рег. знак");
                    break;
            }

            return new TableFileCheck(true, null, missing);
        }

        private static void Optional(SheetTable sheet, List<string> missing, string title, string[] names)
        {
            if (sheet.Column(names) < 0)
                missing.Add(title);
        }

        private static TableFileCheck Invalid(string problem)
        {
            return new TableFileCheck(false, problem, null);
        }

        private static string SheetNameFor(TableFileKind kind)
        {
            switch (kind)
            {
                case TableFileKind.People:
                    return ExcelPersonRepository.SheetName;
                case TableFileKind.Fines:
                    return ExcelFineRepository.SheetName;
                default:
                    return ExcelCarRepository.SheetName;
            }
        }
    }
}
