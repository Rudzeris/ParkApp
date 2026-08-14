namespace ParkApp.components.Application
{
    /// <summary>
    /// К какому документу относится скан. Имена папок на диске выбирает
    /// хранилище, поэтому ViewModel не знает про раскладку каталогов.
    /// </summary>
    public enum ScanCategory
    {
        /// <summary>Постановление о штрафе.</summary>
        Fine,

        /// <summary>Документы машины: СРТС, ПТС.</summary>
        Car,

        /// <summary>Полис ОСАГО.</summary>
        Insurance,

        /// <summary>Заявка на ТО, акт Ф-12.</summary>
        Maintenance
    }
}
