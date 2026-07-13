using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ParkApp.components.Application;
using ParkApp.components.Domain;

namespace ParkApp.components.Infrastructure
{
    public class JsonCarRepository : ICarRepository
    {
        private const string FilePath = "cars.json";

        private async Task<List<Car>> LoadAsync()
        {
            if (!File.Exists(FilePath))
                return new List<Car>
                {
                    new Car
                    {
                        Id = Guid.NewGuid(),
                        Model = "Lada",
                        Location = Location.Park, 
                        Numbers = new List<CarNumber>
                        {
                            new CarNumber(){Type = NumberType.Army, Text = "0123AB"},
                            new CarNumber(){Type = NumberType.NoArmy, Text = "A0123BC"},
                        },
                        Year = 2000,
                        Vin = "VIN1",
                    },
                    new Car
                    {
                        Id = Guid.NewGuid(),
                        Model = "BMW",
                        Location = Location.Park, 
                        Numbers = new List<CarNumber>
                        {
                            new CarNumber(){Type = NumberType.Army, Text = "5555CE"},
                            new CarNumber(){Type = NumberType.NoArmy, Text = "A4444EC"},
                        },
                        Year = 2010,
                        Vin = "VIN2",
                    },
                    new Car
                    {
                        Id = Guid.NewGuid(),
                        Model = "Haval",
                        Location = Location.SVO, 
                        Numbers = new List<CarNumber>
                        {
                            new CarNumber(){Type = NumberType.Army, Text = "0001SV"},
                        },
                        Year = 2020,
                        Vin = "VIN3",
                    },
                };

            var json = File.ReadAllText(FilePath);
            return JsonConvert.DeserializeObject<List<Car>>(json);
        }

        private async Task SaveAsync(List<Car> cars)
        {
            var json = JsonConvert.SerializeObject(cars, Formatting.Indented);
            File.WriteAllText(FilePath, json);
        }

        public async Task<IReadOnlyList<Car>> GetAllAsync()
        {
            return await LoadAsync();
        }

        public async Task AddAsync(Car car)
        {
            var cars = await LoadAsync();
            cars.Add(car);
            await SaveAsync(cars);
        }

        public async Task UpdateAsync(Car car)
        {
            var cars = await LoadAsync();
            var index = cars.FindIndex(x => x.Id == car.Id);
            if (index >= 0) cars[index] = car;
            await SaveAsync(cars);
        }

        public async Task DeleteAsync(Guid id)
        {
            var cars = await LoadAsync();
            cars.RemoveAll(c => c.Id == id);
            await SaveAsync(cars);
        }
    }
}