using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DAL;
using DAL.Data;
using DAL.Entities;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramBattleShips.Game;
using TelegramBotConsole.Game;

namespace TelegramBotConsole
{
    class Program : IDisposable
    {
        private static readonly IConfiguration Configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();

        private static TelegramDbContext Context => DbContextSingletone.GetContext();
        private static readonly string Token = Configuration.GetSection("TelegramBotToken").Value;
        private static readonly ITelegramBotClient Bot = new TelegramBotClient(Token);
        private static readonly Dictionary<Chat, HangGame> Games = new Dictionary<Chat, HangGame>();
        private static readonly GameDispatcher Dispatcher = new GameDispatcher(Bot);

        static async Task Main(string[] args)
        {
            Bot.OnMessage += TryOnMessageReceived;

            Bot.StartReceiving();
            Console.ReadLine();

            Bot.StopReceiving();

            Dispatcher.Dispose();

            await NotifyPlayersAboutShutdown();
        }

        private static async void TryOnMessageReceived(object sender, MessageEventArgs e)
        {
            try
            {
                await OnMessageReceived(sender, e);
            } catch (Exception ex)
            {
                await Bot.SendTextMessageAsync(e.Message.Chat, ex.Message);
            }
        }

        private static async Task NotifyPlayersAboutShutdown()
        {
            foreach (var player in Dispatcher.CurrentPlayers)
            {
                await Bot.SendTextMessageAsync(player.Id, $"Хост завершив роботу бота. Дякую за гру, {player.FirstName} 😉");
            }
        }

        private static async Task OnMessageReceived(object sender, MessageEventArgs e)
        {
            ProcessIncomingMessageAsync(e.Message);

            if (e.Message.Type != MessageType.Text) return;

            var msg = e.Message;
            var msgText = msg.Text.ToLower();
            var chat = msg.Chat;
            var user = msg.From;

            await Dispatcher.TryProcessMessageAsync(user, msgText);

            var game = Games.FirstOrDefault(x => x.Key.Id == chat.Id).Value;
            switch (msgText.Split('@')[0])
            {
                case "/startgame":
                    if (game != null)
                        game.Dispose();
                    Games[Games.Keys.FirstOrDefault(k => k.Id == chat.Id) ?? chat] = new HangGame(chat, Bot);
                    break;
                case "/startsologame":
                    if (game != null)
                        game.Dispose();
                    Games[Games.Keys.FirstOrDefault(k => k.Id == chat.Id) ?? chat] = new HangGame(chat, Bot, isSolo: true);
                    break;
                case "/clear":
                    if (game != null)
                        game.Dispose();
                    break;
            }

            if (msgText.Contains("/try "))
            {
                game = Games.FirstOrDefault(p => p.Key.Id == chat.Id).Value;
                if (game == null)
                {
                    await Bot.SendTextMessageAsync(chat, $"Жодної гри не розпочато, {msg.From.FirstName}!");
                    return;
                }
                var letterStr = msgText.Split(' ', StringSplitOptions.RemoveEmptyEntries).Last();
                if (letterStr.Length != 1)
                {
                    await Bot.SendTextMessageAsync(chat, $"Лише одну літеру за раз можна спробувати, {msg.From.FirstName}!");
                }
                else
                {
                    var letter = letterStr[0];
                    try
                    {
                        await game.MakeAttemptAsync(user, letter);
                    }
                    catch (Exception ex)
                    {
                        await Bot.SendTextMessageAsync(chat, ex.Message);
                    }
                }
            }
        }

        private static async Task ProcessIncomingMessageAsync(Message message)
        {
            var user = message.From;
            await using var context = Context;
            var telegramUser = await context.TelegramUsers.FirstOrDefaultAsync(u => u.UserId == user.Id);
            var telegramMessage = new TelegramMessage(message);
            if (telegramUser == null)
            {
                telegramUser = new TelegramUser(user);
                await context.TelegramUsers.AddAsync(telegramUser);

                Console.WriteLine($"New user was added to the database:\n{telegramUser}");
            }

            telegramUser.Messages.Add(telegramMessage);

            Console.WriteLine($"User {message.From.Username}, {message.From.FirstName} {message.From.LastName}, with id " +
                              $"{message.From.Id} sent: {message.Text,20}");

            await context.SaveChangesAsync();
        }

