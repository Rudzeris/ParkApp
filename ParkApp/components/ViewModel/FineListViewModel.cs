using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ParkApp.components.Application;
using ParkApp.components.Domain;

namespace ParkApp.components.ViewModel
{
    /// <summary>
    /// Список штрафов с отбором по машине, водителю, периоду и тексту.
    /// </summary>
    public class FineListViewModel : ViewModelBase
    {
        private readonly FineService _fines;
        private readonly CarService _cars;
        private readonly IScanStorage _scans;
        private readonly IFineDialogService _dialogs;
        private readonly IFileDialogService _fileDialogs;

        private List<Car> _allCars = new List<Car>();
        private Dictionary<string, Car> _carsByVin = new Dictionary<string, Car>(StringComparer.OrdinalIgnoreCase);
        private bool _isLoading;
        private bool _reloadRequested;

        private CarOption _selectedCarOption;
        private string _driverFilter;
        private DateTime? _dateFrom;
        private DateTime? _dateTo;
        private string _textFilter;
        private FineRowViewModel _selectedFine;
        private decimal _total;
        private string _status;

        public FineListViewModel(
            FineService fines,
            CarService cars,
            IScanStorage scans,
            IFineDialogService dialogs,
            IFileDialogService fileDialogs)
        {
            _fines = fines;
            _cars = cars;
            _scans = scans;
            _dialogs = dialogs;
            _fileDialogs = fileDialogs;

            Fines = new ObservableCollection<FineRowViewModel>();
            CarOptions = new ObservableCollection<CarOption>();

            AddCommand = new RelayCommand(o => Add());
            EditCommand = new RelayCommand(o => Edit(), o => SelectedFine != null);
            DeleteCommand = new RelayCommand(o => Delete(), o => SelectedFine != null);
            OpenScanCommand = new RelayCommand(o => OpenScan(), o => SelectedFine != null && SelectedFine.HasScan);
            RefreshCommand = new RelayCommand(o => Reload());
            ResetFilterCommand = new RelayCommand(o => ResetFilter());
        }

        public ObservableCollection<FineRowViewModel> Fines { get; private set; }
        public ObservableCollection<CarOption> CarOptions { get; private set; }

        public ICommand AddCommand { get; private set; }
        public ICommand EditCommand { get; private set; }
        public ICommand DeleteCommand { get; private set; }
        public ICommand OpenScanCommand { get; private set; }
        public ICommand RefreshCommand { get; private set; }
        public ICommand ResetFilterCommand { get; private set; }

        public CarOption SelectedCarOption
        {
            get { return _selectedCarOption; }
            set { if (SetProperty(ref _selectedCarOption, value)) Reload(); }
        }

        public string DriverFilter
        {
            get { return _driverFilter; }
            set { if (SetProperty(ref _driverFilter, value)) Reload(); }
        }

        public DateTime? DateFrom
        {
            get { return _dateFrom; }
            set { if (SetProperty(ref _dateFrom, value)) Reload(); }
        }

        public DateTime? DateTo
        {
            get { return _dateTo; }
            set { if (SetProperty(ref _dateTo, value)) Reload(); }
        }

        public string TextFilter
        {
            get { return _textFilter; }
            set { if (SetProperty(ref _textFilter, value)) Reload(); }
        }

        public FineRowViewModel SelectedFine
        {
            get { return _selectedFine; }
            set { SetProperty(ref _selectedFine, value); }
        }

        /// <summary>Итоговая сумма отобранных штрафов.</summary>
        public decimal Total
        {
            get { return _total; }
            private set { SetProperty(ref _total, value); }
        }

        public string Status
        {
            get { return _status; }
            private set { SetProperty(ref _status, value); }
        }

        /// <summary>Загружает машины и штрафы. Вызывается после создания окна.</summary>
        public async Task InitializeAsync()
        {
            try
            {
                var cars = await _cars.GetAllAsync();
                _allCars = cars.OrderBy(c => c.Model).ToList();

                _carsByVin = new Dictionary<string, Car>(StringComparer.OrdinalIgnoreCase);
                foreach (var car in _allCars)
                {
                    if (!string.IsNullOrWhiteSpace(car.Vin))
                        _carsByVin[car.Vin.Trim()] = car;
                }

                CarOptions.Clear();
                CarOptions.Add(new CarOption(null, "— все машины —"));
                foreach (var car in _allCars)
                    CarOptions.Add(CarOption.ForCar(car));

                _selectedCarOption = CarOptions[0];
                OnPropertyChanged("SelectedCarOption");

                await ReloadAsync();
            }
            catch (Exception ex)
            {
                _dialogs.ShowError("Не удалось загрузить данные: " + ex.Message);
            }
        }

