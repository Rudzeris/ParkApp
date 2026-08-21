namespace ParkApp.components.Application
{
    /// <summary>
    /// Названия реквизитов для печати. Хранятся строками на листе «Реквизиты»
    /// файла нарядов, чтобы заказчик правил их в Excel, а не в коде.
    /// </summary>
    public static class PrintDetail
    {
        public const string UnitName = "Наименование воинской части";
        public const string OrderPurpose = "Наряд: на использование машин";

        public const string AgreedPosition = "Согласовано: должность";
        public const string AgreedRank = "Согласовано: звание";
        public const string AgreedName = "Согласовано: И. Фамилия";

        public const string ApprovedPosition = "Утверждаю: должность";
        public const string ApprovedRank = "Утверждаю: звание";
        public const string ApprovedName = "Утверждаю: И. Фамилия";

        public const string SignedPosition = "Наряд подписал: должность";
        public const string SignedRank = "Наряд подписал: звание";
        public const string SignedName = "Наряд подписал: И. Фамилия";

        public const string BriefingPosition = "Инструктаж провёл: должность";
        public const string BriefingRank = "Инструктаж провёл: звание";
        public const string BriefingName = "Инструктаж провёл: И. Фамилия";

        public const string ResponsiblePosition = "Ответственный за эксплуатацию: должность";
        public const string ResponsibleRank = "Ответственный за эксплуатацию: звание";
        public const string ResponsibleName = "Ответственный за эксплуатацию: И. Фамилия";

        public const string PermissionPosition = "Разрешение подписал: должность";
        public const string PermissionRank = "Разрешение подписал: звание";
        public const string PermissionName = "Разрешение подписал: И. Фамилия";

        public const string DefaultOperationGroup = "Группа эксплуатации по умолчанию";
        public const string DefaultCargo = "Перевозимый груз по умолчанию";
    }
}
