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

namespace KyivFairBot.Function
{
    public static class Kyiv_Fair_Bot_Message_Trigger
    {
        private static string KyivCityBaseUrl = "https://kyivcity.gov.ua";
        private static string FairsInfoUrl = $"{KyivCityBaseUrl}/biznes_ta_litsenzuvannia/yarmarky_106.html";

        private static HtmlWeb HtmlWeb = new HtmlWeb();

        [FunctionName("Kyiv_Fair_Bot_Message_Trigger")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req,
            ILogger log)
        {

            log.LogInformation("Processing message.");

            var latestFairLink = GetAllFairLinks().First();
            var doc = HtmlWeb.Load(latestFairLink);
            var contentXPath = "/html/body/div[1]/div/div[2]/div[2]/div/div[2]/div[1]/div[4]/div";

            var content = doc
                .DocumentNode
                .SelectSingleNode(contentXPath);

            var fairDates = GetFairDates(content).ToList();
            
            var fairsByDate = content
                .SelectNodes("ul")
                .Select(x => x.SelectNodes("li").Select(e => e.InnerText.Replace("&nbsp", " ").Replace("&ndash", " ")))
                .ToList();

            var fairsDict = new Dictionary<DateTime, IEnumerable<string>>();
            for (var i = 0; i < fairDates.Count(); i++)
            {
                var fairDate = fairDates[i];
                fairsDict[fairDate] = fairsByDate[i];
            }


            return (ActionResult)new OkObjectResult(fairsDict);
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
    }
}
