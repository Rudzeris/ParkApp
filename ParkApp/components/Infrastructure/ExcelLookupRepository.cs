using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ParkApp.components.Application;
using ParkApp.components.Infrastructure.Excel;

namespace ParkApp.components.Infrastructure
{
    /// <summary>
    /// Справочные списки лежат отдельными листами в файле машин: столбец «Название»,
    /// под ним значения. Так заказчик правит списки там же, где ведёт таблицу.
    /// </summary>
    public class ExcelLookupRepository : ILookupRepository
    {
        public const string AffiliationSheetName = "Куда относится";
        public const string VehicleTypeSheetName = "Тип машины";

        private static readonly object Sync = new object();

        public Task<IReadOnlyList<string>> GetAsync(LookupList list)
        {
            lock (Sync)
            {
                IReadOnlyList<string> values = Load(SheetNameFor(list));
                return Task.FromResult(values);
            }
        }

        private static List<string> Load(string sheetName)
        {
            var values = new List<string>();

            // строго по имени: подставить вместо справочника лист «Машины» нельзя
            var sheet = XlsxReader.ReadOrNull(AppPaths.CarsFile, sheetName);
            if (sheet == null)
                return values;

            // столбец «Название»; если шапка названа иначе — берём первый столбец
            var column = sheet.Column("Название", "Значение", "Наименование");
            if (column < 0)
                column = 0;

            var seen = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);

            foreach (var row in sheet.Rows)
            {
                var value = SheetTable.GetString(row, column);
                if (value == null)
                    continue;

                if (seen.Add(value))
                    values.Add(value);
            }

            return values;
        }

        private static string SheetNameFor(LookupList list)
        {
            switch (list)
            {
                case LookupList.VehicleType:
                    return VehicleTypeSheetName;
                default:
                    return AffiliationSheetName;
            }
        }
    }
}
