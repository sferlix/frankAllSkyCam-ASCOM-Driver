using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace AlpacaAllSkyWeather.TrayHost;

/// <summary>
/// Builds the app's tray/window icon in code (cloud + star on a dark circular badge) — no image asset needed.
/// Encodes a real multi-resolution .ico with PNG-compressed frames so every frame keeps true alpha
/// transparency and crisp edges; <see cref="Bitmap.GetHicon"/> would instead produce a hard 1-bit mask
/// with visibly jagged edges at tray size.
/// </summary>
public static class TrayIconFactory
{
    private static readonly Color Badge = Color.FromArgb(0x1E, 0x21, 0x2D);
    private static readonly Color CloudColor = Color.FromArgb(0xE7, 0xEC, 0xF5);
    private static readonly Color StarColor = Color.FromArgb(0xFF, 0xD5, 0x4F);

    private static readonly int[] Sizes = { 16, 20, 24, 32, 48, 64, 128, 256 };

    public static Icon CreateIcon()
    {
        using var stream = new MemoryStream();
        WriteIco(stream, Sizes.Select(RenderFrame).ToArray());
        stream.Position = 0;
        return new Icon(stream);
    }

    private static Bitmap RenderFrame(int size)
    {
        var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        var badgeRect = new RectangleF(size * 0.03f, size * 0.03f, size * 0.94f, size * 0.94f);
        using var badgeBrush = new SolidBrush(Badge);
        g.FillEllipse(badgeBrush, badgeRect);

        var cloudRect = new RectangleF(size * 0.16f, size * 0.30f, size * 0.68f, size * 0.44f);
        WeatherIcons.Cloud(g, cloudRect, CloudColor);

        var starRect = new RectangleF(size * 0.60f, size * 0.12f, size * 0.26f, size * 0.26f);
        WeatherIcons.Star(g, starRect, StarColor);

        return bitmap;
    }

    /// <summary>Writes a standard ICO container (ICONDIR + ICONDIRENTRY[] + PNG-encoded frames) — the
    /// format Windows has supported for any icon size since Vista.</summary>
    private static void WriteIco(Stream stream, Bitmap[] frames)
    {
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);

        writer.Write((short)0);   // reserved
        writer.Write((short)1);   // type = icon
        writer.Write((short)frames.Length);

        var pngBlobs = frames.Select(f =>
        {
            using var ms = new MemoryStream();
            f.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }).ToArray();

        int offset = 6 + 16 * frames.Length;
        for (int i = 0; i < frames.Length; i++)
        {
            var frame = frames[i];
            var png = pngBlobs[i];
            byte dim = (byte)(frame.Width >= 256 ? 0 : frame.Width); // 0 means 256 per the ICO spec
            writer.Write(dim);              // width
            writer.Write(dim);              // height
            writer.Write((byte)0);          // color count (0 = no palette)
            writer.Write((byte)0);          // reserved
            writer.Write((short)1);         // color planes
            writer.Write((short)32);        // bits per pixel
            writer.Write(png.Length);       // size of image data
            writer.Write(offset);           // offset of image data
            offset += png.Length;
        }

        foreach (var png in pngBlobs)
        {
            writer.Write(png);
        }

        foreach (var frame in frames)
        {
            frame.Dispose();
        }
    }
}
