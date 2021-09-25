using System;
using System.Diagnostics.CodeAnalysis;
using Telegram.Bot.Types;

namespace TelegramBotConsole.Game
{
    public sealed class Player : IEquatable<Player>
    {
        public User User { get; }
        public Player(User user) => User = user ?? throw new ArgumentNullException(nameof(user));
        public int Streak { get; set; } = 0;
        public int Guessed { get; set; } = 0;
        public bool Equals([AllowNull] Player other) => other.User.Equals(this.User);
    }
}
