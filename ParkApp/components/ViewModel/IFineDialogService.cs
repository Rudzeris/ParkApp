namespace ParkApp.components.ViewModel
{
    /// <summary>
    /// Окна, которые нужны списку штрафов. Через этот интерфейс ViewModel
    /// открывает редактор и задаёт вопросы, ничего не зная про WPF-окна.
    /// </summary>
    public interface IFineDialogService
    {
        /// <summary>Показывает модальный редактор штрафа. true — пользователь сохранил.</summary>
        bool ShowEditor(FineEditViewModel viewModel);

        /// <summary>Вопрос «да/нет».</summary>
        bool Confirm(string message, string caption);

        /// <summary>Сообщение об ошибке.</summary>
        void ShowError(string message);
    }
}
