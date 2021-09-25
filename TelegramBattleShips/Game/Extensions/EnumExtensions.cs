using TelegramBattleShips.Game.Enums;

namespace TelegramBattleShips.Game.Extensions
{
    public static class EnumExtensions
    {
        public static bool IsHorizontal(this Orientation orientation) => orientation == Orientation.Horizontal;

        public static bool IsVertical(this Orientation orientation) => orientation == Orientation.Vertical;
    }
}
