using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace ParkApp.components.Infrastructure.Excel
{
    /// <summary>
    /// Чтение .xlsx без внешних библиотек. Значения возвращаются строками как есть;
    /// в даты и числа их превращает <see cref="SheetTable"/>, где известен ожидаемый тип.
    /// Такой порядок избавляет от разбора styles.xml, который иначе нужен,
    /// чтобы отличить дату от обычного числа.
    /// </summary>
    public static class XlsxReader
    {
        private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace PackageRelationships = "http://schemas.openxmlformats.org/package/2006/relationships";
        private static readonly XNamespace DocumentRelationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        /// <summary>
        /// Читает лист по имени, а если имя не задано или не найдено — первый лист книги.
        /// </summary>
        public static SheetTable Read(string path, string preferredSheetName = null)
        {
            return ReadCore(path, preferredSheetName, false);
        }

        /// <summary>
        /// Читает лист строго по имени; null, если такого листа в книге нет.
        /// Нужно для справочных листов: молча подсунуть вместо них первый лист нельзя.
        /// </summary>
        public static SheetTable ReadOrNull(string path, string sheetName)
        {
            if (!File.Exists(path))
                return null;

            return ReadCore(path, sheetName, true);
        }

        /// <summary>Имена листов книги в порядке следования. Пустой список, если файл не читается.</summary>
        public static IList<string> SheetNames(string path)
        {
            var names = new List<string>();

            if (!File.Exists(path))
                return names;

            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    var workbookEntry = archive.GetEntry("xl/workbook.xml");
                    if (workbookEntry == null)
                        return names;

                    using (var workbookStream = workbookEntry.Open())
                    {
                        var document = XDocument.Load(workbookStream);
                        if (document.Root == null)
                            return names;

                        foreach (var sheet in document.Root.Descendants(Main + "sheet"))
                        {
                            var name = (string)sheet.Attribute("name");
                            if (!string.IsNullOrWhiteSpace(name))
                                names.Add(name);
                        }
                    }
                }
            }
            catch (InvalidDataException)
            {
                // не .xlsx или файл повреждён — вернём пустой список, разберётся вызывающий
            }

            return names;
        }

        private static SheetTable ReadCore(string path, string preferredSheetName, bool requireSheet)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException(string.Format("Файл «{0}» не найден.", path), path);

            try
            {
                // ReadWrite в доступе для других процессов: файл может быть открыт в Excel
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    var sharedStrings = ReadSharedStrings(archive);
                    var sheetEntryName = FindSheetEntry(archive, preferredSheetName, requireSheet);

                    if (sheetEntryName == null)
                        return null;

                    var entry = archive.GetEntry(sheetEntryName);
                    if (entry == null)
                        throw new InvalidOperationException(string.Format("В файле «{0}» не найден лист с данными.", Path.GetFileName(path)));

                    using (var sheetStream = entry.Open())
                    {
                        var document = XDocument.Load(sheetStream);
                        return SheetTable.FromRows(ReadRows(document, sharedStrings));
                    }
                }
            }
            catch (InvalidDataException ex)
            {
                throw new InvalidOperationException(
                    string.Format("Файл «{0}» повреждён или это не .xlsx. Старый формат .xls нужно пересохранить как .xlsx.",
                        Path.GetFileName(path)), ex);
            }
        }

        private static List<RawCell[]> ReadRows(XDocument document, IList<string> sharedStrings)
        {
            var rows = new List<RawCell[]>();

            if (document.Root == null)
                return rows;

            var sheetData = document.Root.Element(Main + "sheetData");
            if (sheetData == null)
                return rows;

            foreach (var rowElement in sheetData.Elements(Main + "row"))
            {
                var values = new Dictionary<int, RawCell>();
                var maxIndex = -1;
                var nextIndex = 0;

                foreach (var cellElement in rowElement.Elements(Main + "c"))
                {
                    var reference = (string)cellElement.Attribute("r");
                    var index = reference != null ? ColumnIndex(reference) : nextIndex;
                    if (index < 0)
                        index = nextIndex;

                    nextIndex = index + 1;

                    var value = CellValue(cellElement, sharedStrings);
                    if (value == null || string.IsNullOrEmpty(value.Value))
                        continue;

                    values[index] = value;
                    if (index > maxIndex)
                        maxIndex = index;
                }

                var row = new RawCell[maxIndex + 1];
                foreach (var pair in values)
                    row[pair.Key] = pair.Value;

                rows.Add(row);
            }

            return rows;
        }

        private static RawCell CellValue(XElement cell, IList<string> sharedStrings)
        {
            var type = (string)cell.Attribute("t");
            var style = 0;
            int parsedStyle;
            if (int.TryParse((string)cell.Attribute("s"), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedStyle))
                style = parsedStyle;

            if (type == "inlineStr")
            {
                var inline = cell.Element(Main + "is");
                if (inline == null)
                    return null;

                return new RawCell(string.Concat(inline.Descendants(Main + "t").Select(t => t.Value)), true, style);
            }

            var valueElement = cell.Element(Main + "v");
            if (valueElement == null)
                return null;

            var raw = valueElement.Value;

            if (type == "s")
            {
                int index;
                if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out index)
                    && index >= 0 && index < sharedStrings.Count)
                    return new RawCell(sharedStrings[index], true, style);

                return null;
            }

            if (type == "b")
                return new RawCell(raw == "1" ? "ИСТИНА" : "ЛОЖЬ", true, style);

            // числа и даты: дата отличается от числа только стилем, поэтому его и храним
            return new RawCell(raw, type == "str" || type == "e", style);
        }

        private static List<string> ReadSharedStrings(ZipArchive archive)
        {
            var result = new List<string>();

            var entry = archive.GetEntry("xl/sharedStrings.xml");
            if (entry == null)
                return result;

            using (var stream = entry.Open())
            {
                var document = XDocument.Load(stream);
                if (document.Root == null)
                    return result;

                foreach (var item in document.Root.Elements(Main + "si"))
                    result.Add(string.Concat(item.Descendants(Main + "t").Select(t => t.Value)));
            }

            return result;
        }

        /// <summary>
        /// Путь к нужному листу внутри архива через workbook.xml и его связи.
        /// При requireSheet возвращает null, если листа с таким именем нет,
        /// иначе откатывается на первый лист книги.
        /// </summary>
        private static string FindSheetEntry(ZipArchive archive, string preferredSheetName, bool requireSheet)
        {
            var fallback = requireSheet ? null : "xl/worksheets/sheet1.xml";

            var workbookEntry = archive.GetEntry("xl/workbook.xml");
            if (workbookEntry == null)
                return fallback;

            string firstRelationId = null;
            string matchedRelationId = null;

            using (var stream = workbookEntry.Open())
            {
                var document = XDocument.Load(stream);
                if (document.Root != null)
                {
                    foreach (var sheet in document.Root.Descendants(Main + "sheet"))
                    {
                        var relationId = (string)sheet.Attribute(DocumentRelationships + "id");
                        if (relationId == null)
                            continue;

                        if (firstRelationId == null)
                            firstRelationId = relationId;

                        var name = (string)sheet.Attribute("name");
                        if (matchedRelationId == null
                            && !string.IsNullOrWhiteSpace(preferredSheetName)
                            && string.Equals(name, preferredSheetName, StringComparison.CurrentCultureIgnoreCase))
                            matchedRelationId = relationId;
                    }
                }
            }

            if (requireSheet && matchedRelationId == null)
                return null;

            var targetRelationId = matchedRelationId ?? firstRelationId;
            if (targetRelationId == null)
                return fallback;

            var relationsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels");
            if (relationsEntry == null)
                return fallback;

            using (var stream = relationsEntry.Open())
            {
                var document = XDocument.Load(stream);
                if (document.Root == null)
                    return fallback;

                foreach (var relationship in document.Root.Elements(PackageRelationships + "Relationship"))
                {
                    if (!string.Equals((string)relationship.Attribute("Id"), targetRelationId, StringComparison.Ordinal))
                        continue;

                    var target = (string)relationship.Attribute("Target");
                    if (string.IsNullOrWhiteSpace(target))
                        break;

                    return NormalizeTarget(target);
                }
            }

            return fallback;
        }

        private static string NormalizeTarget(string target)
        {
            target = target.Replace('\\', '/').Trim();

            if (target.StartsWith("/", StringComparison.Ordinal))
                return target.Substring(1);

            while (target.StartsWith("../", StringComparison.Ordinal))
                target = target.Substring(3);

            return target.StartsWith("xl/", StringComparison.Ordinal) ? target : "xl/" + target;
        }

        /// <summary>«BC12» → 54 (индекс столбца с нуля).</summary>
        private static int ColumnIndex(string cellReference)
        {
            var index = 0;

            foreach (var c in cellReference)
            {
                if (c >= 'A' && c <= 'Z')
                    index = index * 26 + (c - 'A' + 1);
                else if (c >= 'a' && c <= 'z')
                    index = index * 26 + (c - 'a' + 1);
                else
                    break;
            }

            return index - 1;
        }
    }
}
