namespace ParkApp.components.Application
{
    /// <summary>
    /// Выбор файла и папки пользователем. Вынесен за ViewModel, чтобы та не зависела от WPF-диалогов.
    /// </summary>
    public interface IFileDialogService
    {
        /// <summary>
        /// Показывает диалог выбора файла. Возвращает путь или null, если пользователь отказался.
        /// </summary>
        string PickFile(string title, string filter);

        /// <summary>
        /// Показывает диалог выбора папки. Возвращает путь или null, если пользователь отказался.
        /// </summary>
        string PickFolder(string title, string initialPath);
    }
}
