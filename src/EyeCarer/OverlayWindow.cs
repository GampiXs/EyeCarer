using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Drawing = System.Drawing;

namespace EyeCarer;

internal sealed class OverlayWindow : Window
{
    private readonly Drawing.Rectangle bounds;
    private readonly Settings settings;
    private readonly Grid layers;
    private static ImageBrush? grain;
    public event Action? Dismiss;
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr handle);

    public static BitmapSource? Capture(Drawing.Rectangle bounds)
    {
        try
        {
            using var bitmap = new Drawing.Bitmap(bounds.Width, bounds.Height, Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var graphics = Drawing.Graphics.FromImage(bitmap))
                graphics.CopyFromScreen(bounds.Location, Drawing.Point.Empty, bounds.Size, Drawing.CopyPixelOperation.SourceCopy);
            var handle = bitmap.GetHbitmap();
            try
            {
                var source = Imaging.CreateBitmapSourceFromHBitmap(handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                return source;
            }
            finally { DeleteObject(handle); }
        }
        catch (Exception e) { Log.Error("capture", e); return null; }
    }

    public OverlayWindow(Drawing.Rectangle bounds, BitmapSource? screenshot, Settings settings, bool preview = false)
    {
        this.bounds = bounds;
        this.settings = settings;
        Title = "EyeCarer — Take rest";
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        // The screenshot fills the window, so per-pixel transparency is unnecessary.
        // A normal compositor surface uses much less memory than a layered WPF window.
        AllowsTransparency = false;
        Background = new SolidColorBrush(Color.FromRgb(17, 15, 20));
        Opacity = settings.EnableAnimations ? 0 : 1;
        // Optional QA switch makes tool windows discoverable by desktop automation.
        ShowInTaskbar = preview;
        Topmost = true;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Width = bounds.Width; Height = bounds.Height;
        Left = bounds.Left; Top = bounds.Top;
        layers = new Grid { ClipToBounds = true, Background = new SolidColorBrush(Color.FromRgb(17, 15, 20)) };
        Content = layers;

        // Render order is deliberate: captured desktop -> grain -> red haze -> sharp text.
        if (screenshot != null)
        {
            var image = new Image { Source = PrepareBackground(screenshot, settings.BlurRadius), Stretch = Stretch.Fill };
            layers.Children.Add(image);
        }
        layers.Children.Add(new System.Windows.Shapes.Rectangle { Fill = Grain(), Opacity = settings.GrainStrength * .30, IsHitTestVisible = false });
        var haze = new RadialGradientBrush { Center = new Point(.5, .5), GradientOrigin = new Point(.5, .5), RadiusX = .5, RadiusY = .5 };
        haze.GradientStops.Add(new GradientStop(Color.FromArgb(0, 80, 0, 19), 0));
        haze.GradientStops.Add(new GradientStop(Color.FromArgb(0, 96, 2, 24), 1 - settings.HazeCoverage));
        haze.GradientStops.Add(new GradientStop(Color.FromArgb(90, 111, 7, 32), 1 - settings.HazeCoverage * .44));
        haze.GradientStops.Add(new GradientStop(Color.FromArgb(255, 93, 3, 26), 1));
        haze.Freeze();
        layers.Children.Add(new System.Windows.Shapes.Rectangle { Fill = haze, Opacity = settings.HazeCoverage == 0 ? 0 : settings.HazeOpacity, IsHitTestVisible = false });
        AddCloud(new Point(.04, .2), Color.FromArgb(65, 153, 17, 49), .6, .8);
        AddCloud(new Point(.95, .87), Color.FromArgb(55, 122, 4, 36), .7, .65);
        if (settings.ShowCenterText)
        {
            layers.Children.Add(new TextBlock
            {
                Text = settings.CenterText, FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
                FontSize = 52, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(249, 246, 246)),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(40),
                Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 18, ShadowDepth = 2, Opacity = .45 }
            });
        }
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; Dismiss?.Invoke(); } };
        SourceInitialized += (_, _) => Position();
        Loaded += (_, _) =>
        {
            Position();
            if (settings.EnableAnimations) BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300)));
            Activate(); Focus();
        };
        Closed += (_, _) =>
        {
            BeginAnimation(OpacityProperty, null);
            foreach (var child in layers.Children)
            {
                if (child is Image image) { image.Source = null; image.Effect = null; image.CacheMode = null; }
            }
            layers.Children.Clear(); Content = null;
        };
    }

    private void Position() => SetWindowPos(new WindowInteropHelper(this).Handle, new IntPtr(-1), bounds.X, bounds.Y, bounds.Width, bounds.Height, 0x0040);
    public void FadeOut() { if (settings.EnableAnimations) BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300))); }
    private void AddCloud(Point center, Color color, double rx, double ry)
    {
        var brush = new RadialGradientBrush(color, Colors.Transparent) { Center = center, GradientOrigin = center, RadiusX = rx, RadiusY = ry };
        brush.Freeze();
        layers.Children.Add(new System.Windows.Shapes.Rectangle { Fill = brush, Opacity = settings.HazeOpacity * Math.Min(1,settings.HazeCoverage*2), IsHitTestVisible = false });
    }
    private static ImageBrush Grain()
    {
        if (grain != null) return grain;
        const int size = 192;
        var bytes = new byte[size * size];
        new Random(2026).NextBytes(bytes);
        var noise = BitmapSource.Create(size, size, 96, 96, PixelFormats.Gray8, null, bytes, size);
        noise.Freeze();
        grain = new ImageBrush(noise) { TileMode = TileMode.Tile, ViewportUnits = BrushMappingMode.Absolute, Viewport = new Rect(0, 0, size, size), Stretch = Stretch.Fill };
        grain.Freeze();
        return grain;
    }

    private static BitmapSource? PrepareBackground(BitmapSource screenshot, double radius)
    {
        if (radius == 0) return screenshot;
        try
        {
            // Blur a reduced snapshot once. Scaling the result back restores the
            // configured physical-pixel radius without a live full-screen effect.
            double scale = radius >= 8 ? .5 : 1;
            int width = Math.Max(1, (int)(screenshot.PixelWidth * scale));
            int height = Math.Max(1, (int)(screenshot.PixelHeight * scale));
            var image = new Image
            {
                Source = screenshot, Width = width, Height = height, Stretch = Stretch.Fill,
                Effect = new BlurEffect { Radius = radius * scale, RenderingBias = RenderingBias.Quality, KernelType = KernelType.Gaussian }
            };
            image.Measure(new Size(width, height));
            image.Arrange(new Rect(0, 0, width, height));
            var result = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            result.Render(image); result.Freeze();
            image.Source = null; image.Effect = null;
            return result;
        }
        catch (Exception error) { Log.Error("blur", error); return null; }
    }
}
