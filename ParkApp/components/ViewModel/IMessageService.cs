namespace ParkApp.components.ViewModel
{
    /// <summary>Сообщения пользователю без привязки к WPF.</summary>
    public interface IMessageService
    {
        void ShowWarning(string message);
        void ShowError(string message);
    }
}
