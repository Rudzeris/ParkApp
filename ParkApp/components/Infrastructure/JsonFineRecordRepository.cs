using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ParkApp.components.Application;
using ParkApp.components.Domain;

namespace ParkApp.components.Infrastructure
{
    public class JsonFineRecordRepository : IFineRecordRepository
    {
        private const string FilePath = "fineRecord.json";

        public async Task<IReadOnlyList<FineRecord>> GetAllAsync()
        {
            return await LoadAsync();
        }

        private async Task<List<FineRecord>> LoadAsync()
        {
            if (!File.Exists(FilePath))
                return new List<FineRecord>
                {
                    new FineRecord
                    {
                        CarBrand = "TLC 200", Plate = "1234 ЕХ"
                    },
                    new FineRecord
                    {
                        CarBrand = "Lada Niva", Plate = "2345 АВ"
                    }
                };
            else
                return JsonConvert
                    .DeserializeObject<List<FineRecord>>(File.ReadAllText(FilePath));
        }

        private async Task SaveAsync(IReadOnlyList<FineRecord> list)
        {
            File.WriteAllText(FilePath, JsonConvert.SerializeObject(list));
        }

        public async Task AddAsync(FineRecord car)
        {
            throw new NotImplementedException();
        }

        public async Task UpdateAsync(FineRecord car)
        {
            throw new NotImplementedException();
        }

        public async Task DeleteAsync(Guid id)
        {
            throw new NotImplementedException();
        }
    }
}