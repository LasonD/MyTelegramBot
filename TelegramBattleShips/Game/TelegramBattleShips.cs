using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Telegram.Bot.Types.ReplyMarkups;
using System.Linq;
using System.Timers;
using DAL.Data;
using DAL.Entities;
using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramBattleShips.Game.Enums;

namespace TelegramBattleShips.Game
{
    public class TelegramBattleShips : IDisposable
    {
        private readonly TelegramDbContext _context;
        private const int ButtonsInRow = 5;
        private const int TimerIntervalMs = 15_000;
        private readonly Timer _notifyTimer = new Timer(TimerIntervalMs);
        private readonly double TimeoutOffsetMs = 60_000;
        private double _elapsedMs = 0;
        private bool _disposed;

        public TelegramBattleShips(ITelegramBotClient bot, User user1, TelegramDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            Bot = bot ?? throw new ArgumentNullException(nameof(bot));
            Player1 = new Player(user1 ?? throw new ArgumentNullException(nameof(user1)));

            SendImageMessageAsync(Player1, Player1.GetFieldImageStreamAsync(FieldView.Full).Result, "Твій флот").Wait();
            SendTextMessageAsync(Player1, "Очікується інший грaвець...").Wait();

            _notifyTimer.Elapsed += NotifyTimer_Elapsed;
        }

        public event EventHandler Finish;

        public ITelegramBotClient Bot { get; private set; }
        public Player Player1 { get; private set; }
        public Player Player2 { get; private set; }
        public bool IsPlayer1Turn { get; private set; } = true;
        public bool IsFinished { get; private set; }

        public async Task SetSecondPlayerAsync(User user2)
        {
            _notifyTimer.Enabled = true;

            Player2 = new Player(user2);
            await UpdateAsync($"Гравець {PassivePlayer.Name} приєднався до гри. Його флот",
                $"Ти приєднався до гри, твій суперник - {ActivePlayer.Name}. Очікується його хід");
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
                await SendTextMessageAsync(PassivePlayer, "Зараз не твій хід!");
                return;
            }
           
            if (IsFinished)
            {
                await SendTextMessageAsync(sender, "Гру вже завершено!");
            }

            bool isHit;

            try
            {
                isHit = PassivePlayer.Hit(cell.Replace("/hit", string.Empty).Trim());
            }
            catch
            {
                await SendActivePlayerMessage($"Невалідний ввід: {cell}");
                return;
            }

            RefreshTimer();

            if (isHit)
            {
                if (PassivePlayer.AliveFleet == 0)
                {
                    Finish?.Invoke(this, EventArgs.Empty);
                    IsFinished = true;
                    activePlayerMessage = $"Вітаю з перемогою 😄, {ActivePlayer.Name}, флот гравця {PassivePlayer.Name} розгромлено!";
                    passivePlayerMessage = $"На жаль, гравець {ActivePlayer.Name} розгромив твій флот. Програш 🥺";

                    await IncrementDbUserStatisticsAsync(user, StatisticsCounter.GamesWon);

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

            if (isHit)
            {
                await IncrementDbUserStatisticsAsync(user, StatisticsCounter.UnitsDestroyed);
            }
        }

        private async Task IncrementDbUserStatisticsAsync(User user, StatisticsCounter counter)
        {
            var dbUser = _context.TelegramUsers.FirstOrDefault(u => u.UserId == user.Id);

            if (dbUser == null)
            {
                var newUser = new TelegramUser(user);

                dbUser = (await _context.TelegramUsers.AddAsync(newUser)).Entity;
            }

            switch (counter)
            {
                case StatisticsCounter.UnitsDestroyed:
                    dbUser.ShipUnitsDestroyed++;
                    break;
                case StatisticsCounter.GamesWon:
                    dbUser.BattleShipGamesWon++;
                    break;
                case StatisticsCounter.EnemySurrendedGamesWon:
                    dbUser.EnemySurrendedWons++;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(counter), counter, null);
            }

            await _context.SaveChangesAsync();
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
            await DeletePlayerMessageAsync(ActivePlayer, true);
            await DeletePlayerMessageAsync(PassivePlayer, true);

            await SendImageMessageAsync(ActivePlayer, await GetPassivePlayerImageAsync(FieldView.Restricted), 
                activePlayerCaption.Replace("{0}", PassivePlayer.Name), GetAvailableHitsKeyboard());

            await SendImageMessageAsync(PassivePlayer, await GetPassivePlayerImageAsync(FieldView.Full), 
                passivePlayerCaption.Replace("{0}", ActivePlayer.Name));
        }

