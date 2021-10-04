using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramBotConsole.Game.Exceptions;

namespace TelegramBotConsole.Game
{
    public class HangGame : IDisposable
    {
        private static readonly string[] _standardWords = { "Вогнетривкий", "Абсурд", "Гірський", "Переробка", "Турбогвинтовий", "Автоматичний", "Дагестан", "Абсорбуючий", "Чорноморець", "Дивовижний", "Заріччя", "Переможний", "Лавровий", "Присяжний", "Несподіванка", "Устриця", "Культурний", "Сонячносяйний", "Султан" };
        private string _word;
        private StringBuilder _guessed;

        public HangGame(ChatId chat, ITelegramBotClient bot, bool isSolo = false) : this(_standardWords[new Random().Next(0, _standardWords.Length)], chat, bot, isSolo) { }
        public HangGame(string word, ChatId chat, ITelegramBotClient bot, bool isSolo = false)
        {
            Bot = bot ?? throw new ArgumentNullException(nameof(bot));
            Chat = chat ?? throw new ArgumentNullException(nameof(chat));
            Word = word ?? throw new ArgumentNullException(nameof(word));
            IsSolo = isSolo;
            RefreshPictureWithCaptionAsync().Wait();
        }

        public bool IsSolo { get; set; }
        public HashSet<Player> Players { get; } = new HashSet<Player>();
        public Player LastPlayer { get; private set; }
        public List<char> TriedLetters { get; } = new List<char>();
        public GameState CurrentState { get; private set; } = GameState.Started;
        public Message PictureWithCaption { get; private set; }
        public Message LastWarn { get; private set; }
        public Message LastProgress { get; private set; }
        public Message FinalAudio { get; private set; }
        public Message StatisticsMessage { get; private set; }
        public ChatId Chat { get; }
        public ITelegramBotClient Bot { get; }
        public string Word 
        {
            get => _word;
            private set
            {
                _word = value;
                _guessed = new StringBuilder(string.Concat(Enumerable.Range(0, value.Length)
                    .Select(c => '_')
                    .ToArray()));
            }
        }

        public string StatisticsStr => string.Join("\n", Players
            .OrderByDescending(p => p.Guessed)
            .ThenBy(p => p.User.FirstName)
            .Select(p => $"{p.Guessed} {p.User.FirstName,-5}"));

        public int RemainingCount => Progress.Count(c => c == '_');

        public string Progress => _guessed.ToString().ToUpper();

        public async Task MakeAttemptAsync(User user, char letter)
        {
            string response;

            Player player = null;
            try
            {
                if (CurrentState == GameState.Victory || CurrentState == GameState.Gameover)
                    throw new GameFinishedException();

                letter = char.ToLower(letter);

                player = Players.FirstOrDefault(p => p.User.Equals(user));
                if (player == null)
                    Players.Add(player = new Player(user));

                if (!IsSolo && (LastPlayer?.Equals(player) ?? false))
                    throw new PlayerAttemptsToPlayTwiceException(player);

                if (Regex.IsMatch($"{letter}", @"\P{IsCyrillic}"))
                    throw new NotCyrillicLetterException(letter);

                if (TriedLetters.Contains(letter))
                    throw new LetterIsTriedException(player, letter);

                LastPlayer = player;
            } 
            catch (LetterIsTriedException ex)
            {
                await RemoveLastWarnAsync();
                LastWarn = await Bot.SendTextMessageAsync(Chat, $"{ex.Message}\n<b>Вже випробувані літери: {string.Join(", ", TriedLetters)}</b>", ParseMode.Html);
                return;
            }
            catch (Exception ex)
            {
                await RemoveLastWarnAsync();
                LastWarn = await Bot.SendTextMessageAsync(Chat, ex.Message);
                return;
            }

            TriedLetters.Add(letter); // mark the letter as already tried

            int matchedCount = Word.Count(c => char.ToLower(c).Equals(letter));

            if (matchedCount > 0)
            {
                foreach (Match m in Regex.Matches(Word, $"{letter}", RegexOptions.IgnoreCase))
                    _guessed[m.Index] = letter;

                player.Guessed += matchedCount;
                player.Streak++;

                if (Progress.Equals(Word, StringComparison.OrdinalIgnoreCase))
                {
                    CurrentState = GameState.Victory;

                    response = $"Ура! {player.User.FirstName}, врятував(ла) чоловічка! \U0001F603" +
                        $"Найрезультативнішим гравцем був(ла) <b>" +
                        $"{player.User.FirstName}</b>, вгадавши {LetterNumSpell(player.Guessed)} \U0001F451";
                }
                else
                {
                    response = $"Вітаю, {player.User.FirstName}, ти вгадав(ла) " +
                        $"{matchedCount} {(matchedCount == 1 ? "літеру" : "літери")}! {StreakMessage(player)} Можеш спробувати ще раз. Залишилось {LetterNumSpell(RemainingCount)}.";
                    
                    LastPlayer = null;
                }
            }
            else
            {
                CurrentState += 1;
                response = $"{player.User.FirstName}, на жаль, слово не містить літеру '{letter}'.";
                player.Streak = 0;
                if (CurrentState == GameState.Gameover)
                    response += " Ви програли! \U0001F61E\n" +
                        $"Слово: <b>{Word.ToUpper()}</b>.";
            }

            await RefreshPictureWithCaptionAsync(response);
            if (CurrentState != GameState.Gameover) 
                await RefreshLastProgressAsync();
            await RemoveLastWarnAsync();

            if (CurrentState == GameState.Victory || CurrentState == GameState.Gameover)
            {
                await SendFinalAudioAsync();
                StatisticsMessage = await Bot.SendTextMessageAsync(Chat, $"Підсумки гри:\n<b>{StatisticsStr}</b>\n\n/clear 👐", ParseMode.Html);

                Console.WriteLine($"Response:\n\t{response}\n");
            }
        }

