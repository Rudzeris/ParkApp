using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ParkApp.components.Domain;

namespace ParkApp.components.Application
{
    public interface IDispatchRepository
    {
        /// <summary>Строки наряда на дату.</summary>
        Task<IReadOnlyList<DispatchEntry>> GetByDateAsync(DateTime date);

        /// <summary>Заменяет наряд на дату целиком: наряд на день переписывается, а не дополняется.</summary>
        Task SaveOrderAsync(DateTime date, IReadOnlyList<DispatchEntry> entries);

        /// <summary>Постоянные настройки машин.</summary>
        Task<IReadOnlyList<CarSchedule>> GetSchedulesAsync();

        Task SaveSchedulesAsync(IReadOnlyList<CarSchedule> schedules);

        /// <summary>Реквизиты для печати: наименование части, подписанты.</summary>
        Task<IDictionary<string, string>> GetPrintDetailsAsync();
    }
}
