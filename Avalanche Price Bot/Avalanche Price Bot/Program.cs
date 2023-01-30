using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Avalanche_Price_Bot
{
    internal class Program
    {
        private static DiscordSocketClient _client;
        private static CommandService _commands;

        static void Main(string[] args) => new Program().MainAsync().GetAwaiter().GetResult();

        private async Task MainAsync()
        {
            
            _client = new DiscordSocketClient();
            _commands = new CommandService();

            await _commands.AddModuleAsync<Commands>(null);
            _client.MessageReceived += HandleCommandAsync;

            await _client.LoginAsync(TokenType.Bot, "MTA2OTY3ODcwOTc2Nzk0MjI0Nw.G5cHlq.VvPH-mQcIJ9QENU-PpwZWG5_-SLcT_VmVJWHPQ");
            await _client.StartAsync();
            await _client.SetGameAsync("Message me !prices");
            await Task.Delay(-1);
        }

        private static async Task HandleCommandAsync(SocketMessage messageParam)
        {
            var message = messageParam as SocketUserMessage;
            if (message == null) return;
            int argPos = 0;
            if (!(message.HasCharPrefix('!', ref argPos) ||
                  message.HasMentionPrefix(_client.CurrentUser, ref argPos))) return;
            var context = new SocketCommandContext(_client, message);
            var result = await _commands.ExecuteAsync(context, argPos, null);
            if (!result.IsSuccess)
                await context.Channel.SendMessageAsync(result.ErrorReason);
        }
    }

    public class Commands : ModuleBase
    {
        [Command("prices")]
        public async Task PricesAsync()
        {
            string itemTypes = "any";
            string query = "query GetItems { Items: items(types: [" + itemTypes + "]) { id name avg24hPrice sellFor{ price source } } }";
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.tarkov.dev/graphql");
            request.Content = new StringContent(JsonConvert.SerializeObject(new { query }), Encoding.UTF8, "application/json");
            var client = new HttpClient();
            try
            {
                var response = await client.SendAsync(request);
                response.EnsureSuccessStatusCode();

                // Deserialize and parse the response from the API into a JObject
                var json = JObject.Parse(await response.Content.ReadAsStringAsync());

                // Select the desired information from the response and order it by highest price
                var outputList = json["data"]["Items"]
                .Select(item => new
                {
                    id = item["id"],
                    highestPrice = item["sellFor"].Select(i => (int)i["price"]).Concat(new[] { (int)item["avg24hPrice"] }).Max()
                })
                .OrderByDescending(item => item.highestPrice)
                .ToList();
                var sb = new StringBuilder();
                using (var outFile = new StreamWriter("24HourAveragePrices.txt"))
                {
                    // Iterate through each item in the outputList
                    foreach (var item in outputList)
                    {
                        // Write a string "set_price " followed by the item's id and highestPrice to the file
                        outFile.WriteLine("set_price " + item.id + " " + item.highestPrice);
                    }
                }
                var file = File.OpenRead("24HourAveragePrices.txt");
                var memoryStream = new MemoryStream();
                file.CopyTo(memoryStream);
                memoryStream.Position = 0;
                await Context.Channel.SendFileAsync(memoryStream, "24HourAveragePrices.txt");
                file.Dispose();
                memoryStream.Dispose();
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine("Error: " + ex.Message);
                await ReplyAsync("Error: " + ex.Message);
            }
        }
    }
}