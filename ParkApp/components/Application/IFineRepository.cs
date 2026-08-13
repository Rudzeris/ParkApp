using System.Collections.Generic;
using System.Threading.Tasks;
using ParkApp.components.Domain;

namespace ParkApp.components.Application
{
    public interface IFineRepository
    {
        Task<IReadOnlyList<Fine>> GetAllAsync();
        Task<Fine> GetByNumberAsync(string resolutionNumber);
        Task AddAsync(Fine fine);
        Task UpdateAsync(Fine fine);
        Task DeleteAsync(string resolutionNumber);
    }
}
