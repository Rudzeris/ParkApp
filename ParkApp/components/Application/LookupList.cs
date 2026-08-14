namespace ParkApp.components.Application
{
    /// <summary>
    /// Справочные списки значений. Ведутся отдельными листами в файле машин:
    /// столбец «Название», под ним значения. Имена листов знает только Infrastructure.
    /// </summary>
    public enum LookupList
    {
        /// <summary>Куда относится машина: гараж, обеспечение и т.д.</summary>
        Affiliation,

        /// <summary>Тип машины: легковой седан, автобус и т.д.</summary>
        VehicleType
    }
}
