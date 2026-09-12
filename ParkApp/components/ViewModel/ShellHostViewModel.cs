namespace ParkApp.components.ViewModel
{
    /// <summary>
    /// Содержимое окна: набор вкладок и — только у главного окна — панель
    /// разделов. Окно, в которое вытащили вкладку, панели не имеет: разделы
    /// открывают там, где работают, а не по копии панели в каждом окне.
    /// </summary>
    public class ShellHostViewModel : ViewModelBase
    {
        public ShellHostViewModel(MainViewModel main, ShellViewModel shell)
        {
            Main = main;
            Shell = shell ?? new ShellViewModel();
        }

        /// <summary>Панель разделов. null — окно без панели.</summary>
        public MainViewModel Main { get; private set; }

        public ShellViewModel Shell { get; private set; }

        public bool HasSections
        {
            get { return Main != null; }
        }
    }
}
