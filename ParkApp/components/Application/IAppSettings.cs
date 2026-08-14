namespace ParkApp.components.Application
{
    /// <summary>
    /// Настройки рабочего места. Хранятся у каждого свои: один ведёт штрафы,
    /// другой путевые листы, и лишние разделы им не нужны. Здесь же пути к данным,
    /// чтобы папку с таблицами можно было переназначить без правки конфигов.
    /// </summary>
    public interface IAppSettings
    {
        /// <summary>Включён ли раздел. Нереализованные разделы всегда выключены.</summary>
        bool IsSectionEnabled(AppSection section);

        void SetSectionEnabled(AppSection section, bool enabled);

        /// <summary>Заданный путь или null, если используется значение по умолчанию.</summary>
        string GetPath(PathSetting setting);

        /// <summary>Пустое значение возвращает путь к значению по умолчанию.</summary>
        void SetPath(PathSetting setting, string path);

        /// <summary>Выбранное имя листа или null, если используется имя по умолчанию.</summary>
        string GetSheetName(SheetKind sheet);

        /// <summary>Пустое значение возвращает имя по умолчанию.</summary>
        void SetSheetName(SheetKind sheet, string name);

        /// <summary>Записывает настройки на диск.</summary>
        void Save();
    }
}
