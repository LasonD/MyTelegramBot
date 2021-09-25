using System;

namespace TelegramBotConsole.Game.Exceptions
{
    public class LetterIsTriedException : Exception
    {
        public LetterIsTriedException(Player p, char letter) : base($"{p.User.FirstName}, '{letter}' вже була!")
        {
        }
    }
}
