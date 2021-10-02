using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Args;

namespace BotConsoleInterface2
{
    class Program : IDisposable
    {
        private static readonly string Token = "1201353955:AAHGjdefg5lxS8jTNMqktyHTe1CAW9U6nKc";
        private static readonly ITelegramBotClient Bot = new TelegramBotClient(Token);

        static void Main(string[] args)
        {
            Bot.OnMessage += TryOnMessageReceived;

            Bot.StartReceiving();
            Console.ReadLine();
            Bot.StopReceiving();
        }

        private static async void TryOnMessageReceived(object sender, MessageEventArgs e)
        {
            try
            {
                await OnMessageReceived(sender, e);
            }
            catch (Exception ex)
            {
                await Bot.SendTextMessageAsync(e.Message.Chat, ex.Message);
            }
        }

        private static async Task OnMessageReceived(object sender, MessageEventArgs e)
        {
            var text = e.Message.Text;
            var chatId = e.Message.From.Id; // "1052853772"

            Console.WriteLine($"{e.Message.From.FirstName} sent {text}");

            var message = await Bot.SendTextMessageAsync(chatId, text.First().ToString().ToUpper());

            var previousInitial = text.First();

            foreach (var ch in text.Skip(1))
            {
                if (ch == ' ')
                {
                    message.Text += ' ';
                    continue;
                }

                message.Text = message.Text.Substring(0, message.Text.Length - 1) + previousInitial;

                message = await Bot.EditMessageTextAsync(chatId, message.MessageId, message.Text + ch.ToString().ToUpper());

                await Task.Delay(200);

                previousInitial = ch;
            }

            message.Text = message.Text.Substring(0, message.Text.Length - 1);

            await Bot.EditMessageTextAsync(chatId, message.MessageId, message.Text + previousInitial);
        }

        public void Dispose()
        {

        }
    }
}
