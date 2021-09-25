using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TelegramBotConsole.Game.Exceptions
{
    public class GameFinishedException : Exception
    {
        public GameFinishedException() : base("Гра вже завершена!")
        {
        }
    }
}
