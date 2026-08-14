using System.Collections.Generic;

namespace ParkApp.components.ViewModel
{
    /// <summary>
    /// Спрашивает у пользователя имя листа. Показывает листы, которые есть в книге,
    /// но позволяет и вписать своё — книга могла не открыться целиком.
    /// </summary>
    public interface ISheetPicker
    {
        /// <summary>Возвращает выбранное имя листа или null, если пользователь отказался.</summary>
        string PickSheet(string message, IReadOnlyList<string> availableSheets, string currentName);
    }
}
