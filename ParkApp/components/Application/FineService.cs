
using System.Collections.Generic;
using System.Threading.Tasks;
using ParkApp.components.Domain;

namespace ParkApp.components.Application
{
    public class FineService
    {
        private readonly IFineRecordRepository _repository;

        public FineService(IFineRecordRepository repository)
        {
            _repository = repository;
        }

        public Task<IReadOnlyList<FineRecord>> GetAllAsync() => _repository.GetAllAsync();
    }
}