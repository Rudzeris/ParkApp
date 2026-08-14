using System;
using System.IO;
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

        /// <summary>
        /// Выбор папки. В WPF своего диалога нет, поэтому берётся стандартный
        /// из Windows Forms — он есть в .NET Framework и лишних зависимостей не тянет.
        /// </summary>
        public string PickFolder(string title, string initialPath)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = title;
                dialog.ShowNewFolderButton = true;

                if (!string.IsNullOrWhiteSpace(initialPath) && Directory.Exists(initialPath))
                    dialog.SelectedPath = initialPath;

                return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK
                    ? dialog.SelectedPath
                    : null;
            }
        }
    }
}
