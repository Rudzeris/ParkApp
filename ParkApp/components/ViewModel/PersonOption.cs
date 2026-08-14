using ParkApp.components.Application;
using ParkApp.components.Domain;

namespace ParkApp.components.ViewModel
{
    /// <summary>
    /// Человек в выпадающем списке: номер для связи, строка — для человека.
    /// </summary>
    public class PersonOption
    {
        public PersonOption(int? id, string display)
        {
            Id = id;
            Display = display;
        }

        /// <summary>Номер из таблицы людей. null — «не выбран» / «все».</summary>
        public int? Id { get; private set; }

        public string Display { get; private set; }

        public static PersonOption ForPerson(Person person)
        {
            return new PersonOption(person.Id, Describe(person));
        }

        /// <summary>«сержант Петров Пётр Петрович · Водитель»</summary>
        public static string Describe(Person person)
        {
            if (person == null)
                return "не найден";

            var name = PersonService.Describe(person) ?? "без ФИО";

            return string.IsNullOrWhiteSpace(person.Position)
                ? name
                : string.Format("{0} · {1}", name, person.Position.Trim());
        }
    }
}
