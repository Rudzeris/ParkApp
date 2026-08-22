using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ParkApp.components.Application;
using ParkApp.components.Domain;
using ParkApp.components.Infrastructure.Excel;

namespace ParkApp.components.Infrastructure
{
    /// <summary>
    /// Наряды на выход в «Наряды\Наряды.xlsx». Книга приложения, а не заказчика,
    /// поэтому пишется целиком тремя листами:
    ///   «Наряды»    — строки нарядов по датам;
    ///   «Графики»   — постоянные настройки машин (время, «повторить», цель, маршрут);
    ///   «Реквизиты» — наименование части и подписанты для печати;
    ///   «Списки»    — значения, которые выбирают в таблице наряда.
    /// </summary>
    public class ExcelDispatchRepository : IDispatchRepository
    {
        private static readonly object Sync = new object();

        private static readonly string[] OrderHeaders =
        {
            "Дата наряда", "Номер наряда", "Группа", "Марка машины", "Г.Р.З.", "VIN",
            "Группа эксплуатации", "Для каких целей назначается", "Маршрут движения",
            "В чьё распоряжение", "Время выезда", "Время возвращения", "Примечание"
        };

        private static readonly string[] ScheduleHeaders =
        {
            "VIN", "Г.Р.З.", "Группа эксплуатации", "Для каких целей назначается",
            "Маршрут движения", "В чьё распоряжение", "Время выезда", "Время возвращения",
            "Повторять ежедневно", "Использование вне наряда", "Примечание"
        };

        private static readonly string[] DetailHeaders = { "Параметр", "Значение" };

        public static string OrdersSheetName
        {
            get { return AppSheets.Name(SheetKind.DispatchOrders); }
        }

        public static string SchedulesSheetName
        {
            get { return AppSheets.Name(SheetKind.CarSchedules); }
        }

        public static string DetailsSheetName
        {
            get { return AppSheets.Name(SheetKind.PrintDetails); }
        }

        public static string ListsSheetName
        {
            get { return AppSheets.Name(SheetKind.DispatchLists); }
        }

        public static readonly string[] DateNames = { "Дата наряда", "Дата" };
        public static readonly string[] OrderNumberNames = { "Номер наряда", "Наряд №", "Номер" };
        public static readonly string[] VinNames = { "VIN", "ВИН" };
        public static readonly string[] PlateNames = { "Г.Р.З.", "ГРЗ", "Гос. рег. знак" };

        public Task<IReadOnlyList<DispatchEntry>> GetByDateAsync(DateTime date)
        {
            lock (Sync)
            {
                var day = date.Date;
                IReadOnlyList<DispatchEntry> entries = LoadOrders()
                    .Where(e => e.Date.Date == day)
                    .ToList();

                return Task.FromResult(entries);
            }
        }

