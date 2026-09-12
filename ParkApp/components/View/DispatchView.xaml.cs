using System.Windows;
using System.Windows.Controls;
using ParkApp.components.ViewModel;

namespace ParkApp.components.View
{
    public partial class DispatchView : UserControl
    {
        private bool _loaded;

        public DispatchView()
        {
            InitializeComponent();

            Loaded += async (sender, args) =>
            {
                if (_loaded)
                    return;

                var model = DataContext as DispatchViewModel;
                if (model == null)
                    return;

                _loaded = true;
                await model.InitializeAsync();
            };
        }

        /// <summary>
        /// Щелчок по галочке заодно выделяет строку: «выделить» здесь не значит
        /// ничего своего, кнопке «путевой лист вне наряда» просто нужно знать,
        /// о какой машине речь.
        /// </summary>
        private void OnMarkClick(object sender, RoutedEventArgs e)
        {
            var box = sender as CheckBox;
            if (box == null)
                return;

            Cars.SelectedItem = box.DataContext;
        }
    }
}
