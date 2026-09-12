using System.Collections.Generic;
using System.Linq;

namespace ParkApp.components.Application
{
    /// <summary>Листы, имена которых можно переназначить.</summary>
    public enum SheetKind
    {
        Cars,
        People,
        Fines,
        Affiliation,
        VehicleType,
        DispatchOrders,
        CarSchedules,
        PrintDetails,
        DispatchLists
    }

    public class SheetDescription
    {
        public SheetDescription(SheetKind kind, string title, string defaultName)
        {
            Kind = kind;
            Title = title;
            DefaultName = defaultName;
        }

        public SheetKind Kind { get; private set; }
        public string Title { get; private set; }
        public string DefaultName { get; private set; }
    }

    /// <summary>
    /// Имена листов по умолчанию. Пользователь может назвать листы иначе —
    /// тогда выбранное имя хранится в настройках рабочего места.
    /// </summary>
    public static class SheetCatalog
    {
        private static readonly List<SheetDescription> Items = new List<SheetDescription>
        {
            new SheetDescription(SheetKind.Cars, "Лист машин", "Список всех машин"),
            new SheetDescription(SheetKind.People, "Лист должностных лиц", "Должностные лица"),
            new SheetDescription(SheetKind.Fines, "Лист штрафов", "Штрафы"),
            new SheetDescription(SheetKind.Affiliation, "Лист «Куда относится»", "Куда относится"),
            new SheetDescription(SheetKind.VehicleType, "Лист «Тип машины»", "Тип машины"),
            new SheetDescription(SheetKind.DispatchOrders, "Лист нарядов", "Наряды"),
            new SheetDescription(SheetKind.CarSchedules, "Лист графиков машин", "Графики"),
            new SheetDescription(SheetKind.PrintDetails, "Лист реквизитов для печати", "Реквизиты"),
            new SheetDescription(SheetKind.DispatchLists, "Лист списков для наряда", "Списки")
        };

        public static IReadOnlyList<SheetDescription> All
        {
            get { return Items; }
        }

        public static string DefaultName(SheetKind kind)
        {
            var item = Items.FirstOrDefault(i => i.Kind == kind);
            return item != null ? item.DefaultName : kind.ToString();
        }

        /// <summary>Лист, который ожидается в файле этой таблицы.</summary>
        public static SheetKind ForTable(TableFileKind table)
        {
            switch (table)
            {
                case TableFileKind.People:
                    return SheetKind.People;
                case TableFileKind.Fines:
                    return SheetKind.Fines;
                case TableFileKind.Dispatch:
                    return SheetKind.DispatchOrders;
                default:
                    return SheetKind.Cars;
            }
        }
    }
}
