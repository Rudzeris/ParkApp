using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace ParkApp.components.Infrastructure.Excel
{
    /// <summary>
    /// Запись .xlsx без внешних библиотек: xlsx — это zip с несколькими XML.
    /// Своя реализация выбрана вместо ClosedXML/NPOI по одной причине: проект
    /// на packages.config, где каждую транзитивную зависимость приходится
    /// прописывать в csproj вручную. Здесь нужен плоский лист «шапка + строки»,
    /// и это дешевле, чем тянуть дерево пакетов.
    /// Заменяется на библиотеку правкой двух файлов — XlsxWriter и XlsxReader.
    /// </summary>
    public static class XlsxWriter
    {
        private const string MainNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private const string RelationshipNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        /// <summary>
        /// Перезаписывает файл целиком. Запись идёт во временный файл и подменяет
        /// целевой одним движением: обрыв на середине не оставит битую таблицу.
        /// Прежняя версия сохраняется рядом с расширением .bak.
        /// </summary>
        public static void Write(string path, string sheetName, IList<string> headers, IEnumerable<IList<XlsxCell>> rows)
        {
            Write(path, new List<XlsxSheet> { new XlsxSheet(sheetName, headers, rows) });
        }

        /// <summary>
        /// Перезаписывает книгу целиком. Несколько листов нужны там, где рядом с данными
        /// лежат справочные списки — «Куда относится», «Тип машины».
        /// </summary>
        public static void Write(string path, IList<XlsxSheet> sheets)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Не указан путь к файлу.", "path");

            if (sheets == null || sheets.Count == 0)
                throw new ArgumentException("Нужен хотя бы один лист.", "sheets");

            AppPaths.EnsureFolderFor(path);

            var tempPath = path + ".tmp";
            var backupPath = path + ".bak";

            try
            {
                using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
                {
                    WriteEntry(archive, "[Content_Types].xml", ContentTypes(sheets.Count));
                    WriteEntry(archive, "_rels/.rels", RootRelationships());
                    WriteEntry(archive, "xl/workbook.xml", Workbook(sheets));
                    WriteEntry(archive, "xl/_rels/workbook.xml.rels", WorkbookRelationships(sheets.Count));
                    WriteEntry(archive, "xl/styles.xml", Styles());

                    for (var i = 0; i < sheets.Count; i++)
                    {
                        WriteEntry(archive,
                            string.Format(CultureInfo.InvariantCulture, "xl/worksheets/sheet{0}.xml", i + 1),
                            Sheet(sheets[i].Headers, sheets[i].Rows));
                    }
                }

                if (File.Exists(path))
                {
                    File.Replace(tempPath, path, backupPath);
                }
                else
                {
                    File.Move(tempPath, path);
                }
            }
            catch (IOException ex)
            {
                throw new InvalidOperationException(
                    string.Format(
                        "Не удалось записать файл «{0}». Скорее всего он открыт в Excel — закройте его и повторите.{1}{2}",
                        Path.GetFileName(path), Environment.NewLine, ex.Message),
                    ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new InvalidOperationException(
                    string.Format("Нет прав на запись файла «{0}».", Path.GetFileName(path)), ex);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); }
                    catch (IOException) { /* временный файл — не повод падать */ }
                }
            }
        }

        private static void WriteEntry(ZipArchive archive, string entryName, string content)
        {
            var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
            using (var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false)))
                writer.Write(content);
        }

        private static string ContentTypes(int sheetCount)
        {
            var builder = new StringBuilder();
            builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            builder.Append("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">");
            builder.Append("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>");
            builder.Append("<Default Extension=\"xml\" ContentType=\"application/xml\"/>");
            builder.Append("<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>");

            for (var i = 1; i <= sheetCount; i++)
            {
                builder.Append("<Override PartName=\"/xl/worksheets/sheet").Append(i)
                       .Append(".xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
            }

            builder.Append("<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>");
            builder.Append("</Types>");
            return builder.ToString();
        }

        private static string RootRelationships()
        {
            return
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                "<Relationship Id=\"rId1\" Type=\"" + RelationshipNamespace + "/officeDocument\" Target=\"xl/workbook.xml\"/>" +
                "</Relationships>";
        }

        private static string Workbook(IList<XlsxSheet> sheets)
        {
            var builder = new StringBuilder();
            builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            builder.Append("<workbook xmlns=\"").Append(MainNamespace)
                   .Append("\" xmlns:r=\"").Append(RelationshipNamespace).Append("\"><sheets>");

            for (var i = 0; i < sheets.Count; i++)
            {
                builder.Append("<sheet name=\"").Append(Escape(SafeSheetName(sheets[i].Name)))
                       .Append("\" sheetId=\"").Append(i + 1)
                       .Append("\" r:id=\"rId").Append(i + 1).Append("\"/>");
            }

            builder.Append("</sheets></workbook>");
            return builder.ToString();
        }

        private static string WorkbookRelationships(int sheetCount)
        {
            var builder = new StringBuilder();
            builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            builder.Append("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");

            for (var i = 1; i <= sheetCount; i++)
            {
                builder.Append("<Relationship Id=\"rId").Append(i)
                       .Append("\" Type=\"").Append(RelationshipNamespace)
                       .Append("/worksheet\" Target=\"worksheets/sheet").Append(i).Append(".xml\"/>");
            }

            builder.Append("<Relationship Id=\"rId").Append(sheetCount + 1)
                   .Append("\" Type=\"").Append(RelationshipNamespace)
                   .Append("/styles\" Target=\"styles.xml\"/>");

            builder.Append("</Relationships>");
            return builder.ToString();
        }

        /// <summary>Стили: 0 — обычный, 1 — дата (ДД.ММ.ГГГГ), 2 — деньги, 3 — жирная шапка.</summary>
        private static string Styles()
        {
            return
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<styleSheet xmlns=\"" + MainNamespace + "\">" +
                "<numFmts count=\"1\"><numFmt numFmtId=\"164\" formatCode=\"DD\\.MM\\.YYYY\"/></numFmts>" +
                "<fonts count=\"2\">" +
                "<font><sz val=\"11\"/><name val=\"Calibri\"/></font>" +
                "<font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font>" +
                "</fonts>" +
                "<fills count=\"2\">" +
                "<fill><patternFill patternType=\"none\"/></fill>" +
                "<fill><patternFill patternType=\"gray125\"/></fill>" +
                "</fills>" +
                "<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>" +
                "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
                "<cellXfs count=\"4\">" +
                "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +
                "<xf numFmtId=\"164\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>" +
                "<xf numFmtId=\"4\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>" +
                "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/>" +
                "</cellXfs>" +
                // без cellStyles Excel считает книгу без стиля по умолчанию и предлагает «восстановить»
                "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>" +
                "</styleSheet>";
        }

        private static string Sheet(IList<string> headers, IEnumerable<IList<XlsxCell>> rows)
        {
            var builder = new StringBuilder();
            builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            builder.Append("<worksheet xmlns=\"").Append(MainNamespace).Append("\">");

            // шапка закрепляется, иначе при прокрутке не видно, что за столбец
            builder.Append("<sheetViews><sheetView workbookViewId=\"0\">");
            builder.Append("<pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/>");
            builder.Append("</sheetView></sheetViews>");

            builder.Append("<cols>");
            for (var i = 0; i < headers.Count; i++)
            {
                var width = Math.Min(46, Math.Max(12, (headers[i] ?? string.Empty).Length + 4));
                builder.Append("<col min=\"").Append(i + 1)
                       .Append("\" max=\"").Append(i + 1)
                       .Append("\" width=\"").Append(width.ToString(CultureInfo.InvariantCulture))
                       .Append("\" customWidth=\"1\"/>");
            }
            builder.Append("</cols>");

            builder.Append("<sheetData>");

            var rowNumber = 1;
            builder.Append("<row r=\"1\">");
            for (var i = 0; i < headers.Count; i++)
                AppendCell(builder, i, rowNumber, XlsxCell.Text(headers[i]), 3);
            builder.Append("</row>");

            foreach (var row in rows)
            {
                rowNumber++;
                builder.Append("<row r=\"").Append(rowNumber).Append("\">");
                for (var i = 0; i < row.Count; i++)
                    AppendCell(builder, i, rowNumber, row[i], null);
                builder.Append("</row>");
            }

            builder.Append("</sheetData></worksheet>");
            return builder.ToString();
        }

        private static void AppendCell(StringBuilder builder, int columnIndex, int rowNumber, XlsxCell cell, int? styleOverride)
        {
            if (cell == null || cell.Kind == XlsxCellKind.Empty)
                return;

            var reference = ColumnName(columnIndex) + rowNumber.ToString(CultureInfo.InvariantCulture);
            var style = styleOverride ?? StyleFor(cell.Kind);

            builder.Append("<c r=\"").Append(reference).Append("\"");

            if (style != 0)
                builder.Append(" s=\"").Append(style).Append("\"");

            if (cell.Kind == XlsxCellKind.Text)
            {
                builder.Append(" t=\"inlineStr\"><is><t xml:space=\"preserve\">")
                       .Append(Escape(cell.Value))
                       .Append("</t></is></c>");
            }
            else
            {
                builder.Append("><v>").Append(cell.Value).Append("</v></c>");
            }
        }

        private static int StyleFor(XlsxCellKind kind)
        {
            switch (kind)
            {
                case XlsxCellKind.Date: return 1;
                case XlsxCellKind.Money: return 2;
                default: return 0;
            }
        }

        /// <summary>A, B, ... Z, AA, AB…</summary>
        public static string ColumnName(int columnIndex)
        {
            var name = string.Empty;
            var index = columnIndex;

            while (index >= 0)
            {
                name = (char)('A' + index % 26) + name;
                index = index / 26 - 1;
            }

            return name;
        }

        /// <summary>Excel не принимает в имени листа : \ / ? * [ ] и больше 31 символа.</summary>
        private static string SafeSheetName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "Лист1";

            var cleaned = new string(name.Where(c => ":\\/?*[]".IndexOf(c) < 0).ToArray()).Trim();
            if (cleaned.Length == 0)
                return "Лист1";

            return cleaned.Length > 31 ? cleaned.Substring(0, 31) : cleaned;
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var builder = new StringBuilder(value.Length + 16);
            foreach (var c in value)
            {
                switch (c)
                {
                    case '&': builder.Append("&amp;"); break;
                    case '<': builder.Append("&lt;"); break;
                    case '>': builder.Append("&gt;"); break;
                    case '"': builder.Append("&quot;"); break;
                    case '\'': builder.Append("&apos;"); break;
                    default:
                        // управляющие символы XML не переваривает, а в выгрузках они встречаются
                        if (c >= 0x20 || c == '\t' || c == '\n' || c == '\r')
                            builder.Append(c);
                        break;
                }
            }

            return builder.ToString();
        }
    }
}
