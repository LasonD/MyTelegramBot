using System;

namespace TelegramBattleShips.Game.Exceptions
{
    public class GameIsNotReadyException : Exception
    {
        public GameIsNotReadyException() : base("Not all player are set")
        {

        }
    }
}