        private async Task SendFinalAudioAsync()
        {
            var audioName = GetSongName();
            var caption = CurrentState == GameState.Victory ? "Тримай свій переможний ганста репчик 😋" : "Тримай свою програшну музику 😒";
            await using Stream audioStream = System.IO.File.OpenRead(Path.Combine(Directory.GetCurrentDirectory(), @"Game\Music", audioName));
            FinalAudio = await Bot.SendAudioAsync(Chat, audioStream, caption, title: audioName);
        }

        private async Task RemoveLastWarnAsync()
        {
            if (LastWarn != null)
            {
                await Bot.DeleteMessageAsync(Chat, LastWarn.MessageId);
                LastWarn = null;
            }
        }

        private async Task RefreshPictureWithCaptionAsync(string message = "")
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), @"Game\Images", GetImgName());

            await using Stream imgStream = System.IO.File.OpenRead(path);
            if (PictureWithCaption == null)
            {
                PictureWithCaption = await Bot.SendPhotoAsync(Chat, new InputOnlineFile(imgStream), $"Гра розпочалася! \U0000261D\n" +
                    $"Слово із {Word.Length} літер.", replyMarkup: GetTryInlineButtons(), parseMode: ParseMode.Html);
            }
            else
            {
                await Bot.DeleteMessageAsync(Chat, PictureWithCaption.MessageId);
                PictureWithCaption = await Bot.SendPhotoAsync(Chat, new InputOnlineFile(imgStream), message, replyMarkup: GetTryInlineButtons(), parseMode: ParseMode.Html); // TODO: Bot.EditMessageMediaAsync(Chat, Picture.MessageId, new InputOnlineFile(imgStream))
            }
        }

        private async Task RefreshLastProgressAsync()
        {
            if (LastProgress != null)
            {
                await Bot.DeleteMessageAsync(Chat, LastProgress.MessageId);
                LastProgress = null;
            }

            LastProgress = await Bot.SendTextMessageAsync(Chat, $"👉 <b>{Progress}</b>", ParseMode.Html);
        }

        private ReplyKeyboardMarkup GetTryInlineButtons()
        {
            char[] alphabet = { 'а', 'б', 'в', 'г', 'ґ', 'д', 'е', 'є', 'ж', 'з', 'и', 'і', 'ї', 'й', 'к', 'л', 'м', 'н', 'о', 'п', 'р', 'с', 'т', 'у', 'ф', 'х', 'ц', 'ч', 'ш', 'щ', 'ь', 'ю', 'я'};
            var buttons = alphabet
                .Except(TriedLetters)
                .Select(x => new KeyboardButton { Text = $"/try {x}" });

            int count = 0;
            var buttonsFormatted = new List<IEnumerable<KeyboardButton>>();
            do
            {
                buttonsFormatted.Add(buttons
                .Skip(count)
                .Take(5));
                count += 5;
            } while (count <= buttons.Count());
                
            return new ReplyKeyboardMarkup(buttonsFormatted, oneTimeKeyboard: true, resizeKeyboard: true);
        }

        private string GetSongName() => CurrentState == GameState.Victory ? "victory_song.mp3" : "gameover_song.mp3";

        private string GetImgName() => CurrentState switch
        {
            GameState.Started => "state0.jpg",
            GameState.Head => "state1.jpg",
            GameState.Body => "state2.jpg",
            GameState.FirstHand => "state3.jpg",
            GameState.SecondHand => "state4.jpg",
            GameState.FirstLeg => "state5.jpg",
            GameState.Victory => "victory.jpg",
            GameState.Gameover => "gameover.gif",
            _ => throw new NotImplementedException(),
        };

        private string LetterNumSpell(int num) => num switch
        {
            1 => $"{num} літера",
            2 => $"{num} літери",
            3 => $"{num} літери",
            4 => $"{num} літери",
            _ => $"{num} літер"
        };

        private string StreakMessage(Player p) => p.Streak switch
        {
            0 => string.Empty,
            1 => string.Empty,
            2 => "Подвійне влучення! 🧐",
            3 => "Потрійне влучення! 😮",
            4 => "Чотири влучення під ряд! 😲",
            5 => "Що за чіти? 😵 П'ять влучень під ряд!",
            6 => "Ти сьогодні в ударі! 🤯🤠",
            _  => "Ти сьогодні в ударі! 🤯🤠🙀",
        };

        public async void Dispose()
        {
            try
            {
                if (PictureWithCaption != null)
                    await Bot.DeleteMessageAsync(Chat, PictureWithCaption.MessageId);
            }
            catch (Exception) { }
            try
            {
                if (LastWarn != null)
                    await Bot.DeleteMessageAsync(Chat, LastWarn.MessageId);
            }
            catch (Exception) { }
            try
            {
                if (LastProgress != null)
                    await Bot.DeleteMessageAsync(Chat, LastProgress.MessageId);
            }
            catch (Exception) { }
            try
            {
                if (StatisticsMessage != null)
                    await Bot.DeleteMessageAsync(Chat, StatisticsMessage.MessageId);
            }
            catch (Exception) { }
            try
            {
                if (FinalAudio != null)
                    await Bot.DeleteMessageAsync(Chat, FinalAudio.MessageId);
            }
            catch (Exception) { }
        }
    }

    public enum GameState { Started, Head, Body, FirstHand, SecondHand, FirstLeg, Gameover, Victory }
}
