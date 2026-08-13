namespace ParkApp.components.Application
{
    /// <summary>
    /// Хранилище сканов документов. Скрывает от остальных слоёв то,
    /// где физически лежат файлы: локальная папка сейчас, сетевая шара потом.
    /// </summary>
    public interface IScanStorage
    {
        /// <summary>
        /// Копирует файл в хранилище и возвращает относительный путь для сохранения в записи.
        /// </summary>
        /// <param name="category">Раздел хранилища, например "Fines".</param>
        /// <param name="entityKey">Ключ записи, из него получается имя файла.</param>
        /// <param name="sourceFilePath">Путь к исходному файлу скана.</param>
        string Attach(string category, string entityKey, string sourceFilePath);

        /// <summary>Полный путь к скану по относительному.</summary>
        string GetFullPath(string relativePath);

        /// <summary>Существует ли файл скана.</summary>
        bool Exists(string relativePath);

        /// <summary>Открывает скан системным просмотрщиком.</summary>
        void Open(string relativePath);

        /// <summary>Удаляет файл скана. Отсутствие файла ошибкой не считается.</summary>
        void Delete(string relativePath);
    }
}
