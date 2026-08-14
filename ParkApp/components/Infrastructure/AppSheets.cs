using ParkApp.components.Application;

namespace ParkApp.components.Infrastructure
{
    /// <summary>
    /// Имена листов: выбранное пользователем или принятое по умолчанию.
    /// Как и пути, читается при каждом обращении — смена имени в настройках
    /// действует сразу.
    /// </summary>
    public static class AppSheets
    {
        private static IAppSettings _settings;

        public static void UseSettings(IAppSettings settings)
        {
            _settings = settings;
        }

        public static string Name(SheetKind sheet)
        {
            if (_settings != null)
            {
                var configured = _settings.GetSheetName(sheet);
                if (!string.IsNullOrWhiteSpace(configured))
                    return configured.Trim();
            }

            return SheetCatalog.DefaultName(sheet);
        }
    }
}
