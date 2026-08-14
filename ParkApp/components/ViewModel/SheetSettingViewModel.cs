using ParkApp.components.Application;

namespace ParkApp.components.ViewModel
{
    /// <summary>
    /// Имя листа, у которого нет своего файла — справочные листы лежат
    /// в файле машин.
    /// </summary>
    public class SheetSettingViewModel : ViewModelBase
    {
        private string _name;

        public SheetSettingViewModel(SheetDescription description, string name)
        {
            Description = description;
            _name = name;
        }

        public SheetDescription Description { get; private set; }

        public string Title
        {
            get { return Description.Title; }
        }

        public string Name
        {
            get { return _name; }
            set { SetProperty(ref _name, value); }
        }
    }
}
