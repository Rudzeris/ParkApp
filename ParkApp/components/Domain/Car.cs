using System;
using System.Collections.Generic;

namespace ParkApp.components.Domain
{
    /// <summary>
    /// Машина. Ключ — VIN; на него ссылаются штрафы, путевые листы и остальные документы.
    ///
    /// Почти все поля необязательные: в рабочей таблице у части машин данных нет,
    /// и приложение не должно отказываться такую строку читать.
    /// </summary>
    public class Car
    {
        /// <summary>Технический идентификатор в памяти. В таблице не хранится, ссылаться на него нельзя.</summary>
        public Guid Id { get; set; }

        /// <summary>VIN — ключ машины.</summary>
        public string Vin { get; set; }

        /// <summary>Марка автомобиля.</summary>
        public string Model { get; set; }

        /// <summary>Гос. регистрационные знаки: у машины их может быть 2–3.</summary>
        public List<CarNumber> Numbers { get; set; } = new List<CarNumber>();

        public int? Year { get; set; }

        /// <summary>Местонахождение: ППД или ВО.</summary>
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

        /// <summary>Диагностическая карта.</summary>
        public string DiagnosticCard { get; set; }

        /// <summary>Куда относится: гараж, обеспечение и т.д.</summary>
        public string Affiliation { get; set; }

        /// <summary>Ссылка на должностное лицо: <see cref="Person.Id"/>.</summary>
        public int? OfficialId { get; set; }

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
    }
}