        private async void Reload()
        {
            await ReloadAsync();
        }

        private async Task ReloadAsync()
        {
            // фильтры меняются на каждое нажатие клавиши: пока идёт загрузка,
            // новые запросы не теряем, а выполняем одним повтором после текущего
            if (_isLoading)
            {
                _reloadRequested = true;
                return;
            }

            _isLoading = true;
            try
            {
                do
                {
                    _reloadRequested = false;
                    await LoadOnceAsync();
                }
                while (_reloadRequested);
            }
            finally
            {
                _isLoading = false;
            }
        }

        private async Task LoadOnceAsync()
        {
            try
            {
                var filter = new FineFilter
                {
                    CarVin = _selectedCarOption != null ? _selectedCarOption.Vin : null,
                    DriverName = DriverFilter,
                    From = DateFrom,
                    To = DateTo,
                    Text = TextFilter
                };

                var found = await _fines.FindAsync(filter);

                var selectedNumber = SelectedFine != null ? SelectedFine.ResolutionNumber : null;

                Fines.Clear();
                foreach (var fine in found)
                {
                    Car car;
                    _carsByVin.TryGetValue((fine.CarVin ?? string.Empty).Trim(), out car);
                    Fines.Add(new FineRowViewModel(fine, car, _scans.Exists(fine.ScanPath)));
                }

                if (selectedNumber != null)
                    SelectedFine = Fines.FirstOrDefault(r => r.ResolutionNumber == selectedNumber);

                Total = _fines.GetTotal(found);
                Status = string.Format("Записей: {0}", found.Count);
            }
            catch (Exception ex)
            {
                _dialogs.ShowError("Не удалось загрузить штрафы: " + ex.Message);
            }
        }

        private void ResetFilter()
        {
            _selectedCarOption = CarOptions.Count > 0 ? CarOptions[0] : null;
            _driverFilter = null;
            _dateFrom = null;
            _dateTo = null;
            _textFilter = null;

            OnPropertyChanged("SelectedCarOption");
            OnPropertyChanged("DriverFilter");
            OnPropertyChanged("DateFrom");
            OnPropertyChanged("DateTo");
            OnPropertyChanged("TextFilter");

            Reload();
        }

        private void Add()
        {
            var fine = new Fine
            {
                ResolutionDate = DateTime.Today,
                ViolationDate = DateTime.Today
            };

            // если список отфильтрован по машине, подставляем её — обычно вносят пачку штрафов на одну машину
            if (_selectedCarOption != null && _selectedCarOption.Vin != null)
                fine.CarVin = _selectedCarOption.Vin;

            ShowEditor(fine, true);
        }

        private void Edit()
        {
            if (SelectedFine == null)
                return;

            // редактор получает копию: отмена не должна оставлять правки в списке
            ShowEditor(Copy(SelectedFine.Fine), false);
        }

        private void ShowEditor(Fine fine, bool isNew)
        {
            var editor = new FineEditViewModel(_fines, _scans, _fileDialogs, _allCars, fine, isNew);
            if (_dialogs.ShowEditor(editor))
                Reload();
        }

        private async void Delete()
        {
            var row = SelectedFine;
            if (row == null)
                return;

            var message = string.Format(
                "Удалить постановление № {0} на сумму {1:N2} ?{2}{3}",
                row.ResolutionNumber,
                row.Amount,
                Environment.NewLine,
                row.HasScan ? "Вложенный скан постановления тоже будет удалён." : string.Empty);

            if (!_dialogs.Confirm(message, "Удаление штрафа"))
                return;

            try
            {
                if (row.HasScan)
                    _scans.Delete(row.Fine.ScanPath);

                await _fines.DeleteAsync(row.ResolutionNumber);
                SelectedFine = null;
                await ReloadAsync();
            }
            catch (Exception ex)
            {
                _dialogs.ShowError("Не удалось удалить штраф: " + ex.Message);
            }
        }

        private void OpenScan()
        {
            var row = SelectedFine;
            if (row == null || !row.HasScan)
                return;

            try
            {
                _scans.Open(row.Fine.ScanPath);
            }
            catch (Exception ex)
            {
                _dialogs.ShowError("Не удалось открыть скан: " + ex.Message);
            }
        }

        private static Fine Copy(Fine source)
        {
            return new Fine
            {
                ResolutionNumber = source.ResolutionNumber,
                ResolutionDate = source.ResolutionDate,
                ViolationDate = source.ViolationDate,
                ViolationPlace = source.ViolationPlace,
                CarVin = source.CarVin,
                DriverName = source.DriverName,
                Amount = source.Amount,
                ScanPath = source.ScanPath
            };
        }
    }
}
