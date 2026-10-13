using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace MicVolumeGuard;

/// <summary>Генерация иконки трея с процентом громкости (без внешних файлов).</summary>
internal static class TrayIconFactory
{
    public static Icon Create(int percent, bool locked)
    {
        int size = 16; // системный трей (при DPI>100% Windows сам масштабирует)
        using var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);

            var bg = locked ? Color.FromArgb(0, 120, 215) : Color.FromArgb(90, 90, 90);
            using var bgBrush = new SolidBrush(bg);
            g.FillEllipse(bgBrush, 0, 0, size - 1, size - 1);

            string text = percent switch
            {
                >= 100 => "100", // поместится мелким шрифтом
                >= 10 => percent.ToString(),
                _ => percent.ToString()
            };
            using var font = new Font("Segoe UI", percent >= 100 ? 6.5f : 7.5f, FontStyle.Bold, GraphicsUnit.Point);
            var sz = g.MeasureString(text, font);
            using var fg = new SolidBrush(Color.White);
            g.DrawString(text, font, fg, (size - sz.Width) / 2f, (size - sz.Height) / 2f);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }
}
