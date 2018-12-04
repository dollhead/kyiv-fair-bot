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

            var fairLinks = GetAllFairLinks();
            return (ActionResult)new OkObjectResult(fairLinks);
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
