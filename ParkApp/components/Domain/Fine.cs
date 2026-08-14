using System;

namespace ParkApp.components.Domain
{
    /// <summary>
    /// Строка книги постановлений. Поля повторяют столбцы книги, которую
    /// ведут в Excel, поэтому машина здесь — марка и ГРЗ текстом, а не ссылка:
    /// в книгу записывают то, что напечатано в постановлении, и машины
    /// может не оказаться в реестре.
    /// </summary>
    public class Fine
    {
        /// <summary>№ п/п в книге. У новых записей проставляется при сохранении.</summary>
        public int? RowNumber { get; set; }

        /// <summary>Номер постановления. Ключ записи, после создания не меняется.</summary>
        public string ResolutionNumber { get; set; }

        /// <summary>Дата привлечения — дата постановления.</summary>
        public DateTime ResolutionDate { get; set; }

        /// <summary>Дата правонарушения.</summary>
        public DateTime ViolationDate { get; set; }

        /// <summary>Ф.И.О. правонарушителя. В книге лежит в одной ячейке с датой правонарушения.</summary>
        public string OffenderName { get; set; }

        /// <summary>Марка АТ.</summary>
        public string CarBrand { get; set; }

        /// <summary>ГРЗ. По нему запись связывается с реестром машин.</summary>
        public string CarPlate { get; set; }

        /// <summary>Сумма штрафа, руб.</summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// Столбец «оплата, чек, дата» как он записан в книге,
        /// например «Оплата 19.05.2026 ИД платежа 952951978536EGLG».
        /// Хранится целиком: формулировка бывает разной, и терять её нельзя.
        /// </summary>
        public string PaymentText { get; set; }

        /// <summary>Дата оплаты, разобранная из <see cref="PaymentText"/>.</summary>
        public DateTime? PaidDate { get; set; }

        /// <summary>ИД платежа или номер чека, разобранный из <see cref="PaymentText"/>.</summary>
        public string PaymentReference { get; set; }

        /// <summary>Штраф считается оплаченным, если графа оплаты заполнена.</summary>
        public bool IsPaid
        {
            get { return !string.IsNullOrWhiteSpace(PaymentText); }
        }

        /// <summary>Примечание.</summary>
        public string Notes { get; set; }

        /// <summary>Относительный путь к скану постановления внутри каталога данных.</summary>
        public string ScanPath { get; set; }
    }
}
