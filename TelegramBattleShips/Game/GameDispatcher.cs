using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DAL;
using DAL.Data;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace TelegramBattleShips.Game
{
    public class GameDispatcher : IDisposable
    {
        private const string StartGameCommand = "/startseabattle";
        private const string HitCommandPrefix = "/hit";

        private readonly TelegramDbContext _context = DbContextSingletone.GetContext();
        private readonly ITelegramBotClient _bot;

        public GameDispatcher(ITelegramBotClient bot) => _bot = bot ?? throw new ArgumentNullException(nameof(bot));

        private ConcurrentDictionary<User, TelegramBattleShips> Games { get; } = new ConcurrentDictionary<User, TelegramBattleShips>();

        private Dictionary<int, BlockingCollection<Message>> SentMessages { get; set; } = new Dictionary<int, BlockingCollection<Message>>();

        public async Task<bool> TryProcessMessageAsync(User user, string message)
        {
            await DeleteSentMessagesAsync(user);

            if (message.Equals(StartGameCommand))
            {
                await ProcessStartNewGameCommandAsync(user);
                return true;
            }

            if (message.StartsWith(HitCommandPrefix))
            {
                await ProcessHitCommandAsync(user, message);
                return true;
            }

            return false;
        }

        public IEnumerable<User> CurrentPlayers => Games.Keys;

        private async Task ProcessStartNewGameCommandAsync(User user)
        {
            IEnumerable<(User player, TelegramBattleShips game)> usersAndGamesWaitingForSecondPlayer;

            if (Games.TryGetValue(user, out var g))
            {
                if (g.IsFinished)
                {
                    usersAndGamesWaitingForSecondPlayer = GetPlayersWaitingForSecondPlayerWithGames();

                    if (usersAndGamesWaitingForSecondPlayer.Any())
                    {
                        var (_, game) = usersAndGamesWaitingForSecondPlayer.First();

                        await SetSecondPlayerToGameAsync(game, user);
                    }
                    else
                    {
                        Games[user] = new TelegramBattleShips(_bot, user);
                    }

                    return;
                }

                await SendMessageAsync(user, "Ти вже маєш розпочату гру!");
                return;
            }

            usersAndGamesWaitingForSecondPlayer = GetPlayersWaitingForSecondPlayerWithGames();

            if (usersAndGamesWaitingForSecondPlayer.Any())
            {
                var (waitingPlayer, game) = usersAndGamesWaitingForSecondPlayer.First();

                await SetSecondPlayerToGameAsync(game, user);
            }
            else
            {
                Games[user] = new TelegramBattleShips(_bot, user);

                //await NotifyAboutWaitingGameAsync(user);
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

        private List<(User player, TelegramBattleShips game)> GetPlayersWaitingForSecondPlayerWithGames() => Games
            .Where(kvp => Games.Values.Count(g => kvp.Value == g) == 1)
            .Select(p => (p.Key, p.Value))
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
