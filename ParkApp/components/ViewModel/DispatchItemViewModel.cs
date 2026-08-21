using System;
using System.Globalization;
using ParkApp.components.Application;

namespace ParkApp.components.ViewModel
{
    /// <summary>
    /// Строка наряда: машина, её время и постоянные отметки.
    /// Время редактируется как «06:00» — так его и называют в парке.
    /// </summary>
    public class DispatchItemViewModel : ViewModelBase
    {
        private readonly DispatchPlanItem _item;
        private DateTime _date;

        public DispatchItemViewModel(DispatchPlanItem item, DateTime date)
        {
            _item = item;
            _date = date;
        }

        public DispatchPlanItem Item
        {
            get { return _item; }
        }

        /// <summary>Дата наряда: от неё считаются выезд и возвращение.</summary>
        public void SetDate(DateTime date)
        {
            _date = date.Date;
            Recalculate();
        }

        public bool IsSelected
        {
            get { return _item.IsSelected; }
            set
            {
                if (_item.IsSelected == value)
                    return;

                _item.IsSelected = value;
                OnPropertyChanged("IsSelected");
            }
        }

        public string Brand
        {
            get { return string.IsNullOrWhiteSpace(_item.Brand) ? "без марки" : _item.Brand; }
        }

        public string Plate
        {
            get { return string.IsNullOrWhiteSpace(_item.Plate) ? "—" : _item.Plate; }
        }

        public string GroupName
        {
            get { return _item.GroupName; }
            set { if (_item.GroupName != value) { _item.GroupName = value; OnPropertyChanged("GroupName"); } }
        }

        public string OperationGroup
        {
            get { return _item.OperationGroup; }
            set { if (_item.OperationGroup != value) { _item.OperationGroup = value; OnPropertyChanged("OperationGroup"); } }
        }

        public string Purpose
        {
            get { return _item.Purpose; }
            set { if (_item.Purpose != value) { _item.Purpose = value; OnPropertyChanged("Purpose"); } }
        }

        public string Route
        {
            get { return _item.Route; }
            set { if (_item.Route != value) { _item.Route = value; OnPropertyChanged("Route"); } }
        }

        public string Assignment
        {
            get { return _item.Assignment; }
            set { if (_item.Assignment != value) { _item.Assignment = value; OnPropertyChanged("Assignment"); } }
        }

        public string Notes
        {
            get { return _item.Notes; }
            set { if (_item.Notes != value) { _item.Notes = value; OnPropertyChanged("Notes"); } }
        }

        /// <summary>«Повторить»: машина попадает в наряд каждый день сама.</summary>
        public bool RepeatDaily
        {
            get { return _item.Schedule.RepeatDaily; }
            set
            {
                if (_item.Schedule.RepeatDaily == value)
                    return;

                _item.Schedule.RepeatDaily = value;
                OnPropertyChanged("RepeatDaily");
            }
        }

        /// <summary>Печатать в путевом листе блок разрешения.</summary>
        public bool AllowOutsideOrder
        {
            get { return _item.Schedule.AllowOutsideOrder; }
            set
            {
                if (_item.Schedule.AllowOutsideOrder == value)
                    return;

                _item.Schedule.AllowOutsideOrder = value;
                OnPropertyChanged("AllowOutsideOrder");
            }
        }

        public string DepartureText
        {
            get { return _item.DepartureAt.ToString("HH:mm"); }
            set
            {
                TimeSpan time;
                if (!TryParseTime(value, out time))
                    return;

                _item.Schedule.DepartureTime = time;
                Recalculate();
            }
        }

        public string ReturnText
        {
            get { return _item.ReturnAt.ToString("HH:mm"); }
            set
            {
                TimeSpan time;
                if (!TryParseTime(value, out time))
                    return;

                _item.Schedule.ReturnTime = time;
                Recalculate();
            }
        }

        /// <summary>«с 6 до 6» — сутки; в наряде это видно по дате возвращения.</summary>
        public string ReturnDayText
        {
            get { return _item.ReturnAt.Date > _item.DepartureAt.Date ? "следующий день" : "в тот же день"; }
        }

        private void Recalculate()
        {
            _item.DepartureAt = _item.Schedule.DepartureOn(_date);
            _item.ReturnAt = _item.Schedule.ReturnOn(_date);

            OnPropertyChanged("DepartureText");
            OnPropertyChanged("ReturnText");
            OnPropertyChanged("ReturnDayText");
        }

        /// <summary>Принимает «6», «6:00», «06.00» — так время и вводят.</summary>
        private static bool TryParseTime(string text, out TimeSpan time)
        {
            time = TimeSpan.Zero;

            if (string.IsNullOrWhiteSpace(text))
                return false;

            var normalized = text.Trim().Replace('.', ':').Replace(',', ':');

            int hours;
            if (int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out hours)
                && hours >= 0 && hours < 24)
            {
                time = TimeSpan.FromHours(hours);
                return true;
            }

            if (TimeSpan.TryParse(normalized, CultureInfo.InvariantCulture, out time)
                && time >= TimeSpan.Zero && time < TimeSpan.FromDays(1))
                return true;

            time = TimeSpan.Zero;
            return false;
        }
    }
}
