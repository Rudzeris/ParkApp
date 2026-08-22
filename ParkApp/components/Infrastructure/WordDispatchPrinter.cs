using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ParkApp.components.Application;
using ParkApp.components.Infrastructure.Word;

namespace ParkApp.components.Infrastructure
{
    /// <summary>
    /// Печать наряда и путевых листов по шаблонам из папки Templates.
    /// Шаблоны — это присланные формы с плейсхолдерами {{…}}: их правят в Word,
    /// не трогая приложение.
    /// </summary>
    public class WordDispatchPrinter : IDispatchPrinter
    {
        public const string OrderTemplateName = "Наряд.docx";
        public const string WaybillTemplateName = "Путевой лист.docx";
        public const string WaybillBackTemplateName = "Путевой лист (оборот).docx";

        public string PrintOrder(DispatchPlan plan)
        {
            if (plan == null)
                throw new ArgumentNullException("plan");

            var items = plan.Items.Where(i => i.IsSelected).ToList();
            if (items.Count == 0)
                throw new InvalidOperationException("В наряде нет ни одной машины.");

            var template = WordTemplate.Open(Path.Combine(AppPaths.TemplatesFolder, OrderTemplateName));

            template.Fill(new Dictionary<string, string>
            {
                { "НОМЕР", plan.OrderNumber },
                { "НАЗНАЧЕНИЕ", Detail(plan, PrintDetail.OrderPurpose) },
                { "ДЕНЬ", RussianDate.Day(plan.Date) },
                { "МЕСЯЦ", RussianDate.Month(plan.Date) },
                { "ГОД", RussianDate.Year(plan.Date) },
                { "СОГЛ_ДОЛЖНОСТЬ", Detail(plan, PrintDetail.AgreedPosition) },
                { "СОГЛ_ЗВАНИЕ", Detail(plan, PrintDetail.AgreedRank) },
                { "СОГЛ_ФИО", Detail(plan, PrintDetail.AgreedName) },
                { "УТВ_ДОЛЖНОСТЬ", Detail(plan, PrintDetail.ApprovedPosition) },
                { "УТВ_ЗВАНИЕ", Detail(plan, PrintDetail.ApprovedRank) },
                { "УТВ_ФИО", Detail(plan, PrintDetail.ApprovedName) },
                { "ПОДПИСАНТ_ДОЛЖНОСТЬ", Detail(plan, PrintDetail.SignedPosition) },
                { "ПОДПИСАНТ_ЗВАНИЕ", Detail(plan, PrintDetail.SignedRank) },
                { "ПОДПИСАНТ_ФИО", Detail(plan, PrintDetail.SignedName) }
            });

            template.ExpandRows("{{ГРУППА}}", "{{МАРКА}}", BuildRows(items));

            var path = Path.Combine(AppPaths.DispatchDocumentsFolder,
                string.Format("Наряд {0}.docx", plan.Date.ToString("dd.MM.yyyy")));

            template.Save(path);
            return path;
        }

        public IReadOnlyList<string> PrintWaybills(DispatchPlan plan, IList<DispatchPlanItem> items, bool outsideOrder)
        {
            if (plan == null)
                throw new ArgumentNullException("plan");

            if (items == null || items.Count == 0)
                throw new InvalidOperationException("Не выбрано ни одной машины для путевого листа.");

            var pages = items.Select(item => WaybillPage(plan, item, outsideOrder)).ToList();

            var suffix = outsideOrder ? " (вне наряда)" : string.Empty;

            var front = Path.Combine(AppPaths.DispatchDocumentsFolder,
                string.Format("Путевые листы {0}{1} (лицевая).docx", plan.Date.ToString("dd.MM.yyyy"), suffix));

            var back = Path.Combine(AppPaths.DispatchDocumentsFolder,
                string.Format("Путевые листы {0}{1} (оборот).docx", plan.Date.ToString("dd.MM.yyyy"), suffix));

            WordTemplate.SavePages(Path.Combine(AppPaths.TemplatesFolder, WaybillTemplateName), front, pages);

            // оборот заполнять нечем — нужно ровно столько же страниц, сколько лицевых
            var backPages = pages
                .Select(p => new TemplatePage(new Dictionary<string, string>(), new Dictionary<string, bool>()))
                .ToList();

            WordTemplate.SavePages(Path.Combine(AppPaths.TemplatesFolder, WaybillBackTemplateName), back, backPages);

            return new List<string> { front, back };
        }

