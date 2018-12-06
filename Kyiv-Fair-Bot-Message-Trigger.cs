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
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            [CosmosDB(
                databaseName: "Fairs",
                collectionName: "FutureFairs",
                ConnectionStringSetting = "CosmosDBConnection")]
                IAsyncCollector<Fair> fairDocuments,
            ILogger log)
        {
            log.LogInformation("Processing message.");

            var fairLinks = await GetAllFairLinks();
            var futureFairs = new List<Fair>();

            var saveTasks = new List<Task>();
            foreach (var link in fairLinks)
            {
                saveTasks.Add(SaveFairsByLink(link, fairDocuments));
            }
            
            await Task.WhenAll(saveTasks);
            
            return new OkResult();
        }

        private static async Task SaveFairsByLink(string fairLink, IAsyncCollector<Fair> fairCollector)
        {
            var doc = await HtmlWeb.LoadFromWebAsync(fairLink);
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

                    foreach(var fair in fairs)
                    {
                        await fairCollector.AddAsync(fair);
                    }
                }
            }
        }

        private static IEnumerable<DateTime> GetFairDates(HtmlNode content)
        {
            var provider = new CultureInfo("uk");
            return content
                .SelectNodes("p/strong")
                .Select(x => x.InnerText)
                .Select(text => HttpUtility.HtmlDecode(text))
                .Select(x => 
                {
                    var dayStartIndex = x.IndexOf('(');
                    if (dayStartIndex == -1)
                    {
                        //TODO: this is wrong. Should parse news date.
                        if (x.Split(' ').First().ToLower() == "сьогодні")
                        {
                            return DateTime.UtcNow;
                        }

                        var date = string.Join(' ', x.Split(' ').AsSpan().Slice(0, 2).ToArray());
                        return DateTime.Parse(date, provider, DateTimeStyles.AssumeLocal);
                    }

                    return DateTime.Parse(x.Substring(0, dayStartIndex - 1), provider, DateTimeStyles.AssumeLocal);
                });
            
        }

        private static async Task<IEnumerable<string>> GetAllFairLinks()
        {
            var doc = await HtmlWeb.LoadFromWebAsync(FairsInfoUrl);
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
}
