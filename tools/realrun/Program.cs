using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ParkApp.components.Application;
using ParkApp.components.Domain;
using ParkApp.components.Infrastructure;

class TestSettings : IAppSettings
{
    public readonly Dictionary<PathSetting, string> Paths = new Dictionary<PathSetting, string>();
    public readonly Dictionary<SheetKind, string> Sheets = new Dictionary<SheetKind, string>();
    public bool IsSectionEnabled(AppSection s) { return true; }
    public void SetSectionEnabled(AppSection s, bool e) { }
    public string GetPath(PathSetting s) { string v; return Paths.TryGetValue(s, out v) ? v : null; }
    public void SetPath(PathSetting s, string p) { Paths[s] = p; }
    public string GetSheetName(SheetKind s) { string v; return Sheets.TryGetValue(s, out v) ? v : null; }
    public void SetSheetName(SheetKind s, string n) { Sheets[s] = n; }
    public void Save() { }
}

static class Program
{
    static int failures;
    static readonly XNamespace M = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    static void Check(bool ok, string what)
    {
        Console.WriteLine((ok ? "  ok    " : "  ПЛОХО ") + what);
        if (!ok) failures++;
    }

    static int Main(string[] args)
    {
        var root = Path.GetFullPath(args[0]);
        var work = Path.Combine(root, "work");
        if (Directory.Exists(work)) Directory.Delete(work, true);
        Directory.CreateDirectory(work);
        File.Copy(Path.Combine(root, "fixtures", "Машины.xlsx"), Path.Combine(work, "Машины.xlsx"));
        File.Copy(Path.Combine(root, "fixtures", "Штрафы.xlsx"), Path.Combine(work, "Штрафы.xlsx"));

        var settings = new TestSettings();
        settings.Paths[PathSetting.DataRoot] = work;
        settings.Paths[PathSetting.SharedRoot] = work;
        settings.Paths[PathSetting.CarsFile] = Path.Combine(work, "Машины.xlsx");
        settings.Paths[PathSetting.FinesFile] = Path.Combine(work, "Штрафы.xlsx");
        settings.Sheets[SheetKind.Fines] = "TEST";
        AppPaths.UseSettings(settings);
        AppSheets.UseSettings(settings);

        Cars();
        People();
        Fines(Path.Combine(work, "Штрафы.xlsx"));

        Console.WriteLine();
        Console.WriteLine(failures == 0 ? "ИТОГ: всё сошлось" : "ИТОГ: ПРОВАЛЕНО " + failures);
        return failures == 0 ? 0 : 1;
    }

    static void Cars()
    {
        Console.WriteLine("=== машины (настоящий ExcelCarRepository)");
        var cars = new ExcelCarRepository().GetAllAsync().Result;
        Console.WriteLine("  прочитано: " + cars.Count);
        foreach (var c in cars.Take(5))
            Console.WriteLine("   {0,-9} {1,-16} латиница={2,-16} ГРЗ=[{3}] {4}/{5} полис={6} до {7:dd.MM.yyyy} ДЛ={8}:{9} место={10} штат={11} конф={12}",
                c.Key, c.Model, c.ModelLatin, string.Join(" | ", c.Numbers.Select(n => n.Text)),
                c.PowerKw, c.PowerHp, c.InsurancePolicy, c.InsuranceEndsAt, c.OfficialId, c.OfficialName,
                c.Location, c.IsStaff, c.IsConfiscated);

        Check(cars.Count == 128, "все строки прочитаны, в том числе без VIN: " + cars.Count);
        Check(cars.Select(c => c.Key).Distinct().Count() == cars.Count, "ключи машин не повторяются");
        Check(!cars.Any(c => c.Numbers.Any(n => n.Text.StartsWith("отс"))), "«отс.» не стало номером");
        Check(!cars.Any(c => (c.InsurancePolicy ?? "").StartsWith("отс")), "«отс.» не стало номером полиса");
        var skoda = cars[0];
        Check(skoda.InsurancePolicy == "ТТТ 123456789", "полис отделён от срока: " + skoda.InsurancePolicy);
        Check(skoda.InsuranceEndsAt == new DateTime(2027, 9, 12), "срок страховки: " + skoda.InsuranceEndsAt);
        Check(skoda.OfficialName == "начальник штаба подполковник Антонов А.А.", "должностное лицо из формулы-ссылки");
        Check(skoda.OfficialId == 2, "номер должностного лица: " + skoda.OfficialId);
        Check(skoda.IsStaff == false, "«За штатом» → не штатная");
        Check(skoda.IsConfiscated, "«Конфискат» → конфискат");
        Check(cars[1].OfficialName == null, "ссылка на пустую ячейку (0) не стала фамилией");
        Check(cars[1].Location == Location.Vo, "«СВО» → ВО");
        Check(cars[1].InsuranceEndsAt == null, "формула «отс.» в сроке страховки не стала датой");
        var lexus = cars.First(c => c.Model == "Лексус ЛХ570");
        Check(lexus.Numbers.Count == 2, "два номера через перенос строки: " + lexus.Numbers.Count);
        Check(lexus.PowerKw == 103 && lexus.PowerHp == 140, "дробная мощность 102,7/139,7 → " + lexus.PowerKw + "/" + lexus.PowerHp);
    }

