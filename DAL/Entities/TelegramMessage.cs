using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace DAL.Entities
{
    public class TelegramMessage
    {
        [Key]
        public int Id { get; set; }
        public TelegramMessage() { }
        public TelegramMessage(Message message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            MessageId = message.MessageId;
            Text = message.Text;
            Type = message.Type;
            Sent = message.Date;
            Sender = new TelegramUser(message.From);
        }

        public string Text { get; set; }
        public int MessageId { get; set; }
        public MessageType Type { get; set; }
        public DateTime Sent { get; set; }

        [ForeignKey(nameof(Sender))]
        public int SenderId { get; set; }
        public TelegramUser Sender { get; set; }
        public override string ToString() => JsonConvert.SerializeObject(this,
            new JsonSerializerSettings() 
            { 
                MaxDepth = 1, 
                Formatting = Formatting.Indented, 
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore 
            });
    }
}
