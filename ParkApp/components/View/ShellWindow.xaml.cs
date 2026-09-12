using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ParkApp.components.ViewModel;

namespace ParkApp.components.View
{
    /// <summary>
    /// Окно с вкладками. Вкладку можно вытащить мышью — она станет отдельным
    /// окном такого же устройства, и дальше окна живут сами по себе.
    /// </summary>
    public partial class ShellWindow : Window
    {
        /// <summary>Насколько нужно утащить вкладку вниз, чтобы это считалось отрыванием.</summary>
        private const double TearOff = 40;

        private readonly ShellHostViewModel _host;

        private Point _pressed;
        private DocumentViewModel _dragged;

        public ShellWindow(ShellHostViewModel host)
        {
            InitializeComponent();

            _host = host;
            DataContext = host;
        }

        /// <summary>Окно под одну вытащенную вкладку: без панели разделов.</summary>
        public static ShellWindow ForDocument(DocumentViewModel document, Point at)
        {
            var host = new ShellHostViewModel(null, new ShellViewModel(document));

            return new ShellWindow(host)
            {
                Title = document.Title,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Width = 1100,
                Height = 640,
                Left = at.X - 120,
                Top = at.Y - 16
            };
        }

        public ShellViewModel Shell
        {
            get { return _host.Shell; }
        }

        private void OnTabPressed(object sender, MouseButtonEventArgs e)
        {
            _pressed = e.GetPosition(this);
            _dragged = DocumentOf(sender);
        }

        private void OnTabReleased(object sender, MouseButtonEventArgs e)
        {
            _dragged = null;
        }

        /// <summary>
        /// Вкладку тащат вниз — значит, хотят отдельное окно. Движение вбок
        /// не считается: вбок вкладки таскают, чтобы поменять местами, и
        /// отрывать их при этом было бы неожиданно.
        /// </summary>
        private void OnTabDragged(object sender, MouseEventArgs e)
        {
            if (_dragged == null || e.LeftButton != MouseButtonState.Pressed)
                return;

            var moved = e.GetPosition(this) - _pressed;
            if (moved.Y < TearOff)
                return;

            var document = _dragged;
            _dragged = null;

            Detach(document, PointToScreen(e.GetPosition(this)));
        }

        private void Detach(DocumentViewModel document, Point at)
        {
            if (Shell.Detach(document) == null)
                return;

            var window = ForDocument(document, at);
            window.Show();
            window.Activate();

            CloseIfEmpty();
        }

        /// <summary>
        /// Окно, оставшееся без вкладок, закрывается — кроме главного:
        /// в нём панель разделов, и без него открывать станет нечем.
        /// </summary>
        private void CloseIfEmpty()
        {
            if (_host.HasSections || !Shell.IsEmpty)
                return;

            Close();
        }

        private static DocumentViewModel DocumentOf(object sender)
        {
            var tab = sender as TabItem;
            return tab == null ? null : tab.DataContext as DocumentViewModel;
        }
    }
}
