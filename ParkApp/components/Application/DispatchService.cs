using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ParkApp.components.Domain;

namespace ParkApp.components.Application
{
    /// <summary>
    /// Наряд на выход техники: план на день, «повторить» и сохранение.
    ///
    /// Правило дня: если наряда на дату ещё нет, в него сами попадают машины
    /// с отметкой «повторять» — именно так ездит техника, выезжающая каждый день.
    /// Если наряд уже составлен, показывается он, а не отметки.
    /// </summary>
    public class DispatchService
    {
        /// <summary>Если у машины нет графика: сутки с шести до шести.</summary>
        public static readonly TimeSpan DefaultDeparture = new TimeSpan(6, 0, 0);
        public static readonly TimeSpan DefaultReturn = new TimeSpan(6, 0, 0);

        private readonly IDispatchRepository _repo;
        private readonly CarService _cars;
        private readonly PersonService _people;

        public DispatchService(IDispatchRepository repo, CarService cars, PersonService people)
        {
            _repo = repo;
            _cars = cars;
            _people = people;
        }

        /// <summary>По умолчанию наряд составляют на завтра.</summary>
        public static DateTime DefaultDate
        {
            get { return DateTime.Today.AddDays(1); }
        }

        public async Task<DispatchPlan> GetPlanAsync(DateTime date)
        {
            var day = date.Date;

            var cars = await _cars.GetAllAsync();
            var officials = await OfficialsAsync();
            var schedules = await _repo.GetSchedulesAsync();
            var entries = await _repo.GetByDateAsync(day);
            var details = await _repo.GetPrintDetailsAsync();
            var choices = await _repo.GetChoicesAsync();

            var schedulesByVin = new Dictionary<string, CarSchedule>(StringComparer.OrdinalIgnoreCase);
            foreach (var schedule in schedules)
            {
                if (!string.IsNullOrWhiteSpace(schedule.CarVin) && !schedulesByVin.ContainsKey(schedule.CarVin))
                    schedulesByVin.Add(schedule.CarVin, schedule);
            }

            var entriesByVin = new Dictionary<string, DispatchEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in entries)
            {
                if (!string.IsNullOrWhiteSpace(entry.CarVin) && !entriesByVin.ContainsKey(entry.CarVin))
                    entriesByVin.Add(entry.CarVin, entry);
            }

