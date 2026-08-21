namespace ParkApp.components.Application
{
    /// <summary>Открывает готовый файл системным приложением — Word, просмотрщиком PDF.</summary>
    public interface IFileLauncher
    {
        void Open(string path);
    }
}
