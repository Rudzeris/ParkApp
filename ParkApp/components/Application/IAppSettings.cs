namespace ParkApp.components.Application
{
    /// <summary>
    /// Настройки рабочего места. Хранятся у каждого свои: один ведёт штрафы,
    /// другой путевые листы, и лишние разделы им не нужны.
    /// </summary>
    public interface IAppSettings
    {
        /// <summary>Включён ли раздел. Нереализованные разделы всегда выключены.</summary>
        bool IsSectionEnabled(AppSection section);

        void SetSectionEnabled(AppSection section, bool enabled);

        /// <summary>Записывает настройки на диск.</summary>
        void Save();
    }
}
