using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TelegramBattleShips.Game.Enums;
using TelegramBattleShips.Game.Exceptions;

namespace TelegramBattleShips.Game
{
    public class Field
    {
        private const int WidthPx = 1024;
        private const int HeightPx = 1024;
        private const int BorderWidthPx = 5;
        private const int Rows = 10;
        private const int Cols = 10;
        private const int CellHeight = HeightPx / Rows;
        private const int CellWidth = WidthPx / Cols;
        private const int EmSize = 25;

        private readonly Random _rnd = new Random();
        private readonly CellState[,] _field = new CellState[Rows, Cols];
        private readonly ShipType[] fleetContent = {
            ShipType.Carrier,
            ShipType.BattleShip,
            ShipType.BattleShip,
            ShipType.Destroyer,
            ShipType.Destroyer,
            ShipType.Destroyer,
            ShipType.PatrolBoat,
            ShipType.PatrolBoat,
            ShipType.PatrolBoat,
            ShipType.PatrolBoat,
        };

        private readonly List<Ship> ships = new List<Ship>();

        private readonly string[] RowIdentifiers = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "10" };
        private readonly string[] ColIdentitiers = { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J" };

        public int AliveFleet => ships.Sum(s => s.AliveCells);

        public IEnumerable<string> GetAvailableHits()
        {
            var identifiers = new List<string>();

            for (int row = 0; row < Rows; row++)
            {
                for (int col = 0; col < Cols; col++)
                {
                    var cell = _field[row, col];

                    if (cell == CellState.AliveShip || cell == CellState.ClearWater)
                    {
                        identifiers.Add(GetCellIdentifier(row, col));
                    }
                }
            }

            return identifiers.OrderBy(x => x);
        }

        public bool Hit(string cell)
        {
            var cellIdentifier = cell.Trim();

            if (string.IsNullOrWhiteSpace(cellIdentifier)) throw new ArgumentNullException(nameof(cell));
            if (cellIdentifier.Length != 2 && cellIdentifier.Length != 3) throw new ArgumentException();

            var colIdentifier = cell.Substring(0, 1);
            var rowIdentifier = cell.Substring(1, (cellIdentifier.Length == 2 ? 1 : 2));

            int row = 0, col = 0;

            for (; row < Rows; row++)
            {
                if (RowIdentifiers[row].Equals(rowIdentifier, StringComparison.OrdinalIgnoreCase)) break;
            }

            for (; col < Cols; col++)
            {
                if (ColIdentitiers[col].Equals(colIdentifier, StringComparison.OrdinalIgnoreCase)) break;
            }

            return Hit(row, col);
        }

        public bool Hit(int row, int col)
        {
            if (row < 0 || row >= Rows) throw new ArgumentOutOfRangeException(nameof(row));
            if (col < 0 || col >= Cols) throw new ArgumentOutOfRangeException(nameof(col));

            return ships.Select(s => s.Hit(row, col)).Any(r => r);
        }

        private string GetCellIdentifier(int row, int col) => ColIdentitiers[col] + RowIdentifiers[row];

        public void LocateFleetRandomly()
        {
            foreach (var ship in fleetContent)
            {
                LocateShipRandomly(ship);
            }
        }

        private void LocateShipRandomly(ShipType shipType)
        {
            var shipSize = (int) shipType;

            var isLocated = false;

            while (!isLocated)
            {
                var isHorizontal = _rnd.NextDouble() > 0.5;

                if (isHorizontal)
                {
                    var x = _rnd.Next(0, Cols - shipSize);
                    var y = _rnd.Next(0, Rows);

                    if (!IsValidLocation(x, y, true, shipSize)) continue;

                    var positions = Enumerable
                        .Range(0, shipSize)
                        .Select(i => (x + i, y))
                        .ToArray();

                    ships.Add(new Ship(_field, positions));

                    isLocated = true;
                }
                else
                {
                    var x = _rnd.Next(0, Cols);
                    var y = _rnd.Next(0, Rows - shipSize);

                    if (!IsValidLocation(x, y, false, shipSize)) continue;

                    for (int i = 0; i < shipSize; i++)
                    {
                        _field[x, y + i] = CellState.AliveShip;
                    }

                    isLocated = true;
                }
            }
        }

        private bool IsValidLocation(int x, int y, bool isHorizontal, int shipSize)
        {
            if (isHorizontal)
            {
                for (int i = -1; i <= shipSize + 1; i++)
                {
                    if (!IsClearWaterOrFieldBorder(x + i, y) || 
                        !IsClearWaterOrFieldBorder(x + i, y - 1) || 
                        !IsClearWaterOrFieldBorder(x + i, y + 1)) return false;
                }
            }
            else
            {
                for (int i = -1; i <= shipSize + 1; i++)
                {
                    if (!IsClearWaterOrFieldBorder(x, y + i) ||
                        !IsClearWaterOrFieldBorder(x + 1, y + i) ||
                        !IsClearWaterOrFieldBorder(x - 1, y + i)) return false;
                }
            }

            return true;
        }

        private bool IsClearWaterOrFieldBorder(int x, int y) =>
            x < 0 || y < 0 || x >= Cols || y >= Rows || _field[x, y] == CellState.ClearWater;

        public Task<Stream> GetFieldImageStreamAsync(FieldView fieldType) => Task.Run(() =>
        {
            var bmp = new Bitmap(WidthPx, HeightPx);

            using var graph = Graphics.FromImage(bmp);
            var imageSize = new Rectangle(0, 0, WidthPx, HeightPx);

            graph.FillRectangle(Brushes.White, imageSize);

            for (int row = 0; row < Rows; row++)
            {
                for (int col = 0; col < Cols; col++)
                {
                    DrawFieldCell(row, col, fieldType, graph);
                    DrawCellIdentifier(row, col, graph);
                }
            }

            var tempFile = Path.GetRandomFileName();

            bmp.Save(tempFile, ImageFormat.Png);

            return File.OpenRead(tempFile) as Stream;
        });

        private void DrawFieldCell(int row, int col, FieldView fieldType, Graphics graph)
        {
            var x = col * CellWidth;
            var y = row * CellHeight;
            var cell = new Rectangle(x, y, CellWidth, CellHeight);

            switch (_field[row, col])
            {
                case CellState.AliveShip when fieldType == FieldView.Restricted:
                case CellState.ClearWater:
                    graph.FillRectangle(Brushes.LightBlue, cell);
                    break;
                case CellState.AliveShip:
                    FillAliveShipCell(graph, cell);
                    break;
                case CellState.Miss:
                    FillMissedCell(graph, cell);
                    break;
                case CellState.PartiallyDestroyedShip:
                    FillPartiallyDestroyedCell(graph, cell);
                    break;
                case CellState.DestroyedShip:
                    FillDestroyedCell(graph, cell);
                    break;
            }
        }

        private void FillDestroyedCell(Graphics graph, Rectangle cell)
        {
            graph.FillRectangle(Brushes.OrangeRed, cell);

            graph.DrawLine(new Pen(Brushes.DarkRed, BorderWidthPx), 
                new Point(cell.X, cell.Y), 
                new Point(cell.X + CellWidth, cell.Y + CellHeight));

            graph.DrawLine(new Pen(Brushes.DarkRed, BorderWidthPx), 
                new Point(cell.X + CellWidth, cell.Y), 
                new Point(cell.X, cell.Y + CellHeight));

            graph.DrawRectangle(new Pen(Brushes.DarkRed, BorderWidthPx), cell);
        }

        private void FillPartiallyDestroyedCell(Graphics graph, Rectangle cell)
        {
            graph.FillRectangle(Brushes.OrangeRed, cell);

            graph.DrawRectangle(new Pen(Brushes.DarkRed, BorderWidthPx), cell);
        }

        private void FillMissedCell(Graphics graph, Rectangle cell)
        {
            graph.FillRectangle(Brushes.LightBlue, cell);

            graph.DrawLine(new Pen(Brushes.DodgerBlue, BorderWidthPx), 
                new Point(cell.X + EmSize, cell.Y + EmSize), 
                new Point(cell.X + CellWidth - EmSize, cell.Y + CellHeight - EmSize));

            graph.DrawLine(new Pen(Brushes.DodgerBlue, BorderWidthPx), 
                new Point(cell.X + CellWidth - EmSize, cell.Y + EmSize), 
                new Point(cell.X + EmSize, cell.Y + CellHeight - EmSize));
        }

        private void FillAliveShipCell(Graphics graph, Rectangle cell)
        {
            graph.FillRectangle(Brushes.SandyBrown, cell);

            graph.DrawRectangle(new Pen(Brushes.SaddleBrown, BorderWidthPx), cell);
        }

        private void DrawCellIdentifier(int row, int col, Graphics graph)
        {
            var x = col * CellWidth;
            var y = row * CellHeight;
            var cell = new Rectangle(x + EmSize / 2, y + EmSize / 2, CellWidth, CellHeight);

            var identifier = GetCellIdentifier(row, col);

            graph.DrawString(identifier,
                new Font(FontFamily.GenericSansSerif, EmSize, FontStyle.Regular, GraphicsUnit.Pixel),
                Brushes.CadetBlue, cell);
        }
    }
}
