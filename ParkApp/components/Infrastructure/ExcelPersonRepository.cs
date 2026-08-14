using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ParkApp.components.Application;
using ParkApp.components.Domain;
using ParkApp.components.Infrastructure.Excel;

namespace ParkApp.components.Infrastructure
{
    /// <summary>
    /// Должностные лица из «Люди\Люди.xlsx». Как и машины, справочник пока только читается:
    /// редактор появится вместе с карточкой машины (этап 1).
    /// </summary>
    public class ExcelPersonRepository : IPersonRepository
    {
        private const string SheetName = "Люди";
        private static readonly object Sync = new object();

        public Task<IReadOnlyList<Person>> GetAllAsync()
        {
            lock (Sync)
            {
                IReadOnlyList<Person> people = Load();
                return Task.FromResult(people);
            }
        }

        private static List<Person> Load()
        {
            var path = AppPaths.PeopleFile;

            if (!File.Exists(path))
                CreateDemoFile(path);

            var sheet = XlsxReader.Read(path, SheetName);

            var idColumn = sheet.Column("№", "№ п/п", "Id", "Код");
            var positionColumn = sheet.Column("Должность");
            var rankColumn = sheet.Column("Звание", "Воинское звание");
            var nameColumn = sheet.Column("ФИО", "Ф.И.О.", "Фамилия, имя, отчество");
            var phoneColumn = sheet.Column("Телефон", "Номер телефона", "Тел.");

            if (nameColumn < 0)
                throw new InvalidOperationException(
                    "В файле «Люди.xlsx» не найден столбец «ФИО».");

            var people = new List<Person>();
            var autoId = 0;

            foreach (var row in sheet.Rows)
            {
                var fullName = SheetTable.GetString(row, nameColumn);
                if (fullName == null)
                    continue;

                autoId++;

                people.Add(new Person
                {
                    // если номера нет, нумеруем по порядку строк — так ссылка из машины
                    // всё равно попадёт в нужного человека
                    Id = SheetTable.GetInt(row, idColumn) ?? autoId,
                    Position = SheetTable.GetString(row, positionColumn),
                    Rank = SheetTable.GetString(row, rankColumn),
                    FullName = fullName,
                    Phone = SheetTable.GetString(row, phoneColumn)
                });
            }

            return people;
        }

        private static void CreateDemoFile(string path)
        {
            var headers = new[] { "№", "Должность", "Звание", "ФИО", "Телефон" };

            var rows = new List<IList<XlsxCell>>
            {
                new List<XlsxCell>
                {
                    XlsxCell.Number(1), XlsxCell.Text("Начальник гаража"), XlsxCell.Text("майор"),
                    XlsxCell.Text("Иванов Иван Иванович"), XlsxCell.Text("+7 900 000-00-01")
                },
                new List<XlsxCell>
                {
                    XlsxCell.Number(2), XlsxCell.Text("Водитель"), XlsxCell.Text("сержант"),
                    XlsxCell.Text("Петров Пётр Петрович"), XlsxCell.Text("+7 900 000-00-02")
                },
                new List<XlsxCell>
                {
                    XlsxCell.Number(3), XlsxCell.Text("Водитель"), XlsxCell.Text("рядовой"),
                    XlsxCell.Text("Сидоров Сидор Сидорович"), XlsxCell.Text("+7 900 000-00-03")
                }
            };

            XlsxWriter.Write(path, SheetName, headers, rows);
        }
    }
}
