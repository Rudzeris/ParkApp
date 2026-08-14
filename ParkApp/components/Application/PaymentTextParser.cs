using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ParkApp.components.Application
{
    /// <summary>
    /// Графа «оплата, чек, дата» — свободный текст вида
    /// «Оплата 19.05.2026 ИД платежа 952951978536EGLG».
    ///
    /// Из него вытаскиваются дата и идентификатор платежа, чтобы можно было
    /// отбирать неоплаченные и показывать дату. Исходная строка при этом
    /// сохраняется: пока пользователь не менял оплату, в книгу возвращается
    /// его собственная формулировка.
    /// </summary>
    public static class PaymentTextParser
    {
        private static readonly Regex DatePattern =
            new Regex(@"\b(\d{1,2})[.\-/](\d{1,2})[.\-/](\d{2,4})\b", RegexOptions.Compiled);

        /// <summary>«ИД платежа 9529…», «чек 1234», «№ 1234».</summary>
        private static readonly Regex LabeledReference =
            new Regex(@"(?:ид\s*платежа|идентификатор\s*платежа|чек|квитанция|№)\s*[:№]?\s*([A-Za-zА-Яа-я0-9\-]+)",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>Длинный «код» без подписи — тоже похоже на идентификатор платежа.</summary>
        private static readonly Regex BareReference =
            new Regex(@"\b(?=[A-Za-z0-9\-]{8,})[A-Za-z0-9\-]*\d[A-Za-z0-9\-]*\b", RegexOptions.Compiled);

        public static DateTime? ParseDate(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var match = DatePattern.Match(text);
            if (!match.Success)
                return null;

            int day, month, year;
            if (!int.TryParse(match.Groups[1].Value, out day)
                || !int.TryParse(match.Groups[2].Value, out month)
                || !int.TryParse(match.Groups[3].Value, out year))
                return null;

            if (year < 100)
                year += 2000;

            try
            {
                return new DateTime(year, month, day);
            }
            catch (ArgumentOutOfRangeException)
            {
                // «32.13.2026» — в книге бывает и такое, но датой это не сделать
                return null;
            }
        }

        public static string ParseReference(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var labeled = LabeledReference.Match(text);
            if (labeled.Success)
                return labeled.Groups[1].Value;

            // отбрасываем дату, чтобы не принять её за номер платежа
            var withoutDate = DatePattern.Replace(text, " ");

            var bare = BareReference.Match(withoutDate);
            return bare.Success ? bare.Value : null;
        }

        /// <summary>
        /// Собирает графу оплаты в том же виде, в каком её пишут в книге.
        /// Пустая строка означает «не оплачен».
        /// </summary>
        public static string Format(bool isPaid, DateTime? paidDate, string reference)
        {
            if (!isPaid)
                return null;

            var text = "Оплата";

            if (paidDate.HasValue)
                text += " " + paidDate.Value.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

            if (!string.IsNullOrWhiteSpace(reference))
                text += " ИД платежа " + reference.Trim();

            return text;
        }

        /// <summary>
        /// Изменил ли пользователь оплату. Если нет — в книгу возвращается
        /// исходная строка, без переписывания чужой формулировки.
        /// </summary>
        public static bool SameAs(string text, bool isPaid, DateTime? paidDate, string reference)
        {
            var wasPaid = !string.IsNullOrWhiteSpace(text);
            if (wasPaid != isPaid)
                return false;

            if (!wasPaid)
                return true;

            var sameDate = Nullable.Equals(ParseDate(text), paidDate);
            var sameReference = string.Equals(
                (ParseReference(text) ?? string.Empty).Trim(),
                (reference ?? string.Empty).Trim(),
                StringComparison.CurrentCultureIgnoreCase);

            return sameDate && sameReference;
        }
    }
}
