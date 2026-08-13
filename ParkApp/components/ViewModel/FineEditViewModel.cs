using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ParkApp.components.Application;
using ParkApp.components.Domain;

namespace ParkApp.components.ViewModel
{
    /// <summary>
    /// Редактор штрафа. Ошибки показываются списком при сохранении:
    /// часть правил межполевые (дата нарушения ≤ даты постановления),
    /// поштучно их не проверить.
    /// </summary>
    public class FineEditViewModel : ViewModelBase
    {
        private const string ScanCategory = "Fines";

        private readonly FineService _fines;
        private readonly IScanStorage _scans;
        private readonly IFileDialogService _fileDialogs;
        private readonly Fine _fine;
        private readonly bool _isNew;

        private string _resolutionNumber;
        private DateTime? _resolutionDate;
        private DateTime? _violationDate;
        private string _violationPlace;
        private string _driverName;
        private string _amountText;
        private CarOption _selectedCar;
        private string _scanPath;

        public FineEditViewModel(
            FineService fines,
            IScanStorage scans,
            IFileDialogService fileDialogs,
            IEnumerable<Car> cars,
            Fine fine,
            bool isNew)
        {
            _fines = fines;
            _scans = scans;
            _fileDialogs = fileDialogs;
            _fine = fine;
            _isNew = isNew;

            Cars = new ObservableCollection<CarOption>();
            KnownDrivers = new ObservableCollection<string>();
            Errors = new ObservableCollection<string>();

            foreach (var car in cars ?? Enumerable.Empty<Car>())
                Cars.Add(CarOption.ForCar(car));

            _resolutionNumber = fine.ResolutionNumber;
            _resolutionDate = fine.ResolutionDate == default(DateTime) ? (DateTime?)null : fine.ResolutionDate;
            _violationDate = fine.ViolationDate == default(DateTime) ? (DateTime?)null : fine.ViolationDate;
            _violationPlace = fine.ViolationPlace;
            _driverName = fine.DriverName;
            _amountText = fine.Amount > 0m ? fine.Amount.ToString("0.##", CultureInfo.CurrentCulture) : string.Empty;
            _scanPath = fine.ScanPath;
            _selectedCar = Cars.FirstOrDefault(c => string.Equals(c.Vin, fine.CarVin, StringComparison.OrdinalIgnoreCase));

            AttachScanCommand = new RelayCommand(o => AttachScan());
            OpenScanCommand = new RelayCommand(o => OpenScan(), o => HasScan);
            RemoveScanCommand = new RelayCommand(o => RemoveScan(), o => HasScan);
            SaveCommand = new RelayCommand(o => Save());
            CancelCommand = new RelayCommand(o => Close(false));
        }

        /// <summary>Закрыть окно. true — штраф сохранён.</summary>
        public event EventHandler<bool> RequestClose;

        public ObservableCollection<CarOption> Cars { get; private set; }
        public ObservableCollection<string> KnownDrivers { get; private set; }
        public ObservableCollection<string> Errors { get; private set; }

        public ICommand AttachScanCommand { get; private set; }
        public ICommand OpenScanCommand { get; private set; }
        public ICommand RemoveScanCommand { get; private set; }
        public ICommand SaveCommand { get; private set; }
        public ICommand CancelCommand { get; private set; }

        public string Title
        {
            get { return _isNew ? "Новый штраф" : "Штраф № " + _fine.ResolutionNumber; }
        }

        /// <summary>Номер постановления — ключ записи, у существующего штрафа не меняется.</summary>
        public bool CanEditNumber
        {
            get { return _isNew; }
        }

        public string ResolutionNumber
        {
            get { return _resolutionNumber; }
            set { SetProperty(ref _resolutionNumber, value); }
        }

        public DateTime? ResolutionDate
        {
            get { return _resolutionDate; }
            set { SetProperty(ref _resolutionDate, value); }
        }

        public DateTime? ViolationDate
        {
            get { return _violationDate; }
            set { SetProperty(ref _violationDate, value); }
        }

        public string ViolationPlace
        {
            get { return _violationPlace; }
            set { SetProperty(ref _violationPlace, value); }
        }

        public string DriverName
        {
            get { return _driverName; }
            set { SetProperty(ref _driverName, value); }
        }

        public string AmountText
        {
            get { return _amountText; }
            set { SetProperty(ref _amountText, value); }
        }

        public CarOption SelectedCar
        {
            get { return _selectedCar; }
            set { SetProperty(ref _selectedCar, value); }
        }

