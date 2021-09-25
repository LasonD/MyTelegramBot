using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Telegram.Bot.Types;

namespace DAL.Entities
{
    public class TelegramUser
    {
        [Key]
        public int Id { get; set; }
        public TelegramUser() { }
        public TelegramUser(User user)
        {
            if (user == null)
                throw new ArgumentNullException(nameof(user));
            UserId = user.Id;
            FirstName = user.FirstName;
            LastName = user.LastName;
            Username = user.Username;
        }
        public long UserId { get; set; }
        public string Username { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public List<TelegramMessage> Messages { get; set; } = new List<TelegramMessage>();
        public override string ToString() => JsonConvert.SerializeObject(this,
            new JsonSerializerSettings()
            {
                MaxDepth = 1,
                Formatting = Formatting.Indented,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            });
    }
}
