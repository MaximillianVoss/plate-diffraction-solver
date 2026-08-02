using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Diffraction.WpfPrototype.Controls;

public sealed class ScientificPlot : FrameworkElement
{
    public static readonly DependencyProperty PlotKindProperty = DependencyProperty.Register(
        nameof(PlotKind),
        typeof(string),
        typeof(ScientificPlot),
        new FrameworkPropertyMetadata("Energy", FrameworkPropertyMetadataOptions.AffectsRender));

    private Point? _cursor;

    public ScientificPlot()
    {
        SnapsToDevicePixels = true;
        MouseMove += OnMouseMove;
        MouseLeave += (_, _) =>
        {
            _cursor = null;
            InvalidateVisual();
        };
    }

    public string PlotKind
    {
        get => (string)GetValue(PlotKindProperty);
        set => SetValue(PlotKindProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        double width = Math.Max(1, ActualWidth);
        double height = Math.Max(1, ActualHeight);
        drawingContext.DrawRectangle(Brushes.White, new Pen(ToBrush("#D7DEE7"), 1), new Rect(0.5, 0.5, width - 1, height - 1));

        const double left = 54;
        const double right = 18;
        double top = PlotKind == "Energy" && width < 390 ? 54 : 36;
        const double bottom = 44;
        Rect plot = new(left, top, Math.Max(20, width - left - right), Math.Max(20, height - top - bottom));

        (double xMin, double xMax, double yMin, double yMax, string xLabel, string yLabel, PlotSeries[] series) = GetDefinition();
        DrawGrid(drawingContext, plot, xMin, xMax, yMin, yMax);

        foreach (PlotSeries item in series)
            DrawSeries(drawingContext, plot, xMin, xMax, yMin, yMax, item);

        DrawLegend(drawingContext, plot, series);
        DrawText(drawingContext, xLabel, new Point(plot.Left + plot.Width / 2, height - 20), 11, ToBrush("#364152"), centered: true);
        DrawRotatedText(drawingContext, yLabel, new Point(15, plot.Top + plot.Height / 2), 11, ToBrush("#364152"));

        if (_cursor is Point cursor && plot.Contains(cursor))
            DrawCursor(drawingContext, plot, cursor, xMin, xMax, yMin, yMax);
    }

    private void DrawGrid(DrawingContext dc, Rect plot, double xMin, double xMax, double yMin, double yMax)
    {
        Pen gridPen = new(ToBrush("#E7EBF0"), 1);
        Pen axisPen = new(ToBrush("#667085"), 1);
        const int divisions = 5;

        for (int i = 0; i <= divisions; i++)
        {
            double x = plot.Left + plot.Width * i / divisions;
            double y = plot.Top + plot.Height * i / divisions;
            dc.DrawLine(gridPen, new Point(x, plot.Top), new Point(x, plot.Bottom));
            dc.DrawLine(gridPen, new Point(plot.Left, y), new Point(plot.Right, y));

            double xValue = xMin + (xMax - xMin) * i / divisions;
            double yValue = yMax - (yMax - yMin) * i / divisions;
            DrawText(dc, FormatTick(xValue), new Point(x, plot.Bottom + 14), 9.5, ToBrush("#667085"), centered: true);
            DrawText(dc, FormatTick(yValue), new Point(plot.Left - 8, y - 6), 9.5, ToBrush("#667085"), rightAligned: true);
        }

        dc.DrawLine(axisPen, new Point(plot.Left, plot.Bottom), new Point(plot.Right, plot.Bottom));
        dc.DrawLine(axisPen, new Point(plot.Left, plot.Top), new Point(plot.Left, plot.Bottom));
    }

    private static void DrawSeries(
        DrawingContext dc,
        Rect plot,
        double xMin,
        double xMax,
        double yMin,
        double yMax,
        PlotSeries series)
    {
        var geometry = new StreamGeometry();
        using (StreamGeometryContext context = geometry.Open())
        {
            const int samples = 220;
            for (int i = 0; i < samples; i++)
            {
                double xValue = xMin + (xMax - xMin) * i / (samples - 1);
                double yValue = series.Evaluate(xValue);
                double x = plot.Left + (xValue - xMin) / (xMax - xMin) * plot.Width;
                double y = plot.Bottom - (yValue - yMin) / (yMax - yMin) * plot.Height;
                Point point = new(x, y);
                if (i == 0)
                    context.BeginFigure(point, false, false);
                else
                    context.LineTo(point, true, false);
            }
        }

        geometry.Freeze();
        Pen pen = new(new SolidColorBrush(series.Color), series.Thickness)
        {
            LineJoin = PenLineJoin.Round,
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            DashStyle = series.IsDashed ? DashStyles.Dash : DashStyles.Solid
        };
        pen.Freeze();
        dc.DrawGeometry(null, pen, geometry);
    }

    private void DrawLegend(DrawingContext dc, Rect plot, IReadOnlyList<PlotSeries> series)
    {
        double x = plot.Left + 6;
        double y = 13;
        foreach (PlotSeries item in series)
        {
            double itemWidth = Math.Max(112, 35 + MeasureText(item.Name, 10));
            if (x > plot.Left + 6 && x + itemWidth > plot.Right)
            {
                x = plot.Left + 6;
                y += 18;
            }

            Pen pen = new(new SolidColorBrush(item.Color), 2) { DashStyle = item.IsDashed ? DashStyles.Dash : DashStyles.Solid };
            dc.DrawLine(pen, new Point(x, y + 5), new Point(x + 22, y + 5));
            DrawText(dc, item.Name, new Point(x + 28, y), 10, ToBrush("#475467"));
            x += itemWidth;
        }
    }

    private void DrawCursor(DrawingContext dc, Rect plot, Point cursor, double xMin, double xMax, double yMin, double yMax)
    {
        Pen cursorPen = new(ToBrush("#344054"), 1) { DashStyle = DashStyles.Dash };
        dc.DrawLine(cursorPen, new Point(cursor.X, plot.Top), new Point(cursor.X, plot.Bottom));
        dc.DrawLine(cursorPen, new Point(plot.Left, cursor.Y), new Point(plot.Right, cursor.Y));

        double xValue = xMin + (cursor.X - plot.Left) / plot.Width * (xMax - xMin);
        double yValue = yMax - (cursor.Y - plot.Top) / plot.Height * (yMax - yMin);
        string value = $"x={xValue:0.###}; y={yValue:0.###}";
        double boxWidth = MeasureText(value, 10) + 16;
        double boxX = Math.Min(cursor.X + 10, plot.Right - boxWidth);
        double boxY = Math.Max(plot.Top + 4, cursor.Y - 29);
        dc.DrawRoundedRectangle(ToBrush("#F9FAFB"), new Pen(ToBrush("#98A2B3"), 1), new Rect(boxX, boxY, boxWidth, 24), 3, 3);
        DrawText(dc, value, new Point(boxX + 8, boxY + 5), 10, ToBrush("#344054"));
    }

    private (double, double, double, double, string, string, PlotSeries[]) GetDefinition()
    {
        return PlotKind switch
        {
            "Slice" => (-1.5, -0.5, -1.05, 0.65, "x", "Re u(x, λ/10)", new[]
            {
                new PlotSeries("Без скин-слоя", Color.FromRgb(37, 99, 235), x => 0.18 * Math.Cos(9 * (x + 1.5)) - 0.78 + 0.55 * Math.Pow(x + 1, 2), 2.2),
                new PlotSeries("Со скин-слоем", Color.FromRgb(220, 38, 38), x => 0.16 * Math.Cos(9 * (x + 1.5) + 0.08) - 0.80 + 0.51 * Math.Pow(x + 1, 2), 2.2)
            }),
            "Method" => (-1.5, -0.5, -1.0, 0.65, "x", "Re u(x, λ/10)", new[]
            {
                new PlotSeries("Коллокация", Color.FromRgb(37, 99, 235), x => -0.82 + 2.7 * Math.Pow(x + 1, 2) + 0.05 * Math.Sin(12 * x), 2.2),
                new PlotSeries("Галеркин", Color.FromRgb(219, 39, 119), x => -0.81 + 2.62 * Math.Pow(x + 1, 2), 2.2, true)
            }),
            "Difference" => (-1.5, -0.5, 0, 0.09, "x", "|Δu|", new[]
            {
                new PlotSeries("|u_col − u_gal|", Color.FromRgb(234, 88, 12), x => 0.047 + 0.012 * Math.Pow(Math.Sin(7 * x), 2) + 0.010 * (x + 1.5), 2.2)
            }),
            _ => (0, 0.1, 0, 1.05, "Толщина δ", "Доля падающей энергии", new[]
            {
                new PlotSeries("R_scat обратно", Color.FromRgb(37, 99, 235), x => 0.63 - 0.15 * (1 - Math.Exp(-28 * x)), 2.3),
                new PlotSeries("T_scat вперёд", Color.FromRgb(22, 163, 74), x => 0.25 - 0.015 * (1 - Math.Exp(-22 * x)), 2.3),
                new PlotSeries("A_J пластина", Color.FromRgb(234, 88, 12), x => 0.12 + 0.16 * (1 - Math.Exp(-20 * x)), 2.3)
            })
        };
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        _cursor = e.GetPosition(this);
        InvalidateVisual();
    }

    private static string FormatTick(double value)
    {
        double abs = Math.Abs(value);
        return abs > 0 && abs < 0.001 ? value.ToString("0.0E+0", CultureInfo.InvariantCulture) : value.ToString("0.###", CultureInfo.CurrentCulture);
    }

    private static Brush ToBrush(string color) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));

    private void DrawText(DrawingContext dc, string text, Point point, double size, Brush brush, bool centered = false, bool rightAligned = false)
    {
        FormattedText formatted = CreateText(text, size, brush);
        double x = centered ? point.X - formatted.Width / 2 : rightAligned ? point.X - formatted.Width : point.X;
        dc.DrawText(formatted, new Point(x, point.Y));
    }

    private void DrawRotatedText(DrawingContext dc, string text, Point point, double size, Brush brush)
    {
        FormattedText formatted = CreateText(text, size, brush);
        dc.PushTransform(new RotateTransform(-90, point.X, point.Y));
        dc.DrawText(formatted, new Point(point.X - formatted.Width / 2, point.Y - formatted.Height / 2));
        dc.Pop();
    }

    private FormattedText CreateText(string text, double size, Brush brush)
        => new(text, CultureInfo.GetCultureInfo("ru-RU"), FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);

    private double MeasureText(string text, double size) => CreateText(text, size, Brushes.Black).Width;

    private sealed record PlotSeries(string Name, Color Color, Func<double, double> Evaluate, double Thickness, bool IsDashed = false);
}

