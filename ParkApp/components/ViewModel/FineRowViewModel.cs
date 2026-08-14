using System;
using ParkApp.components.Domain;

namespace ParkApp.components.ViewModel
{
    /// <summary>
    /// Строка книги постановлений. Марка и ГРЗ показываются так, как записаны
    /// в книге; машина из реестра подтягивается по ГРЗ — это подсказка, а не источник.
    /// </summary>
    public class FineRowViewModel
    {
        public FineRowViewModel(Fine fine, Car car, bool hasScan)
        {
            Fine = fine;
            Car = car;
            HasScan = hasScan;
        }

        public Fine Fine { get; private set; }

        /// <summary>Машина из реестра, найденная по ГРЗ. null — такой машины в реестре нет.</summary>
        public Car Car { get; private set; }

        public bool HasScan { get; private set; }

        public int? RowNumber { get { return Fine.RowNumber; } }
        public string ResolutionNumber { get { return Fine.ResolutionNumber; } }
        public DateTime ResolutionDate { get { return Fine.ResolutionDate; } }
        public DateTime ViolationDate { get { return Fine.ViolationDate; } }
        public decimal Amount { get { return Fine.Amount; } }
        public bool IsPaid { get { return Fine.IsPaid; } }

        public string OffenderName
        {
            get { return string.IsNullOrWhiteSpace(Fine.OffenderName) ? "не указан" : Fine.OffenderName; }
        }

        public string CarBrand
        {
            get { return string.IsNullOrWhiteSpace(Fine.CarBrand) ? "—" : Fine.CarBrand; }
        }

        public string CarPlate
        {
            get { return string.IsNullOrWhiteSpace(Fine.CarPlate) ? "—" : Fine.CarPlate; }
        }

        /// <summary>Что нашлось в реестре машин по этому ГРЗ.</summary>
        public string RegistryText
        {
            get { return Car != null ? CarOption.ModelText(Car) : "нет в реестре"; }
        }

        /// <summary>Графа оплаты как она записана в книге.</summary>
        public string PaidText
        {
            get { return Fine.IsPaid ? Fine.PaymentText : "не оплачен"; }
        }

        public string Notes
        {
            get { return string.IsNullOrWhiteSpace(Fine.Notes) ? "—" : Fine.Notes; }
        }

        /// <summary>Отметка о вложенном скане постановления.</summary>
        public string ScanMark { get { return HasScan ? "есть" : "—"; } }
    }
}
