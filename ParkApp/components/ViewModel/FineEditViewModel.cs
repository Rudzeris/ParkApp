using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Input;
using ParkApp.components.Application;
using ParkApp.components.Domain;

namespace ParkApp.components.ViewModel
{
    /// <summary>
    /// Запись книги постановлений. Поля повторяют столбцы книги.
    ///
    /// Марка и ГРЗ — обычный текст: в книгу пишут то, что напечатано
    /// в постановлении. Кнопка «Из реестра» подставляет их из карточки машины,
    /// чтобы не набирать руками, но не запрещает вписать своё.
    /// </summary>
    public class FineEditViewModel : ViewModelBase
    {
        private readonly FineService _fines;
        private readonly IScanStorage _scans;
        private readonly IFileDialogService _fileDialogs;
        private readonly Fine _fine;
        private readonly bool _isNew;

        private string _resolutionNumber;
        private DateTime? _resolutionDate;
        private DateTime? _violationDate;
        private string _offenderName;
        private string _carBrand;
        private string _carPlate;
        private string _amountText;
        private bool _isPaid;
        private DateTime? _paidDate;
        private string _paymentReference;
        private string _notes;
        private CarOption _selectedCar;
        private string _scanPath;
        private string _carSearch;

        /// <summary>Все машины реестра; Cars — то, что осталось после поиска.</summary>
        private readonly List<CarOption> _allCars = new List<CarOption>();

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
            Errors = new ObservableCollection<string>();

            foreach (var car in cars ?? Enumerable.Empty<Car>())
            {
                var option = CarOption.ForCar(car);
                _allCars.Add(option);
                Cars.Add(option);
            }

            _resolutionNumber = fine.ResolutionNumber;
            _resolutionDate = fine.ResolutionDate == default(DateTime) ? (DateTime?)null : fine.ResolutionDate;
            _violationDate = fine.ViolationDate == default(DateTime) ? (DateTime?)null : fine.ViolationDate;
            _offenderName = fine.OffenderName;
            _carBrand = fine.CarBrand;
            _carPlate = fine.CarPlate;
            _amountText = fine.Amount > 0m ? fine.Amount.ToString("0.##", CultureInfo.CurrentCulture) : string.Empty;
            _isPaid = fine.IsPaid;
            _paidDate = fine.PaidDate;
            _paymentReference = fine.PaymentReference;
            _notes = fine.Notes;
            _scanPath = fine.ScanPath;

            _selectedCar = Cars.FirstOrDefault(c => FineService.SamePlate(c.Plate, fine.CarPlate));

            UseRegistryCarCommand = new RelayCommand(o => UseRegistryCar(), o => SelectedCar != null);
            AttachScanCommand = new RelayCommand(o => AttachScan());
            OpenScanCommand = new RelayCommand(o => OpenScan(), o => HasScan);
            RemoveScanCommand = new RelayCommand(o => RemoveScan(), o => HasScan);
            SaveCommand = new RelayCommand(o => Save());
            CancelCommand = new RelayCommand(o => Close(false));
        }

        /// <summary>Закрыть окно. true — запись сохранена.</summary>
        public event EventHandler<bool> RequestClose;

        public ObservableCollection<CarOption> Cars { get; private set; }

        /// <summary>
        /// Поиск машины. Список машин длинный, а человек помнит либо номер,
        /// либо марку, либо кусок VIN — по мере ввода список сокращается.
        ///
        ///   «0123АВ»  — точное совпадение;
        ///   *123*     — звёздочка это любые символы, в том числе ни одного;
        ///   камаз 43  — похожие слова, опечатка прощается.
        /// </summary>
        public string CarSearch
        {
            get { return _carSearch; }
            set
            {
                if (SetProperty(ref _carSearch, value))
                    FilterCars();
            }
        }

        /// <summary>Сколько машин осталось после поиска — видно, что список сузился.</summary>
        public string CarSearchStatus
        {
            get
            {
                if (Cars.Count == _allCars.Count)
                    return string.Format("машин: {0}", _allCars.Count);

                return Cars.Count == 0
                    ? "ничего не найдено"
                    : string.Format("найдено: {0} из {1}", Cars.Count, _allCars.Count);
            }
        }

        private void FilterCars()
        {
            var query = SearchQuery.Parse(_carSearch);

            // выбранная машина не должна пропасть из списка от того,
            // что человек набрал в поиске что-то другое
            var selected = _selectedCar;

            Cars.Clear();
            foreach (var car in _allCars.Where(c => query.Matches(c.SearchFields)))
                Cars.Add(car);

            if (selected != null && !Cars.Contains(selected))
                Cars.Insert(0, selected);

            OnPropertyChanged("CarSearchStatus");
        }
        public ObservableCollection<string> Errors { get; private set; }

