using System;
using System.Collections.Generic;

namespace ParkApp.components.Domain
{
    public class Car
    {
        public Guid Id { get; set; }
        public List<CarNumber> Numbers { get; set; } = new List<CarNumber>();
        public string Model { get; set; }
        public Location Location { get; set; } // №1 / №2
        public string Vin { get; set; }
        public int Year { get; set; }
    }
}