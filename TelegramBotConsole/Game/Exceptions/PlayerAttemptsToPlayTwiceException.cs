using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TelegramBotConsole.Game.Exceptions
{
    public class PlayerAttemptsToPlayTwiceException : Exception
    {
        public PlayerAttemptsToPlayTwiceException(Player p) : base($"{p.User.FirstName}, ти вже робив(ла) спробу!")
        {
        }
    }
}
