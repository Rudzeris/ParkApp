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
        public static string SheetName
        {
            get { return AppSheets.Name(SheetKind.People); }
        }

        public static readonly string[] IdNames = { "№", "№ п/п", "Id", "Код" };
        public static readonly string[] FullNameNames = { "ФИО", "Ф.И.О.", "Фамилия, имя, отчество" };
        public static readonly string[] PositionNames = { "Должность" };
        public static readonly string[] RankNames = { "Звание", "Воинское звание" };
        public static readonly string[] PhoneNames = { "Телефон", "Номер телефона", "Тел." };
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
            {
                if (AppPaths.IsConfigured(PathSetting.PeopleFile) || AppPaths.IsConfigured(PathSetting.SharedRoot))
                    throw new FileNotFoundException(string.Format(
                        "Файл людей не найден:{0}{1}{0}{0}Проверьте путь в настройках — возможно, файл переместили.",
                        Environment.NewLine, path));

                CreateDemoFile(path);
            }

            // строго по имени: имя листа настраивается, и молча прочитать
            // вместо него первый лист книги было бы хуже, чем сказать об ошибке
            var sheet = XlsxReader.ReadOrNull(path, SheetName);
            if (sheet == null)
                throw new InvalidOperationException(string.Format(
                    "В файле «{0}» нет листа «{1}».{2}{2}Укажите имя листа в настройках.",
                    Path.GetFileName(path), SheetName, Environment.NewLine));

            var idColumn = sheet.Column(IdNames);
            var positionColumn = sheet.Column(PositionNames);
            var rankColumn = sheet.Column(RankNames);
            var nameColumn = sheet.Column(FullNameNames);
            var phoneColumn = sheet.Column(PhoneNames);

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
