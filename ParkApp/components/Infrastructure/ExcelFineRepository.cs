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
    /// Книга постановлений — та же таблица, которую ведут в Excel.
    ///
    /// Два правила, из которых следует вся реализация:
    /// 1. Порядок строк сохраняется, новые дописываются в конец: это журнал,
    ///    а не отсортированный список.
    /// 2. Столбцы, которых приложение не знает, возвращаются на место как есть —
    ///    вместе с типом и оформлением ячейки. Иначе перезапись превратила бы
    ///    дату в чужом столбце в число.
    /// </summary>
    public class ExcelFineRepository : IFineRepository
    {
        private static readonly object Sync = new object();

        /// <summary>Шапка книги. Используется, только если файла ещё нет.</summary>
        private static readonly string[] DefaultHeaders =
        {
            "№ п/п",
            "Дата правонарушения, Ф.И.О. правонарушителя",
            "Марка АТ",
            "ГРЗ",
            "Дата привлечения",
            "Номер постановления",
            "Сумма штрафа",
            "оплата, чек, дата",
            "Примечание",
            ScanHeader
        };

        /// <summary>Столбец приложения: в книге его нет, но ссылку на скан хранить где-то надо.</summary>
        public const string ScanHeader = "Скан постановления";

        public static readonly string[] RowNumberNames = { "№ п/п", "№" };
        public static readonly string[] ViolationNames =
        {
            "Дата правонарушения, Ф.И.О. правонарушителя", "Дата правонарушения", "Правонарушение"
        };
        public static readonly string[] BrandNames = { "Марка АТ", "Марка", "Марка автомобиля" };
        public static readonly string[] PlateNames = { "ГРЗ", "Гос. рег. знак", "Гос. номер", "Госномер" };
        public static readonly string[] ResolutionDateNames = { "Дата привлечения", "Дата постановления" };
        public static readonly string[] NumberNames = { "Номер постановления", "№ постановления", "Постановление" };
        public static readonly string[] AmountNames = { "Сумма штрафа", "Сумма" };
        public static readonly string[] PaymentNames = { "оплата, чек, дата", "Оплата", "Оплата, чек, дата" };
        public static readonly string[] NotesNames = { "Примечание", "Примечания" };
        public static readonly string[] ScanNames = { ScanHeader, "Скан" };

        /// <summary>Дата внутри текста ячейки: «Аюпов Д.А. 10.02.2026».</summary>
        private static readonly Regex DateInText =
            new Regex(@"\b\d{1,2}[.\-/]\d{1,2}[.\-/]\d{2,4}\b", RegexOptions.Compiled);

        // состояние последнего чтения: нужно, чтобы вернуть в файл чужие столбцы и порядок строк
        private static List<string> _headers = new List<string>();
        private static Dictionary<string, RawCell[]> _rawByNumber =
            new Dictionary<string, RawCell[]>(StringComparer.OrdinalIgnoreCase);
        private static List<string> _order = new List<string>();

        public static string SheetName
        {
            get { return AppSheets.Name(SheetKind.Fines); }
        }

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
                        string.Format("Постановление № {0} уже есть в книге.", fine.ResolutionNumber));

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
                        "Книга постановлений не найдена:{0}{1}{0}{0}Проверьте путь в настройках — возможно, файл переместили.",
                        Environment.NewLine, path));

                var seed = CreateSeed();
                ResetState();
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

            var layout = Layout.From(sheet);
            if (layout.Number < 0)
                throw new InvalidOperationException(
                    "В книге постановлений не найден столбец «Номер постановления». " +
                    "Проверьте, что шапка таблицы на первой заполненной строке листа.");

            _headers = sheet.Headers.ToList();
            _rawByNumber = new Dictionary<string, RawCell[]>(StringComparer.OrdinalIgnoreCase);
            _order = new List<string>();

            var fines = new List<Fine>();

            for (var i = 0; i < sheet.Rows.Count; i++)
            {
                var row = sheet.Rows[i];
                var number = SheetTable.GetString(row, layout.Number);

                // строка без номера постановления — это ключ записи, без него она бессмысленна
                if (number == null)
                    continue;

                var fine = new Fine
                {
                    RowNumber = SheetTable.GetInt(row, layout.RowNumber),
                    ResolutionNumber = number,
                    ResolutionDate = SheetTable.GetDate(row, layout.ResolutionDate) ?? default(DateTime),
                    CarBrand = SheetTable.GetString(row, layout.Brand),
                    CarPlate = SheetTable.GetString(row, layout.Plate),
                    Amount = SheetTable.GetDecimal(row, layout.Amount) ?? 0m,
                    PaymentText = SheetTable.GetString(row, layout.Payment),
                    Notes = SheetTable.GetString(row, layout.Notes),
                    ScanPath = SheetTable.GetString(row, layout.Scan)
                };

                ReadViolationCell(row, layout.Violation, fine);

                fine.PaidDate = PaymentTextParser.ParseDate(fine.PaymentText);
                fine.PaymentReference = PaymentTextParser.ParseReference(fine.PaymentText);

                fines.Add(fine);

                var key = Key(number);
                if (!_rawByNumber.ContainsKey(key))
                {
                    _rawByNumber.Add(key, i < sheet.RawRows.Count ? sheet.RawRows[i] : null);
                    _order.Add(key);
                }
            }

            return fines;
        }

        /// <summary>
        /// «Аюпов Д.А. 10.02.2026» — дата и ФИО в одной ячейке.
        /// Дата ищется в тексте, остальное считается фамилией.
        /// </summary>
        private static void ReadViolationCell(string[] row, int column, Fine fine)
        {
            var text = SheetTable.GetString(row, column);
            if (text == null)
                return;

            var match = DateInText.Match(text);
            if (match.Success)
            {
                var parsed = PaymentTextParser.ParseDate(match.Value);
                if (parsed.HasValue)
                    fine.ViolationDate = parsed.Value;

                var name = text.Remove(match.Index, match.Length);
                fine.OffenderName = CleanName(name);
                return;
            }

            // ячейка может быть просто датой (Excel хранит её числом)
            var asDate = SheetTable.GetDate(row, column);
            if (asDate.HasValue)
                fine.ViolationDate = asDate.Value;
            else
                fine.OffenderName = CleanName(text);
        }

        private static string CleanName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var cleaned = value.Replace('\r', ' ').Replace('\n', ' ').Trim(' ', ',', ';', '-');
            return cleaned.Length == 0 ? null : cleaned;
        }

        /// <summary>Собирает ячейку «дата правонарушения + ФИО» обратно, как её пишут в книге.</summary>
        private static string BuildViolationCell(Fine fine)
        {
            var date = fine.ViolationDate == default(DateTime)
                ? null
                : fine.ViolationDate.ToString("dd.MM.yyyy");

            if (string.IsNullOrWhiteSpace(fine.OffenderName))
                return date;

            return date == null
                ? fine.OffenderName.Trim()
                : fine.OffenderName.Trim() + Environment.NewLine + date;
        }

        private static void Save(List<Fine> fines)
        {
            var headers = _headers.Count > 0 ? new List<string>(_headers) : new List<string>(DefaultHeaders);

            // столбца для скана в книге может не быть — добавляем свой в конец
            var layout = Layout.From(headers);
            if (layout.Scan < 0)
            {
                headers.Add(ScanHeader);
                layout = Layout.From(headers);
            }

            var nextRowNumber = NextRowNumber(fines);
            var rows = new List<IList<XlsxCell>>();

            foreach (var fine in Ordered(fines))
            {
                RawCell[] raw;
                _rawByNumber.TryGetValue(Key(fine.ResolutionNumber), out raw);

                var cells = new XlsxCell[headers.Count];

                // сначала возвращаем на место всё, что было в строке
                if (raw != null)
                {
                    for (var i = 0; i < cells.Length && i < raw.Length; i++)
                    {
                        if (raw[i] != null)
                            cells[i] = XlsxCell.Raw(raw[i].Value, raw[i].IsText, raw[i].Style, raw[i].Formula);
                    }
                }

                // затем перезаписываем только известные столбцы
                Set(cells, layout.Violation, XlsxCell.Text(BuildViolationCell(fine)));
                Set(cells, layout.Brand, XlsxCell.Text(fine.CarBrand));
                Set(cells, layout.Plate, XlsxCell.Text(fine.CarPlate));
                Set(cells, layout.ResolutionDate, XlsxCell.Date(fine.ResolutionDate));
                Set(cells, layout.Number, XlsxCell.Text(fine.ResolutionNumber));
                Set(cells, layout.Amount, XlsxCell.Money(fine.Amount));
                Set(cells, layout.Payment, XlsxCell.Text(fine.PaymentText));
                Set(cells, layout.Notes, XlsxCell.Text(fine.Notes));
                Set(cells, layout.Scan, XlsxCell.Text(fine.ScanPath));

                // номер по порядку трогаем только у новых строк: чужую нумерацию не переписываем
                if (raw == null)
                {
                    if (!fine.RowNumber.HasValue)
                        fine.RowNumber = nextRowNumber++;

                    Set(cells, layout.RowNumber, XlsxCell.Number(fine.RowNumber));
                }

                rows.Add(cells.ToList());
            }

            XlsxWriter.Write(AppPaths.FinesFile, SheetName, headers, rows);
        }

        /// <summary>Существующие строки в прежнем порядке, новые — в конец.</summary>
        private static IEnumerable<Fine> Ordered(List<Fine> fines)
        {
            var byKey = new Dictionary<string, Fine>(StringComparer.OrdinalIgnoreCase);
            foreach (var fine in fines)
            {
                var key = Key(fine.ResolutionNumber);
                if (!byKey.ContainsKey(key))
                    byKey.Add(key, fine);
            }

            var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var key in _order)
            {
                Fine fine;
                if (byKey.TryGetValue(key, out fine) && written.Add(key))
                    yield return fine;
            }

            foreach (var fine in fines)
            {
                if (written.Add(Key(fine.ResolutionNumber)))
                    yield return fine;
            }
        }

        private static int NextRowNumber(IEnumerable<Fine> fines)
        {
            var used = fines.Where(f => f.RowNumber.HasValue).Select(f => f.RowNumber.Value).ToList();
            return used.Count > 0 ? used.Max() + 1 : 1;
        }

        private static void Set(XlsxCell[] cells, int column, XlsxCell value)
        {
            if (column >= 0 && column < cells.Length)
                cells[column] = value;
        }

        private static void ResetState()
        {
            _headers = new List<string>();
            _rawByNumber = new Dictionary<string, RawCell[]>(StringComparer.OrdinalIgnoreCase);
            _order = new List<string>();
        }

        private static string Key(string resolutionNumber)
        {
            return (resolutionNumber ?? string.Empty).Trim();
        }

        private static bool SameNumber(string left, string right)
        {
            return string.Equals(Key(left), Key(right), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Номера столбцов книги.</summary>
        private class Layout
        {
            public int RowNumber = -1;
            public int Violation = -1;
            public int Brand = -1;
            public int Plate = -1;
            public int ResolutionDate = -1;
            public int Number = -1;
            public int Amount = -1;
            public int Payment = -1;
            public int Notes = -1;
            public int Scan = -1;

            public static Layout From(SheetTable sheet)
            {
                return new Layout
                {
                    RowNumber = sheet.Column(RowNumberNames),
                    Violation = sheet.Column(ViolationNames),
                    Brand = sheet.Column(BrandNames),
                    Plate = sheet.Column(PlateNames),
                    ResolutionDate = sheet.Column(ResolutionDateNames),
                    Number = sheet.Column(NumberNames),
                    Amount = sheet.Column(AmountNames),
                    Payment = sheet.Column(PaymentNames),
                    Notes = sheet.Column(NotesNames),
                    Scan = sheet.Column(ScanNames)
                };
            }

            public static Layout From(IList<string> headers)
            {
                return From(SheetTable.FromHeaders(headers));
            }
        }

        /// <summary>Пример книги для первого запуска. Заменяется реальной.</summary>
        private static List<Fine> CreateSeed()
        {
            var today = DateTime.Today;

            return new List<Fine>
            {
                new Fine
                {
                    RowNumber = 1,
                    ResolutionNumber = "12312312",
                    ResolutionDate = today.AddDays(-20),
                    ViolationDate = today.AddDays(-21),
                    OffenderName = "Аюпов Д.А.",
                    CarBrand = "УАЗ-3163",
                    CarPlate = "А123ВС16",
                    Amount = 800m,
                    PaymentText = "Оплата " + today.AddDays(-2).ToString("dd.MM.yyyy") + " ИД платежа 952951978536EGLG",
                    Notes = "рапорт не отнесли в ФЭС"
                },
                new Fine
                {
                    RowNumber = 2,
                    ResolutionNumber = "18810516250405554443",
                    ResolutionDate = today.AddDays(-8),
                    ViolationDate = today.AddDays(-10),
                    OffenderName = "Петров П.П.",
                    CarBrand = "КамАЗ-5350",
                    CarPlate = "5555СЕ",
                    Amount = 1500m
                }
            };
        }
    }
}
