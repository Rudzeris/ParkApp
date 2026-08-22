using System;
using System.Collections.Generic;
using System.Linq;

namespace ParkApp.components.Application
{
    /// <summary>
    /// Группы машин в наряде. Их две, и порядок в документе — их собственный,
    /// а не алфавитный: «Обеспечение служебной деятельности» идёт первым,
    /// «Дежурные средства» — вторым. По алфавиту вышло бы наоборот, поэтому
    /// порядок задан списком, а не сортировкой.
    ///
    /// Группа — свойство машины, а не решение на день: в таблице машин она
    /// лежит в столбце «Куда относится», и приложение берёт её оттуда, а не
    /// спрашивает заново на каждый наряд. Значения ведутся справочным листом
    /// в файле машин, поэтому список здесь задаёт только порядок печати:
    /// машина с незнакомой группой не теряется — её группа встанет после
    /// известных, в алфавитном порядке.
    /// </summary>
    public static class DispatchGroups
    {
        public const string Service = "Обеспечение служебной деятельности";
        public const string Duty = "Дежурные средства";

        public static readonly string[] Known = { Service, Duty };

        /// <summary>Место группы в наряде: сначала известные по порядку, потом остальные.</summary>
        public static int Order(string name)
        {
            var text = (name ?? string.Empty).Trim();

            for (var i = 0; i < Known.Length; i++)
            {
                if (string.Equals(Known[i], text, StringComparison.CurrentCultureIgnoreCase))
                    return i;
            }

            return Known.Length;
        }

        /// <summary>
        /// Приводит значение к принятому написанию, если оно узнаётся.
        /// В таблице могут написать «дежурные средства» — в наряде это должна
        /// быть та же группа, а не вторая с маленькой буквы.
        /// </summary>
        public static string Normalize(string name)
        {
            var text = (name ?? string.Empty).Trim();
            if (text.Length == 0)
                return null;

            var known = Known.FirstOrDefault(
                k => string.Equals(k, text, StringComparison.CurrentCultureIgnoreCase));

            return known ?? text;
        }

        /// <summary>Группы в порядке печати наряда.</summary>
        public static IEnumerable<IGrouping<string, T>> InOrder<T>(IEnumerable<IGrouping<string, T>> groups)
        {
            return groups
                .OrderBy(g => Order(g.Key))
                .ThenBy(g => g.Key, StringComparer.CurrentCulture);
        }
    }
}