    static void People()
    {
        Console.WriteLine("=== должностные лица (настоящий ExcelPersonRepository)");
        var people = new ExcelPersonRepository().GetAllAsync().Result;
        Console.WriteLine("  прочитано: " + people.Count + " — " + string.Join("; ", people.Select(p => p.Id + ":" + p.FullName)));
        Check(people.Count == 1, "пустые строки-заготовки не стали людьми");
        Check(people.Count > 0 && people[0].Id == 2, "номер берётся из столбца «№», а не по порядку");
    }

    static void Fines(string path)
    {
        Console.WriteLine("=== книга постановлений (настоящий ExcelFineRepository)");
        var repo = new ExcelFineRepository();
        var service = new FineService(repo);
        var fines = service.GetAllAsync().Result;
        foreach (var f in fines)
            Console.WriteLine("   №{0,-11} {1,-15} наруш.{2:dd.MM.yyyy} привл.{3:dd.MM.yyyy} {4,6} оплачен={5} {6:dd.MM.yyyy} ид={7} прим={8}",
                f.ResolutionNumber, f.OffenderName, f.ViolationDate, f.ResolutionDate, f.Amount, f.IsPaid,
                f.PaidDate, f.PaymentReference, f.Notes);
        Check(fines.Count == 4, "прочитано записей (одна с повторным номером): " + fines.Count);
        Check(fines.All(f => f.ResolutionDate != default(DateTime)), "дата привлечения, хранимая текстом, прочитана");
        Check(fines.All(f => f.ViolationDate != default(DateTime)), "дата нарушения вынута из ячейки с Ф.И.О.");
        Check(fines[0].PaidDate == new DateTime(2026, 2, 19), "дата оплаты из графы оплаты");
        Check(fines[0].PaymentReference == "54354", "ИД платежа: " + fines[0].PaymentReference);
        Check(!fines[2].IsPaid, "без графы оплаты — не оплачен");

        var before = Snapshot(path);

        // правка существующей записи
        var first = fines[0];
        first.Amount = 2500m;
        service.UpdateAsync(first).Wait();

        // новая запись
        service.AddAsync(new Fine
        {
            ResolutionNumber = "18810000000000000001",
            ResolutionDate = new DateTime(2026, 9, 1),
            ViolationDate = new DateTime(2026, 8, 30),
            OffenderName = "Петров П.П.",
            CarBrand = "КамАЗ",
            CarPlate = "1234 АБ 34",
            Amount = 500m
        }).Wait();

        var after = Snapshot(path);
        Console.WriteLine("  после сохранения:");
        foreach (var row in after.Rows.Take(8))
            Console.WriteLine("    " + row);
        if (after.Merges.Count > 0) Console.WriteLine("    объединения: " + string.Join(", ", after.Merges));

        Check(after.Find("ОБЩАЯ СУММА") != null, "строка «ОБЩАЯ СУММА» на месте");
        Check(after.SheetNames.Count == before.SheetNames.Count,
              "другие листы книги не удалены: было " + string.Join(", ", before.SheetNames)
              + "; стало " + string.Join(", ", after.SheetNames));
        Check(after.MaxStyle < after.StyleCount,
              "номера стилей ячеек в пределах таблицы стилей: наибольший " + after.MaxStyle
              + " при " + after.StyleCount + " стилях (иначе Excel считает файл повреждённым)");
        Check(after.Find("Дубль Д.Д.") != null, "запись с повторным номером постановления не пропала");
        Check(after.Formulas.Any(f => f.StartsWith("SUM(")), "формула суммы сохранена: " + string.Join(", ", after.Formulas));
        Check(after.Find("Пока не платить до уточнения") != null, "примечание правее шапки (L) на месте");
        Check(after.Merges.Count == before.Merges.Count,
              "объединённые ячейки сохранены: было " + string.Join(",", before.Merges) + " стало " + string.Join(",", after.Merges));
        Check(after.RowOf("ОБЩАЯ СУММА") == 2, "«ОБЩАЯ СУММА» осталась во 2-й строке: " + after.RowOf("ОБЩАЯ СУММА"));
        Check(after.RowOf("18810000000000000001") > after.RowOf("3454353524"), "новая запись дописана в конец");

        var sumRange = after.Formulas.FirstOrDefault(f => f.StartsWith("SUM("));
        var lastRow = after.RowOf("18810000000000000001");
        Check(sumRange != null && Covers(sumRange, lastRow), "новая запись попадает в формулу суммы " + sumRange + " (строка " + lastRow + ")");

        var reread = new ExcelFineRepository().GetAllAsync().Result;
        Check(reread.Count == 4, "после записи и повторного чтения записей: " + reread.Count);
        var r0 = reread.FirstOrDefault(f => f.ResolutionNumber == "435435");
        Check(r0 != null && r0.Amount == 2500m, "правка суммы сохранилась");
        Check(r0 != null && r0.PaymentText != null && r0.PaymentText.Contains("ИД платежа 54354"),
              "графа оплаты не переписана своей формулировкой: " + (r0 == null ? "—" : r0.PaymentText.Replace("\n", "⏎")));
        Check(r0 != null && r0.ResolutionDate == new DateTime(2026, 1, 11), "дата привлечения не съехала");

        service.DeleteAsync("5435435").Wait();
        var final = Snapshot(path);
        Check(final.Find("Либо платить, по старым, но делать приказ") == null,
              "удалённая запись ушла вместе со своей пометкой правее шапки");
        Check(final.Find("ОБЩАЯ СУММА") != null, "после удаления «ОБЩАЯ СУММА» на месте");
    }

