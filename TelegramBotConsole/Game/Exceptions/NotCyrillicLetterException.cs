using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TelegramBotConsole.Game.Exceptions
{
    public class NotCyrillicLetterException : Exception
    {
        public NotCyrillicLetterException(char letter) : base(@$"Літера повинна відноситися до кирилиці! '{letter}' не є кириличною.")
        {
        }
    }
}
