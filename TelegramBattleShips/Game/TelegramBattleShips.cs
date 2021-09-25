using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Telegram.Bot.Types.ReplyMarkups;
using System.Linq;
using System.Timers;
using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramBattleShips.Game.Enums;

namespace TelegramBattleShips.Game
{
    public class TelegramBattleShips : IDisposable
    {
        private const int ButtonsInRow = 5;
        private const int TimerIntervalMs = 15_000;
        private readonly Timer notifyTimer = new Timer(TimerIntervalMs) { AutoReset = true };
        private readonly double TimeoutOffsetMs = 60_000;
        private double elapsedMs = 0;

        public TelegramBattleShips(ITelegramBotClient bot, User user1)
        {
            Bot = bot ?? throw new ArgumentNullException(nameof(bot));
            Player1 = new Player(user1 ?? throw new ArgumentNullException(nameof(user1)));

            SendMessageAsync(Player1, Player1.GetFieldImageStreamAsync(FieldView.Full).Result, "Твій флот").Wait();
            SendMessageAsync(Player1, "Очікується другий грaвець...").Wait();

            notifyTimer.Elapsed += NotifyTimer_Elapsed;
        }

        public ITelegramBotClient Bot { get; private set; }
        public Player Player1 { get; private set; }
        public Player Player2 { get; private set; }
        public bool IsPlayer1Turn { get; private set; } = true;
        public bool IsFinished { get; private set; }

        public async Task SetSecondPlayerAsync(User user2)
        {
            notifyTimer.Enabled = true;

            Player2 = new Player(user2);
            await UpdateAsync();
        }

        public ReplyKeyboardMarkup GetAvailableHitsKeyboard()
        {
            var buttons = PassivePlayer
                .GetAvailableHits()
                .Select(x => new KeyboardButton($"/hit {x}"))
                .ToList();

            var keyboard = new List<IEnumerable<KeyboardButton>>();
            var count = 0;

            do
            {
                keyboard.Add(buttons
                    .Skip(count)
                    .Take(ButtonsInRow));
                count += ButtonsInRow;
            } while (count <= buttons.Count);

            return new ReplyKeyboardMarkup(keyboard, oneTimeKeyboard: true, resizeKeyboard: true);
        }

        public async Task HitAsync(User user, string cell)
        {
            if (!TryRecognizePlayer(user, out var sender))
            {
                return;
            }

            string activePlayerMessage;
            string passivePlayerMessage;

            if (sender != ActivePlayer)
            {
                await SendMessageAsync(PassivePlayer, "Зараз не твій хід!");
                return;
            }
           
            if (IsFinished)
            {
                await SendMessageAsync(sender, "Гру вже завершено!");
            }

            var isHit = false;

            try
            {
                isHit = PassivePlayer.Hit(cell.Replace("/hit", string.Empty).Trim());
            }
            catch
            {
                await SendActivePlayerMessage($"Невалідний ввід: {cell}");
                return;
            }

            elapsedMs = 0;
            notifyTimer.Interval = TimerIntervalMs;
            if (isHit)
            {
                if (PassivePlayer.AliveFleet == 0)
                {
                    IsFinished = true;
                    activePlayerMessage = $"Вітаю з перемогою 😄, {ActivePlayer.Name}, флот гравця {PassivePlayer.Name} розгромлено!";
                    passivePlayerMessage = $"На жаль, гравець {ActivePlayer.Name} розгромив твій флот. Програш 🥺";

                    await FinalUpdateAsync();
                }
                else
                {
                    passivePlayerMessage = $"Гравець {ActivePlayer.Name} влучив у твій корабель!";
                    activePlayerMessage = $"Вітаю, {ActivePlayer.Name}. Влучання!";

                    if (ActivePlayer.Streak > 1)
                    {
                        activePlayerMessage += $" Так тримати, {ActivePlayer.Streak} влучань під ряд 🤗!\nМожеш спробувати ще раз.";
                    }

                    activePlayerMessage += " Можеш спробувати ще раз.";
                }
            }
            else
            {
                activePlayerMessage = "Нема влучання";
                passivePlayerMessage = $"На щастя, гравець {ActivePlayer.Name} не влучив.\nТвій хід.";
            }

            await UpdateAsync();
            await SendActivePlayerMessage(activePlayerMessage);
            await SendPassivePlayerMessage(passivePlayerMessage);

            if (!isHit)
            {
                IsPlayer1Turn = !IsPlayer1Turn;
                await Task.Delay(1000);
                await UpdateAsync();
            }
        }

