using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ParkApp.components.Application
{
    /// <summary>
    /// Поиск по списку с тремя способами записи запроса. Один список, один
    /// способ ввода — но искать приходится по-разному: то нужен ровно этот
    /// номер, то кусок VIN из середины, то марка, набранная с опечаткой.
    ///
    ///   «0123АВ»   — в кавычках: точное совпадение целиком;
    ///   *123*      — со звёздочкой: она означает любые символы, в том числе
    ///                ни одного, поэтому «*123» найдёт и «123», и «АВ123»;
    ///   камаз 43   — без кавычек и звёздочек: похожие слова. Каждое слово
    ///                запроса должно найтись в строке — началом слова, куском
    ///                или с точностью до опечатки.
    ///
    /// Разбор запроса отделён от того, где он применяется: поиск нужен и в
    /// выборе машины, и в списках, а правила должны быть везде одни.
    /// </summary>
    public class SearchQuery
    {
        private enum Kind
        {
            Empty,
            Exact,
            Pattern,
            Similar
        }

        private static readonly char[] WordSeparators =
            { ' ', '\t', '\n', '\r', ',', ';', '·', '/', '\\', '-' };

        private readonly Kind _kind;
        private readonly string _exact;
        private readonly Regex _pattern;
        private readonly List<string> _words;

        private SearchQuery(Kind kind, string exact, Regex pattern, List<string> words)
        {
            _kind = kind;
            _exact = exact;
            _pattern = pattern;
            _words = words ?? new List<string>();
        }

        public static readonly SearchQuery Empty = new SearchQuery(Kind.Empty, null, null, null);

        /// <summary>Пустой запрос ничего не отбирает: показывается весь список.</summary>
        public bool IsEmpty
        {
            get { return _kind == Kind.Empty; }
        }

        public static SearchQuery Parse(string text)
        {
            var trimmed = (text ?? string.Empty).Trim();
            if (trimmed.Length == 0)
                return Empty;

            // «в кавычках» — ровно это значение, без догадок
            if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[trimmed.Length - 1] == '"')
            {
                var exact = trimmed.Substring(1, trimmed.Length - 2).Trim();
                return exact.Length == 0
                    ? Empty
                    : new SearchQuery(Kind.Exact, Normalize(exact), null, null);
            }

            if (trimmed.IndexOf('*') >= 0)
                return new SearchQuery(Kind.Pattern, null, BuildPattern(trimmed), null);

            var words = trimmed
                .Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries)
                .Select(Normalize)
                .Where(w => w.Length > 0)
                .ToList();

            return words.Count == 0 ? Empty : new SearchQuery(Kind.Similar, null, null, words);
        }

        /// <summary>
        /// Подходит ли запись. Поля перечисляются подряд: машину ищут и по
        /// номеру, и по VIN, и по марке, а какое из них человек набрал —
        /// заранее неизвестно.
        /// </summary>
        public bool Matches(params string[] fields)
        {
            if (_kind == Kind.Empty)
                return true;

            if (fields == null || fields.Length == 0)
                return false;

            var values = fields
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .Select(Normalize)
                .ToList();

            if (values.Count == 0)
                return false;

            switch (_kind)
            {
                case Kind.Exact:
                    // совпасть должно поле целиком: «1234» — это не номер
                    // «1234 АБ 34», и выдавать его за точное совпадение нельзя
                    return values.Any(v => v == _exact);

                case Kind.Pattern:
                    return values.Any(v => _pattern.IsMatch(v));

                default:
                    // все слова запроса должны найтись: «камаз 43» — это
                    // «КамАЗ-4310», а не всё подряд, где есть «43»
                    return _words.All(word => values.Any(v => Similar(v, word)));
            }
        }

        /// <summary>Звёздочка — любые символы, в том числе ни одного.</summary>
        private static Regex BuildPattern(string text)
        {
            var builder = new StringBuilder("^");

            foreach (var part in text.Split('*').Select(Normalize))
            {
                if (builder.Length > 1)
                    builder.Append(".*");

                builder.Append(Regex.Escape(part));
            }

            builder.Append("$");

            return new Regex(builder.ToString(), RegexOptions.CultureInvariant);
        }

        private static bool Similar(string value, string word)
        {
            if (value.IndexOf(word, StringComparison.Ordinal) >= 0)
                return true;

            // опечатка прощается только в словах, где её видно: в «уаз»
            // одна перепутанная буква — это уже другое слово
            foreach (var candidate in Words(value))
            {
                if (candidate.StartsWith(word, StringComparison.Ordinal))
                    return true;

                var allowed = word.Length >= 6 ? 2 : word.Length >= 4 ? 1 : 0;
                if (allowed == 0)
                    continue;

                if (Math.Abs(candidate.Length - word.Length) <= allowed
                    && Distance(candidate, word) <= allowed)
                    return true;

                // опечатка в начале длинного слова: «мерседас» → «мерседес-бенц»
                if (candidate.Length > word.Length
                    && Distance(candidate.Substring(0, word.Length), word) <= allowed)
                    return true;
            }

            return false;
        }

        private static IEnumerable<string> Words(string value)
        {
            return value.Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>
        /// Расстояние Левенштейна: сколько правок отделяет одно слово от другого.
        /// Нужно, чтобы «камас» находил «КамАЗ», а «мерседес» — «мерседес-бенц».
        /// </summary>
        private static int Distance(string left, string right)
        {
            var previous = new int[right.Length + 1];
            var current = new int[right.Length + 1];

            for (var j = 0; j <= right.Length; j++)
                previous[j] = j;

            for (var i = 1; i <= left.Length; i++)
            {
                current[0] = i;

                for (var j = 1; j <= right.Length; j++)
                {
                    var cost = left[i - 1] == right[j - 1] ? 0 : 1;

                    current[j] = Math.Min(
                        Math.Min(current[j - 1] + 1, previous[j] + 1),
                        previous[j - 1] + cost);
                }

                Array.Copy(current, previous, current.Length);
            }

            return previous[right.Length];
        }

        /// <summary>
        /// Приведение к сравнимому виду. Регистр не важен, «ё» пишут и «е»,
        /// а в номерах машин кириллические двойники латинских букв выглядят
        /// одинаково — «А123ВС» можно набрать и той, и другой раскладкой.
        /// </summary>
        private static string Normalize(string value)
        {
            const string cyrillic = "АВЕКМНОРСТУХавекмнорстух";
            const string latin = "ABEKMHOPCTYXabekmhopctyx";

            var result = new StringBuilder(value.Length);

            foreach (var symbol in value.ToLowerInvariant())
            {
                var letter = symbol == 'ё' ? 'е' : symbol;

                var index = cyrillic.IndexOf(letter);
                result.Append(index >= 0 ? char.ToLowerInvariant(latin[index]) : letter);
            }

            return result.ToString().Trim();
        }
    }
}