        private async Task FinalUpdateAsync()
        {
            await DeletePlayerMessageAsync(PassivePlayer, true);

            await SendImageMessageAsync(PassivePlayer, await GetActivePlayerFieldImageAsync(FieldView.Full),
                $"Флот гравця {ActivePlayer.Name}");
        }

        private Task SendActivePlayerMessage(string message) => SendTextMessageAsync(ActivePlayer, message);

        private Task SendPassivePlayerMessage(string message) => SendTextMessageAsync(PassivePlayer, message);

        private async Task SendTextMessageAsync(Player player, string text)
        {
            try
            {
                await DeletePlayerMessageAsync(player);
            } catch {}

            try
            {
                var message = await Bot.SendTextMessageAsync(player.UserId, text);

                player.LastSentTextMessage = message;
            }
            catch { }
        }

        private async Task SendImageMessageAsync(Player player, Stream stream, string caption, IReplyMarkup replyMarkup = null)
        {
            try
            {
                await DeletePlayerMessageAsync(player, withImage: true);
            } catch {}

            try
            {
                var message = await Bot.SendPhotoAsync(player.UserId, stream, caption, replyMarkup: replyMarkup);

                player.LastSentImageMessage = message;
            } catch {}
        }

        private async Task DeletePlayerMessageAsync(Player player, bool withImage = false)
        {
            if (player?.LastSentTextMessage != null)
            {
                try
                {
                    await Bot.DeleteMessageAsync(player.UserId, player.LastSentTextMessage.MessageId);
                }
                catch
                {
                    // TODO: add logging later
                }
                finally
                {
                    player.LastSentTextMessage = null;
                }
            }

            if (player != null && withImage && player.LastSentImageMessage != null)
            {
                try
                {
                    await Bot.DeleteMessageAsync(player.UserId, player.LastSentImageMessage.MessageId);
                }
                catch
                {
                    // TODO: add logging later
                }
                finally
                {
                    player.LastSentImageMessage = null;
                }
            }
        }

        private async void NotifyTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            _elapsedMs += _notifyTimer.Interval;

            if (_elapsedMs >= TimeoutOffsetMs)
            {
                await FinalUpdateAsync();
                await SendActivePlayerMessage($"На жаль, ти здався й отримав поразку! 😱 Переміг гравець {PassivePlayer.Name}");
                await SendPassivePlayerMessage($"Вітаю, гравець {ActivePlayer.Name} здався, а тому ти отримав перемогу!");

                await IncrementDbUserStatisticsAsync(PassivePlayer.TelegramUser, StatisticsCounter.EnemySurrendedGamesWon);

                await Task.Delay(5_000);

                IsFinished = true;

                Finish?.Invoke(this, EventArgs.Empty);

                Dispose();

                return;
            }

            _notifyTimer.Start();

            var remainingSec = (int)((TimeoutOffsetMs - _elapsedMs) / 1000);

            await SendActivePlayerMessage($"{PassivePlayer.Name} очікує твій хід, поспіши, або гра завершиться через {remainingSec} секунд");
            await SendPassivePlayerMessage($"Очікуй хід гравця {ActivePlayer.Name}. У нього залишилось {remainingSec} секунд");
        }

        private void RefreshTimer()
        {
            _notifyTimer.Stop();
            _notifyTimer.Interval = TimerIntervalMs;
            _elapsedMs = 0;
            _notifyTimer.Start();
        }

        public async void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            _notifyTimer.Stop();
            _notifyTimer.Dispose();
            _notifyTimer.Elapsed -= NotifyTimer_Elapsed;

            await DeletePlayerMessageAsync(ActivePlayer, true);
            await DeletePlayerMessageAsync(PassivePlayer, true);
        }

        private enum StatisticsCounter
        {
            UnitsDestroyed = 0,
            GamesWon = 1,
            EnemySurrendedGamesWon = 2
        }
    }
}
