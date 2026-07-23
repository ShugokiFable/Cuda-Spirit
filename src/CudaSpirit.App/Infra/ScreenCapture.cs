using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DrawingPixelFormat = System.Drawing.Imaging.PixelFormat;

namespace CudaSpirit.App.Infra;

/// <summary>
/// Captures the user's own screen for the F11 "analyze screen" vision button and manual vision.
/// This grabs the desktop the user is already looking at - it is not reading the game process or
/// its memory. GDI BitBlt captures the visible desktop/window surface without process-memory access. The result is either a base64-PNG data URL
/// (for the AI vision model) or a raw BGRA pixel buffer (for OCR).
/// </summary>
public static class ScreenCapture
{
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int nIndex);
    private const int SM_XVIRTUALSCREEN = 76;
    private const int SM_YVIRTUALSCREEN = 77;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;

    /// <summary>Capture the whole virtual desktop as a base64 data URL (PNG).</summary>
    public static string CaptureVirtualScreenDataUrl(long maxDimension = 1600)
    {
        int x = GetSystemMetrics(SM_XVIRTUALSCREEN);
        int y = GetSystemMetrics(SM_YVIRTUALSCREEN);
        int w = Math.Max(1, GetSystemMetrics(SM_CXVIRTUALSCREEN));
        int h = Math.Max(1, GetSystemMetrics(SM_CYVIRTUALSCREEN));

        using var full = new Bitmap(w, h, DrawingPixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(full))
            g.CopyFromScreen(x, y, 0, 0, new Size(w, h), CopyPixelOperation.SourceCopy);

        using var scaled = Downscale(full, maxDimension);
        return ToPngDataUrl(scaled);
    }

    /// <summary>Load an image file and return it as a base64 PNG data URL.</summary>
    public static string FileToDataUrl(string path, long maxDimension = 1600)
    {
        using var img = Image.FromFile(path);
        using var bmp = new Bitmap(img);
        using var scaled = Downscale(bmp, maxDimension);
        return ToPngDataUrl(scaled);
    }

    // ── Region capture (for F11 screen analysis) ────────────────────────────

    /// <summary>Tightly-packed BGRA pixel buffer (stride = width * 4).</summary>
    public sealed record CapturedFrame(byte[] Pixels, int Width, int Height)
    {
        public int Stride => Width * 4;
    }

    /// <summary>
    /// Capture a screen-space region via GDI BitBlt. Returns null on failure. Never throws.
    /// Used by the overlay F11 analysis to capture just the game window area.
    /// </summary>
    public static CapturedFrame? CaptureRegion(Rectangle region)
    {
        if (region.Width <= 0 || region.Height <= 0) return null;
        try
        {
            using var bmp = new Bitmap(region.Width, region.Height, DrawingPixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
                g.CopyFromScreen(region.Location, Point.Empty, region.Size);

            var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height),
                ImageLockMode.ReadOnly, DrawingPixelFormat.Format32bppArgb);
            try
            {
                var pixels = new byte[bmp.Width * bmp.Height * 4];
                for (int y = 0; y < bmp.Height; y++)
                    Marshal.Copy(data.Scan0 + y * data.Stride, pixels, y * bmp.Width * 4, bmp.Width * 4);
                return new CapturedFrame(pixels, bmp.Width, bmp.Height);
            }
            finally
            {
                bmp.UnlockBits(data);
            }
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Capture a screen-space region and return a base64 PNG data URL suitable for a vision model.
    /// Optionally downscales to <paramref name="maxWidth"/> to keep the API payload small.
    /// </summary>
    public static string? CaptureRegionAsDataUrl(Rectangle region, int maxWidth = 1600)
    {
        var frame = CaptureRegion(region);
        if (frame is null) return null;
        return ToBase64PngDataUrl(frame, maxWidth);
    }

    // ── Encoding helpers ────────────────────────────────────────────────────

    /// <summary>PNG-encode a captured frame (optionally downscaled) as a base64 data URL for the vision model.</summary>
    public static string ToBase64PngDataUrl(CapturedFrame frame, int maxWidth = 1600)
    {
        BitmapSource source = BitmapSource.Create(frame.Width, frame.Height, 96, 96,
            PixelFormats.Bgra32, null, frame.Pixels, frame.Stride);
        if (frame.Width > maxWidth)
        {
            double scale = (double)maxWidth / frame.Width;
            source = new TransformedBitmap(source, new ScaleTransform(scale, scale));
        }
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return "data:image/png;base64," + Convert.ToBase64String(ms.ToArray());
    }

    /// <summary>PNG-encode a captured frame as raw base64 (no data URL prefix). Used for API calls that need pure base64.</summary>
    public static string ToBase64Png(CapturedFrame frame, int maxWidth = 1600)
    {
        BitmapSource source = BitmapSource.Create(frame.Width, frame.Height, 96, 96,
            PixelFormats.Bgra32, null, frame.Pixels, frame.Stride);
        if (frame.Width > maxWidth)
        {
            double scale = (double)maxWidth / frame.Width;
            source = new TransformedBitmap(source, new ScaleTransform(scale, scale));
        }
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return Convert.ToBase64String(ms.ToArray());
    }

    private static Bitmap Downscale(Bitmap src, long maxDimension)
    {
        long maxSide = Math.Max(src.Width, src.Height);
        if (maxSide <= maxDimension) return (Bitmap)src.Clone();

        double scale = (double)maxDimension / maxSide;
        int nw = Math.Max(1, (int)(src.Width * scale));
        int nh = Math.Max(1, (int)(src.Height * scale));
        var dst = new Bitmap(nw, nh, DrawingPixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(dst);
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        g.DrawImage(src, 0, 0, nw, nh);
        return dst;
    }

    private static string ToPngDataUrl(Bitmap bmp)
    {
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return "data:image/png;base64," + Convert.ToBase64String(ms.ToArray());
    }
}