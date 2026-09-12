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
    /// Должностные лица. В рабочей книге это лист «Должностные лица» в том же
    /// файле, что и машины: отдельного файла людей у заказчика нет, и заводить
    /// его вместо существующего листа значило бы плодить вторую копию.
    /// Путь и имя листа настраиваются, поэтому отдельный файл тоже возможен.
    ///
    /// На листе всего три столбца: «№», «Должностное лицо» и «Был». Должность,
    /// звание и фамилия записаны одной строкой — так их и печатают в документах,
    /// поэтому разбирать их на части незачем.
    ///
    /// Справочник пока только читается: редактор появится вместе с карточкой машины.
    /// </summary>
    public class ExcelPersonRepository : IPersonRepository
    {
        public static string SheetName
        {
            get { return AppSheets.Name(SheetKind.People); }
        }

        public static readonly string[] IdNames = { "№", "№ п/п", "Id", "Код" };
        public static readonly string[] FullNameNames =
            { "Должностное лицо", "ФИО", "Ф.И.О.", "Фамилия, имя, отчество" };
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

                // по умолчанию люди лежат в книге машин: создавать её должен
                // репозиторий машин, иначе заготовка людей затрёт список машин
                if (string.Equals(path, AppPaths.CarsFile, StringComparison.OrdinalIgnoreCase))
                    ExcelCarRepository.EnsureDemoFile();
                else
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
                throw new InvalidOperationException(string.Format(
                    "На листе «{0}» не найден столбец «Должностное лицо» или «ФИО».{1}{1}" +
                    "Проверьте имя листа в настройках: людей приложение ищет там же, " +
                    "где и машины.", SheetName, Environment.NewLine));

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
            var sheet = DemoSheet();
            XlsxWriter.Write(path, sheet.Name, sheet.Headers, sheet.Rows);
        }

        /// <summary>
        /// Лист должностных лиц для файла-образца. Шапка повторяет рабочую:
        /// номер, одна строка «должность звание фамилия» и «Был» — кто занимал
        /// эту должность раньше.
        /// </summary>
        public static XlsxSheet DemoSheet()
        {
            var headers = new[] { "№", "Должностное лицо", "Был" };

            var rows = new List<IList<XlsxCell>>
            {
                new List<XlsxCell>
                {
                    XlsxCell.Number(1),
                    XlsxCell.Text("начальник автомобильной службы майор Иванов И.И."),
                    XlsxCell.Empty
                },
                new List<XlsxCell>
                {
                    XlsxCell.Number(2),
                    XlsxCell.Text("заместитель командира по вооружению подполковник Сидоров С.С."),
                    XlsxCell.Empty
                },
                new List<XlsxCell>
                {
                    XlsxCell.Number(3),
                    XlsxCell.Text("начальник штаба подполковник Антонов А.А."),
                    XlsxCell.Empty
                }
            };

            return new XlsxSheet(SheetName, headers, rows);
        }
    }
}
