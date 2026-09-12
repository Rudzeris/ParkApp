using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        public IReadOnlyList<string> GetSheetNames(string path)
        {
            return XlsxReader.SheetNames(path).ToList();
        }

        public TableFileCheck Check(TableFileKind kind, string path, string sheetName)
        {
            if (string.IsNullOrWhiteSpace(path))
                return Invalid("путь к файлу не указан");

            if (!File.Exists(path))
                return Invalid("файл не найден");

            var extension = (Path.GetExtension(path) ?? string.Empty).ToLowerInvariant();
            if (extension != ".xlsx")
                return Invalid("нужен файл .xlsx — старый .xls откройте в Excel и сохраните как «Книга Excel (*.xlsx)»");

            if (string.IsNullOrWhiteSpace(sheetName))
                sheetName = AppSheets.Name(SheetCatalog.ForTable(kind));

            var available = XlsxReader.SheetNames(path).ToList();

            SheetTable sheet;
            try
            {
                sheet = XlsxReader.ReadOrNull(path, sheetName);
            }
            catch (Exception ex)
            {
                return Invalid("файл не читается: " + ex.Message);
            }

            if (sheet == null)
            {
                // поправимый случай: файл тот, а лист называется иначе
                return new TableFileCheck(false,
                    string.Format("в книге нет листа «{0}»", sheetName),
                    null, true, available);
            }

            if (sheet.Headers.Count == 0)
                return Invalid(string.Format("лист «{0}» пуст — не найдена строка заголовков", sheetName));

            return CheckColumns(kind, sheet);
        }

        private static TableFileCheck CheckColumns(TableFileKind kind, SheetTable sheet)
        {
            var missing = new List<string>();

            switch (kind)
            {
                case TableFileKind.People:
                    if (sheet.Column(ExcelPersonRepository.FullNameNames) < 0)
                        return Invalid("не найден обязательный столбец «ФИО»");

                    Optional(sheet, missing, "№", ExcelPersonRepository.IdNames);
                    Optional(sheet, missing, "Должность", ExcelPersonRepository.PositionNames);
                    Optional(sheet, missing, "Звание", ExcelPersonRepository.RankNames);
                    Optional(sheet, missing, "Телефон", ExcelPersonRepository.PhoneNames);
                    break;

                case TableFileKind.Fines:
                    if (sheet.Column(ExcelFineRepository.NumberNames) < 0)
                        return Invalid("не найден обязательный столбец «Номер постановления»");

                    Optional(sheet, missing, "Дата правонарушения, Ф.И.О.", ExcelFineRepository.ViolationNames);
                    Optional(sheet, missing, "Марка АТ", ExcelFineRepository.BrandNames);
                    Optional(sheet, missing, "ГРЗ", ExcelFineRepository.PlateNames);
                    Optional(sheet, missing, "Дата привлечения", ExcelFineRepository.ResolutionDateNames);
                    Optional(sheet, missing, "Сумма штрафа", ExcelFineRepository.AmountNames);
                    Optional(sheet, missing, "оплата, чек, дата", ExcelFineRepository.PaymentNames);
                    Optional(sheet, missing, "Примечание", ExcelFineRepository.NotesNames);
                    break;

                default:
                    // VIN обязательным быть не может: в рабочей таблице он заполнен
                    // у части машин, и требовать его значило бы забраковать живой файл
                    if (sheet.Column(ExcelCarRepository.ModelNames) < 0)
                        return Invalid("не найден столбец «Марка автомобиля» — без него это не список машин");

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

            return new TableFileCheck(true, null, missing, false, null);
        }

        private static void Optional(SheetTable sheet, List<string> missing, string title, string[] names)
        {
            if (sheet.Column(names) < 0)
                missing.Add(title);
        }

        private static TableFileCheck Invalid(string problem)
        {
            return new TableFileCheck(false, problem, null, false, null);
        }
    }
}
