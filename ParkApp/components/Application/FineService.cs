using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ParkApp.components.Domain;

namespace ParkApp.components.Application
{
    /// <summary>
    /// Сценарии работы со штрафами: отбор, подсчёт итогов, проверка и сохранение.
    /// Правила проверки живут здесь, а не в окне, чтобы одинаково работать
    /// при вводе из UI и при возможном импорте.
    /// </summary>
    public class FineService
    {
        private readonly IFineRepository _repo;

        public FineService(IFineRepository repo) => _repo = repo;

        public Task<IReadOnlyList<Fine>> GetAllAsync() => _repo.GetAllAsync();

        /// <summary>Штрафы по условиям отбора, свежие сверху.</summary>
        public async Task<IReadOnlyList<Fine>> FindAsync(FineFilter filter)
        {
            var all = await _repo.GetAllAsync();
            IEnumerable<Fine> query = all;

            if (filter != null)
            {
                if (!string.IsNullOrWhiteSpace(filter.CarVin))
                    query = query.Where(f => string.Equals(f.CarVin, filter.CarVin, StringComparison.OrdinalIgnoreCase));

                if (filter.DriverId.HasValue)
                    query = query.Where(f => f.DriverId == filter.DriverId.Value);

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

                if (!string.IsNullOrWhiteSpace(filter.Text))
                    query = query.Where(f => Contains(f.ResolutionNumber, filter.Text)
                                             || Contains(f.ViolationPlace, filter.Text));
            }

            return query
                .OrderByDescending(f => f.ViolationDate)
                .ThenBy(f => f.ResolutionNumber)
                .ToList();
        }

        public Task<Fine> GetByNumberAsync(string resolutionNumber) => _repo.GetByNumberAsync(resolutionNumber);

        /// <summary>Итоговая сумма по набору штрафов.</summary>
        public decimal GetTotal(IEnumerable<Fine> fines) => fines == null ? 0m : fines.Sum(f => f.Amount);

        /// <summary>
        /// Проверяет штраф. Пустой список — ошибок нет.
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
                    errors.Add(string.Format("Постановление № {0} уже есть в базе.", number));
            }

            var today = DateTime.Today;
            var empty = default(DateTime);

            if (fine.ResolutionDate == empty)
                errors.Add("Укажите дату постановления.");
            else if (fine.ResolutionDate.Date > today)
                errors.Add("Дата постановления не может быть в будущем.");

            if (fine.ViolationDate == empty)
                errors.Add("Укажите дату нарушения.");
            else if (fine.ViolationDate.Date > today)
                errors.Add("Дата нарушения не может быть в будущем.");

            if (fine.ResolutionDate != empty && fine.ViolationDate != empty
                && fine.ViolationDate.Date > fine.ResolutionDate.Date)
                errors.Add("Дата нарушения не может быть позже даты постановления.");

            if (string.IsNullOrWhiteSpace(fine.CarVin))
                errors.Add("Выберите машину.");

            if (fine.Amount <= 0m)
                errors.Add("Сумма штрафа должна быть больше нуля.");

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
            fine.ViolationPlace = Trim(fine.ViolationPlace);
            fine.CarVin = (fine.CarVin ?? string.Empty).Trim();
            fine.ResolutionDate = fine.ResolutionDate.Date;
            fine.ViolationDate = fine.ViolationDate.Date;
        }

        private static string Trim(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            return value.Trim();
        }

        private static bool Contains(string source, string part)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrWhiteSpace(part))
                return false;
            return source.IndexOf(part.Trim(), StringComparison.CurrentCultureIgnoreCase) >= 0;
        }
    }
}
