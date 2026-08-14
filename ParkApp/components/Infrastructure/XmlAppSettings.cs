using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using ParkApp.components.Application;

namespace ParkApp.components.Infrastructure
{
    /// <summary>
    /// Настройки в «Настройки.xml» рядом со своими документами.
    /// Файл, а не реестр: рабочее место переносится копированием папки.
    /// При первом запуске включены все готовые разделы.
    /// </summary>
    public class XmlAppSettings : IAppSettings
    {
        private const string FileName = "Настройки.xml";

        private readonly HashSet<AppSection> _enabled = new HashSet<AppSection>();

        public XmlAppSettings()
        {
            Load();
        }

        private static string FilePath
        {
            get { return Path.Combine(AppPaths.DataRoot, FileName); }
        }

        public bool IsSectionEnabled(AppSection section)
        {
            // выключить нереализованный раздел нельзя случайно — он и так недоступен
            var description = SectionCatalog.All.FirstOrDefault(s => s.Section == section);
            if (description == null || !description.IsAvailable)
                return false;

            return _enabled.Contains(section);
        }

        public void SetSectionEnabled(AppSection section, bool enabled)
        {
            if (enabled)
                _enabled.Add(section);
            else
                _enabled.Remove(section);
        }

        public void Save()
        {
            var document = new XDocument(
                new XComment(" Разделы, включённые на этом рабочем месте. Меняются в приложении: Настройки. "),
                new XElement("settings",
                    new XElement("sections",
                        SectionCatalog.All
                            .Where(s => s.IsAvailable)
                            .Select(s => new XElement("section",
                                new XAttribute("name", s.Section.ToString()),
                                new XAttribute("enabled", _enabled.Contains(s.Section)))))));

            AppPaths.EnsureFolderFor(FilePath);
            document.Save(FilePath);
        }

        private void Load()
        {
            _enabled.Clear();

            if (!File.Exists(FilePath))
            {
                // первый запуск: показываем всё, что готово
                foreach (var section in SectionCatalog.All.Where(s => s.IsAvailable))
                    _enabled.Add(section.Section);

                return;
            }

            try
            {
                var document = XDocument.Load(FilePath);
                if (document.Root == null)
                    return;

                foreach (var element in document.Root.Descendants("section"))
                {
                    var name = (string)element.Attribute("name");
                    var enabled = (bool?)element.Attribute("enabled") ?? false;

                    if (!enabled || string.IsNullOrWhiteSpace(name))
                        continue;

                    AppSection section;
                    if (Enum.TryParse(name, true, out section))
                        _enabled.Add(section);
                }
            }
            catch (Exception)
            {
                // испорченный файл настроек не повод не запускаться: включаем готовые разделы
                foreach (var section in SectionCatalog.All.Where(s => s.IsAvailable))
                    _enabled.Add(section.Section);
            }
        }
    }
}
