namespace ParkApp.components.Application
{
    /// <summary>
    /// Выбор файла пользователем. Вынесен за ViewModel, чтобы та не зависела от WPF-диалогов.
    /// </summary>
    public interface IFileDialogService
    {
        /// <summary>
        /// Показывает диалог выбора файла. Возвращает путь или null, если пользователь отказался.
        /// </summary>
        string PickFile(string title, string filter);
    }
}
