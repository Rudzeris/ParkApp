using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ParkApp.components.Application;
using ParkApp.components.Domain;

namespace ParkApp.components.Infrastructure
{
    /// <summary>
    /// Хранение штрафов в fines.json рядом с приложением.
    /// Файл читается и пишется целиком: на ожидаемых объёмах (тысячи записей) этого достаточно,
    /// а дальше по плану переезд на СУБД, а не усложнение работы с файлом.
    /// </summary>
    public class JsonFineRepository : IFineRepository
    {
        private const string FileName = "fines.json";
        private static readonly object Sync = new object();

        private static string FilePath
        {
            get { return AppPaths.DataFile(FileName); }
        }

        public Task<IReadOnlyList<Fine>> GetAllAsync()
        {
            lock (Sync)
            {
                IReadOnlyList<Fine> fines = LoadCore();
                return Task.FromResult(fines);
            }
        }

        public Task<Fine> GetByNumberAsync(string resolutionNumber)
        {
            lock (Sync)
            {
                var key = (resolutionNumber ?? string.Empty).Trim();
                var fine = LoadCore().FirstOrDefault(f => SameNumber(f.ResolutionNumber, key));
                return Task.FromResult(fine);
            }
        }

        public Task AddAsync(Fine fine)
        {
            if (fine == null)
                throw new ArgumentNullException("fine");

            lock (Sync)
            {
                var fines = LoadCore();
                if (fines.Any(f => SameNumber(f.ResolutionNumber, fine.ResolutionNumber)))
                    throw new InvalidOperationException(
                        string.Format("Постановление № {0} уже есть в базе.", fine.ResolutionNumber));

                fines.Add(fine);
                SaveCore(fines);
            }

            return Task.CompletedTask;
        }

        public Task UpdateAsync(Fine fine)
        {
            if (fine == null)
                throw new ArgumentNullException("fine");

            lock (Sync)
            {
                var fines = LoadCore();
                var index = fines.FindIndex(f => SameNumber(f.ResolutionNumber, fine.ResolutionNumber));
                if (index < 0)
                    throw new InvalidOperationException(
                        string.Format("Постановление № {0} не найдено.", fine.ResolutionNumber));

                fines[index] = fine;
                SaveCore(fines);
            }

            return Task.CompletedTask;
        }

        public Task DeleteAsync(string resolutionNumber)
        {
            lock (Sync)
            {
                var fines = LoadCore();
                fines.RemoveAll(f => SameNumber(f.ResolutionNumber, resolutionNumber));
                SaveCore(fines);
            }

            return Task.CompletedTask;
        }

        private static List<Fine> LoadCore()
        {
            if (!File.Exists(FilePath))
            {
                // демо-данные сохраняются сразу, иначе они «воскресали» бы после каждого удаления
                var seed = CreateSeed();
                SaveCore(seed);
                return seed;
            }

            var json = File.ReadAllText(FilePath);
            if (string.IsNullOrWhiteSpace(json))
                return new List<Fine>();

            return JsonConvert.DeserializeObject<List<Fine>>(json) ?? new List<Fine>();
        }

        private static void SaveCore(List<Fine> fines)
        {
            var json = JsonConvert.SerializeObject(fines, Formatting.Indented);
            File.WriteAllText(FilePath, json);
        }

        private static bool SameNumber(string left, string right)
        {
            return string.Equals(
                (left ?? string.Empty).Trim(),
                (right ?? string.Empty).Trim(),
                StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Демонстрационные штрафы на машины из сида JsonCarRepository.</summary>
        private static List<Fine> CreateSeed()
        {
            var today = DateTime.Today;

            return new List<Fine>
            {
                new Fine
                {
                    ResolutionNumber = "18810516250401234567",
                    ResolutionDate = today.AddDays(-20),
                    ViolationDate = today.AddDays(-25),
                    ViolationPlace = "г. Казань, пр. Победы, 12",
                    CarVin = "VIN1",
                    DriverName = "Иванов И.И.",
                    Amount = 500m
                },
                new Fine
                {
                    ResolutionNumber = "18810516250405554443",
                    ResolutionDate = today.AddDays(-8),
                    ViolationDate = today.AddDays(-10),
                    ViolationPlace = "трасса М-7, 812 км",
                    CarVin = "VIN2",
                    DriverName = "Петров П.П.",
                    Amount = 1500m
                },
                new Fine
                {
                    ResolutionNumber = "18810516250409876543",
                    ResolutionDate = today.AddDays(-3),
                    ViolationDate = today.AddDays(-3),
                    ViolationPlace = null,
                    CarVin = "VIN2",
                    DriverName = null,
                    Amount = 800m
                }
            };
        }
    }
}