public sealed class FieldMapView : FrameworkElement
{
    public static readonly DependencyProperty SkinEnabledProperty = DependencyProperty.Register(
        nameof(SkinEnabled),
        typeof(bool),
        typeof(FieldMapView),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender, (_, _) => { }));

    private WriteableBitmap? _bitmap;
    private Size _bitmapSize;
    private bool _bitmapSkinMode;
    private Point? _cursor;

    public FieldMapView()
    {
        MouseMove += (_, e) =>
        {
            Point position = e.GetPosition(this);
            _cursor = position;
            Rect plot = GetPlotRect();
            if (plot.Contains(position))
            {
                CursorChanged?.Invoke(this, new FieldCursorChangedEventArgs(new Point(
                    (position.X - plot.Left) / plot.Width,
                    (position.Y - plot.Top) / plot.Height)));
            }
            InvalidateVisual();
        };
        MouseLeave += (_, _) =>
        {
            _cursor = null;
            CursorChanged?.Invoke(this, new FieldCursorChangedEventArgs(null));
            InvalidateVisual();
        };
    }

    public event EventHandler<FieldCursorChangedEventArgs>? CursorChanged;

    public bool SkinEnabled
    {
        get => (bool)GetValue(SkinEnabledProperty);
        set => SetValue(SkinEnabledProperty, value);
    }

    public void SetLinkedCursor(Point? normalizedPosition)
    {
        Rect plot = GetPlotRect();
        _cursor = normalizedPosition is Point normalized
            ? new Point(plot.Left + normalized.X * plot.Width, plot.Top + normalized.Y * plot.Height)
            : null;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double width = Math.Max(1, ActualWidth);
        double height = Math.Max(1, ActualHeight);
        dc.DrawRectangle(Brushes.White, new Pen(ToBrush("#D7DEE7"), 1), new Rect(0.5, 0.5, width - 1, height - 1));

        Rect plot = GetPlotRect();
        int bitmapWidth = Math.Max(80, Math.Min(620, (int)Math.Round(plot.Width)));
        int bitmapHeight = Math.Max(80, Math.Min(420, (int)Math.Round(plot.Height)));

        if (_bitmap == null || _bitmapSize.Width != bitmapWidth || _bitmapSize.Height != bitmapHeight || _bitmapSkinMode != SkinEnabled)
        {
            _bitmap = BuildBitmap(bitmapWidth, bitmapHeight, SkinEnabled);
            _bitmapSize = new Size(bitmapWidth, bitmapHeight);
            _bitmapSkinMode = SkinEnabled;
        }

        dc.DrawImage(_bitmap, plot);
        dc.DrawRectangle(null, new Pen(ToBrush("#667085"), 1), plot);

        double plateY = plot.Top + plot.Height / 2;
        double plateLeft = plot.Left + plot.Width * 0.125;
        double plateRight = plot.Left + plot.Width * 0.375;
        dc.DrawLine(new Pen(ToBrush("#111827"), 4), new Point(plateLeft, plateY), new Point(plateRight, plateY));
        dc.DrawLine(new Pen(Brushes.White, 1), new Point(plateLeft, plateY), new Point(plateRight, plateY));

        DrawAxisLabels(dc, plot);
        DrawColorScale(dc, new Rect(plot.Right + 10, plot.Top, 11, plot.Height));

        if (_cursor is Point cursor && plot.Contains(cursor))
            DrawCursor(dc, plot, cursor);
    }

    private Rect GetPlotRect()
    {
        double width = Math.Max(1, ActualWidth);
        double height = Math.Max(1, ActualHeight);
        return new Rect(38, 18, Math.Max(40, width - 76), Math.Max(40, height - 52));
    }

    private WriteableBitmap BuildBitmap(int width, int height, bool skin)
    {
        int stride = width * 4;
        byte[] pixels = new byte[stride * height];
        for (int py = 0; py < height; py++)
        {
            double y = 3.0 - 6.0 * py / Math.Max(1, height - 1);
            for (int px = 0; px < width; px++)
            {
                double x = -2.0 + 4.0 * px / Math.Max(1, width - 1);
                double sourceX = -1.5;
                double radius = Math.Sqrt(Math.Pow(x - sourceX, 2) + y * y);
                double incident = 0.52 + 0.24 * Math.Sin(7.2 * (0.68 * x + 0.73 * y));
                double edgeWave = 0.31 * Math.Sin(12.5 * radius + (skin ? 0.16 : 0.0)) * Math.Exp(-0.11 * radius);
                double shadow = y < 0 && x > -1.5 && x < -0.5 ? -0.22 * Math.Exp(-2.4 * Math.Abs(y)) : 0;
                double attenuation = skin ? 0.91 : 1.0;
                double value = Math.Clamp(Math.Abs(incident + attenuation * edgeWave + shadow), 0, 1);
                Color color = ScientificColorMap(value);
                int offset = py * stride + px * 4;
                pixels[offset] = color.B;
                pixels[offset + 1] = color.G;
                pixels[offset + 2] = color.R;
                pixels[offset + 3] = 255;
            }
        }

        var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
        bitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, stride, 0);
        bitmap.Freeze();
        return bitmap;
    }

    private void DrawAxisLabels(DrawingContext dc, Rect plot)
    {
        DrawText(dc, "−2", new Point(plot.Left, plot.Bottom + 5), 9, ToBrush("#667085"));
        DrawText(dc, "0", new Point(plot.Left + plot.Width / 2 - 3, plot.Bottom + 5), 9, ToBrush("#667085"));
        DrawText(dc, "2", new Point(plot.Right - 6, plot.Bottom + 5), 9, ToBrush("#667085"));
        DrawText(dc, "x", new Point(plot.Left + plot.Width / 2, plot.Bottom + 18), 10, ToBrush("#344054"));
        DrawText(dc, "y", new Point(12, plot.Top + plot.Height / 2), 10, ToBrush("#344054"));
    }

    private void DrawColorScale(DrawingContext dc, Rect scale)
    {
        const int segments = 80;
        for (int i = 0; i < segments; i++)
        {
            double t = 1.0 - i / (double)(segments - 1);
            Rect segment = new(scale.Left, scale.Top + scale.Height * i / segments, scale.Width, scale.Height / segments + 1);
            dc.DrawRectangle(new SolidColorBrush(ScientificColorMap(t)), null, segment);
        }
        dc.DrawRectangle(null, new Pen(ToBrush("#667085"), 1), scale);
        DrawText(dc, "1,0", new Point(scale.Right + 3, scale.Top - 4), 8.5, ToBrush("#667085"));
        DrawText(dc, "0", new Point(scale.Right + 3, scale.Bottom - 10), 8.5, ToBrush("#667085"));
    }

    private void DrawCursor(DrawingContext dc, Rect plot, Point cursor)
    {
        Pen pen = new(Brushes.White, 1);
        dc.DrawLine(pen, new Point(cursor.X - 8, cursor.Y), new Point(cursor.X + 8, cursor.Y));
        dc.DrawLine(pen, new Point(cursor.X, cursor.Y - 8), new Point(cursor.X, cursor.Y + 8));
        double x = -2 + (cursor.X - plot.Left) / plot.Width * 4;
        double y = 3 - (cursor.Y - plot.Top) / plot.Height * 6;
        string label = $"x={x:0.00}; y={y:0.00}";
        dc.DrawRoundedRectangle(ToBrush("#111827"), null, new Rect(plot.Left + 5, plot.Top + 5, 98, 22), 3, 3);
        DrawText(dc, label, new Point(plot.Left + 11, plot.Top + 9), 9, Brushes.White);
    }

    private static Color ScientificColorMap(double value)
    {
        value = Math.Clamp(value, 0, 1);
        (Color a, Color b, double local) = value switch
        {
            < 0.35 => (Color.FromRgb(31, 64, 122), Color.FromRgb(20, 158, 192), value / 0.35),
            < 0.7 => (Color.FromRgb(20, 158, 192), Color.FromRgb(65, 184, 126), (value - 0.35) / 0.35),
            _ => (Color.FromRgb(65, 184, 126), Color.FromRgb(249, 214, 78), (value - 0.7) / 0.3)
        };
        return Color.FromRgb(
            (byte)(a.R + (b.R - a.R) * local),
            (byte)(a.G + (b.G - a.G) * local),
            (byte)(a.B + (b.B - a.B) * local));
    }

    private static Brush ToBrush(string color) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));

    private void DrawText(DrawingContext dc, string text, Point point, double size, Brush brush)
    {
        var formatted = new FormattedText(text, CultureInfo.GetCultureInfo("ru-RU"), FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(formatted, point);
    }
}

