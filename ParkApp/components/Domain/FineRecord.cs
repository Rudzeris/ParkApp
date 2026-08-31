using System;

namespace ParkApp.components.Domain
{
    public class FineRecord
    {
        public int Id { get; set; }
        public DateTime ViolationDate { get; set; }
        public string FullName { get; set; }
        public string CarBrand { get; set; }
        public string Plate { get; set; }
        public DateTime DecisionDate { get; set; }
        public string DecisionNumber { get; set; }
        public decimal FineAmount { get; set; }
        public string PaymentInfo { get; set; }
        public string Note { get; set; }
        public string FilePath { get; set; } // путь к файлу постановления
    }
}