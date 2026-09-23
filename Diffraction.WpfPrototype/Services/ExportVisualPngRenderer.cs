using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Diffraction.WpfPrototype.Controls;
using Diffraction.WpfPrototype.Models;

namespace Diffraction.WpfPrototype.Services;

/// <summary>
/// Рендерит графики и карты поля в PNG через off-screen отрисовку WPF-контролов.
/// Должен вызываться в UI-потоке приложения.
/// </summary>
public static class ExportVisualPngRenderer
{
    private const double DefaultWidth = 960;
    private const double DefaultHeight = 540;

    public static byte[] RenderPlotPng(PlotData plot, double width = DefaultWidth, double height = DefaultHeight)
    {
        ArgumentNullException.ThrowIfNull(plot);
        var element = new ScientificPlot { Data = plot };
        return RenderToPng(element, width, height);
    }

    public static byte[] RenderFieldMapPng(FieldMapData map, double width = DefaultWidth, double height = DefaultHeight)
    {
        ArgumentNullException.ThrowIfNull(map);
        var element = new FieldMapView { Data = map };
        return RenderToPng(element, width, height);
    }

    private static byte[] RenderToPng(UIElement element, double width, double height)
    {
        var size = new Size(width, height);
        element.Measure(size);
        element.Arrange(new Rect(size));
        element.UpdateLayout();

        var bitmap = new RenderTargetBitmap(
            (int)Math.Ceiling(width),
            (int)Math.Ceiling(height),
            96,
            96,
            PixelFormats.Pbgra32);
        bitmap.Render(element);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }
}
