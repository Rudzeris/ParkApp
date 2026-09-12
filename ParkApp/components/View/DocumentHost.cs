using System.Linq;
using ParkApp.components.ViewModel;

namespace ParkApp.components.View
{
    /// <summary>
    /// Куда открывать вкладку. Окон может быть несколько, и открываться она
    /// должна в том, с которым сейчас работают, — иначе новая вкладка уедет
    /// в окно, которое человек отодвинул в сторону.
    /// </summary>
    public static class DocumentHost
    {
        public static DocumentViewModel Open(object content, string title, string key)
        {
            var shell = Active();
            return shell == null ? null : shell.Shell.Open(content, title, key);
        }

        private static ShellWindow Active()
        {
            // полное имя обязательно: рядом лежит пространство имён ParkApp.components.Application
            var application = System.Windows.Application.Current;
            if (application == null)
                return null;

            var windows = application.Windows.OfType<ShellWindow>().ToList();
            return windows.FirstOrDefault(w => w.IsActive) ?? windows.FirstOrDefault();
        }
    }
}