        public Task SaveOrderAsync(DateTime date, IReadOnlyList<DispatchEntry> entries)
        {
            lock (Sync)
            {
                var day = date.Date;

                // наряд на день переписывается целиком: это один документ, а не накопление
                var others = LoadOrders().Where(e => e.Date.Date != day).ToList();
                others.AddRange(entries ?? new List<DispatchEntry>());

                Save(others, LoadSchedules(), LoadDetails(), LoadChoices());
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<CarSchedule>> GetSchedulesAsync()
        {
            lock (Sync)
            {
                IReadOnlyList<CarSchedule> schedules = LoadSchedules();
                return Task.FromResult(schedules);
            }
        }

        public Task SaveSchedulesAsync(IReadOnlyList<CarSchedule> schedules)
        {
            lock (Sync)
            {
                Save(LoadOrders(), (schedules ?? new List<CarSchedule>()).ToList(), LoadDetails(), LoadChoices());
            }

            return Task.CompletedTask;
        }

        public Task<IDictionary<string, string>> GetPrintDetailsAsync()
        {
            lock (Sync)
            {
                IDictionary<string, string> details = LoadDetails();
                return Task.FromResult(details);
            }
        }

        public Task<DispatchChoices> GetChoicesAsync()
        {
            lock (Sync)
            {
                return Task.FromResult(LoadChoices());
            }
        }

        private static void EnsureFile()
        {
            var path = AppPaths.DispatchFile;
            if (File.Exists(path))
                return;

            if (AppPaths.IsConfigured(PathSetting.DispatchFile) || AppPaths.IsConfigured(PathSetting.DataRoot))
                throw new FileNotFoundException(string.Format(
                    "Файл нарядов не найден:{0}{1}{0}{0}Проверьте путь в настройках — возможно, файл переместили.",
                    Environment.NewLine, path));

            Save(new List<DispatchEntry>(), new List<CarSchedule>(), DefaultDetails(), DispatchChoices.Default());
        }

        private static SheetTable ReadSheet(string sheetName)
        {
            EnsureFile();

            var sheet = XlsxReader.ReadOrNull(AppPaths.DispatchFile, sheetName);
            if (sheet == null)
                throw new InvalidOperationException(string.Format(
                    "В файле «{0}» нет листа «{1}».{2}{2}Укажите имя листа в настройках.",
                    Path.GetFileName(AppPaths.DispatchFile), sheetName, Environment.NewLine));

            return sheet;
        }

        private static List<DispatchEntry> LoadOrders()
        {
            var sheet = ReadSheet(OrdersSheetName);

            var dateColumn = sheet.Column(DateNames);
            if (dateColumn < 0)
                throw new InvalidOperationException("В листе нарядов не найден столбец «Дата наряда».");

            var numberColumn = sheet.Column(OrderNumberNames);
            var groupColumn = sheet.Column("Группа");
            var brandColumn = sheet.Column("Марка машины", "Марка");
            var plateColumn = sheet.Column(PlateNames);
            var vinColumn = sheet.Column(VinNames);
            var operationColumn = sheet.Column("Группа эксплуатации");
            var purposeColumn = sheet.Column("Для каких целей назначается", "Цель");
            var routeColumn = sheet.Column("Маршрут движения", "Маршрут");
            var assignmentColumn = sheet.Column("В чьё распоряжение", "В чье распоряжение");
            var departureColumn = sheet.Column("Время выезда", "Выезд");
            var returnColumn = sheet.Column("Время возвращения", "Возвращение");
            var notesColumn = sheet.Column("Примечание");

            var entries = new List<DispatchEntry>();

            foreach (var row in sheet.Rows)
            {
                var date = SheetTable.GetDate(row, dateColumn);
                if (!date.HasValue)
                    continue;

                entries.Add(new DispatchEntry
                {
                    Date = date.Value,
                    OrderNumber = SheetTable.GetString(row, numberColumn),
                    GroupName = SheetTable.GetString(row, groupColumn),
                    CarBrand = SheetTable.GetString(row, brandColumn),
                    CarPlate = SheetTable.GetString(row, plateColumn),
                    CarVin = SheetTable.GetString(row, vinColumn),
                    OperationGroup = SheetTable.GetString(row, operationColumn),
                    Purpose = SheetTable.GetString(row, purposeColumn),
                    Route = SheetTable.GetString(row, routeColumn),
                    Assignment = SheetTable.GetString(row, assignmentColumn),
                    DepartureAt = ReadMoment(row, departureColumn, date.Value),
                    ReturnAt = ReadMoment(row, returnColumn, date.Value),
                    Notes = SheetTable.GetString(row, notesColumn)
                });
            }

            return entries;
        }

        /// <summary>
        /// Время выезда и возвращения хранится датой со временем. Если в ячейке
        /// оказалось только время, считаем его временем дня наряда.
        /// </summary>
        private static DateTime ReadMoment(string[] row, int column, DateTime orderDate)
        {
            var text = SheetTable.GetString(row, column);
            if (text == null)
                return default(DateTime);

            double serial;
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out serial) && serial > 0)
            {
                var moment = XlsxCell.FromSerial(serial);
                return moment.Date == DateTime.MinValue.Date ? orderDate.Date.Add(moment.TimeOfDay) : moment;
            }

            DateTime parsed;
            if (DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out parsed))
                return parsed;

            TimeSpan time;
            if (TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out time))
                return orderDate.Date.Add(time);