            var plan = new DispatchPlan
            {
                Date = day,
                IsNew = entries.Count == 0,
                Details = details,
                Choices = choices,
                OrderNumber = entries.Select(e => e.OrderNumber).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n))
            };

            var defaultOperationGroup = Detail(details, PrintDetail.DefaultOperationGroup);

            foreach (var car in cars)
            {
                var vin = (car.Vin ?? string.Empty).Trim();

                CarSchedule schedule;
                if (!schedulesByVin.TryGetValue(vin, out schedule))
                    schedule = DefaultSchedule(car, defaultOperationGroup);

                DispatchEntry entry;
                var inOrder = entriesByVin.TryGetValue(vin, out entry);

                var item = new DispatchPlanItem
                {
                    Car = car,
                    Schedule = schedule,
                    Plate = FirstPlate(car) ?? schedule.CarPlate,
                    IsSelected = inOrder || (plan.IsNew && schedule.RepeatDaily),

                    // группа — это «куда относится» из карточки машины, её не выбирают
                    GroupName = DispatchGroups.Normalize(car.Affiliation),

                    OperationGroup = inOrder ? entry.OperationGroup : schedule.OperationGroup,
                    Purpose = inOrder ? entry.Purpose : schedule.Purpose,
                    Route = inOrder ? entry.Route : schedule.Route,
                    Assignment = Assignment(car, officials, inOrder ? entry.Assignment : schedule.Assignment),
                    Notes = inOrder ? entry.Notes : schedule.Notes,
                    DepartureAt = inOrder && entry.DepartureAt != default(DateTime)
                        ? entry.DepartureAt
                        : schedule.DepartureOn(day),
                    ReturnAt = inOrder && entry.ReturnAt != default(DateTime)
                        ? entry.ReturnAt
                        : schedule.ReturnOn(day)
                };

                plan.Items.Add(item);
            }

            IncludeUsedValues(plan);

            if (string.IsNullOrWhiteSpace(plan.OrderNumber))
                plan.OrderNumber = await NextOrderNumberAsync(day);

            return plan;
        }

        /// <summary>
        /// Должностные лица по номеру. Реестр людей может быть недоступен —
        /// это не повод не дать составить наряд, просто графа останется пустой.
        /// </summary>
        private async Task<IDictionary<int, Person>> OfficialsAsync()
        {
            if (_people == null)
                return new Dictionary<int, Person>();

            try
            {
                return await _people.GetByIdAsync();
            }
            catch (Exception)
            {
                return new Dictionary<int, Person>();
            }
        }

        /// <summary>
        /// «В чьё распоряжение» — тоже данные машины: её должностное лицо
        /// из таблицы людей, «должность звание Фамилия И.О.». Записанное
        /// в наряде значение важнее: машину могли отдать другому.
        /// </summary>
        private static string Assignment(Car car, IDictionary<int, Person> officials, string stored)
        {
            if (!string.IsNullOrWhiteSpace(stored))
                return stored;

            if (car == null || !car.OfficialId.HasValue)
                return null;

            Person person;
            return officials.TryGetValue(car.OfficialId.Value, out person)
                ? PersonService.DescribeFull(person)
                : null;
        }

        /// <summary>
        /// Дополняет списки тем, что уже стоит у машин. В таблице наряда выбирают
        /// только из списка, поэтому значение, записанное раньше или правкой файла
        /// в Excel, обязано в списке оказаться — иначе выбор его молча потеряет.
        /// </summary>
        private static void IncludeUsedValues(DispatchPlan plan)
        {
            var choices = plan.Choices ?? (plan.Choices = new DispatchChoices());

            choices.Include(DispatchChoices.OperationGroupColumn, plan.Items.Select(i => i.OperationGroup));
            choices.Include(DispatchChoices.PurposeColumn, plan.Items.Select(i => i.Purpose));
            choices.Include(DispatchChoices.RouteColumn, plan.Items.Select(i => i.Route));
            choices.Include(DispatchChoices.AssignmentColumn, plan.Items.Select(i => i.Assignment));
            choices.Include(DispatchChoices.NotesColumn, plan.Items.Select(i => i.Notes));
        }

        /// <summary>
        /// Сохраняет наряд и заодно график машин: время и отметки, выставленные
        /// в окне, становятся постоянными настройками машины.
        /// </summary>
        public async Task SaveAsync(DispatchPlan plan)
        {
            if (plan == null)
                throw new ArgumentNullException("plan");

            var day = plan.Date.Date;

            var entries = plan.Items
                .Where(i => i.IsSelected)
                .Select(i => new DispatchEntry
                {
                    Date = day,
                    OrderNumber = plan.OrderNumber,
                    CarVin = i.Car != null ? i.Car.Vin : null,
                    CarBrand = i.Brand,
                    CarPlate = i.Plate,
                    GroupName = i.GroupName,
                    OperationGroup = i.OperationGroup,
                    Purpose = i.Purpose,
                    Route = i.Route,
                    Assignment = i.Assignment,
                    DepartureAt = i.DepartureAt,
                    ReturnAt = i.ReturnAt,
                    Notes = i.Notes
                })
                .ToList();

            await _repo.SaveOrderAsync(day, entries);
            await SaveSchedulesAsync(plan);
        }

        /// <summary>Переносит настройки из окна в графики машин, не трогая чужие строки.</summary>
        private async Task SaveSchedulesAsync(DispatchPlan plan)
        {
            var existing = await _repo.GetSchedulesAsync();

            var byVin = new Dictionary<string, CarSchedule>(StringComparer.OrdinalIgnoreCase);
            foreach (var schedule in existing)
            {
                if (!string.IsNullOrWhiteSpace(schedule.CarVin))
                    byVin[schedule.CarVin.Trim()] = schedule;
            }

            foreach (var item in plan.Items)
            {
                var vin = item.Car != null ? (item.Car.Vin ?? string.Empty).Trim() : string.Empty;
                if (vin.Length == 0)
                    continue;

                CarSchedule schedule;
                if (!byVin.TryGetValue(vin, out schedule))
                {
                    schedule = new CarSchedule { CarVin = vin };
                    byVin.Add(vin, schedule);
                }

                schedule.CarPlate = item.Plate;
                schedule.OperationGroup = item.OperationGroup;
                schedule.Purpose = item.Purpose;
                schedule.Route = item.Route;
                schedule.Assignment = item.Assignment;
                schedule.Notes = item.Notes;
                schedule.DepartureTime = item.DepartureAt.TimeOfDay;
                schedule.ReturnTime = item.ReturnAt.TimeOfDay;
                schedule.RepeatDaily = item.Schedule.RepeatDaily;
                schedule.AllowOutsideOrder = item.Schedule.AllowOutsideOrder;
            }

            await _repo.SaveSchedulesAsync(byVin.Values.ToList());
        }

        /// <summary>Следующий номер наряда: максимальный числовой плюс один.</summary>
        private async Task<string> NextOrderNumberAsync(DateTime date)
        {
            var maximum = 0;

            for (var day = 1; day <= 62; day++)
            {
                var entries = await _repo.GetByDateAsync(date.AddDays(-day));
                foreach (var entry in entries)
                {
                    int number;
                    if (int.TryParse((entry.OrderNumber ?? string.Empty).Trim(),
                            NumberStyles.Integer, CultureInfo.InvariantCulture, out number) && number > maximum)
                        maximum = number;
                }
            }

            return (maximum + 1).ToString(CultureInfo.InvariantCulture);
        }

        private static CarSchedule DefaultSchedule(Car car, string operationGroup)
        {
            return new CarSchedule
            {
                CarVin = car.Vin,
                CarPlate = FirstPlate(car),
                DepartureTime = DefaultDeparture,
                ReturnTime = DefaultReturn,
                OperationGroup = string.IsNullOrWhiteSpace(operationGroup) ? "тр." : operationGroup
            };
        }

        private static string FirstPlate(Car car)
        {
            if (car == null || car.Numbers == null)
                return null;

            var number = car.Numbers.FirstOrDefault(n => n != null && !string.IsNullOrWhiteSpace(n.Text));
            return number == null ? null : number.Text.Trim();
        }

        public static string Detail(IDictionary<string, string> details, string key)
        {
            if (details == null)
                return null;

            string value;
            return details.TryGetValue(key, out value) ? value : null;
        }
    }
}
