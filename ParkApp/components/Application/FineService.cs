using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ParkApp.components.Domain;

namespace ParkApp.components.Application
{
    /// <summary>
    /// Сценарии работы с книгой постановлений: отбор, подсчёт итогов, проверка и сохранение.
    /// Правила проверки живут здесь, а не в окне, чтобы одинаково работать
    /// при вводе из UI и при возможном импорте.
    /// </summary>
    public class FineService
    {
        private readonly IFineRepository _repo;

        public FineService(IFineRepository repo) => _repo = repo;

        public Task<IReadOnlyList<Fine>> GetAllAsync() => _repo.GetAllAsync();

        /// <summary>Штрафы по условиям отбора. Порядок книги сохраняется.</summary>
        public async Task<IReadOnlyList<Fine>> FindAsync(FineFilter filter)
        {
            var all = await _repo.GetAllAsync();
            IEnumerable<Fine> query = all;

            if (filter != null)
            {
                if (!string.IsNullOrWhiteSpace(filter.CarPlate))
                    query = query.Where(f => SamePlate(f.CarPlate, filter.CarPlate));

                if (!string.IsNullOrWhiteSpace(filter.OffenderName))
                    query = query.Where(f => Contains(f.OffenderName, filter.OffenderName));

                if (filter.From.HasValue)
                {
                    var from = filter.From.Value.Date;
                    query = query.Where(f => f.ViolationDate.Date >= from);
                }

                if (filter.To.HasValue)
                {
                    var to = filter.To.Value.Date;
                    query = query.Where(f => f.ViolationDate.Date <= to);
                }

                if (filter.IsPaid.HasValue)
                    query = query.Where(f => f.IsPaid == filter.IsPaid.Value);

                if (!string.IsNullOrWhiteSpace(filter.Text))
                    query = query.Where(f => Contains(f.ResolutionNumber, filter.Text)
                                             || Contains(f.CarBrand, filter.Text)
                                             || Contains(f.Notes, filter.Text));
            }

            return query.ToList();
        }

        public Task<Fine> GetByNumberAsync(string resolutionNumber) => _repo.GetByNumberAsync(resolutionNumber);

        /// <summary>Итоговая сумма по набору штрафов.</summary>
        public decimal GetTotal(IEnumerable<Fine> fines) => fines == null ? 0m : fines.Sum(f => f.Amount);

        /// <summary>Сколько из этих штрафов ещё не оплачено — главный вопрос к книге.</summary>
        public decimal GetUnpaidTotal(IEnumerable<Fine> fines)
        {
            return fines == null ? 0m : fines.Where(f => !f.IsPaid).Sum(f => f.Amount);
        }

        /// <summary>Гос. номера, которые уже встречались в книге — для подсказки при вводе.</summary>
        public async Task<IReadOnlyList<string>> GetKnownPlatesAsync()
        {
            var all = await _repo.GetAllAsync();
            return all
                .Select(f => (f.CarPlate ?? string.Empty).Trim())
                .Where(plate => plate.Length > 0)
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .OrderBy(plate => plate)
                .ToList();
        }

        /// <summary>
        /// Проверяет запись. Пустой список — ошибок нет.
        /// </summary>
        /// <param name="isNew">true для новой записи: тогда проверяется уникальность номера постановления.</param>
        public async Task<IReadOnlyList<string>> ValidateAsync(Fine fine, bool isNew)
        {
            var errors = new List<string>();

            if (fine == null)
            {
                errors.Add("Штраф не задан.");
                return errors;
            }

            var number = (fine.ResolutionNumber ?? string.Empty).Trim();
            if (number.Length == 0)
            {
                errors.Add("Укажите номер постановления.");
            }
            else if (isNew)
            {
                var existing = await _repo.GetByNumberAsync(number);
                if (existing != null)
                    errors.Add(string.Format("Постановление № {0} уже есть в книге.", number));
            }

            var today = DateTime.Today;
            var empty = default(DateTime);

            if (fine.ResolutionDate == empty)
                errors.Add("Укажите дату привлечения.");
            else if (fine.ResolutionDate.Date > today)
                errors.Add("Дата привлечения не может быть в будущем.");

            if (fine.ViolationDate == empty)
                errors.Add("Укажите дату правонарушения.");
            else if (fine.ViolationDate.Date > today)
                errors.Add("Дата правонарушения не может быть в будущем.");

            if (fine.ResolutionDate != empty && fine.ViolationDate != empty
                && fine.ViolationDate.Date > fine.ResolutionDate.Date)
                errors.Add("Дата правонарушения не может быть позже даты привлечения.");

            if (string.IsNullOrWhiteSpace(fine.CarPlate))
                errors.Add("Укажите гос. рег. знак.");

            if (fine.Amount <= 0m)
                errors.Add("Сумма штрафа должна быть больше нуля.");

            if (fine.PaidDate.HasValue)
            {
                if (fine.PaidDate.Value.Date > today)
                    errors.Add("Дата оплаты не может быть в будущем.");

                if (fine.ResolutionDate != empty && fine.PaidDate.Value.Date < fine.ResolutionDate.Date)
                    errors.Add("Дата оплаты не может быть раньше даты привлечения.");
            }

            return errors;
        }

        public async Task AddAsync(Fine fine)
        {
            await EnsureValidAsync(fine, true);
            Normalize(fine);
            await _repo.AddAsync(fine);
        }

        public async Task UpdateAsync(Fine fine)
        {
            await EnsureValidAsync(fine, false);
            Normalize(fine);
            await _repo.UpdateAsync(fine);
        }

        public Task DeleteAsync(string resolutionNumber) => _repo.DeleteAsync(resolutionNumber);

        private async Task EnsureValidAsync(Fine fine, bool isNew)
        {
            var errors = await ValidateAsync(fine, isNew);
            if (errors.Count > 0)
                throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }

        private static void Normalize(Fine fine)
        {
            fine.ResolutionNumber = (fine.ResolutionNumber ?? string.Empty).Trim();
            fine.CarPlate = (fine.CarPlate ?? string.Empty).Trim();
            fine.CarBrand = Trim(fine.CarBrand);
            fine.OffenderName = Trim(fine.OffenderName);
            fine.Notes = Trim(fine.Notes);
            fine.PaymentReference = Trim(fine.PaymentReference);
            fine.PaymentText = Trim(fine.PaymentText);
            fine.ResolutionDate = fine.ResolutionDate.Date;
            fine.ViolationDate = fine.ViolationDate.Date;

            if (fine.PaidDate.HasValue)
                fine.PaidDate = fine.PaidDate.Value.Date;
        }

        private static string Trim(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            return value.Trim();
        }

        /// <summary>Сравнение гос. номеров без пробелов и дефисов.</summary>
        public static bool SamePlate(string left, string right)
        {
            return string.Equals(NormalizePlate(left), NormalizePlate(right), StringComparison.CurrentCultureIgnoreCase);
        }

        /// <summary>Гос. номер без пробелов и дефисов, в верхнем регистре.</summary>
        public static string NormalizePlate(string plate)
        {
            if (string.IsNullOrWhiteSpace(plate))
                return string.Empty;

            return new string(plate.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        }

        private static bool Contains(string source, string part)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrWhiteSpace(part))
                return false;
            return source.IndexOf(part.Trim(), StringComparison.CurrentCultureIgnoreCase) >= 0;
        }
    }
}
