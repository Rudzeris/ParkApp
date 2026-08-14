using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ParkApp.components.Domain;

namespace ParkApp.components.Application
{
    public class CarService
    {
        private readonly ICarRepository _repo;

        public CarService(ICarRepository repo) => _repo = repo;

        public async Task<IReadOnlyList<Car>> GetByLocationAsync(Location location)
        {
            var all = await _repo.GetAllAsync();
            return all.Where(c => c.Location == location).ToList();
        }

        public Task<IReadOnlyList<Car>> GetAllAsync() => _repo.GetAllAsync();

        /// <summary>
        /// Значения машин, которых нет в справочных списках. Это опечатки в таблице:
        /// «Гаражж» вместо «Гараж» разъедется в фильтрах и отчётах, а глазами в файле не видно.
        /// </summary>
        public static IReadOnlyList<string> FindUnknownValues(
            IEnumerable<Car> cars,
            IReadOnlyList<string> affiliations,
            IReadOnlyList<string> vehicleTypes)
        {
            var problems = new List<string>();

            foreach (var car in cars)
            {
                if (!LookupService.IsAllowed(affiliations, car.Affiliation))
                    problems.Add(string.Format(
                        "«Куда относится»: значение «{0}» (машина {1}) отсутствует в списке",
                        car.Affiliation, car.Vin));

                if (!LookupService.IsAllowed(vehicleTypes, car.VehicleType))
                    problems.Add(string.Format(
                        "«Тип машины»: значение «{0}» (машина {1}) отсутствует в списке",
                        car.VehicleType, car.Vin));
            }

            return problems;
        }
    }
}
