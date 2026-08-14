using Microsoft.Win32;
using ParkApp.components.Application;

namespace ParkApp.components.Infrastructure
{
    public class WpfFileDialogService : IFileDialogService
    {
        public string PickFile(string title, string filter)
        {
            var dialog = new OpenFileDialog
            {
                Title = title,
                Filter = filter,
                CheckFileExists = true,

                // без этого выбор файла меняет текущий каталог процесса,
                // что ломает относительные пути
                RestoreDirectory = true
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }
    }
}