        public string ScanPath
        {
            get { return _scanPath; }
            private set
            {
                if (SetProperty(ref _scanPath, value))
                {
                    OnPropertyChanged("HasScan");
                    OnPropertyChanged("ScanDisplay");
                }
            }
        }

        public bool HasScan
        {
            get { return !string.IsNullOrWhiteSpace(ScanPath); }
        }

        public string ScanDisplay
        {
            get { return HasScan ? Path.GetFileName(ScanPath) : "скан не вложен"; }
        }

        public bool HasErrors
        {
            get { return Errors.Count > 0; }
        }

        /// <summary>Подтягивает подсказки по водителям. Вызывается после открытия окна.</summary>
        public async Task InitializeAsync()
        {
            try
            {
                var drivers = await _fines.GetKnownDriversAsync();
                KnownDrivers.Clear();
                foreach (var driver in drivers)
                    KnownDrivers.Add(driver);
            }
            catch
            {
                // подсказки по водителям не критичны: без них форма работает
            }
        }

        private void AttachScan()
        {
            var number = (ResolutionNumber ?? string.Empty).Trim();
            if (number.Length == 0)
            {
                ShowErrors(new[] { "Сначала укажите номер постановления — по нему называется файл скана." });
                return;
            }

            var path = _fileDialogs.PickFile(
                "Выберите скан постановления",
                "Сканы (*.pdf;*.jpg;*.jpeg;*.png;*.tif;*.tiff)|*.pdf;*.jpg;*.jpeg;*.png;*.tif;*.tiff|Все файлы (*.*)|*.*");

            if (path == null)
                return;

            try
            {
                ScanPath = _scans.Attach(ScanCategory, number, path);
                Errors.Clear();
                OnPropertyChanged("HasErrors");
            }
            catch (Exception ex)
            {
                ShowErrors(new[] { "Не удалось вложить скан: " + ex.Message });
            }
        }

        private void OpenScan()
        {
            try
            {
                _scans.Open(ScanPath);
            }
            catch (Exception ex)
            {
                ShowErrors(new[] { "Не удалось открыть скан: " + ex.Message });
            }
        }

        private void RemoveScan()
        {
            try
            {
                _scans.Delete(ScanPath);
            }
            catch (Exception ex)
            {
                ShowErrors(new[] { "Не удалось удалить файл скана: " + ex.Message });
            }

            ScanPath = null;
        }

        private async void Save()
        {
            try
            {
                var problems = new List<string>();

                decimal amount;
                var amountParsed = TryParseAmount(AmountText, out amount);
                if (!amountParsed)
                    problems.Add("Сумма штрафа указана неверно. Пример: 1500 или 1500,50");

                var candidate = new Fine
                {
                    ResolutionNumber = ResolutionNumber,
                    ResolutionDate = ResolutionDate ?? default(DateTime),
                    ViolationDate = ViolationDate ?? default(DateTime),
                    ViolationPlace = ViolationPlace,
                    CarVin = SelectedCar != null ? SelectedCar.Vin : null,
                    DriverName = DriverName,
                    Amount = amount,
                    ScanPath = ScanPath
                };

                foreach (var error in await _fines.ValidateAsync(candidate, _isNew))
                {
                    // если строка суммы вообще не разобрана, замечание сервиса про ноль только дублирует ошибку ввода
                    if (!amountParsed && error.StartsWith("Сумма", StringComparison.CurrentCulture))
                        continue;

                    problems.Add(error);
                }

                if (problems.Count > 0)
                {
                    ShowErrors(problems);
                    return;
                }

                if (_isNew)
                    await _fines.AddAsync(candidate);
                else
                    await _fines.UpdateAsync(candidate);

                Close(true);
            }
            catch (Exception ex)
            {
                ShowErrors(new[] { ex.Message });
            }
        }

        private void ShowErrors(IEnumerable<string> problems)
        {
            Errors.Clear();
            foreach (var problem in problems)
                Errors.Add(problem);

            OnPropertyChanged("HasErrors");
        }

        private void Close(bool saved)
        {
            var handler = RequestClose;
            if (handler != null)
                handler(this, saved);
        }

        /// <summary>Принимает и «1500,50», и «1500.50» — пользователи вводят и так, и так.</summary>
        private static bool TryParseAmount(string text, out decimal amount)
        {
            amount = 0m;

            if (string.IsNullOrWhiteSpace(text))
                return false;

            var normalized = text.Trim().Replace(" ", string.Empty).Replace(',', '.');

            return decimal.TryParse(
                normalized,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out amount);
        }
    }
}
