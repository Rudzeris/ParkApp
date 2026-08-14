using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ParkApp.components.Application
{
    /// <summary>
    /// Справочные списки: значения для «Куда относится» и «Тип машины».
    /// Списки живут в данных, а не в коде — так заказчик правит их сам,
    /// без пересборки приложения.
    /// </summary>
    public class LookupService
    {
        private readonly ILookupRepository _repo;

        public LookupService(ILookupRepository repo) => _repo = repo;

        public Task<IReadOnlyList<string>> GetAsync(LookupList list) => _repo.GetAsync(list);

        /// <summary>
        /// Есть ли значение в списке. Пустой список означает «проверять нечем» —
        /// тогда любое значение считается допустимым.
        /// </summary>
        public static bool IsAllowed(IReadOnlyList<string> allowed, string value)
        {
            if (allowed == null || allowed.Count == 0)
                return true;

            if (string.IsNullOrWhiteSpace(value))
                return true;

            return allowed.Any(item => string.Equals(
                (item ?? string.Empty).Trim(),
                value.Trim(),
                StringComparison.CurrentCultureIgnoreCase));
        }
    }
}
