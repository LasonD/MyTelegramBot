using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DAL;
using DAL.Data;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace TelegramBattleShips.Game
{
    public class GameDispatcher : IDisposable
    {
        private const string StartGameCommand = "/startseabattle";
        private const string HitCommandPrefix = "/hit ";
        private const string BattleShipsLeaderBoardCommand = "/leaderboard";
        private const string ClearCommand = "/clear";

        private readonly TelegramDbContext _context = DbContextSingletone.GetContext();
        private readonly ITelegramBotClient _bot;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1);
        private readonly string[] PlaceEmoji = { "🥇", "🥈", "🥉", "4️⃣", "5️⃣", "6️⃣", "7️⃣", "8️⃣", "9️⃣", "🔟" };

        public GameDispatcher(ITelegramBotClient bot)
        {
            _bot = bot ?? throw new ArgumentNullException(nameof(bot));
            _bot.OnMessage += OnMessageHandler;
        }

        private ConcurrentDictionary<User, TelegramBattleShips> Games { get; } = new ConcurrentDictionary<User, TelegramBattleShips>();

        private Dictionary<int, BlockingCollection<Message>> SentMessages { get; set; } = new Dictionary<int, BlockingCollection<Message>>();

        private async void OnMessageHandler(object sender, MessageEventArgs e)
        {
            var msg = e.Message;

            if (msg.Type != MessageType.Text) return;

            var text = msg.Text;
            var user = msg.From;

            if (text.Equals(StartGameCommand, StringComparison.OrdinalIgnoreCase))
            {
                await ProcessStartNewGameCommandAsync(user);
            }

            if (text.StartsWith(HitCommandPrefix, StringComparison.OrdinalIgnoreCase))
            {
                await ProcessHitCommandAsync(user, text);
            }

            if (text.Equals(BattleShipsLeaderBoardCommand, StringComparison.OrdinalIgnoreCase))
            {
                await ProcessLeaderBoardCommandAsync(user);
            }
        }

        private async Task ProcessLeaderBoardCommandAsync(User user)
        {
            var leaders = _context
                .TelegramUsers
                .Where(x => x.BattleShipGamesWon > 0 || x.ShipUnitsDestroyed > 0)
                .OrderByDescending(x => x.BattleShipGamesWon)
                .ThenByDescending(x => x.ShipUnitsDestroyed)
                .Take(10)
                .AsEnumerable()
                .Select((x, i) => $"<b>{PlaceEmoji[i]}. {x.FirstName} {x.LastName}</b>\tворожих юнітів знищено: <b>{x.ShipUnitsDestroyed}</b>\tвиграшів: <b>{x.BattleShipGamesWon}</b>")
                .Prepend("Топ 10 гравців")
                .ToList();

            if (!leaders.Any())
            {
                await _bot.SendTextMessageAsync(user.Id, "Таблиця лідерів поки що пуста.\nТи можеш це змінити!😊");
            }
            else
            {
                await _bot.SendTextMessageAsync(user.Id, string.Join("\n", leaders), ParseMode.Html);
            }
        }

        public IEnumerable<User> CurrentPlayers => Games.Keys;

        private async Task ProcessStartNewGameCommandAsync(User user)
        {
            await DeleteSentMessagesAsync(user);

            try
            {
                await _semaphore.WaitAsync();

                if (Games.TryGetValue(user, out var game))
                {
                    if (game.Player2 == null)
                    {
                        await SendMessageAsync(user, "Твоя гра створена, очікуй іншого гравця!");
                        return;
                    }
                    else
                    {
                        if (user.Equals(game.PassivePlayer.TelegramUser))
                        {
                            await SendMessageAsync(user, $"Ти вже маєш розпочту гру!\nОчікуй хід гравця {game.ActivePlayer.Name}");
                        }
                        else if (user.Equals(game.ActivePlayer.TelegramUser))
                        {
                            await SendMessageAsync(user, $"Ти вже маєш розпочту гру!\nГравець {game.PassivePlayer.Name} очікує твій хід.");
                        }
                        
                        return;
                    }
                }

                var waitingGames = GetGamesWaitingForSecondPlayer();

                if (waitingGames.Any())
                {
                    await SetSecondPlayerToGameAsync(waitingGames.First(), user);
                }
                else
                {
                    var newGame = new TelegramBattleShips(_bot, user, _context);
                    Games[user] = newGame;
                    newGame.Finish += OnGameFinishedHandler;

                    //await NotifyAboutWaitingGameAsync(user);
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private void OnGameFinishedHandler(object sender, EventArgs e)
        {
            try
            {
                Games
                    .Where(p => p.Value.IsFinished)
                    .ToList()
                    .ForEach(async p =>
                    {
                        var (player, _) = p;
                        Games.Remove(player, out _);
                        await DeleteSentMessagesAsync(player);
                    });

                var game = (TelegramBattleShips)sender;

                game.Dispose();
            }
            catch
            {
                // TODO: Add logging
            }
        }

        private async Task SetSecondPlayerToGameAsync(TelegramBattleShips game, User player2)
        {
            if (game.Player2 != null) throw new InvalidOperationException("Second player is already set for game.");
            if (game.Player1 == null) throw new InvalidOperationException("First player is not set for game.");

            Games[player2] = game;

            await game.SetSecondPlayerAsync(player2);
            await SendMessageAsync(game.Player1.TelegramUser, $"Користувач {player2.FirstName} {player2.LastName} приєднався до гри.\nТвій хід.");
        }

        private List<TelegramBattleShips> GetGamesWaitingForSecondPlayer() => Games
            .Where(kvp => Games.Values.Count(g => kvp.Value == g) == 1)
            .Select(p => p.Value)
            .ToList();

        private async Task NotifyAboutWaitingGameAsync(User waitingPlayer)
        {
            foreach (var user in _context.TelegramUsers.Where(u => !u.UserId.Equals(waitingPlayer.Id)))
            {
                try
                {
                    await SendMessageAsync((int)user.UserId,
                        $"Користувач {waitingPlayer.FirstName} {waitingPlayer.LastName} очікує " +
                        $"іншого гравця в морський бій. Щоб приєднатись, введи команду {StartGameCommand}");
                }
                catch
                {
                    //
                }
            }
        }

        private async Task ProcessHitCommandAsync(User user, string command)
        {
            if (!Games.ContainsKey(user))
            {
                await SendMessageAsync(user, "Ти не маєш розпочатої гри!");
                return;
            }

            var game = Games[user];

            await game.HitAsync(user, command);
        }

        private Task SendMessageAsync(User user, string text) => SendMessageAsync(user.Id, text);

        private async Task SendMessageAsync(int id, string text)
        {
            var message = await _bot.SendTextMessageAsync(id, text);

            SentMessages.TryAdd(id, new BlockingCollection<Message>());

            SentMessages[id].Add(message);
        }

        private async Task DeleteSentMessagesAsync(User user)
        {
            if (SentMessages.TryGetValue(user.Id, out var messages))
            {
                foreach (var m in messages)
                {
                    await _bot.DeleteMessageAsync(m.Chat.Id, m.MessageId);
                }

                SentMessages[user.Id] = new BlockingCollection<Message>();
            }
        }

        private async Task DeleteSentMessagesAsync()
        {
            foreach (var user in CurrentPlayers)
            {
                await DeleteSentMessagesAsync(user);
            }
        }

        public async void Dispose()
        {
            foreach (var game in Games.Values)
            {
                try
                {
                    game.Dispose();
                }
                catch
                {
                    //
                }
            }

            await DeleteSentMessagesAsync();
        }
    }
}
