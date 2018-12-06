using System;
using System.Collections.Generic;
using System.Linq;

namespace KyivFairsBot
{
    public class Fair
    {
        public static List<string> Neighborhoods = new List<string>()
        {
            "Голосіївський",
            "Дарницький",
            "Деснянський",
            "Дніпровський",
            "Оболонський",
            "Печерський",
            "Подільський",
            "Святошинський",
            "Солом'янський",
            "Шевченківський"
        };

        private const int NumberOfCharsToCompare = 3;

        private string _neighborhood;

        public DateTime Date { get; set; }

        public string Neighborhood
        {
            get { return _neighborhood; }

            set
            {
                if (value.StartsWith("у"))
                {
                    value = value.Substring(2);
                }

                var firstChars = value.Substring(0, NumberOfCharsToCompare);
                var neighborhoodName = Neighborhoods.FirstOrDefault(n => n.Substring(0, NumberOfCharsToCompare) == firstChars);
                _neighborhood = neighborhoodName;
            }
        }

        public string Location { get; set; }
    }
}