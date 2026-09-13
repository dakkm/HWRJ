using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using PreProcess.Wpf.ViewModels;

namespace PreProcess.Wpf.Views
{
    public sealed class TrajectoryView : FrameworkElement
    {
        public static readonly DependencyProperty ModelProperty = DependencyProperty.Register(
            "Model", typeof(TrajectoryDiagramViewModel), typeof(TrajectoryView),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public TrajectoryDiagramViewModel Model
        {
            get { return (TrajectoryDiagramViewModel)GetValue(ModelProperty); }
            set { SetValue(ModelProperty, value); }
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            dc.DrawRectangle(Brushes.White, new Pen(new SolidColorBrush(Color.FromRgb(212, 221, 232)), 1), new Rect(0, 0, ActualWidth, ActualHeight));
            TrajectoryDiagramViewModel model = Model;
            if (model == null || !model.HasData || ActualWidth < 160 || ActualHeight < 120)
            {
                Text(dc, "暂无轨迹数据", 18, 18, Brushes.Gray);
                return;
            }

            double left = 64, top = 48, right = Math.Max(left + 10, ActualWidth - 20), bottom = Math.Max(top + 10, ActualHeight - 52);
            var allPoints = model.Series.SelectMany(series => series.Points).ToList();
            double minX = allPoints.Min(point => point.X), maxX = allPoints.Max(point => point.X);
            double minY = allPoints.Min(point => point.Y), maxY = allPoints.Max(point => point.Y);
            ExpandRange(ref minX, ref maxX);
            ExpandRange(ref minY, ref maxY);

            var axis = new Pen(new SolidColorBrush(Color.FromRgb(82, 101, 122)), 1);
            dc.DrawLine(axis, new Point(left, top), new Point(left, bottom));
            dc.DrawLine(axis, new Point(left, bottom), new Point(right, bottom));
            for (int tick = 0; tick <= 4; tick++)
            {
                double ratio = tick / 4.0;
                double x = left + ratio * (right - left), y = bottom - ratio * (bottom - top);
                dc.DrawLine(new Pen(Brushes.Gainsboro, 1), new Point(left, y), new Point(right, y));
                dc.DrawLine(new Pen(Brushes.Gainsboro, 1), new Point(x, top), new Point(x, bottom));
                Text(dc, (minY + ratio * (maxY - minY)).ToString("G5", CultureInfo.InvariantCulture), 4, y - 8, Brushes.DimGray);
                Text(dc, (minX + ratio * (maxX - minX)).ToString("G5", CultureInfo.InvariantCulture), x - 12, bottom + 6, Brushes.DimGray);
            }

            foreach (ChartSeriesViewModel series in model.Series)
            {
                var points = series.Points.Where(point => IsFinite(point.X) && IsFinite(point.Y)).ToList();
                if (points.Count == 0) continue;
                var pen = new Pen(new SolidColorBrush(series.Color), 1.8);
                StreamGeometry geometry = new StreamGeometry();
                using (StreamGeometryContext context = geometry.Open())
                {
                    Point start = Map(points[0], minX, maxX, minY, maxY, left, right, top, bottom);
                    context.BeginFigure(start, false, false);
                    foreach (ChartPointViewModel point in points.Skip(1))
                        context.LineTo(Map(point, minX, maxX, minY, maxY, left, right, top, bottom), true, false);
                }
                geometry.Freeze();
                dc.DrawGeometry(null, pen, geometry);

                Point first = Map(points[0], minX, maxX, minY, maxY, left, right, top, bottom);
                Point last = Map(points[points.Count - 1], minX, maxX, minY, maxY, left, right, top, bottom);
                dc.DrawEllipse(Brushes.White, pen, first, 3.5, 3.5);
                dc.DrawRectangle(new SolidColorBrush(series.Color), null, new Rect(last.X - 3.5, last.Y - 3.5, 7, 7));
            }

            double legendX = left;
            foreach (ChartSeriesViewModel series in model.Series.Take(8))
            {
                dc.DrawLine(new Pen(new SolidColorBrush(series.Color), 2), new Point(legendX, 18), new Point(legendX + 18, 18));
                Text(dc, series.Name, legendX + 22, 10, Brushes.DimGray);
                legendX += Math.Min(130, 35 + (series.Name ?? String.Empty).Length * 12);
                if (legendX > right - 70) break;
            }
            Text(dc, "○ 起点   ■ 终点", Math.Max(left, right - 105), 30, Brushes.DimGray);
            Text(dc, model.XAxisTitle, (left + right) / 2 - 20, ActualHeight - 20, Brushes.Black);
            Text(dc, model.YAxisTitle, 4, 2, Brushes.Black);
        }

        private static Point Map(ChartPointViewModel point, double minX, double maxX, double minY, double maxY,
            double left, double right, double top, double bottom)
        {
            return new Point(
                left + (point.X - minX) / (maxX - minX) * (right - left),
                bottom - (point.Y - minY) / (maxY - minY) * (bottom - top));
        }

        private static void ExpandRange(ref double minimum, ref double maximum)
        {
            if (maximum <= minimum) { minimum -= .5; maximum += .5; return; }
            double padding = (maximum - minimum) * .05;
            minimum -= padding;
            maximum += padding;
        }

        private static bool IsFinite(double value) { return !Double.IsNaN(value) && !Double.IsInfinity(value); }

        private static void Text(DrawingContext dc, string text, double x, double y, Brush brush)
        {
            dc.DrawText(new FormattedText(text ?? String.Empty, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), 11, brush, 1.0), new Point(x, y));
        }
    }
}
