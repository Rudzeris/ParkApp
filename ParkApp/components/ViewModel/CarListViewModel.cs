using System.Collections.ObjectModel;
using System.Linq;
using ParkApp.components.Application;
using ParkApp.components.Domain;

namespace ParkApp.components.ViewModel
{
    public class CarListViewModel : ViewModelBase
    {
        private readonly CarService _service;

        public ObservableCollection<Car> Cars { get; } = new ObservableCollection<Car>();

        public CarListViewModel(CarService service)
        {
            _service = service;
            Load();
        }

        private async void Load()
        {
            var cars = await _service.GetAllAsync();
            Cars.Clear();
            foreach (var car in cars)
                Cars.Add(car);
        }
    }
}