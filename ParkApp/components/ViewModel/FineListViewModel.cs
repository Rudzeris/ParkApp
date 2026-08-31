using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ParkApp.components.Application;
using ParkApp.components.Domain;

namespace ParkApp.components.ViewModel
{
    public class FineListViewModel
    {
        private readonly FineService _service;

        public FineListViewModel(FineService service)
        {
            _service = service;
            Load();
        }

        public List<FineRecord> FineRecords { get; } = new List<FineRecord>();

        private async Task Load()
        {
            var fines = await _service.GetAllAsync();
            FineRecords.Clear();
            foreach (var fine in fines) FineRecords.Add(fine);
        }
    }
}