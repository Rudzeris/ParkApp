namespace ParkApp.components.ViewModel
{
    /// <summary>
    /// Окна, которые нужны списку штрафов. Через этот интерфейс ViewModel
    /// открывает редактор и задаёт вопросы, ничего не зная про WPF-окна.
    /// </summary>
    public interface IFineDialogService
    {
        /// <summary>
        /// Открывает редактор записи. Он больше не модальный — это вкладка,
        /// и рядом можно держать открытой саму книгу. Поэтому результат
        /// возвращается не отсюда, а событием <see cref="FineEditViewModel.RequestClose"/>.
        /// </summary>
        void OpenEditor(FineEditViewModel viewModel);

        /// <summary>Вопрос «да/нет».</summary>
        bool Confirm(string message, string caption);

        /// <summary>Сообщение об ошибке.</summary>
        void ShowError(string message);
    }
}
