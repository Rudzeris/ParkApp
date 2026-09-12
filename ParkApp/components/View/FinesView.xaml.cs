using System.Windows.Controls;
using System.Windows.Input;
using ParkApp.components.ViewModel;

namespace ParkApp.components.View
{
    public partial class FinesView : UserControl
    {
        private bool _loaded;

        public FinesView()
        {
            InitializeComponent();

            // вкладку могут вытащить в другое окно — тогда Loaded придёт ещё
            // раз; перечитывать книгу в этот момент незачем
            Loaded += async (sender, args) =>
            {
                if (_loaded)
                    return;

                var model = DataContext as FineListViewModel;
                if (model == null)
                    return;

                _loaded = true;
                await model.InitializeAsync();
            };
        }

        /// <summary>
        /// Правая кнопка выделяет строку, над которой щёлкнули. Сам по себе
        /// WPF этого не делает: меню появилось бы над одной строкой, а команда
        /// применилась бы к другой — выделенной до этого.
        /// </summary>
        private void OnRowRightClick(object sender, MouseButtonEventArgs e)
        {
            var row = sender as ListViewItem;
            if (row == null)
                return;

            row.IsSelected = true;
            row.Focus();
        }
    }
}
