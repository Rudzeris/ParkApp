using System;

namespace ParkApp.components.Domain
{
    /// <summary>
    /// Постоянные настройки машины для наряда: как она обычно ездит.
    /// Заполняется один раз и подставляется в каждый новый наряд.
    /// </summary>
    public class CarSchedule
    {
        /// <summary>VIN машины — ключ. VIN стабильнее ГРЗ, который меняют при перерегистрации.</summary>
        public string CarVin { get; set; }

        /// <summary>ГРЗ — чтобы файл графиков читался человеком.</summary>
        public string CarPlate { get; set; }

        /// <summary>Время выезда, обычно 05:00–07:00.</summary>
        public TimeSpan DepartureTime { get; set; }

        /// <summary>Время возвращения. Если оно не позже выезда — машина возвращается на следующий день.</summary>
        public TimeSpan ReturnTime { get; set; }

        /// <summary>«Повторить»: машина попадает в наряд каждый день сама.</summary>
        public bool RepeatDaily { get; set; }

        /// <summary>Печатать в путевом листе блок «Использование машины … разрешаю».</summary>
        public bool AllowOutsideOrder { get; set; }

        /// <summary>Заголовок группы в наряде: машины идут в нём по группам.</summary>
        public string GroupName { get; set; }

        /// <summary>Группа эксплуатации, обычно «тр.».</summary>
        public string OperationGroup { get; set; }

        /// <summary>Для каких целей назначается машина.</summary>
        public string Purpose { get; set; }

        /// <summary>Маршрут движения.</summary>
        public string Route { get; set; }

        /// <summary>В чьё распоряжение.</summary>
        public string Assignment { get; set; }

        public string Notes { get; set; }

        /// <summary>«с 6 до 6» — сутки, «с 6 до 22» — в тот же день.</summary>
        public bool ReturnsNextDay
        {
            get { return ReturnTime <= DepartureTime; }
        }

        /// <summary>Время выезда на указанную дату наряда.</summary>
        public DateTime DepartureOn(DateTime date)
        {
            return date.Date.Add(DepartureTime);
        }

        /// <summary>Время возвращения; для суточных выездов это следующий день.</summary>
        public DateTime ReturnOn(DateTime date)
        {
            var day = ReturnsNextDay ? date.Date.AddDays(1) : date.Date;
            return day.Add(ReturnTime);
        }
    }
}
