using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.Documents.Client;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using System.Collections.Generic;
using System.Linq;

namespace KyivFairsBot
{
    public static class KyivFairBotMessageTrigger
    {
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

        private static string TelegramToken = System.Environment.GetEnvironmentVariable("TELEGRAM_TOKEN");
        private static ITelegramBotClient BotClient = new TelegramBotClient(TelegramToken);

        [FunctionName("KyivFairBotMessageTrigger")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req,
            [CosmosDB(
                databaseName: "Fairs",
                collectionName: "FutureFairs",
                ConnectionStringSetting = "CosmosDBConnection")] DocumentClient client,
            ILogger log)
        {
            var requestContent = await req.ReadAsStringAsync();
            var update = JsonConvert.DeserializeObject<Update>(requestContent);

            if (update.Message.Text == "/start")
            {
                var buttonRows = Neighborhoods.Select(n => new List<InlineKeyboardButton>{InlineKeyboardButton.WithCallbackData(n)});
                var inlineKeyboard = new InlineKeyboardMarkup(buttonRows);

                await BotClient.SendTextMessageAsync(
                    update.Message.Chat.Id,
                    "Виберіть ваш район:",
                    replyMarkup: inlineKeyboard);
            }

            return new OkResult();
        }
    }
}
