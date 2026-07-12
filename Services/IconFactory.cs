using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace IpWidget.Services;

/// <summary>Draws the app/tray icon at runtime so we don't ship a binary asset.</summary>
public static class IconFactory
{
    // mdi "earth"
    private const string Earth =
        "M17.9,17.39C17.64,16.59 16.89,16 16,16H15V13A1,1 0 0,0 14,12H8V10H10A1,1 0 0,0 " +
        "11,9V7H13A2,2 0 0,0 15,5V4.59C17.93,5.77 20,8.64 20,12C20,14.08 19.2,15.97 17.9," +
        "17.39M11,19.93C7.05,19.44 4,16.08 4,12C4,11.38 4.08,10.78 4.21,10.21L9,15V16A2,2 " +
        "0 0,0 11,18M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2Z";

    public static WindowIcon Create()
    {
        var rtb = new RenderTargetBitmap(new PixelSize(64, 64), new Vector(96, 96));
        using (var ctx = rtb.CreateDrawingContext())
        {
            var bg = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            };
            bg.GradientStops.Add(new GradientStop(Color.Parse("#7C6CFF"), 0));
            bg.GradientStops.Add(new GradientStop(Color.Parse("#4FD1FF"), 1));

            ctx.DrawRectangle(bg, null, new RoundedRect(new Rect(0, 0, 64, 64), 14));

            // 24-unit glyph -> 38px, centered in 64px
            var glyph = Geometry.Parse(Earth);
            glyph.Transform = new MatrixTransform(
                Matrix.CreateScale(1.5833, 1.5833) * Matrix.CreateTranslation(13, 13));
            ctx.DrawGeometry(Brushes.White, null, glyph);
        }

        return new WindowIcon(rtb);
    }
}
