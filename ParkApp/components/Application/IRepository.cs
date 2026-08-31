using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ParkApp.components.Domain;

namespace ParkApp.components.Application
{
    public interface IRepository <T>
    {
        Task<IReadOnlyList<T>> GetAllAsync();
        Task AddAsync(T car);
        Task UpdateAsync(T car);
        Task DeleteAsync(Guid id);
    }
}