        public bool TryRecognizePlayer(User user, out Player player)
        {
            player = user.Id == ActivePlayer.UserId ? ActivePlayer : user.Id == PassivePlayer.UserId ? PassivePlayer : default;

            return player != default;
        }

        private Task<Stream> GetActivePlayerFieldImageAsync(FieldView view) => ActivePlayer.GetFieldImageStreamAsync(view);

        private Task<Stream> GetPassivePlayerImageAsync(FieldView view) => PassivePlayer.GetFieldImageStreamAsync(view);

        public Player ActivePlayer => IsPlayer1Turn ? Player1 : Player2;

        public Player PassivePlayer => IsPlayer1Turn ? Player2 : Player1;

        private async Task UpdateAsync(string activePlayerCaption = "Флот гравця {0}", string passivePlayerCaption = "Твій флот. Очікується хід гравця {0}")
        {
            await DeleteSentMessagesAsync();

            await SendMessageAsync(ActivePlayer, await GetPassivePlayerImageAsync(FieldView.Restricted), 
                activePlayerCaption.Replace("{0}", PassivePlayer.Name), GetAvailableHitsKeyboard());

            await SendMessageAsync(PassivePlayer, await GetPassivePlayerImageAsync(FieldView.Full), 
                passivePlayerCaption.Replace("{0}", ActivePlayer.Name));
        }

        private async Task FinalUpdateAsync()
        {
            await DeleteSentMessagesAsync();

            await SendMessageAsync(PassivePlayer, await GetActivePlayerFieldImageAsync(FieldView.Full),
                $"Флот гравця {ActivePlayer.Name}");
        }

        private Task SendActivePlayerMessage(string message) => SendMessageAsync(ActivePlayer, message);

        private Task SendPassivePlayerMessage(string message) => SendMessageAsync(PassivePlayer, message);

        private async Task SendMessageAsync(Player player, string text)
        {
            var message = await Bot.SendTextMessageAsync(player.UserId, text);

            player.LastSentTextMessage = message;
        }

        private async Task SendMessageAsync(Player player, Stream stream, string caption, IReplyMarkup replyMarkup = null)
        {
            var message = await Bot.SendPhotoAsync(player.UserId, stream, caption, replyMarkup: replyMarkup);

            player.LastSentTextMessage = message;
        }

        private async Task DeleteSentMessagesAsync()
        {
            try
            {
                await Bot.DeleteMessageAsync(ActivePlayer.UserId, ActivePlayer.LastSentTextMessage.MessageId);
            }
            catch (Exception e)
            {
                Console.WriteLine("An error occurred while deleting a message: " + e.Message);
            }

            try
            {
                await Bot.DeleteMessageAsync(PassivePlayer.UserId, PassivePlayer.LastSentTextMessage.MessageId);
            }
            catch (Exception e)
            {
                Console.WriteLine("An error occurred while deleting a message: " + e.Message);
            }
        }

        private async void NotifyTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            //elapsedMs += notifyTimer.Interval;

            //if (elapsedMs >= TimeoutOffsetMs)
            //{
            //    IsFinished = true;
            //    await FinalUpdateAsync();
            //    await SendActivePlayerMessage($"На жаль, ти здався й отримав поразку! 😱 Переміг гравець {PassivePlayer.Name}");
            //    await SendPassivePlayerMessage($"Вітаю, гравець {ActivePlayer.Name} здався, а тому ти отримав перемогу!");

            //    await Task.Delay(5_000);

            //    Dispose();
            //}

            //var remainingSec = (int)((TimeoutOffsetMs - elapsedMs) / 1000);

            //await SendActivePlayerMessage($"{PassivePlayer.Name} очікує твій хід, поспіши, або гра завершиться через {remainingSec} секунд");
            //await SendPassivePlayerMessage($"Очікуй хід гравця {ActivePlayer.Name}. У нього залишилось {remainingSec} секунд");
        }

        public async void Dispose()
        {
            await DeleteSentMessagesAsync();
        }
    }
}
