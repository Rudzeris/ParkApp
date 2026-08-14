using System.Collections.Generic;
using System.Threading.Tasks;

namespace ParkApp.components.Application
{
    public interface ILookupRepository
    {
        /// <summary>Значения списка. Пустой список — листа нет или он не заполнен.</summary>
        Task<IReadOnlyList<string>> GetAsync(LookupList list);
    }
}
