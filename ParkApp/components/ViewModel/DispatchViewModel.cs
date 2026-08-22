using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ParkApp.components.Application;

namespace ParkApp.components.ViewModel
{
    /// <summary>
    /// Наряд на выход техники: отметить машины, сохранить наряд, напечатать
    /// наряд и путевые листы.
    ///
    /// Дата по умолчанию — завтра: наряд составляют накануне.
    /// </summary>
    public class DispatchViewModel : ViewModelBase
    {
        private readonly DispatchService _dispatch;
        private readonly IDispatchPrinter _printer;
        private readonly IFileLauncher _files;
        private readonly IMessageService _messages;

        private DispatchPlan _plan;
        private DateTime _date;
        private string _orderNumber;
        private string _status;
        private DispatchItemViewModel _selectedItem;
        private bool _isLoading;

        public DispatchViewModel(
            DispatchService dispatch,
            IDispatchPrinter printer,
            IFileLauncher files,
            IMessageService messages)
        {
            _dispatch = dispatch;
            _printer = printer;
            _files = files;
            _messages = messages;

            _date = DispatchService.DefaultDate;

            Items = new ObservableCollection<DispatchItemViewModel>();

            SaveCommand = new RelayCommand(o => Save());
            PrintOrderCommand = new RelayCommand(o => PrintOrder());
            PrintWaybillsCommand = new RelayCommand(o => PrintWaybills());
            PrintOutsideWaybillCommand = new RelayCommand(o => PrintOutsideWaybill(), o => SelectedItem != null);
            RefreshCommand = new RelayCommand(o => Reload());
            SelectRepeatingCommand = new RelayCommand(o => SelectRepeating());
            ClearSelectionCommand = new RelayCommand(o => ClearSelection());
        }

        public ObservableCollection<DispatchItemViewModel> Items { get; private set; }

        public ICommand SaveCommand { get; private set; }
        public ICommand PrintOrderCommand { get; private set; }
        public ICommand PrintWaybillsCommand { get; private set; }
        public ICommand PrintOutsideWaybillCommand { get; private set; }
        public ICommand RefreshCommand { get; private set; }
        public ICommand SelectRepeatingCommand { get; private set; }
        public ICommand ClearSelectionCommand { get; private set; }

        /// <summary>Дата наряда. По умолчанию завтра, но можно выбрать любую.</summary>
        public DateTime Date
        {
            get { return _date; }
            set
            {
                if (!SetProperty(ref _date, value.Date))
                    return;

                OnPropertyChanged("DateTitle");
                Reload();
            }
        }

        public string DateTitle
        {
            get
            {
                return string.Format("Наряд на «{0}» {1} {2} г.",
                    RussianDate.Day(_date), RussianDate.Month(_date), RussianDate.Year(_date));
            }
        }

        public string OrderNumber
        {
            get { return _orderNumber; }
            set
            {
                if (SetProperty(ref _orderNumber, value) && _plan != null)
                    _plan.OrderNumber = value;
            }
        }

        public DispatchItemViewModel SelectedItem
        {
            get { return _selectedItem; }
            set { SetProperty(ref _selectedItem, value); }
        }

        public string Status
        {
            get { return _status; }
            private set { SetProperty(ref _status, value); }
        }

        public async Task InitializeAsync()
        {
            await LoadAsync();
        }

        private async void Reload()
        {
            await LoadAsync();
        }

        private async Task LoadAsync()
        {
            if (_isLoading)
                return;

            _isLoading = true;
            try
            {
                _plan = await _dispatch.GetPlanAsync(_date);

                Items.Clear();
                foreach (var item in _plan.Items)
                {
                    var row = new DispatchItemViewModel(item, _date, _plan.Choices);

                    // счётчик в статусе должен меняться сразу при снятии галки
                    row.PropertyChanged += OnItemChanged;
                    Items.Add(row);
                }

                _orderNumber = _plan.OrderNumber;
                OnPropertyChanged("OrderNumber");

                UpdateStatus();
            }
            catch (Exception ex)
            {
                _messages.ShowError("Не удалось загрузить наряд: " + ex.Message);
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void OnItemChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsSelected")
                UpdateStatus();
        }

        private void UpdateStatus()
        {
            var selected = Items.Count(i => i.IsSelected);

            Status = _plan != null && _plan.IsNew
                ? string.Format("Новый наряд. Машин в наряде: {0} из {1} (отмечены «повторяющиеся»)", selected, Items.Count)
                : string.Format("Машин в наряде: {0} из {1}", selected, Items.Count);
        }

        private void SelectRepeating()
        {
            foreach (var item in Items)
                item.IsSelected = item.RepeatDaily;

            UpdateStatus();
        }

        private void ClearSelection()
        {
            foreach (var item in Items)
                item.IsSelected = false;

            UpdateStatus();
        }

        private async void Save()
        {
            if (_plan == null)
                return;

            try
            {
                _plan.OrderNumber = OrderNumber;
                await _dispatch.SaveAsync(_plan);

                await LoadAsync();
                Status = "Наряд сохранён. " + Status;
            }
            catch (Exception ex)
            {
                _messages.ShowError("Не удалось сохранить наряд: " + ex.Message);
            }
        }

        private void PrintOrder()
        {
            if (_plan == null)
                return;

            try
            {
                _plan.OrderNumber = OrderNumber;

                var path = _printer.PrintOrder(_plan);
                _files.Open(path);
                Status = "Наряд напечатан: " + path;
            }
            catch (Exception ex)
            {
                _messages.ShowError("Не удалось напечатать наряд: " + ex.Message);
            }
        }

        private void PrintWaybills()
        {
            if (_plan == null)
                return;

            var items = _plan.Items.Where(i => i.IsSelected).ToList();
            if (items.Count == 0)
            {
                _messages.ShowWarning("Не отмечено ни одной машины.");
                return;
            }

            Print(items, false);
        }

        /// <summary>
        /// Путевой лист машине, которой нет в наряде: она понадобилась ночью
        /// или внезапно, а накануне её в наряд не внесли.
        /// </summary>
        private void PrintOutsideWaybill()
        {
            if (_plan == null || SelectedItem == null)
                return;

            Print(new List<DispatchPlanItem> { SelectedItem.Item }, true);
        }

        private void Print(IList<DispatchPlanItem> items, bool outsideOrder)
        {
            try
            {
                var files = _printer.PrintWaybills(_plan, items, outsideOrder);

                foreach (var file in files)
                    _files.Open(file);

                Status = string.Format("Путевые листы напечатаны: {0} шт.", items.Count);
            }
            catch (Exception ex)
            {
                _messages.ShowError("Не удалось напечатать путевые листы: " + ex.Message);
            }
        }
    }
}
