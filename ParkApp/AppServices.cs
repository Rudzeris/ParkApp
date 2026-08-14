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
        public static PersonService People { get; private set; }
        public static LookupService Lookups { get; private set; }
        public static FineService Fines { get; private set; }
        public static IScanStorage Scans { get; private set; }
        public static IFileDialogService FileDialogs { get; private set; }
        public static IFineDialogService FineDialogs { get; private set; }
        public static IMainDialogService MainDialogs { get; private set; }
        public static IMessageService Messages { get; private set; }
        public static ITableFileValidator TableFiles { get; private set; }
        public static ISheetPicker SheetPicker { get; private set; }
        public static IAppSettings Settings { get; private set; }

        public static void Initialize()
        {
            // настройки читаются первыми: в них лежат пути, по которым работают репозитории
            Settings = new XmlAppSettings();
            AppPaths.UseSettings(Settings);
            AppSheets.UseSettings(Settings);

            // хранилище выбирается здесь и только здесь: замена Excel на БД —
            // это несколько строк ниже, остальные слои не меняются
            ICarRepository carRepository = new ExcelCarRepository();
            IPersonRepository personRepository = new ExcelPersonRepository();
            ILookupRepository lookupRepository = new ExcelLookupRepository();
            IFineRepository fineRepository = new ExcelFineRepository();

            Cars = new CarService(carRepository);
            People = new PersonService(personRepository);
            Lookups = new LookupService(lookupRepository);
            Fines = new FineService(fineRepository);

            Scans = new FileScanStorage();
            FileDialogs = new WpfFileDialogService();
            FineDialogs = new WpfFineDialogService();
            Messages = new WpfMessageService();
            TableFiles = new ExcelTableValidator();
            SheetPicker = new WpfSheetPicker();
            MainDialogs = new WpfMainDialogService(CreateFineListViewModel);
        }

        public static MainViewModel CreateMainViewModel()
        {
            return new MainViewModel(
                CreateCarListViewModel(),
                Settings,
                MainDialogs,
                CreateSettingsViewModel);
        }

        public static CarListViewModel CreateCarListViewModel()
        {
            return new CarListViewModel(Cars, People, Lookups);
        }

        public static FineListViewModel CreateFineListViewModel()
        {
            return new FineListViewModel(Fines, Cars, People, Scans, FineDialogs, FileDialogs);
        }

        public static SettingsViewModel CreateSettingsViewModel()
        {
            return new SettingsViewModel(
                Settings, FileDialogs, TableFiles, Messages, SheetPicker, AppPaths.DefaultPath);
        }
    }
}