        /// <summary>Строки таблицы наряда: заголовок группы, затем её машины.</summary>
        private static IList<TemplateRow> BuildRows(IList<DispatchPlanItem> items)
        {
            var rows = new List<TemplateRow>();
            var number = 0;

            var groups = DispatchGroups.InOrder(
                items.GroupBy(i => (i.GroupName ?? string.Empty).Trim()));

            foreach (var group in groups)
            {
                if (group.Key.Length > 0)
                {
                    rows.Add(new TemplateRow(true, new Dictionary<string, string>
                    {
                        { "ГРУППА", group.Key }
                    }));
                }

                foreach (var item in group)
                {
                    number++;

                    rows.Add(new TemplateRow(false, new Dictionary<string, string>
                    {
                        { "НОМЕР_СТРОКИ", number.ToString(CultureInfo.InvariantCulture) },
                        { "МАРКА", item.Brand },
                        { "ГРЗ", item.Plate },
                        { "ГРУППА_ЭКСПЛУАТАЦИИ", item.OperationGroup },
                        { "ЦЕЛЬ", item.Purpose },
                        { "МАРШРУТ", item.Route },
                        { "РАСПОРЯЖЕНИЕ", item.Assignment },
                        { "ВЫЕЗД_ДАТА", RussianDate.ShortDate(item.DepartureAt) },
                        { "ВЫЕЗД_ВРЕМЯ", RussianDate.Time(item.DepartureAt) },
                        { "ВОЗВРАТ_ДАТА", RussianDate.ShortDate(item.ReturnAt) },
                        { "ВОЗВРАТ_ВРЕМЯ", RussianDate.Time(item.ReturnAt) },
                        { "ПРИМЕЧАНИЕ", item.Notes }
                    }));
                }
            }

            return rows;
        }

        private static TemplatePage WaybillPage(DispatchPlan plan, DispatchPlanItem item, bool outsideOrder)
        {
            var values = new Dictionary<string, string>
            {
                { "НОМЕР", string.Empty },
                { "ДЕНЬ", RussianDate.Day(plan.Date) },
                { "МЕСЯЦ", RussianDate.Month(plan.Date) },
                { "ГОД", RussianDate.Year(plan.Date) },
                { "ЧАСТЬ", Detail(plan, PrintDetail.UnitName) },
                { "ДЕЙСТВ_ДЕНЬ", RussianDate.Day(item.ReturnAt) },
                { "ДЕЙСТВ_МЕСЯЦ", RussianDate.Month(item.ReturnAt) },
                { "ДЕЙСТВ_ГОД", RussianDate.Year(item.ReturnAt) },
                { "ИНСТРУКТАЖ_ДОЛЖНОСТЬ", Detail(plan, PrintDetail.BriefingPosition) },
                { "ИНСТРУКТАЖ_ЗВАНИЕ", Detail(plan, PrintDetail.BriefingRank) },
                { "ИНСТРУКТАЖ_ФИО", Detail(plan, PrintDetail.BriefingName) },
                { "ОТВЕТСТВЕННЫЙ_ДОЛЖНОСТЬ", Detail(plan, PrintDetail.ResponsiblePosition) },
                { "ОТВЕТСТВЕННЫЙ_ЗВАНИЕ", Detail(plan, PrintDetail.ResponsibleRank) },
                { "ОТВЕТСТВЕННЫЙ_ФИО", Detail(plan, PrintDetail.ResponsibleName) },
                { "МАРШРУТ", item.Route },
                { "УБЫТИЕ_ДАТА", RussianDate.ShortDate(item.DepartureAt) },
                { "УБЫТИЕ_ВРЕМЯ", RussianDate.Time(item.DepartureAt) },
                { "ПРИБЫТИЕ_ДАТА", RussianDate.ShortDate(item.ReturnAt) },
                { "ПРИБЫТИЕ_ВРЕМЯ", RussianDate.Time(item.ReturnAt) },
                { "ЦЕЛЬ", item.Purpose },
                { "МАРКА", item.Brand },
                { "ГРЗ", item.Plate },
                { "ГРУППА_ЭКСПЛУАТАЦИИ", item.OperationGroup },
                { "ГРУЗ", Detail(plan, PrintDetail.DefaultCargo) },

                // разрешение сверху листа
                { "РАЗРЕШЕНИЕ_ЗАГОЛОВОК", outsideOrder ? "Использование машины вне наряда" : "Использование машины" },
                { "РАЗРЕШЕНИЕ_С", string.Format("{0} {1}", RussianDate.ShortDate(item.DepartureAt), RussianDate.Time(item.DepartureAt)) },
                { "РАЗРЕШЕНИЕ_ДО", string.Format("{0} {1}", RussianDate.ShortDate(item.ReturnAt), RussianDate.Time(item.ReturnAt)) },
                { "РАЗРЕШЕНИЕ_ДЕНЬ", RussianDate.Day(plan.Date) },
                { "РАЗРЕШЕНИЕ_МЕСЯЦ", RussianDate.Month(plan.Date) },
                { "РАЗРЕШЕНИЕ_ГОД", RussianDate.Year(plan.Date) },
                { "РАЗРЕШИЛ_ДОЛЖНОСТЬ", Detail(plan, PrintDetail.PermissionPosition) },
                { "РАЗРЕШИЛ_ЗВАНИЕ", Detail(plan, PrintDetail.PermissionRank) },
                { "РАЗРЕШИЛ_ФИО", Detail(plan, PrintDetail.PermissionName) }
            };

            // блок печатается либо у машин, которым он положен, либо когда едут вне наряда
            var showPermission = outsideOrder || (item.Schedule != null && item.Schedule.AllowOutsideOrder);

            var blocks = new Dictionary<string, bool> { { "РАЗРЕШЕНИЕ", showPermission } };

            return new TemplatePage(values, blocks);
        }

        private static string Detail(DispatchPlan plan, string key)
        {
            return DispatchService.Detail(plan.Details, key) ?? string.Empty;
        }
    }
}
