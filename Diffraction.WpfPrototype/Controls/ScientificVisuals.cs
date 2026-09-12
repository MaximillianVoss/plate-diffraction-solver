using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Diffraction.WpfPrototype.Models;

namespace Diffraction.WpfPrototype.Controls;

public sealed class ScientificPlot : FrameworkElement
{
    public static readonly DependencyProperty DataProperty = DependencyProperty.Register(
        nameof(Data),
        typeof(PlotData),
        typeof(ScientificPlot),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

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

    public PlotData? Data
    {
        get => (PlotData?)GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        double width = Math.Max(1, ActualWidth);
        double height = Math.Max(1, ActualHeight);
        drawingContext.DrawRectangle(Brushes.White, new Pen(ToBrush("#D7DEE7"), 1), new Rect(0.5, 0.5, width - 1, height - 1));

        const double left = 54;
        const double right = 18;
        PlotData data = Data ?? PlotData.Empty("x", "y");
        double top = data.Series.Count > 2 && width < 390 ? 54 : 36;
        const double bottom = 44;
        Rect plot = new(left, top, Math.Max(20, width - left - right), Math.Max(20, height - top - bottom));

        DrawGrid(drawingContext, plot, data.XMinimum, data.XMaximum, data.YMinimum, data.YMaximum);

        if (data.HasData)
        {
            drawingContext.PushClip(new RectangleGeometry(plot));
            foreach (PlotSeriesData item in data.Series)
                DrawSeries(
                    drawingContext,
                    plot,
                    data.XMinimum,
                    data.XMaximum,
                    data.YMinimum,
                    data.YMaximum,
                    item);
            drawingContext.Pop();
            DrawLegend(drawingContext, plot, data.Series);
        }
        else
        {
            DrawText(
                drawingContext,
                data.EmptyMessage,
                new Point(plot.Left + plot.Width / 2, plot.Top + plot.Height / 2 - 8),
                11,
                ToBrush("#667085"),
                centered: true);
        }

        DrawText(drawingContext, data.XAxisTitle, new Point(plot.Left + plot.Width / 2, height - 20), 11, ToBrush("#364152"), centered: true);
        DrawRotatedText(drawingContext, data.YAxisTitle, new Point(15, plot.Top + plot.Height / 2), 11, ToBrush("#364152"));

        if (_cursor is Point cursor && plot.Contains(cursor))
            DrawCursor(
                drawingContext,
                plot,
                cursor,
                data.XMinimum,
                data.XMaximum,
                data.YMinimum,
                data.YMaximum);
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

            double xValue = xMin + (xMax - xMin) * (i / (double)divisions);
            double yValue = yMax - (yMax - yMin) * (i / (double)divisions);
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
        PlotSeriesData series)
    {
        if (series.Points.Count == 0)
            return;

        var geometry = new StreamGeometry();
        using (StreamGeometryContext context = geometry.Open())
        {
            for (int i = 0; i < series.Points.Count; i++)
            {
                PlotPointData source = series.Points[i];
                double xValue = source.X;
                double yValue = source.Y;
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
        Brush seriesBrush = ToBrush(series.Color);
        Pen pen = new(seriesBrush, series.Thickness)
        {
            LineJoin = PenLineJoin.Round,
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            DashStyle = series.IsDashed ? DashStyles.Dash : DashStyles.Solid
        };
        pen.Freeze();
        dc.DrawGeometry(null, pen, geometry);

        if (!series.ShowMarkers)
            return;

        foreach (PlotPointData source in series.Points)
        {
            double x = plot.Left + (source.X - xMin) / (xMax - xMin) * plot.Width;
            double y = plot.Bottom - (source.Y - yMin) / (yMax - yMin) * plot.Height;
            dc.DrawEllipse(seriesBrush, new Pen(Brushes.White, 1), new Point(x, y), 3.5, 3.5);
        }
    }

    private void DrawLegend(DrawingContext dc, Rect plot, IReadOnlyList<PlotSeriesData> series)
    {
        double x = plot.Left + 6;
        double y = 13;
        foreach (PlotSeriesData item in series)
        {
            double itemWidth = Math.Max(112, 35 + MeasureText(item.Name, 10));
            if (x > plot.Left + 6 && x + itemWidth > plot.Right)
            {
                x = plot.Left + 6;
                y += 18;
            }

            Pen pen = new(ToBrush(item.Color), 2) { DashStyle = item.IsDashed ? DashStyles.Dash : DashStyles.Solid };
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

}

public sealed class FieldMapView : FrameworkElement
{
    public static readonly DependencyProperty DataProperty = DependencyProperty.Register(
        nameof(Data),
        typeof(FieldMapData),
        typeof(FieldMapView),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.AffectsRender,
            (dependencyObject, _) =>
            {
                FieldMapView view = (FieldMapView)dependencyObject;
                view._bitmap = null;
                view._bitmapData = null;
            }));

    private WriteableBitmap? _bitmap;
    private FieldMapData? _bitmapData;
    private Point? _cursor;

    public FieldMapView()
    {
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
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

    public FieldMapData? Data
    {
        get => (FieldMapData?)GetValue(DataProperty);
        set => SetValue(DataProperty, value);
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
        FieldMapData? data = Data;
        if (data is null)
        {
            dc.DrawRectangle(ToBrush("#F9FAFB"), new Pen(ToBrush("#D7DEE7"), 1), plot);
            DrawText(dc, "Выполните расчёт", new Point(plot.Left + 12, plot.Top + 12), 10.5, ToBrush("#667085"));
            return;
        }

        if (_bitmap == null || !ReferenceEquals(_bitmapData, data))
        {
            _bitmap = BuildBitmap(data);
            _bitmapData = data;
        }

        dc.DrawImage(_bitmap, plot);
        dc.DrawRectangle(null, new Pen(ToBrush("#667085"), 1), plot);

        if (data.YMinimum <= 0 && data.YMaximum >= 0)
        {
            double plateY = ToScreenY(0, plot, data);
            double plateLeft = ToScreenX(Math.Max(data.PlateStart, data.XMinimum), plot, data);
            double plateRight = ToScreenX(Math.Min(data.PlateEnd, data.XMaximum), plot, data);
            if (plateRight > plateLeft)
            {
                dc.DrawLine(new Pen(ToBrush("#111827"), 4), new Point(plateLeft, plateY), new Point(plateRight, plateY));
                dc.DrawLine(new Pen(Brushes.White, 1), new Point(plateLeft, plateY), new Point(plateRight, plateY));
            }
        }

        DrawAxisLabels(dc, plot, data);
        DrawColorScale(dc, new Rect(plot.Right + 10, plot.Top, 11, plot.Height), data);

        if (_cursor is Point cursor && plot.Contains(cursor))
            DrawCursor(dc, plot, cursor, data);
    }

    private Rect GetPlotRect()
    {
        double width = Math.Max(1, ActualWidth);
        double height = Math.Max(1, ActualHeight);
        return new Rect(38, 18, Math.Max(40, width - 76), Math.Max(40, height - 52));
    }

    private static WriteableBitmap BuildBitmap(FieldMapData data)
    {
        int width = data.Width;
        int height = data.Height;
        int stride = width * 4;
        byte[] pixels = new byte[stride * height];
        for (int py = 0; py < height; py++)
        {
            for (int px = 0; px < width; px++)
            {
                double value = data.Values[py * width + px];
                double normalized = (value - data.ScaleMinimum) /
                    Math.Max(data.ScaleMaximum - data.ScaleMinimum, 1e-15);
                Color color = ScientificColorMap(normalized);
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

    private void DrawAxisLabels(DrawingContext dc, Rect plot, FieldMapData data)
    {
        DrawText(dc, FormatTick(data.XMinimum), new Point(plot.Left, plot.Bottom + 5), 9, ToBrush("#667085"));
        DrawText(dc, FormatTick((data.XMinimum + data.XMaximum) / 2.0), new Point(plot.Left + plot.Width / 2 - 8, plot.Bottom + 5), 9, ToBrush("#667085"));
        DrawText(dc, FormatTick(data.XMaximum), new Point(plot.Right - 18, plot.Bottom + 5), 9, ToBrush("#667085"));
        DrawText(dc, "x", new Point(plot.Left + plot.Width / 2, plot.Bottom + 18), 10, ToBrush("#344054"));
        DrawText(dc, "y", new Point(12, plot.Top + plot.Height / 2), 10, ToBrush("#344054"));
    }

    private void DrawColorScale(DrawingContext dc, Rect scale, FieldMapData data)
    {
        const int segments = 80;
        for (int i = 0; i < segments; i++)
        {
            double t = 1.0 - i / (double)(segments - 1);
            Rect segment = new(scale.Left, scale.Top + scale.Height * i / segments, scale.Width, scale.Height / segments + 1);
            dc.DrawRectangle(new SolidColorBrush(ScientificColorMap(t)), null, segment);
        }
        dc.DrawRectangle(null, new Pen(ToBrush("#667085"), 1), scale);
        DrawText(dc, FormatTick(data.ScaleMaximum), new Point(scale.Right + 3, scale.Top - 4), 8.5, ToBrush("#667085"));
        DrawText(dc, FormatTick(data.ScaleMinimum), new Point(scale.Right + 3, scale.Bottom - 10), 8.5, ToBrush("#667085"));
    }

    private void DrawCursor(DrawingContext dc, Rect plot, Point cursor, FieldMapData data)
    {
        Pen pen = new(Brushes.White, 1);
        dc.DrawLine(pen, new Point(cursor.X - 8, cursor.Y), new Point(cursor.X + 8, cursor.Y));
        dc.DrawLine(pen, new Point(cursor.X, cursor.Y - 8), new Point(cursor.X, cursor.Y + 8));
        double x = data.XMinimum + (cursor.X - plot.Left) / plot.Width * (data.XMaximum - data.XMinimum);
        double y = data.YMaximum - (cursor.Y - plot.Top) / plot.Height * (data.YMaximum - data.YMinimum);
        string label = $"x={x:0.00}; y={y:0.00}";
        dc.DrawRoundedRectangle(ToBrush("#111827"), null, new Rect(plot.Left + 5, plot.Top + 5, 98, 22), 3, 3);
        DrawText(dc, label, new Point(plot.Left + 11, plot.Top + 9), 9, Brushes.White);
    }

    private static double ToScreenX(double x, Rect plot, FieldMapData data) =>
        plot.Left + (x - data.XMinimum) / (data.XMaximum - data.XMinimum) * plot.Width;

    private static double ToScreenY(double y, Rect plot, FieldMapData data) =>
        plot.Bottom - (y - data.YMinimum) / (data.YMaximum - data.YMinimum) * plot.Height;

    private static string FormatTick(double value) =>
        value.ToString("0.###", CultureInfo.CurrentCulture);

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
