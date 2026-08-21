using System;
using System.Diagnostics;
using System.IO;
using ParkApp.components.Application;

namespace ParkApp.components.Infrastructure
{
    public class ShellFileLauncher : IFileLauncher
    {
        public void Open(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new FileNotFoundException(string.Format("Файл не найден: {0}", path));

            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
    }
}
