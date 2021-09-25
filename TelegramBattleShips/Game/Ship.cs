using System.Collections.Generic;
using System.Linq;
using TelegramBattleShips.Game.Enums;

namespace TelegramBattleShips.Game
{
    public class Ship
    {
        private readonly CellState[,] _map;
        public ShipType ShipType { get; }
        public bool IsHorizontal { get; }
        public bool IsAlive { get; private set; } = true;
        public int AliveCells => Cells.Count(c => c.State == CellState.AliveShip);
        private ShipCell[] Cells { get; }

        public Ship(CellState[,] map, (int row, int col)[] positions)
        {
            _map = map;
            ShipType = (ShipType)positions.Length;
            Cells = positions.Select(p => new ShipCell(p.row, p.col)).ToArray();
            IsHorizontal = positions.All(p => p.row == positions.First().row);

            Cells.ToList().ForEach(c => _map[c.Row, c.Col] = c.State);
        }

        public bool Hit(int row, int col)
        {
            var cell = Cells.FirstOrDefault(p => p.Row == row && p.Col == col && p.State == CellState.AliveShip);

            if (cell != null)
            {
                cell.State = CellState.PartiallyDestroyedShip;
                _map[cell.Row, cell.Col] = cell.State;

                if (Cells.All(c => c.State != CellState.AliveShip))
                {
                    foreach (var c in Cells)
                    {
                        c.State = CellState.DestroyedShip;
                        _map[c.Row, c.Col] = c.State;
                    }

                    IsAlive = false;
                    MapDestructedArea();
                }

                return true;
            }

            _map[row, col] = CellState.Miss;

            return false;
        }

        private void MapDestructedArea()
        {
            Cells.ToList().ForEach(c =>
            {
                MapIfWithinMap(c.Row + 1, c.Col);
                MapIfWithinMap(c.Row, c.Col + 1);
                MapIfWithinMap(c.Row + 1, c.Col + 1);
                MapIfWithinMap(c.Row - 1, c.Col);
                MapIfWithinMap(c.Row, c.Col - 1);
                MapIfWithinMap(c.Row - 1, c.Col - 1);
                MapIfWithinMap(c.Row + 1, c.Col - 1);
                MapIfWithinMap(c.Row - 1, c.Col + 1);
            });
        }

        private void MapIfWithinMap(int row, int col)
        {
            if (row < 0 || row >= 10 || col < 0 || col >= 10) return;

            var cell = Cells.FirstOrDefault(c => c.Row == row && c.Col == col);

            _map[row, col] = cell?.State ?? CellState.Miss;
        }

        public class ShipCell
        {
            public ShipCell(int row, int col)
            {
                Row = row;
                Col = col;
            }

            public int Row { get; }
            public int Col { get; }
            public CellState State { get; set; } = CellState.AliveShip;
        }
    }
}
