using System;

namespace TelegramBattleShips.Game.Exceptions
{
    public class NotYourTurnException : Exception
    {
        public NotYourTurnException() : base("Зараз не твоя черга!")
        {

        }
    }
}
