using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ParkApp.components.Application;

namespace ParkApp.components.ViewModel
{
    /// <summary>
    /// Строка наряда: машина, её время и постоянные отметки.
    ///
    /// Что известно про машину — марка, Г.Р.З., группа, должностное лицо —
    /// подставляется из её карточки и в строке не правится: это данные машины,
    /// и заводят их там, где машину заводят. Остальное — группу эксплуатации,
    /// цель, маршрут, примечание — вписывают, а уже введённое предлагается
    /// списком, чтобы не набирать одно и то же каждый день.
    /// </summary>
    public class DispatchItemViewModel : ViewModelBase
    {
        private readonly DispatchPlanItem _item;
        private readonly DispatchChoices _choices;
        private DateTime _date;

        public DispatchItemViewModel(DispatchPlanItem item, DateTime date, DispatchChoices choices)
        {
            _item = item;
            _date = date;
            _choices = choices ?? new DispatchChoices();

            OperationGroupOptions = Options(DispatchChoices.OperationGroupColumn);
            PurposeOptions = Options(DispatchChoices.PurposeColumn);
            RouteOptions = Options(DispatchChoices.RouteColumn);
            AssignmentOptions = Options(DispatchChoices.AssignmentColumn);
            NotesOptions = Options(DispatchChoices.NotesColumn);
        }

        public IReadOnlyList<string> OperationGroupOptions { get; private set; }
        public IReadOnlyList<string> PurposeOptions { get; private set; }
        public IReadOnlyList<string> RouteOptions { get; private set; }
        public IReadOnlyList<string> AssignmentOptions { get; private set; }
        public IReadOnlyList<string> NotesOptions { get; private set; }

        /// <summary>Подсказки для графы: то, что уже вводили.</summary>
        private IReadOnlyList<string> Options(string column)
        {
            return _choices.AllOf(column).Select(v => v.Trim()).ToList();
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

        /// <summary>«Куда относится» из карточки машины. В наряде не правится.</summary>
        public string GroupName
        {
            get { return string.IsNullOrWhiteSpace(_item.GroupName) ? "—" : _item.GroupName; }
        }

        public string OperationGroup
        {
            get { return _item.OperationGroup; }
            set
            {
                var stored = Stored(value);
                if (_item.OperationGroup == stored)
                    return;

                _item.OperationGroup = stored;
                OnPropertyChanged("OperationGroup");
            }
        }

        public string Purpose
        {
            get { return _item.Purpose; }
            set
            {
                var stored = Stored(value);
                if (_item.Purpose == stored)
                    return;

                _item.Purpose = stored;
                OnPropertyChanged("Purpose");
            }
        }

        public string Route
        {
            get { return _item.Route; }
            set
            {
                var stored = Stored(value);
                if (_item.Route == stored)
                    return;

                _item.Route = stored;
                OnPropertyChanged("Route");
            }
        }

        public string Assignment
        {
            get { return _item.Assignment; }
            set
            {
                var stored = Stored(value);
                if (_item.Assignment == stored)
                    return;

                _item.Assignment = stored;
                OnPropertyChanged("Assignment");
            }
        }

        public string Notes
        {
            get { return _item.Notes; }
            set
            {
                var stored = Stored(value);
                if (_item.Notes == stored)
                    return;

                _item.Notes = stored;
                OnPropertyChanged("Notes");
            }
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

        /// <summary>Пустая графа хранится как пустая ячейка, а не как пробелы.</summary>
        private static string Stored(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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
