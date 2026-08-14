using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        private readonly CarService _cars;
        private readonly PersonService _people;

        private string _error;

        public CarListViewModel(CarService cars, PersonService people)
        {
            _cars = cars;
            _people = people;

            Cars = new ObservableCollection<CarRowViewModel>();
            Load();
        }

        public ObservableCollection<CarRowViewModel> Cars { get; private set; }

        /// <summary>Текст ошибки чтения таблиц; пусто, если всё прочиталось.</summary>
        public string Error
        {
            get { return _error; }
            private set { SetProperty(ref _error, value); }
        }

        public bool HasError
        {
            get { return !string.IsNullOrEmpty(_error); }
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
                OnPropertyChanged("HasError");
            }
        }

        private async Task LoadAsync()
        {
            var cars = await _cars.GetAllAsync();

            IDictionary<int, Person> peopleById;
            try
            {
                peopleById = await _people.GetByIdAsync();
            }
            catch (Exception ex)
            {
                // справочник людей не критичен: список машин показываем и без него
                peopleById = new Dictionary<int, Person>();
                Error = "Справочник людей не прочитан: " + ex.Message;
                OnPropertyChanged("HasError");
            }

            Cars.Clear();

            foreach (var car in cars)
            {
                Person official = null;
                if (car.OfficialId.HasValue)
                    peopleById.TryGetValue(car.OfficialId.Value, out official);

                Cars.Add(new CarRowViewModel(car, official));
            }
        }
    }
}
