using System.Collections.Generic;
using System.Threading.Tasks;
using ParkApp.components.Domain;

namespace ParkApp.components.Application
{
    public interface IPersonRepository
    {
        Task<IReadOnlyList<Person>> GetAllAsync();
    }
}
