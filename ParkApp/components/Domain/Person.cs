namespace ParkApp.components.Domain
{
    /// <summary>
    /// Должностное лицо. На него ссылается машина через <see cref="Car.OfficialId"/>.
    /// </summary>
    public class Person
    {
        /// <summary>Номер в таблице — ключ, по нему ссылаются другие таблицы.</summary>
        public int Id { get; set; }

        /// <summary>Должность.</summary>
        public string Position { get; set; }

        /// <summary>Воинское звание.</summary>
        public string Rank { get; set; }

        /// <summary>ФИО.</summary>
        public string FullName { get; set; }

        /// <summary>Номер телефона.</summary>
        public string Phone { get; set; }
    }
}