        public void Dispose()
        {

        }
    }
}

#region backup 

//_bot.SetMyCommandsAsync(new List<BotCommand>
//{
//    new BotCommand { Command = "/startgame", Description = "почати нову гру 'шибениця', якщо немає розпочатої" },
//    new BotCommand { Command = "/startgameforced", Description = "почати нову гру 'шибениця'" },
//    new BotCommand { Command = "/try [letter]", Description = "спробувати вгадати літеру" },
//    new BotCommand { Command = "/letterstried", Description = "список уже випробуваних літер" },
//});

//        private async static void OnMessageReceived(object sender, MessageEventArgs e)
//        {
//            var msg = e.Message;
//            var userId = msg.From.Id;
//            var chatId = msg.Chat.Id;
//            var msgId = msg.MessageId;
//            try
//            {
//                switch (msg.Text)
//                {
//                    case "/invice":
//                        await _bot.SendInvoiceAsync((int)chatId, "Title", "Description", "Payload", "ProviderToken", "StartParameter", "UAH", new LabeledPrice[] { new LabeledPrice { Amount = 5, Label = "Product 1" } });
//                        break;
//                    case "/start":
//                        await _bot.SendTextMessageAsync(chatId, @"/start
///users
///messages
///photoPoll
///dice
///darts
///basketball
///dontTouchIt");
//                        break;
//                    case "/messages":
//                        using (TelegramDbContext context = Context)
//                        {
//                            foreach (var m in context.Messages.Include(m => m.Sender))
//                                await _bot.SendTextMessageAsync(chatId, m.ToString());
//                        }
//                        return;
//                    case "/users":
//                        using (TelegramDbContext context = Context)
//                        {
//                            foreach (var user in context.TelegramUsers.Include(u => u.Messages))
//                                await _bot.SendTextMessageAsync(chatId, user.ToString());
//                        }
//                        return;
//                    case "/photoPoll":
//                        var photos = await _bot.GetUserProfilePhotosAsync(msg.From.Id);
//                        Console.WriteLine($"Photos total count: {photos.TotalCount}");

//                        if (photos.TotalCount == 0)
//                        {
//                            await _bot.SendTextMessageAsync(chatId, $"Sorry, {msg.From.FirstName}, you don't have any photos!");
//                            return;
//                        }
//                        else if (photos.TotalCount == 1)
//                        {
//                            await _bot.SendTextMessageAsync(chatId, $"Sorry, {msg.From.FirstName}, you have only one photo!");
//                            return;
//                        }

//                        await _bot.SendTextMessageAsync(msg.Chat.Id, $"<strong>Your photos:</strong>", ParseMode.Html);

//                        foreach (var photoLengths in photos.Photos)
//                        {
//                            await _bot.SendPhotoAsync(chatId, new InputOnlineFile(photoLengths[0].FileId));
//                            await Task.Delay(500);
//                        }

//                        var options = photos.Photos.Select(p => p[0]).Select((p, i) => $"{i + 1}").ToArray();
//                        int sent = 0;
//                        while (sent < options.Length)
//                        {
//                            await _bot.SendPollAsync(chatId, "Which is the best?", options.Skip(sent).Take(8), isAnonymous: false);
//                            sent += 8;
//                        }
//                        break;
//                    case "/dice":
//                        await _bot.SendDiceAsync(chatId, emoji: Emoji.Dice);
//                        break;
//                    case "/basketball":
//                        await _bot.SendDiceAsync(chatId, emoji: Emoji.Basketball);
//                        break;
//                    case "/darts":
//                        await _bot.SendDiceAsync(chatId, emoji: Emoji.Darts);
//                        break;
//                    case "/dontTouchIt":
//                        await _bot.SetGameScoreAsync(userId, 250, chatId, msgId);
//                        break;
//                    default:
//                        break;
//                }

//                Console.WriteLine($"User {msg.From.FirstName} sent message: {msg.Text}");

//                await ProcessIncomingMessageAsync(e.Message);
//            } catch (Exception ex)
//            {
//                await Task.Delay(5000);
//                await _bot.SendTextMessageAsync(chatId, "Error: " + ex.Message);
//            }
//        }
#endregion
