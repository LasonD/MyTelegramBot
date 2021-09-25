using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TelegramBotConsole.Drawing
{
    public class Drawer
    {
        public static string ImgPath { get; } = Path.Combine(Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.FullName, "Drawing/Images/painting.png");
        public void Draw(float x, float y, Color color)
        {
            using (Image image = Image.FromFile(ImgPath))
            {
                Graphics g = Graphics.FromImage(image);

                g.DrawRectangle(new Pen(color), x, y, 100, 100);

                image.Save(ImgPath);
            }
        }
    }
}
