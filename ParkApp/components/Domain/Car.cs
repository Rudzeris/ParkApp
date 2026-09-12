using System;
using System.Collections.Generic;

namespace ParkApp.components.Domain
{
    /// <summary>
    /// Машина. Ключ — VIN, на него ссылаются документы; но в рабочей таблице
    /// VIN заполнен далеко не у всех строк, поэтому там, где нужна ссылка,
    /// используется <see cref="Key"/>: VIN, а если его нет — номер строки.
    ///
    /// Почти все поля необязательные: в рабочей таблице у части машин данных нет,
    /// и приложение не должно отказываться такую строку читать.
    /// </summary>
    public class Car
    {
        /// <summary>Технический идентификатор в памяти. В таблице не хранится, ссылаться на него нельзя.</summary>
        public Guid Id { get; set; }

        /// <summary>VIN — ключ машины. Может быть пуст: в таблице он заполнен не везде.</summary>
        public string Vin { get; set; }

        /// <summary>Номер строки в таблице машин. Нужен, чтобы отличать строки без VIN.</summary>
        public int RowNumber { get; set; }

        /// <summary>
        /// Чем ссылаться на машину. VIN, если он есть; иначе — номер строки.
        /// Без этого все строки без VIN слились бы в одну.
        /// </summary>
        public string Key
        {
            get
            {
                return string.IsNullOrWhiteSpace(Vin)
                    ? "стр." + RowNumber.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : Vin.Trim();
            }
        }

        /// <summary>Марка автомобиля — как записана в таблице по-русски.</summary>
        public string Model { get; set; }

        /// <summary>
        /// Второе написание марки. В таблице два столбца «Марка автомобиля»:
        /// первый по-русски, второй латиницей («Тойота Фортунер» / «Toyota Fortuner»).
        /// </summary>
        public string ModelLatin { get; set; }

        /// <summary>Гос. регистрационные знаки: у машины их может быть 2–3.</summary>
        public List<CarNumber> Numbers { get; set; } = new List<CarNumber>();

        public int? Year { get; set; }

        /// <summary>Местонахождение: ППД или ВО (в таблице пишут «СВО»).</summary>
        public Location? Location { get; set; }

        /// <summary>Модель и № двигателя.</summary>
        public string EngineModel { get; set; }

        /// <summary>Мощность двигателя, кВт (первое число из «220/299»).</summary>
        public int? PowerKw { get; set; }

        /// <summary>Мощность двигателя, л.с. (второе число из «220/299»).</summary>
        public int? PowerHp { get; set; }

        /// <summary>№ шасси (рама).</summary>
        public string Chassis { get; set; }

        /// <summary>№ кузова.</summary>
        public string BodyNumber { get; set; }

        /// <summary>ПФМ.</summary>
        public string Pfm { get; set; }

        /// <summary>ПТС.</summary>
        public string Pts { get; set; }

        /// <summary>Свидетельство о регистрации.</summary>
        public string RegCertificate { get; set; }

        /// <summary>Страховой полис (номер из таблицы машин; отдельный учёт страховок — этап 4).</summary>
        public string InsurancePolicy { get; set; }

        /// <summary>
        /// Дата истечения страховки. В таблице она и отдельным столбцом,
        /// и хвостом в ячейке полиса («ТТТ 7095971328 23.04.2027») — берётся
        /// то, что нашлось.
        /// </summary>
        public DateTime? InsuranceEndsAt { get; set; }

        /// <summary>Диагностическая карта.</summary>
        public string DiagnosticCard { get; set; }

        /// <summary>Куда относится: гараж, обеспечение и т.д.</summary>
        public string Affiliation { get; set; }

        /// <summary>Ссылка на должностное лицо: <see cref="Person.Id"/>.</summary>
        public int? OfficialId { get; set; }

        /// <summary>
        /// Должностное лицо как оно записано в таблице машин. В рабочей книге
        /// эта ячейка — ссылка на лист «Должностные лица», поэтому имя приходит
        /// готовой строкой; номер из соседнего столбца остаётся связью.
        /// </summary>
        public string OfficialName { get; set; }

        /// <summary>Штатная (true) или вне штата (false).</summary>
        public bool? IsStaff { get; set; }

        /// <summary>Конфискат.</summary>
        public bool IsConfiscated { get; set; }

        /// <summary>Вместимость, человек.</summary>
        public int? Capacity { get; set; }

        /// <summary>Тип машины: легковой седан, легковой универсал, автобус и т.д.</summary>
        public string VehicleType { get; set; }

        public string Color { get; set; }

        /// <summary>Объём двигателя, см³.</summary>
        public int? EngineVolume { get; set; }

        /// <summary>Максимальная масса.</summary>
        public decimal? MaxMass { get; set; }

        /// <summary>Масса.</summary>
        public decimal? Mass { get; set; }

        /// <summary>Особые отметки.</summary>
        public string Notes { get; set; }

        /// <summary>Номер ВАИ.</summary>
        public string Vai { get; set; }

        /// <summary>Дата получения машины.</summary>
        public DateTime? ReceivedAt { get; set; }

        /// <summary>Дата передачи машины.</summary>
        public DateTime? HandedOverAt { get; set; }

        /// <summary>От кого получили машину.</summary>
        public string ReceivedFrom { get; set; }

        /// <summary>Кому отдали машину.</summary>
        public string HandedOverTo { get; set; }
    }
}
