using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http;
using HtmlAgilityPack;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Web;

namespace KyivFairBot.Function
{
    public static class Kyiv_Fair_Bot_Message_Trigger
    {
        private static string KyivCityBaseUrl = "https://kyivcity.gov.ua";
        private static string FairsInfoUrl = $"{KyivCityBaseUrl}/biznes_ta_litsenzuvannia/yarmarky_106.html";

        private static List<string> Neighborhoods = new List<string>()
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
        
        private static HtmlWeb HtmlWeb = new HtmlWeb();

        [FunctionName("Kyiv_Fair_Bot_Message_Trigger")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req,
            ILogger log)
        {

            log.LogInformation("Processing message.");

            var fairLinks = GetAllFairLinks();
            var futureFairs = new List<Fair>();

            foreach (var link in fairLinks)
            {
                var futureFairsByLink = GetFutureFairsByLink(link);
                if (futureFairsByLink.Any())
                {
                    futureFairs.AddRange(futureFairsByLink);
                    continue;
                }

                break;
            }

            return (ActionResult)new OkObjectResult(futureFairs);
        }

        private static IEnumerable<Fair> GetFutureFairsByLink(string fairLink)
        {
            var futureFairs = new List<Fair>();
            var doc = HtmlWeb.Load(fairLink);
            var contentXPath = "/html/body/div[1]/div/div[2]/div[2]/div/div[2]/div[1]/div[4]/div";

            var content = doc
                .DocumentNode
                .SelectSingleNode(contentXPath);

            var fairDates = GetFairDates(content).ToList();

            var fairsByDate = content
                .SelectNodes("ul")
                .Select(x => x.SelectNodes("li").Select(e => HttpUtility.HtmlDecode(e.InnerText)))
                .ToList();


            for (var i = 0; i < fairDates.Count(); i++)
            {
                var fairDate = fairDates[i];

                //TODO: we should use local time here but it's a little bit complicated since I am on linux.
                if (fairDate > DateTime.UtcNow)
                {
                    var fairs = fairsByDate[i]
                        .Select(f => new Fair
                        {
                            Date = fairDate,
                            Location = f.Split("–").Last().Trim(),
                            Neighborhood = f.Split("–").First().Trim()
                        });

                    futureFairs.AddRange(fairs);
                }
            }

            return futureFairs;
        }

        private static IEnumerable<DateTime> GetFairDates(HtmlNode content)
        {
            var provider = new CultureInfo("uk");
            return content
                .SelectNodes("p/strong")
                .Select(x => x.InnerText)
                .Select(x => 
                {
                    var dayStartIndex = x.IndexOf('(');
                    return DateTime.Parse(x.Substring(0, dayStartIndex - 1), provider, DateTimeStyles.AssumeLocal);
                });
            
        }

        private static IEnumerable<string> GetAllFairLinks()
        {
            var doc = HtmlWeb.Load(FairsInfoUrl);
            var fairsXPath = "//*[@id='ultab4']/li/div[3]/div[1]/a";
            var fairLinks = doc
                .DocumentNode
                .SelectNodes(fairsXPath)
                .Select(fair => $"{KyivCityBaseUrl}{fair.Attributes["href"].Value}");

            return fairLinks;
        }

        public class Fair
        {
            private const int NumberOfCharsToCompare = 3;

            private string _neighborhood;

            public DateTime Date { get; set; }

            public string Neighborhood 
            {
                 get { return _neighborhood; }

                 set 
                 {
                    var firstChars = value.Substring(0, NumberOfCharsToCompare);
                    var neighborhoodName = Neighborhoods.FirstOrDefault(n => n.Substring(0, NumberOfCharsToCompare) == firstChars);
                    _neighborhood = neighborhoodName;
                 }
            }

            public string Location { get; set; }
        }
    }
}
