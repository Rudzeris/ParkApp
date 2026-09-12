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
    /// Книга постановлений: отбор по машине, правонарушителю, периоду, оплате и тексту.
    /// </summary>
    public class FineListViewModel : ViewModelBase
    {
        private readonly FineService _fines;
        private readonly CarService _cars;
        private readonly IScanStorage _scans;
        private readonly IFineDialogService _dialogs;
        private readonly IFileDialogService _fileDialogs;

        private List<Car> _allCars = new List<Car>();
        private Dictionary<string, Car> _carsByPlate = new Dictionary<string, Car>(StringComparer.OrdinalIgnoreCase);
        private bool _isLoading;
        private bool _reloadRequested;

        private CarOption _selectedCarOption;
        private string _offenderFilter;
        private DateTime? _dateFrom;
        private DateTime? _dateTo;
        private string _textFilter;
        private PaymentFilterOption _selectedPaymentOption;
        private FineRowViewModel _selectedFine;
        private decimal _total;
        private decimal _unpaidTotal;
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

            PaymentOptions = new ObservableCollection<PaymentFilterOption>
            {
                new PaymentFilterOption("— все —", null),
                new PaymentFilterOption("не оплаченные", false),
                new PaymentFilterOption("оплаченные", true)
            };
            _selectedPaymentOption = PaymentOptions[0];

            AddCommand = new RelayCommand(o => Add());
            EditCommand = new RelayCommand(o => Edit(), o => SelectedFine != null);
            DeleteCommand = new RelayCommand(o => Delete(), o => SelectedFine != null);
            OpenScanCommand = new RelayCommand(o => OpenScan(), o => SelectedFine != null && SelectedFine.HasScan);
            RefreshCommand = new RelayCommand(o => Reload());
            ResetFilterCommand = new RelayCommand(o => ResetFilter());
        }

        public ObservableCollection<FineRowViewModel> Fines { get; private set; }
        public ObservableCollection<CarOption> CarOptions { get; private set; }
        public ObservableCollection<PaymentFilterOption> PaymentOptions { get; private set; }

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

        /// <summary>Часть Ф.И.О. правонарушителя.</summary>
        public string OffenderFilter
        {
            get { return _offenderFilter; }
            set { if (SetProperty(ref _offenderFilter, value)) Reload(); }
        }

        public PaymentFilterOption SelectedPaymentOption
        {
            get { return _selectedPaymentOption; }
            set { if (SetProperty(ref _selectedPaymentOption, value)) Reload(); }
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

        /// <summary>Сколько из отобранного ещё не оплачено.</summary>
        public decimal UnpaidTotal
        {
            get { return _unpaidTotal; }
            private set { SetProperty(ref _unpaidTotal, value); }
        }

        public string Status
        {
            get { return _status; }
            private set { SetProperty(ref _status, value); }
        }

        /// <summary>Загружает машины и книгу. Вызывается после создания окна.</summary>
        public async Task InitializeAsync()
        {
            try
            {
                await LoadCarsAsync();
                await ReloadAsync();
            }
            catch (Exception ex)
            {
                _dialogs.ShowError("Не удалось загрузить данные: " + ex.Message);
            }
        }

        /// <summary>
        /// Реестр машин нужен только для подсказки по ГРЗ: книгу можно вести
        /// и без него, поэтому его недоступность не мешает открыть окно.
        /// </summary>
        private async Task LoadCarsAsync()
        {
            try
            {
                var cars = await _cars.GetAllAsync();
                _allCars = cars.OrderBy(CarOption.ModelText).ToList();
            }
            catch (Exception ex)
            {
                _allCars = new List<Car>();
                _dialogs.ShowError("Реестр машин не прочитан, подсказки по ГРЗ недоступны: " + ex.Message);
            }

            _carsByPlate = new Dictionary<string, Car>(StringComparer.OrdinalIgnoreCase);
            foreach (var car in _allCars)
            {
                foreach (var number in car.Numbers ?? new List<CarNumber>())
                {
                    if (number == null || string.IsNullOrWhiteSpace(number.Text))
                        continue;

                    var key = FineService.NormalizePlate(number.Text);
                    if (key.Length > 0 && !_carsByPlate.ContainsKey(key))
                        _carsByPlate.Add(key, car);
                }
            }

            CarOptions.Clear();
            CarOptions.Add(new CarOption(null, "— все машины —"));
            foreach (var car in _allCars)
                CarOptions.Add(CarOption.ForCar(car));

            _selectedCarOption = CarOptions[0];
            OnPropertyChanged("SelectedCarOption");
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
                    CarPlate = _selectedCarOption != null ? _selectedCarOption.Plate : null,
                    OffenderName = OffenderFilter,
                    From = DateFrom,
                    To = DateTo,
                    Text = TextFilter,
                    IsPaid = _selectedPaymentOption != null ? _selectedPaymentOption.IsPaid : null
                };

                var found = await _fines.FindAsync(filter);

                var selectedNumber = SelectedFine != null ? SelectedFine.ResolutionNumber : null;

                Fines.Clear();
                foreach (var fine in found)
                {
                    Car car;
                    _carsByPlate.TryGetValue(FineService.NormalizePlate(fine.CarPlate), out car);

                    Fines.Add(new FineRowViewModel(fine, car, _scans.Exists(fine.ScanPath)));
                }

                if (selectedNumber != null)
                    SelectedFine = Fines.FirstOrDefault(r => r.ResolutionNumber == selectedNumber);

                Total = _fines.GetTotal(found);
                UnpaidTotal = _fines.GetUnpaidTotal(found);
                Status = string.Format("Записей: {0}", found.Count);
            }
            catch (Exception ex)
            {
                _dialogs.ShowError("Не удалось прочитать книгу постановлений: " + ex.Message);
            }
        }

        private void ResetFilter()
        {
            _selectedCarOption = CarOptions.Count > 0 ? CarOptions[0] : null;
            _selectedPaymentOption = PaymentOptions.Count > 0 ? PaymentOptions[0] : null;
            _offenderFilter = null;
            _dateFrom = null;
            _dateTo = null;
            _textFilter = null;

            OnPropertyChanged("SelectedCarOption");
            OnPropertyChanged("SelectedPaymentOption");
            OnPropertyChanged("OffenderFilter");
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

            // если список отфильтрован по машине, подставляем её: обычно вносят пачку на одну машину
            if (_selectedCarOption != null && _selectedCarOption.Plate != null)
            {
                fine.CarPlate = _selectedCarOption.Plate;

                Car car;
                if (_carsByPlate.TryGetValue(FineService.NormalizePlate(fine.CarPlate), out car))
                    fine.CarBrand = car.Model;
            }

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

            // редактор открывается вкладкой и живёт сам по себе; список
            // перечитывается, когда запись сохранили
            editor.RequestClose += (sender, saved) =>
            {
                if (saved)
                    Reload();
            };

            _dialogs.OpenEditor(editor);
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

            if (!_dialogs.Confirm(message, "Удаление записи"))
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
                _dialogs.ShowError("Не удалось удалить запись: " + ex.Message);
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
                RowNumber = source.RowNumber,
                ResolutionNumber = source.ResolutionNumber,
                ResolutionDate = source.ResolutionDate,
                ViolationDate = source.ViolationDate,
                OffenderName = source.OffenderName,
                CarBrand = source.CarBrand,
                CarPlate = source.CarPlate,
                Amount = source.Amount,
                PaymentText = source.PaymentText,
                PaidDate = source.PaidDate,
                PaymentReference = source.PaymentReference,
                Notes = source.Notes,
                ScanPath = source.ScanPath
            };
        }
    }
}
