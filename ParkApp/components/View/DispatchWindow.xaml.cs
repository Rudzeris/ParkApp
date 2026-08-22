using System.Windows;
using System.Windows.Controls;
using ParkApp.components.ViewModel;

namespace ParkApp.components.View
{
    public partial class DispatchWindow : Window
    {
        public DispatchWindow(DispatchViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            // данные тянем после показа окна, чтобы конструктор не ждал файловых операций
            Loaded += async (sender, args) => await viewModel.InitializeAsync();
        }

        /// <summary>
        /// Щелчок по галочке заодно выделяет строку.
        ///
        /// Обычно в DataGrid это два действия: сначала выделить строку, потом
        /// отметить. Но здесь галочка — главное действие окна, и «выделить»
        /// не значит ничего своего: кнопка «путевой лист вне наряда» просто
        /// должна знать, о какой машине речь. Поэтому строка выделяется сама.
        /// </summary>
        private void OnMarkClick(object sender, RoutedEventArgs e)
        {
            var box = sender as CheckBox;
            if (box == null)
                return;

            var grid = Cars;
            if (grid == null)
                return;

            grid.SelectedItem = box.DataContext;
        }
    }
}