    static bool Covers(string formula, int row)
    {
        var m = Regex.Match(formula, @"[A-Z]+(\d+):[A-Z]+(\d+)");
        return m.Success && int.Parse(m.Groups[1].Value) <= row && row <= int.Parse(m.Groups[2].Value);
    }

    class Sheet
    {
        public List<string> Rows = new List<string>();
        public List<string> Formulas = new List<string>();
        public List<string> Merges = new List<string>();
        public List<string> SheetNames = new List<string>();
        public int StyleCount;
        public int MaxStyle = -1;
        public Dictionary<int, List<string>> Values = new Dictionary<int, List<string>>();
        public string Find(string text) { return Rows.FirstOrDefault(r => r.Contains(text)); }
        public int RowOf(string text) { foreach (var kv in Values) if (kv.Value.Any(v => v == text || v.Contains(text))) return kv.Key; return -1; }
    }

    static Sheet Snapshot(string path)
    {
        var s = new Sheet();
        using (var z = ZipFile.OpenRead(path))
        {
            var shared = new List<string>();
            var sst = z.GetEntry("xl/sharedStrings.xml");
            if (sst != null)
                using (var st = sst.Open())
                    shared = XDocument.Load(st).Descendants(M + "si").Select(si => string.Concat(si.Descendants(M + "t").Select(t => t.Value))).ToList();

            var wb = XDocument.Load(z.GetEntry("xl/workbook.xml").Open());
            s.SheetNames = wb.Descendants(M + "sheet").Select(x => (string)x.Attribute("name")).ToList();
            var styles = z.GetEntry("xl/styles.xml");
            if (styles != null)
            {
                var xfs = XDocument.Load(styles.Open()).Descendants(M + "cellXfs").FirstOrDefault();
                s.StyleCount = xfs == null ? 0 : xfs.Elements(M + "xf").Count();
            }
            var sheetEntry = z.Entries.First(e => e.FullName.StartsWith("xl/worksheets/sheet"));
            var doc = XDocument.Load(sheetEntry.Open());
            foreach (var row in doc.Descendants(M + "row"))
            {
                var n = int.Parse((string)row.Attribute("r"));
                var vals = new List<string>();
                var line = new List<string>();
                foreach (var c in row.Elements(M + "c"))
                {
                    var t = (string)c.Attribute("t");
                    var f = c.Element(M + "f");
                    string v = t == "inlineStr" ? string.Concat(c.Descendants(M + "t").Select(x => x.Value))
                             : (string)c.Element(M + "v");
                    if (t == "s" && v != null) v = shared[int.Parse(v)];
                    if (f != null) s.Formulas.Add(f.Value);
                    var st = (string)c.Attribute("s");
                    if (st != null) s.MaxStyle = Math.Max(s.MaxStyle, int.Parse(st));
                    vals.Add(v ?? "");
                    line.Add((string)c.Attribute("r") + "=" + (v ?? "").Replace("\n", "⏎") + (f != null ? " ⟵=" + f.Value : ""));
                }
                s.Values[n] = vals;
                s.Rows.Add(n + ": " + string.Join(" | ", line));
            }
            s.Merges = doc.Descendants(M + "mergeCell").Select(m => (string)m.Attribute("ref")).ToList();
        }
        return s;
    }
}
