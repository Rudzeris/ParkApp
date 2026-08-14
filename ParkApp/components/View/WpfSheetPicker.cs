using System.Collections.Generic;
using System.Linq;
using System.Windows;
using ParkApp.components.ViewModel;

namespace ParkApp.components.View
{
    public class WpfSheetPicker : ISheetPicker
    {
        public string PickSheet(string message, IReadOnlyList<string> availableSheets, string currentName)
        {
            var window = new SheetPickWindow(message, availableSheets, currentName);

            var owner = FindActiveWindow();
            if (owner != null && !ReferenceEquals(owner, window))
                window.Owner = owner;

            return window.ShowDialog() == true ? window.SheetName : null;
        }

        private static Window FindActiveWindow()
        {
            // полное имя обязательно: рядом лежит пространство имён ParkApp.components.Application
            var application = System.Windows.Application.Current;
            if (application == null)
                return null;

            return application.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
        }
    }
}
