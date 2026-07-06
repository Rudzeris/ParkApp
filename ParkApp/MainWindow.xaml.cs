using System.IO;
using System.Windows;
using Xceed.Words.NET;

namespace ParkApp
{
    public partial class MainWindow
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            var car = new Car("Lada", "1234AB");

            var templatePath = @"F:\Atangulov\Projects\ParkApp\Templates\template.docx";

            // Создаём папку Output рядом с exe
            var outputDir = Path.Combine(Directory.GetCurrentDirectory(), "Output");
            Directory.CreateDirectory(outputDir);

            var outputPath = Path.Combine(outputDir, $"Car_{car.Number}.docx");

            var doc = DocX.Load(templatePath);

            doc.ReplaceText("{car_model}", car.Model);
            doc.ReplaceText("{car_number}", car.Number);

            doc.SaveAs(outputPath);

            MessageBox.Show("Документ создан!");
        }
    }

    class Car
    {
        public string Model { get; private set; }
        public string Number { get; private set; }

        public Car(string model, string number)
        {
            Model = model;
            Number = number;
        }
    }
}