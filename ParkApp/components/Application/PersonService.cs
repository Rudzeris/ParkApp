using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ParkApp.components.Domain;

namespace ParkApp.components.Application
{
    /// <summary>Должностные лица: справочник для подстановки в машины и документы.</summary>
    public class PersonService
    {
        private readonly IPersonRepository _repo;

        public PersonService(IPersonRepository repo) => _repo = repo;

        public async Task<IReadOnlyList<Person>> GetAllAsync()
        {
            var people = await _repo.GetAllAsync();
            return people.OrderBy(p => p.FullName).ToList();
        }

        /// <summary>Люди по их номеру — для соединения с таблицей машин.</summary>
        public async Task<IDictionary<int, Person>> GetByIdAsync()
        {
            var people = await _repo.GetAllAsync();
            var result = new Dictionary<int, Person>();

            foreach (var person in people)
            {
                if (!result.ContainsKey(person.Id))
                    result.Add(person.Id, person);
            }

            return result;
        }

        /// <summary>
        /// «начальник штаба подполковник Иванов И.И.» — как в графе наряда
        /// «в чьё распоряжение»: должность, звание и фамилия одной строкой.
        /// </summary>
        public static string DescribeFull(Person person)
        {
            if (person == null)
                return null;

            var parts = new[] { person.Position, person.Rank, person.FullName }
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .Select(part => part.Trim());

            var text = string.Join(" ", parts);
            return text.Length > 0 ? text : null;
        }

        /// <summary>«мл. сержант Иванов И.И.» — звание и ФИО одной строкой.</summary>
        public static string Describe(Person person)
        {
            if (person == null)
                return null;

            var parts = new[] { person.Rank, person.FullName }
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .Select(part => part.Trim());

            var text = string.Join(" ", parts);
            return text.Length > 0 ? text : null;
        }
    }
}
