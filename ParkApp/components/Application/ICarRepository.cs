using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ParkApp.components.Domain;

namespace ParkApp.components.Application
{
    public interface ICarRepository
    {
        Task<IReadOnlyList<Car>> GetAllAsync();
        Task AddAsync(Car car);
        Task UpdateAsync(Car car);
        Task DeleteAsync(Guid id);
    }

}