            return default(DateTime);
        }

        private static List<CarSchedule> LoadSchedules()
        {
            var sheet = ReadSheet(SchedulesSheetName);

            var vinColumn = sheet.Column(VinNames);
            if (vinColumn < 0)
                throw new InvalidOperationException("В листе графиков не найден столбец «VIN».");

            var plateColumn = sheet.Column(PlateNames);
            var operationColumn = sheet.Column("Группа эксплуатации");
            var purposeColumn = sheet.Column("Для каких целей назначается", "Цель");
            var routeColumn = sheet.Column("Маршрут движения", "Маршрут");
            var assignmentColumn = sheet.Column("В чьё распоряжение", "В чье распоряжение");
            var departureColumn = sheet.Column("Время выезда", "Выезд");
            var returnColumn = sheet.Column("Время возвращения", "Возвращение");
            var repeatColumn = sheet.Column("Повторять ежедневно", "Повторять", "Повторить");
            var outsideColumn = sheet.Column("Использование вне наряда", "Вне наряда");
            var notesColumn = sheet.Column("Примечание");

            var schedules = new List<CarSchedule>();

            foreach (var row in sheet.Rows)
            {
                var vin = SheetTable.GetString(row, vinColumn);
                if (vin == null)
                    continue;

                schedules.Add(new CarSchedule
                {
                    CarVin = vin,
                    CarPlate = SheetTable.GetString(row, plateColumn),
                    OperationGroup = SheetTable.GetString(row, operationColumn),
                    Purpose = SheetTable.GetString(row, purposeColumn),
                    Route = SheetTable.GetString(row, routeColumn),
                    Assignment = SheetTable.GetString(row, assignmentColumn),
                    DepartureTime = ReadTime(row, departureColumn),
                    ReturnTime = ReadTime(row, returnColumn),
                    RepeatDaily = SheetTable.GetYesNo(row, repeatColumn) ?? false,
                    AllowOutsideOrder = SheetTable.GetYesNo(row, outsideColumn) ?? false,
                    Notes = SheetTable.GetString(row, notesColumn)
                });
            }

            return schedules;
        }

        /// <summary>Время из ячейки: «06:00» текстом или доля суток, как хранит Excel.</summary>
        private static TimeSpan ReadTime(string[] row, int column)
        {
            var text = SheetTable.GetString(row, column);
            if (text == null)
                return TimeSpan.Zero;

            TimeSpan time;
            if (TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out time))
                return time;

            double serial;
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out serial))
                return XlsxCell.FromSerial(serial).TimeOfDay;

            return TimeSpan.Zero;
        }

        private static Dictionary<string, string> LoadDetails()
        {
            var sheet = ReadSheet(DetailsSheetName);

            var keyColumn = sheet.Column("Параметр", "Ключ");
            var valueColumn = sheet.Column("Значение");

            var details = new Dictionary<string, string>(StringComparer.CurrentCultureIgnoreCase);

            if (keyColumn < 0 || valueColumn < 0)
                return details;

            foreach (var row in sheet.Rows)
            {
                var key = SheetTable.GetString(row, keyColumn);
                if (key == null)
                    continue;

                details[key] = SheetTable.GetString(row, valueColumn) ?? string.Empty;
            }

            return details;
        }

        /// <summary>
        /// Списки для выбора в наряде. Лист новый: у тех, кто завёл файл раньше,
        /// его нет — тогда берём заготовку, а не роняем окно.
        /// </summary>
        private static DispatchChoices LoadChoices()
        {
            EnsureFile();

            var sheet = XlsxReader.ReadOrNull(AppPaths.DispatchFile, ListsSheetName);
            if (sheet == null)
                return DispatchChoices.Default();

            var choices = new DispatchChoices();

            foreach (var column in DispatchChoices.Columns)
            {
                var index = sheet.Column(column);
                if (index < 0)
                    continue;

                var values = new List<string>();
                var known = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);

                foreach (var row in sheet.Rows)
                {
                    var value = SheetTable.GetString(row, index);
                    if (value == null)
                        continue;

                    if (known.Add(value))
                        values.Add(value);
                }

                choices.SetByColumn(column, values);
            }

            return choices;
        }

        private static void Save(
            List<DispatchEntry> entries,
            List<CarSchedule> schedules,
            Dictionary<string, string> details,
            DispatchChoices choices)
        {
            var orderRows = entries
                .OrderBy(e => e.Date)
                .ThenBy(e => e.GroupName)
                .Select(e => (IList<XlsxCell>)new List<XlsxCell>
                {
                    XlsxCell.Date(e.Date),
                    XlsxCell.Text(e.OrderNumber),
                    XlsxCell.Text(e.GroupName),
                    XlsxCell.Text(e.CarBrand),
                    XlsxCell.Text(e.CarPlate),
                    XlsxCell.Text(e.CarVin),
                    XlsxCell.Text(e.OperationGroup),
                    XlsxCell.Text(e.Purpose),
                    XlsxCell.Text(e.Route),
                    XlsxCell.Text(e.Assignment),
                    XlsxCell.DateAndTime(e.DepartureAt),
                    XlsxCell.DateAndTime(e.ReturnAt),
                    XlsxCell.Text(e.Notes)
                })
                .ToList();

            var scheduleRows = schedules
                .OrderBy(s => s.CarPlate)
                .Select(s => (IList<XlsxCell>)new List<XlsxCell>
                {
                    XlsxCell.Text(s.CarVin),
                    XlsxCell.Text(s.CarPlate),
                    XlsxCell.Text(s.OperationGroup),
                    XlsxCell.Text(s.Purpose),
                    XlsxCell.Text(s.Route),
                    XlsxCell.Text(s.Assignment),
                    XlsxCell.Text(FormatTime(s.DepartureTime)),
                    XlsxCell.Text(FormatTime(s.ReturnTime)),
                    XlsxCell.Text(s.RepeatDaily ? "да" : "нет"),
                    XlsxCell.Text(s.AllowOutsideOrder ? "да" : "нет"),
                    XlsxCell.Text(s.Notes)
                })
                .ToList();

            var detailRows = details
                .Select(pair => (IList<XlsxCell>)new List<XlsxCell>
                {
                    XlsxCell.Text(pair.Key),
                    XlsxCell.Text(pair.Value)
                })
                .ToList();

            XlsxWriter.Write(AppPaths.DispatchFile, new List<XlsxSheet>
            {
                new XlsxSheet(OrdersSheetName, OrderHeaders, orderRows),
                new XlsxSheet(SchedulesSheetName, ScheduleHeaders, scheduleRows),
                new XlsxSheet(DetailsSheetName, DetailHeaders, detailRows),
                new XlsxSheet(ListsSheetName, DispatchChoices.Columns, ListRows(choices))
            });
        }

        /// <summary>
        /// Лист «Списки»: по столбцу на список, длина столбцов разная,
        /// поэтому короткие добиваются пустыми ячейками.
        /// </summary>
        private static List<IList<XlsxCell>> ListRows(DispatchChoices choices)
        {
            var source = choices ?? DispatchChoices.Default();

            var columns = DispatchChoices.Columns
                .Select(column => source.AllOf(column).ToList())
                .ToList();

            var height = columns.Count == 0 ? 0 : columns.Max(c => c.Count);

            var rows = new List<IList<XlsxCell>>();

            for (var line = 0; line < height; line++)
            {
                var row = new List<XlsxCell>();
                foreach (var column in columns)
                    row.Add(XlsxCell.Text(line < column.Count ? column[line] : null));

                rows.Add(row);
            }

            return rows;
        }

        private static string FormatTime(TimeSpan time)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}", (int)time.TotalHours, time.Minutes);
        }

        /// <summary>Реквизиты-заготовка: пользователь правит их в Excel.</summary>
        public static Dictionary<string, string> DefaultDetails()
        {
            return new Dictionary<string, string>(StringComparer.CurrentCultureIgnoreCase)
            {
                { PrintDetail.UnitName, "" },
                { PrintDetail.OrderPurpose, "" },
                { PrintDetail.AgreedPosition, "" },
                { PrintDetail.AgreedRank, "" },
                { PrintDetail.AgreedName, "" },
                { PrintDetail.ApprovedPosition, "" },
                { PrintDetail.ApprovedRank, "" },
                { PrintDetail.ApprovedName, "" },
                { PrintDetail.SignedPosition, "" },
                { PrintDetail.SignedRank, "" },
                { PrintDetail.SignedName, "" },
                { PrintDetail.BriefingPosition, "" },
                { PrintDetail.BriefingRank, "" },
                { PrintDetail.BriefingName, "" },
                { PrintDetail.ResponsiblePosition, "" },
                { PrintDetail.ResponsibleRank, "" },
                { PrintDetail.ResponsibleName, "" },
                { PrintDetail.PermissionPosition, "" },
                { PrintDetail.PermissionRank, "" },
                { PrintDetail.PermissionName, "" },
                { PrintDetail.DefaultOperationGroup, "тр." },
                { PrintDetail.DefaultCargo, "л/с" }
            };
        }
    }
}
