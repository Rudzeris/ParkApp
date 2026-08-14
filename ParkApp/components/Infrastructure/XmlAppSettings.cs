using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using ParkApp.components.Application;

namespace ParkApp.components.Infrastructure
{
    /// <summary>
    /// Настройки в «Настройки.xml» рядом с приложением.
    ///
    /// Именно рядом с приложением, а не в папке данных: в настройках лежит путь
    /// к этой самой папке, и класть их внутрь неё — замкнутый круг.
    /// Файл, а не реестр: рабочее место переносится копированием каталога.
    /// </summary>
    public class XmlAppSettings : IAppSettings
    {
        private const string FileName = "Настройки.xml";

        private readonly HashSet<AppSection> _enabled = new HashSet<AppSection>();
        private readonly Dictionary<PathSetting, string> _paths = new Dictionary<PathSetting, string>();
        private readonly Dictionary<SheetKind, string> _sheets = new Dictionary<SheetKind, string>();

        public XmlAppSettings()
        {
            Load();
        }

        private static string FilePath
        {
            get { return Path.Combine(AppPaths.AppFolder, FileName); }
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

        public string GetPath(PathSetting setting)
        {
            string value;
            return _paths.TryGetValue(setting, out value) ? value : null;
        }

        public void SetPath(PathSetting setting, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                _paths.Remove(setting);
            else
                _paths[setting] = path.Trim();
        }

        public string GetSheetName(SheetKind sheet)
        {
            string value;
            return _sheets.TryGetValue(sheet, out value) ? value : null;
        }

        public void SetSheetName(SheetKind sheet, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                _sheets.Remove(sheet);
            else
                _sheets[sheet] = name.Trim();
        }

        public void Save()
        {
            var document = new XDocument(
                new XComment(" Настройки этого рабочего места. Меняются в приложении: Настройки. "),
                new XElement("settings",
                    new XElement("sections",
                        SectionCatalog.All
                            .Where(s => s.IsAvailable)
                            .Select(s => new XElement("section",
                                new XAttribute("name", s.Section.ToString()),
                                new XAttribute("enabled", _enabled.Contains(s.Section))))),
                    new XElement("paths",
                        _paths.Select(pair => new XElement("path",
                            new XAttribute("name", pair.Key.ToString()),
                            new XAttribute("value", pair.Value)))),
                    new XElement("sheets",
                        _sheets.Select(pair => new XElement("sheet",
                            new XAttribute("name", pair.Key.ToString()),
                            new XAttribute("value", pair.Value))))));

            AppPaths.EnsureFolderFor(FilePath);
            document.Save(FilePath);
        }

        private void Load()
        {
            _enabled.Clear();
            _paths.Clear();
            _sheets.Clear();

            if (!File.Exists(FilePath))
            {
                EnableAvailableSections();
                return;
            }

            try
            {
                var document = XDocument.Load(FilePath);
                if (document.Root == null)
                {
                    EnableAvailableSections();
                    return;
                }

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

                foreach (var element in document.Root.Descendants("sheet"))
                {
                    var sheetName = (string)element.Attribute("name");
                    var sheetValue = (string)element.Attribute("value");

                    if (string.IsNullOrWhiteSpace(sheetName) || string.IsNullOrWhiteSpace(sheetValue))
                        continue;

                    SheetKind sheet;
                    if (Enum.TryParse(sheetName, true, out sheet))
                        _sheets[sheet] = sheetValue.Trim();
                }

                foreach (var element in document.Root.Descendants("path"))
                {
                    var name = (string)element.Attribute("name");
                    var value = (string)element.Attribute("value");

                    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(value))
                        continue;

                    PathSetting setting;
                    if (Enum.TryParse(name, true, out setting))
                        _paths[setting] = value.Trim();
                }
            }
            catch (Exception)
            {
                // испорченный файл настроек не повод не запускаться: включаем готовые разделы
                EnableAvailableSections();
            }
        }

        private void EnableAvailableSections()
        {
            foreach (var section in SectionCatalog.All.Where(s => s.IsAvailable))
                _enabled.Add(section.Section);
        }
    }
}
