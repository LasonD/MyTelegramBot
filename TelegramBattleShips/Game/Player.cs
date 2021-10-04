using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using TelegramBattleShips.Game.Enums;

namespace TelegramBattleShips.Game
{
    public class Player
    {
        public Player(User user)
        {
            TelegramUser = user ?? throw new ArgumentNullException(nameof(user));
            Field.LocateFleetRandomly();
        }

        private Field Field { get; set; } = new Field();

        public int Streak { get; private set; }

        public User TelegramUser { get; private set; }

        public Message OldSentImageMessage { get; set; }

        public Message OldSentTextMessage { get; set; }

        public Message LastSentImageMessage { get; set; }

        public Message LastSentTextMessage { get; set; }

        public int UserId => TelegramUser.Id;

        public int AliveFleet => Field.AliveFleet;

        public string Name => $"{TelegramUser.FirstName} {TelegramUser.LastName}".Trim();

        public bool Hit(string cell)
        {
            var isHit = Field.Hit(cell);

            Streak = isHit ? ++Streak : 0;

            return isHit;
        }

        public IEnumerable<string> GetAvailableHits() => Field.GetAvailableHits();

        public Task<Stream> GetFieldImageStreamAsync(FieldView fieldType) => Field.GetFieldImageStreamAsync(fieldType);
    }
}
