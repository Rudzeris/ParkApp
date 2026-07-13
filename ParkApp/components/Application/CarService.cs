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
    }

}