        public ICommand UseRegistryCarCommand { get; private set; }
        public ICommand AttachScanCommand { get; private set; }
        public ICommand OpenScanCommand { get; private set; }
        public ICommand RemoveScanCommand { get; private set; }
        public ICommand SaveCommand { get; private set; }
        public ICommand CancelCommand { get; private set; }

        public string Title
        {
            get { return _isNew ? "Новая запись книги постановлений" : "Постановление № " + _fine.ResolutionNumber; }
        }

        /// <summary>Номер постановления — ключ записи, у сохранённой не меняется.</summary>
        public bool CanEditNumber
        {
            get { return _isNew; }
        }

        public string ResolutionNumber
        {
            get { return _resolutionNumber; }
            set { SetProperty(ref _resolutionNumber, value); }
        }

        /// <summary>Дата привлечения.</summary>
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

        public string OffenderName
        {
            get { return _offenderName; }
            set { SetProperty(ref _offenderName, value); }
        }

        public string CarBrand
        {
            get { return _carBrand; }
            set { SetProperty(ref _carBrand, value); }
        }

        public string CarPlate
        {
            get { return _carPlate; }
            set { SetProperty(ref _carPlate, value); }
        }

        public string AmountText
        {
            get { return _amountText; }
            set { SetProperty(ref _amountText, value); }
        }

        public bool IsPaid
        {
            get { return _isPaid; }
            set { SetProperty(ref _isPaid, value); }
        }

        /// <summary>Дата оплаты. Может остаться пустой у оплаченного штрафа.</summary>
        public DateTime? PaidDate
        {
            get { return _paidDate; }
            set
            {
                if (!SetProperty(ref _paidDate, value))
                    return;

                // указали дату — значит оплачен; отдельно ставить галку не нужно
                if (value.HasValue)
                    IsPaid = true;
            }
        }

        /// <summary>ИД платежа или номер чека.</summary>
        public string PaymentReference
        {
            get { return _paymentReference; }
            set
            {
                if (SetProperty(ref _paymentReference, value) && !string.IsNullOrWhiteSpace(value))
                    IsPaid = true;
            }
        }

        public string Notes
        {
            get { return _notes; }
            set { SetProperty(ref _notes, value); }
        }

        /// <summary>Машина из реестра — только для подстановки марки и номера.</summary>
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

        private void UseRegistryCar()
        {
            if (SelectedCar == null)
                return;

            if (!string.IsNullOrWhiteSpace(SelectedCar.Plate))
                CarPlate = SelectedCar.Plate;

            var car = SelectedCar;
            var brand = car.Display;

            // в списке строка вида «УАЗ-3163 · 0123АВ · VIN»; в книгу нужна только марка
            var separator = brand.IndexOf('·');
            CarBrand = separator > 0 ? brand.Substring(0, separator).Trim() : brand;
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
                ScanPath = _scans.Attach(ScanCategory.Fine, number, path);
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
                    problems.Add("Сумма штрафа указана неверно. Пример: 800 или 1500,50");

                var candidate = new Fine
                {
                    RowNumber = _fine.RowNumber,
                    ResolutionNumber = ResolutionNumber,
                    ResolutionDate = ResolutionDate ?? default(DateTime),
                    ViolationDate = ViolationDate ?? default(DateTime),
                    OffenderName = OffenderName,
                    CarBrand = CarBrand,
                    CarPlate = CarPlate,
                    Amount = amount,
                    PaidDate = PaidDate,
                    PaymentReference = PaymentReference,
                    PaymentText = BuildPaymentText(),
                    Notes = Notes,
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

        /// <summary>
        /// Пока оплату не трогали, в книгу возвращается исходная формулировка:
        /// переписывать чужую запись своим шаблоном незачем.
        /// </summary>
        private string BuildPaymentText()
        {
            if (PaymentTextParser.SameAs(_fine.PaymentText, IsPaid, PaidDate, PaymentReference))
                return _fine.PaymentText;

            return PaymentTextParser.Format(IsPaid, PaidDate, PaymentReference);
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

            var normalized = new string(text
                .Replace(',', '.')
                .Where(c => char.IsDigit(c) || c == '.' || c == '-')
                .ToArray());

            return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);
        }
    }
}
