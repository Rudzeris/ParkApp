using System.Collections.Generic;

namespace ParkApp.components.Application
{
    /// <summary>
    /// Печать наряда и путевых листов по шаблонам Word.
    /// Возвращает пути к готовым файлам — открывает их уже вызывающий.
    /// </summary>
    public interface IDispatchPrinter
    {
        string PrintOrder(DispatchPlan plan);

        /// <summary>
        /// Путевые листы: два файла — все лицевые стороны и все обороты
        /// в том же порядке. Так их печатают пачкой на двусторонней печати.
        /// </summary>
        /// <param name="outsideOrder">
        /// Машина едет вне наряда: в разрешении печатается «Использование машины
        /// вне наряда … разрешаю» вместо «Использование машины … разрешаю».
        /// </param>
        IReadOnlyList<string> PrintWaybills(DispatchPlan plan, IList<DispatchPlanItem> items, bool outsideOrder);
    }
}
