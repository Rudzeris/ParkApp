using System.Collections.Generic;

namespace ParkApp.components.Application
{
    /// <summary>Разделы приложения. Каждый ведёт свой вид документов.</summary>
    public enum AppSection
    {
        Fines,
        Waybills,
        DispatchOrders,
        Insurances,
        Maintenance,
        People
    }

    /// <summary>Раздел: как называется и готов ли он.</summary>
    public class SectionDescription
    {
        public SectionDescription(AppSection section, string title, bool isAvailable)
        {
            Section = section;
            Title = title;
            IsAvailable = isAvailable;
        }

        public AppSection Section { get; private set; }
        public string Title { get; private set; }

        /// <summary>Реализован ли раздел. Нереализованные видны в настройках, но выключены.</summary>
        public bool IsAvailable { get; private set; }
    }

    /// <summary>
    /// Перечень разделов. Держится в одном месте, чтобы новый раздел появлялся
    /// и в настройках, и на панели главного окна одной записью.
    /// </summary>
    public static class SectionCatalog
    {
        private static readonly List<SectionDescription> Sections = new List<SectionDescription>
        {
            new SectionDescription(AppSection.Fines, "Штрафы", true),
            new SectionDescription(AppSection.Waybills, "Путевые листы", false),
            new SectionDescription(AppSection.DispatchOrders, "Наряд на выход", true),
            new SectionDescription(AppSection.Insurances, "Страховки", false),
            new SectionDescription(AppSection.Maintenance, "ТО и акты Ф-12", false),
            new SectionDescription(AppSection.People, "Люди", false)
        };

        public static IReadOnlyList<SectionDescription> All
        {
            get { return Sections; }
        }
    }
}
