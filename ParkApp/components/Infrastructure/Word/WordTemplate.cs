using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ParkApp.components.Infrastructure.Word
{
    /// <summary>
    /// Заполнение шаблонов .docx. Как и с xlsx, работаем с файлом напрямую:
    /// docx — это zip с word/document.xml внутри, а нужна только подстановка
    /// значений и размножение строк таблицы.
    ///
    /// Шаблоны лежат в папке Templates и правятся в Word: приложение ищет
    /// в них плейсхолдеры вида {{КЛЮЧ}}. У плейсхолдера может быть заполнитель —
    /// {{КЛЮЧ|_______}}: если значения нет, печатается он, и в бумаге остаётся
    /// линия, по которой допишут от руки. Без заполнителя пустое место просто
    /// остаётся пустым — фигурных скобок в готовом документе не бывает.
    /// </summary>
    public class WordTemplate
    {
        private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        private static readonly XNamespace Xml = "http://www.w3.org/XML/1998/namespace";
        private static readonly Regex Placeholder =
            new Regex(@"\{\{(?<key>[^{}|]*?)(\|(?<fill>[^{}]*))?\}\}", RegexOptions.Compiled);

        private readonly Dictionary<string, byte[]> _parts = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        private XDocument _document;

        private WordTemplate()
        {
        }

        public static WordTemplate Open(string templatePath)
        {
            if (!File.Exists(templatePath))
                throw new FileNotFoundException(string.Format(
                    "Шаблон не найден:{0}{1}{0}{0}Проверьте папку Templates рядом с приложением.",
                    Environment.NewLine, templatePath));

            var template = new WordTemplate();

            try
            {
                using (var stream = new FileStream(templatePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    foreach (var entry in archive.Entries)
                    {
                        using (var entryStream = entry.Open())
                        using (var memory = new MemoryStream())
                        {
                            entryStream.CopyTo(memory);
                            template._parts[entry.FullName] = memory.ToArray();
                        }
                    }
                }
            }
            catch (InvalidDataException ex)
            {
                throw new InvalidOperationException(string.Format(
                    "Файл «{0}» повреждён или это не .docx.", Path.GetFileName(templatePath)), ex);
            }

            byte[] documentPart;
            if (!template._parts.TryGetValue("word/document.xml", out documentPart))
                throw new InvalidOperationException(string.Format(
                    "В файле «{0}» нет word/document.xml — это не документ Word.", Path.GetFileName(templatePath)));

            using (var memory = new MemoryStream(documentPart))
                template._document = XDocument.Load(memory);

            return template;
        }

        private XElement Body
        {
            get
            {
                var body = _document.Root == null ? null : _document.Root.Element(W + "body");
                if (body == null)
                    throw new InvalidOperationException("В шаблоне нет тела документа.");

                return body;
            }
        }

        /// <summary>Подставляет значения по всему документу.</summary>
        public void Fill(IDictionary<string, string> values)
        {
            if (values == null || values.Count == 0)
                return;

            foreach (var paragraph in _document.Descendants(W + "p").ToList())
                FillParagraph(paragraph, values);
        }

        /// <summary>
        /// Размножает строки таблицы. Строка-шаблон опознаётся по плейсхолдеру:
        /// одна для заголовка группы, другая для машины. Порядок строк сохраняется,
        /// поэтому группы и машины идут вперемешку так, как их передали.
        /// </summary>
        public void ExpandRows(string groupMarker, string rowMarker, IList<TemplateRow> rows)
        {
            var groupTemplate = FindRow(groupMarker);
            var rowTemplate = FindRow(rowMarker);

            if (rowTemplate == null)
                throw new InvalidOperationException(string.Format(
                    "В шаблоне не найдена строка таблицы с {0}.", rowMarker));

            var anchor = rowTemplate;

            foreach (var row in rows)
            {
                var source = row.IsGroup ? groupTemplate : rowTemplate;
                if (source == null)
                    continue;

                var copy = new XElement(source);
                foreach (var paragraph in copy.Descendants(W + "p").ToList())
                    FillParagraph(paragraph, row.Values);

                anchor.AddAfterSelf(copy);
                anchor = copy;
            }

            // строки-шаблоны в готовом документе не нужны
            rowTemplate.Remove();
            if (groupTemplate != null)
                groupTemplate.Remove();
        }

        /// <summary>
        /// Оставляет или убирает блок между {{#ИМЯ}} и {{/ИМЯ}}.
        /// Блок «Использование машины вне наряда» печатается не всегда.
        /// </summary>
        public void KeepBlock(string name, bool keep)
        {
            var open = "{{#" + name + "}}";
            var close = "{{/" + name + "}}";

            var paragraphs = _document.Descendants(W + "p").ToList();

            var start = paragraphs.FindIndex(p => ParagraphText(p).Contains(open));
            if (start < 0)
                return;

            var end = paragraphs.FindIndex(start, p => ParagraphText(p).Contains(close));
            if (end < 0)
                end = start;

            if (keep)
            {
                // сами метки в документе не нужны
                for (var i = start; i <= end; i++)
                {
                    RemoveMarker(paragraphs[i], open);
                    RemoveMarker(paragraphs[i], close);
                }

                return;
            }

            for (var i = start; i <= end; i++)
                paragraphs[i].Remove();
        }

        /// <summary>
        /// Собирает документ из нескольких копий шаблона — по одной на машину,
        /// с разрывом страницы между ними.
        /// </summary>
        public static void SavePages(string templatePath, string outputPath, IList<TemplatePage> pages)
        {
            if (pages == null || pages.Count == 0)
                throw new InvalidOperationException("Нечего печатать: не выбрано ни одной машины.");

            var result = Open(templatePath);
            var body = result.Body;

            // последний sectPr описывает страницу и должен остаться в конце
            var sectPr = body.Elements(W + "sectPr").LastOrDefault();
            var pageTemplate = body.Elements().Where(e => e != sectPr).ToList();

            foreach (var element in pageTemplate)
                element.Remove();

            for (var i = 0; i < pages.Count; i++)
            {
                if (i > 0)
                    AddBeforeSectPr(body, sectPr, PageBreak());

                foreach (var element in pageTemplate)
                {
                    var copy = new XElement(element);
                    AddBeforeSectPr(body, sectPr, copy);
                }
            }

            // заполняем уже собранный документ: у каждой копии свои значения
            result.FillPages(pages);
            result.Save(outputPath);
        }

        /// <summary>Записывает готовый документ.</summary>
        public void Save(string outputPath)
        {
            StripPlaceholders();

            AppPaths.EnsureFolderFor(outputPath);

            using (var memory = new MemoryStream())
            {
                _document.Save(memory);
                _parts["word/document.xml"] = memory.ToArray();
            }

            var tempPath = outputPath + ".tmp";

            try
            {
                using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
                {
                    foreach (var part in _parts)
                    {
                        var entry = archive.CreateEntry(part.Key, CompressionLevel.Optimal);
                        using (var entryStream = entry.Open())
                            entryStream.Write(part.Value, 0, part.Value.Length);
                    }
                }

                if (File.Exists(outputPath))
                    File.Delete(outputPath);

                File.Move(tempPath, outputPath);
            }
            catch (IOException ex)
            {
                throw new InvalidOperationException(string.Format(
                    "Не удалось записать «{0}». Скорее всего файл открыт в Word — закройте его и повторите.{1}{2}",
                    Path.GetFileName(outputPath), Environment.NewLine, ex.Message), ex);
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

        /// <summary>
        /// Заполняет копии страниц. Границы копий — разрывы страницы; блок
        /// «Разрешение» занимает несколько абзацев подряд, поэтому его ищем
        /// по всей странице, а не внутри одного элемента.
        /// </summary>
        private void FillPages(IList<TemplatePage> pages)
        {
            foreach (var page in SplitPages().Select((elements, index) => new { elements, index }))
            {
                if (page.index >= pages.Count)
                    break;

                var content = pages[page.index];

                foreach (var name in content.Blocks.Keys)
                    KeepBlockIn(page.elements, name, content.Blocks[name]);

                foreach (var element in page.elements)
                {
                    // абзац мог быть удалён вместе с выключенным блоком
                    if (element.Parent == null)
                        continue;

                    foreach (var paragraph in element.DescendantsAndSelf(W + "p").ToList())
                        FillParagraph(paragraph, content.Values);
                }
            }
        }

        /// <summary>Разбивает тело документа на страницы по разрывам.</summary>
        private List<List<XElement>> SplitPages()
        {
            var pages = new List<List<XElement>>();
            var current = new List<XElement>();

            foreach (var element in Body.Elements().ToList())
            {
                if (IsPageBreak(element))
                {
                    pages.Add(current);
                    current = new List<XElement>();
                    continue;
                }

                if (element.Name == W + "sectPr")
                    continue;

                current.Add(element);
            }

            pages.Add(current);
            return pages;
        }

        /// <summary>Блок в пределах одной страницы: за её границы не выходим.</summary>
        private static void KeepBlockIn(IList<XElement> elements, string name, bool keep)
        {
            var open = "{{#" + name + "}}";
            var close = "{{/" + name + "}}";

            var paragraphs = elements.SelectMany(e => e.DescendantsAndSelf(W + "p")).ToList();

            var start = paragraphs.FindIndex(p => ParagraphText(p).Contains(open));
            if (start < 0)
                return;

            var end = paragraphs.FindIndex(start, p => ParagraphText(p).Contains(close));
            if (end < 0)
                end = start;

            if (keep)
            {
                for (var i = start; i <= end; i++)
                {
                    RemoveMarker(paragraphs[i], open);
                    RemoveMarker(paragraphs[i], close);
                }

                return;
            }

            for (var i = start; i <= end; i++)
                paragraphs[i].Remove();
        }

        private XElement FindRow(string marker)
        {
            return _document.Descendants(W + "tr")
                .FirstOrDefault(tr => tr.Descendants(W + "t").Any(t => (t.Value ?? string.Empty).Contains(marker)));
        }

        private static void FillParagraph(XElement paragraph, IDictionary<string, string> values)
        {
            if (values == null || values.Count == 0)
                return;

            ReplaceInParagraph(paragraph, match =>
            {
                string value;
                if (!values.TryGetValue(match.Groups["key"].Value, out value))
                    return null; // чужой плейсхолдер: его заполнит другой проход

                return Fallback(value, match);
            });
        }

        /// <summary>
        /// Подстановка по всему абзацу с сохранением «run»-ов.
        ///
        /// Word режет текст на десятки кусков, и плейсхолдер обычно лежит сразу
        /// в нескольких. Схлопывать абзац в первый кусок нельзя: в форме на этих
        /// местах стоят подчёркнутые линии для подписей и текст, набранный другим
        /// начертанием, — всё это пропало бы. Поэтому меняется ровно найденный
        /// отрезок, а значение попадает в первый задетый кусок и наследует его
        /// оформление: подпись печатается на линии, а не рядом с ней.
        /// </summary>
        private static void ReplaceInParagraph(XElement paragraph, Func<Match, string> resolve)
        {
            var texts = paragraph.Descendants(W + "t").ToList();
            if (texts.Count == 0)
                return;

            var parts = texts.Select(t => t.Value ?? string.Empty).ToList();
            if (string.Concat(parts).IndexOf("{{", StringComparison.Ordinal) < 0)
                return;

            var changed = false;
            var from = 0;

            while (true)
            {
                var whole = string.Concat(parts);
                if (from > whole.Length)
                    break;

                var match = Placeholder.Match(whole, from);
                if (!match.Success)
                    break;

                var value = resolve(match);
                if (value == null)
                {
                    from = match.Index + match.Length;
                    continue;
                }

                Splice(parts, match.Index, match.Length, value);
                changed = true;
                from = match.Index + value.Length;
            }

            if (!changed)
                return;

            for (var i = 0; i < texts.Count; i++)
                SetText(texts[i], parts[i]);
        }

        /// <summary>Меняет отрезок склеенного текста, не трогая куски вокруг него.</summary>
        private static void Splice(IList<string> parts, int start, int length, string replacement)
        {
            var end = start + length;
            var offset = 0;
            var placed = false;

            for (var i = 0; i < parts.Count; i++)
            {
                var part = parts[i];
                var partStart = offset;
                var partEnd = offset + part.Length;
                offset = partEnd;

                if (partEnd <= start || partStart >= end)
                    continue;

                var head = part.Substring(0, Math.Max(start, partStart) - partStart);
                var tail = part.Substring(Math.Min(end, partEnd) - partStart);

                parts[i] = placed ? head + tail : head + replacement + tail;
                placed = true;
            }
        }

        /// <summary>
        /// Чем печатать пустое значение. В форме на месте номера и дат стоят
        /// прочерки — если приложению нечего подставить, они должны остаться:
        /// пустая строка в бумаге хуже линии, на которой можно дописать.
        /// </summary>
        private static string Fallback(string value, Match match)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value;

            var fill = match.Groups["fill"];
            return fill.Success ? fill.Value : string.Empty;
        }

        /// <summary>Убирает служебную метку блока, не разрушая оформление абзаца.</summary>
        private static void RemoveMarker(XElement paragraph, string marker)
        {
            var texts = paragraph.Descendants(W + "t").ToList();
            if (texts.Count == 0)
                return;

            var parts = texts.Select(t => t.Value ?? string.Empty).ToList();

            var index = string.Concat(parts).IndexOf(marker, StringComparison.Ordinal);
            if (index < 0)
                return;

            Splice(parts, index, marker.Length, string.Empty);

            for (var i = 0; i < texts.Count; i++)
                SetText(texts[i], parts[i]);
        }

        private static string ParagraphText(XElement paragraph)
        {
            return string.Concat(paragraph.Descendants(W + "t").Select(t => t.Value));
        }

        /// <summary>
        /// Плейсхолдеры, которым не нашлось значения, заменяются заполнителем
        /// из шаблона — а если его нет, убираются совсем.
        /// </summary>
        private void StripPlaceholders()
        {
            foreach (var paragraph in _document.Descendants(W + "p").ToList())
                ReplaceInParagraph(paragraph, match => Fallback(string.Empty, match));
        }

        private static void SetText(XElement text, string value)
        {
            text.Value = value ?? string.Empty;

            // без этого Word съест ведущие и хвостовые пробелы
            text.SetAttributeValue(Xml + "space", "preserve");
        }

        private static XElement PageBreak()
        {
            return new XElement(W + "p",
                new XElement(W + "r",
                    new XElement(W + "br", new XAttribute(W + "type", "page"))));
        }

        private static bool IsPageBreak(XElement element)
        {
            return element.Name == W + "p"
                   && element.Descendants(W + "br").Any(br => (string)br.Attribute(W + "type") == "page");
        }

        private static void AddBeforeSectPr(XElement body, XElement sectPr, XElement element)
        {
            if (sectPr != null)
                sectPr.AddBeforeSelf(element);
            else
                body.Add(element);
        }
    }

    /// <summary>Строка таблицы: заголовок группы или машина.</summary>
    public class TemplateRow
    {
        public TemplateRow(bool isGroup, IDictionary<string, string> values)
        {
            IsGroup = isGroup;
            Values = values;
        }

        public bool IsGroup { get; private set; }
        public IDictionary<string, string> Values { get; private set; }
    }

    /// <summary>Одна копия шаблона — например, путевой лист на одну машину.</summary>
    public class TemplatePage
    {
        public TemplatePage(IDictionary<string, string> values, IDictionary<string, bool> blocks)
        {
            Values = values;
            Blocks = blocks ?? new Dictionary<string, bool>();
        }

        public IDictionary<string, string> Values { get; private set; }

        /// <summary>Блоки, которые нужно оставить или убрать: {{#ИМЯ}} … {{/ИМЯ}}.</summary>
        public IDictionary<string, bool> Blocks { get; private set; }
    }
}
