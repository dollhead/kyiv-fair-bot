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
using Microsoft.Azure.Documents.Linq;
using System.Text;

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

            var callbackQuery = update.CallbackQuery;
            if (callbackQuery != null)
            {
                var collectionUri = UriFactory.CreateDocumentCollectionUri("Fairs", "FutureFairs");

                var feedOptions = new FeedOptions{EnableCrossPartitionQuery = true};
                var query = client.CreateDocumentQuery<Fair>(collectionUri, feedOptions)
                    .Where(p => p.Neighborhood.Contains(callbackQuery.Data))
                    .AsDocumentQuery();

                var response = new StringBuilder();
                while (query.HasMoreResults)
                {
                    foreach (var result in await query.ExecuteNextAsync<Fair>())
                    {
                        response.Append($"Дата: {result.Date}, Місце: {result.Location}");
                    }
                }

                var responseString = response.ToString();
                if (string.IsNullOrEmpty(responseString))
                {
                    responseString = "На жаль, у вашому районі найближчим часом ярмарки не заплановані.";
                }

                await BotClient.SendTextMessageAsync(update.CallbackQuery.Message.Chat.Id,
                    responseString);

                return new OkResult();    
            }

            if (update.Message.Text == "/start")
            {
                var buttonRows = Neighborhoods.Select(n => new List<InlineKeyboardButton>{InlineKeyboardButton.WithCallbackData(n)});
                var inlineKeyboard = new InlineKeyboardMarkup(buttonRows);

                await BotClient.SendTextMessageAsync(update.Message.Chat.Id,
                    "Виберіть ваш район:",
                    replyMarkup: inlineKeyboard);
            }

            return new OkResult();
        }
    }
}
