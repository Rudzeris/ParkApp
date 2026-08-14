using System.Collections.Generic;
using System.Windows;

namespace ParkApp.components.View
{
    public partial class SheetPickWindow : Window
    {
        public SheetPickWindow(string message, IEnumerable<string> availableSheets, string currentName)
        {
            InitializeComponent();

            MessageText.Text = message;

            foreach (var sheet in availableSheets)
                SheetBox.Items.Add(sheet);

            SheetBox.Text = currentName ?? string.Empty;
        }

        /// <summary>Введённое или выбранное имя листа.</summary>
        public string SheetName
        {
            get { return (SheetBox.Text ?? string.Empty).Trim(); }
        }

        private void OnOkClick(object sender, RoutedEventArgs e)
        {
            if (SheetName.Length == 0)
            {
                MessageBox.Show("Укажите имя листа.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
