using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using ParkApp.components.Application;
using ParkApp.components.Domain;

namespace ParkApp.components.ViewModel
{
    /// <summary>
    /// Список машин. Ответственный за машину подтягивается из справочника людей —
    /// это то самое «соединение таблиц», ради которого обычно заводят базу данных:
    /// словарь по ключу вместо JOIN.
    /// </summary>
    public class CarListViewModel : ViewModelBase
    {
        /// <summary>Сколько несовпадений со справочниками показывать, чтобы не залить окно.</summary>
        private const int MaxShownProblems = 8;

        private readonly CarService _cars;
        private readonly PersonService _people;
        private readonly LookupService _lookups;

        private string _error;
        private string _warning;

        public CarListViewModel(CarService cars, PersonService people, LookupService lookups)
        {
            _cars = cars;
            _people = people;
            _lookups = lookups;

            Cars = new ObservableCollection<CarRowViewModel>();
            Load();
        }

        public ObservableCollection<CarRowViewModel> Cars { get; private set; }

        /// <summary>Ошибка чтения таблиц; пусто, если всё прочиталось.</summary>
        public string Error
        {
            get { return _error; }
            private set
            {
                if (SetProperty(ref _error, value))
                    OnPropertyChanged("HasError");
            }
        }

        public bool HasError
        {
            get { return !string.IsNullOrEmpty(_error); }
        }

        /// <summary>Значения, которых нет в справочных листах — обычно опечатки.</summary>
        public string Warning
        {
            get { return _warning; }
            private set
            {
                if (SetProperty(ref _warning, value))
                    OnPropertyChanged("HasWarning");
            }
        }

        public bool HasWarning
        {
            get { return !string.IsNullOrEmpty(_warning); }
        }

        /// <summary>Перечитать таблицы — например, после смены путей в настройках.</summary>
        public void Reload()
        {
            Error = null;
            Warning = null;
            Load();
        }

        private async void Load()
        {
            try
            {
                await LoadAsync();
            }
            catch (Exception ex)
            {
                // без перехвата исключение из async void уронило бы приложение молча
                Error = "Не удалось прочитать данные: " + ex.Message;
            }
        }

        private async Task LoadAsync()
        {
            var cars = await _cars.GetAllAsync();
            var peopleById = await LoadPeopleAsync();

            Cars.Clear();

            foreach (var car in cars)
            {
                Person official = null;
                if (car.OfficialId.HasValue)
                    peopleById.TryGetValue(car.OfficialId.Value, out official);

                Cars.Add(new CarRowViewModel(car, official));
            }

            await CheckLookupsAsync(cars);
        }

        private async Task<IDictionary<int, Person>> LoadPeopleAsync()
        {
            try
            {
                return await _people.GetByIdAsync();
            }
            catch (Exception ex)
            {
                // справочник людей не критичен: список машин показываем и без него
                Error = "Справочник людей не прочитан: " + ex.Message;
                return new Dictionary<int, Person>();
            }
        }

        /// <summary>Сверяет «Куда относится» и «Тип машины» со справочными листами.</summary>
        private async Task CheckLookupsAsync(IEnumerable<Car> cars)
        {
            try
            {
                var affiliations = await _lookups.GetAsync(LookupList.Affiliation);
                var vehicleTypes = await _lookups.GetAsync(LookupList.VehicleType);

                var problems = CarService.FindUnknownValues(cars, affiliations, vehicleTypes);
                if (problems.Count == 0)
                {
                    Warning = null;
                    return;
                }

                var shown = problems.Take(MaxShownProblems).ToList();
                var text = "Значения вне справочных списков:" + Environment.NewLine
                           + string.Join(Environment.NewLine, shown);

                if (problems.Count > shown.Count)
                    text += string.Format("{0}…и ещё {1}", Environment.NewLine, problems.Count - shown.Count);

                Warning = text;
            }
            catch (Exception ex)
            {
                Warning = "Справочные списки не прочитаны: " + ex.Message;
            }
        }
    }
}
