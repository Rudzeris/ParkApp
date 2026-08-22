using System;
using System.Collections.Generic;
using ParkApp.components.Domain;

namespace ParkApp.components.Application
{
    /// <summary>Машина в плане наряда на день: сама машина, её график и решение на этот день.</summary>
    public class DispatchPlanItem
    {
        public Car Car { get; set; }

        /// <summary>Постоянные настройки машины. Есть всегда — для новой машины создаётся по умолчанию.</summary>
        public CarSchedule Schedule { get; set; }

        /// <summary>Машина включена в наряд на эту дату.</summary>
        public bool IsSelected { get; set; }

        public DateTime DepartureAt { get; set; }
        public DateTime ReturnAt { get; set; }

        public string GroupName { get; set; }
        public string OperationGroup { get; set; }
        public string Purpose { get; set; }
        public string Route { get; set; }
        public string Assignment { get; set; }
        public string Notes { get; set; }

        public string Brand
        {
            get { return Car != null ? Car.Model : null; }
        }

        public string Plate { get; set; }
    }

    /// <summary>Наряд на выход техники на одну дату.</summary>
    public class DispatchPlan
    {
        public DispatchPlan()
        {
            Items = new List<DispatchPlanItem>();
        }

        public DateTime Date { get; set; }
        public string OrderNumber { get; set; }

        /// <summary>Наряда на эту дату ещё не было — машины отмечены по признаку «повторять».</summary>
        public bool IsNew { get; set; }

        public IList<DispatchPlanItem> Items { get; set; }

        /// <summary>Реквизиты для печати.</summary>
        public IDictionary<string, string> Details { get; set; }

        /// <summary>Что можно выбрать в таблице наряда.</summary>
        public DispatchChoices Choices { get; set; }
    }
}