public sealed class FieldCursorChangedEventArgs : EventArgs
{
    public FieldCursorChangedEventArgs(Point? normalizedPosition)
    {
        NormalizedPosition = normalizedPosition;
    }

    public Point? NormalizedPosition { get; }
}

public sealed class PlateGeometryView : FrameworkElement
{
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double width = Math.Max(1, ActualWidth);
        double height = Math.Max(1, ActualHeight);
        dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));

        double axisY = height * 0.64;
        double originX = width * 0.44;
        Pen axis = new(ToBrush("#344054"), 1.2);
        dc.DrawLine(axis, new Point(20, axisY), new Point(width - 22, axisY));
        dc.DrawLine(axis, new Point(originX, height - 17), new Point(originX, 20));
        DrawArrow(dc, new Point(width - 22, axisY), new Vector(1, 0));
        DrawArrow(dc, new Point(originX, 20), new Vector(0, -1));

        double plateLeft = width * 0.19;
        double plateRight = width * 0.72;
        const double visibleThickness = 13;
        Rect plate = new(plateLeft, axisY - visibleThickness / 2, plateRight - plateLeft, visibleThickness);
        dc.DrawRectangle(ToBrush("#B8C0CC"), new Pen(ToBrush("#475467"), 1.2), plate);

        Point rayStart = new(width * 0.18, height * 0.16);
        Point rayEnd = new(originX, axisY - visibleThickness / 2);
        Pen ray = new(ToBrush("#2563EB"), 2.1);
        dc.DrawLine(ray, rayStart, rayEnd);
        Vector direction = rayEnd - rayStart;
        direction.Normalize();
        DrawArrow(dc, rayEnd, direction);

        Pen normal = new(ToBrush("#667085"), 1) { DashStyle = DashStyles.Dash };
        dc.DrawLine(normal, new Point(originX, axisY - 2), new Point(originX, height * 0.22));

        double deltaX = plateRight + 13;
        dc.DrawLine(axis, new Point(deltaX, plate.Top), new Point(deltaX, plate.Bottom));
        dc.DrawLine(axis, new Point(deltaX - 4, plate.Top), new Point(deltaX + 4, plate.Top));
        dc.DrawLine(axis, new Point(deltaX - 4, plate.Bottom), new Point(deltaX + 4, plate.Bottom));

        DrawText(dc, "θ", new Point(originX - 21, height * 0.32), 12, ToBrush("#344054"));
        DrawText(dc, "α₁", new Point(plateLeft - 6, axisY + 15), 10, ToBrush("#344054"));
        DrawText(dc, "β₁", new Point(plateRight - 6, axisY + 15), 10, ToBrush("#344054"));
        DrawText(dc, "δ", new Point(deltaX + 6, axisY - 8), 11, ToBrush("#344054"));
        DrawText(dc, "x", new Point(width - 18, axisY + 4), 10, ToBrush("#344054"));
        DrawText(dc, "y", new Point(originX + 5, 15), 10, ToBrush("#344054"));
    }

    private void DrawArrow(DrawingContext dc, Point tip, Vector direction)
    {
        direction.Normalize();
        Vector perpendicular = new(-direction.Y, direction.X);
        Point a = tip - direction * 9 + perpendicular * 4;
        Point b = tip - direction * 9 - perpendicular * 4;
        var geometry = new StreamGeometry();
        using (StreamGeometryContext context = geometry.Open())
        {
            context.BeginFigure(tip, true, true);
            context.LineTo(a, true, false);
            context.LineTo(b, true, false);
        }
        geometry.Freeze();
        dc.DrawGeometry(ToBrush("#344054"), null, geometry);
    }

    private static Brush ToBrush(string color) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));

    private void DrawText(DrawingContext dc, string text, Point point, double size, Brush brush)
    {
        var formatted = new FormattedText(text, CultureInfo.GetCultureInfo("ru-RU"), FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(formatted, point);
    }
}
