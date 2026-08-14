using ParkApp.components.Application;
using ParkApp.components.Infrastructure;
using ParkApp.components.View;
using ParkApp.components.ViewModel;

namespace ParkApp
{
    /// <summary>
    /// Композиционный корень: единственное место, где выбираются конкретные реализации.
    /// Полноценный DI-контейнер на текущем количестве сервисов не окупается,
    /// но точка сборки одна — заменить её на контейнер можно правкой этого файла.
    /// </summary>
    public static class AppServices
    {
        public static CarService Cars { get; private set; }
        public static FineService Fines { get; private set; }
        public static IScanStorage Scans { get; private set; }
        public static IFileDialogService FileDialogs { get; private set; }
        public static IFineDialogService FineDialogs { get; private set; }

        public static void Initialize()
        {
            // хранилище выбирается здесь и только здесь: замена Excel на БД —
            // это две строки ниже, остальные слои не меняются
            ICarRepository carRepository = new ExcelCarRepository();
            IFineRepository fineRepository = new ExcelFineRepository();

            Cars = new CarService(carRepository);
            Fines = new FineService(fineRepository);
            Scans = new FileScanStorage();
            FileDialogs = new WpfFileDialogService();
            FineDialogs = new WpfFineDialogService();
        }

        public static CarListViewModel CreateCarListViewModel()
        {
            return new CarListViewModel(Cars);
        }

        public static FineListViewModel CreateFineListViewModel()
        {
            return new FineListViewModel(Fines, Cars, Scans, FineDialogs, FileDialogs);
        }
    }
}
