namespace ParkApp.components.ViewModel
{
    /// <summary>Вариант отбора по оплате: все, оплаченные, не оплаченные.</summary>
    public class PaymentFilterOption
    {
        public PaymentFilterOption(string display, bool? isPaid)
        {
            Display = display;
            IsPaid = isPaid;
        }

        public string Display { get; private set; }

        /// <summary>null — без ограничения.</summary>
        public bool? IsPaid { get; private set; }
    }
}
