using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using PreProcess.Wpf.ViewModels;

namespace PreProcess.Wpf.Views
{
    // 定义 ChartView 类型，集中封装与该领域对象相关的状态和行为。
    public sealed class ChartView : FrameworkElement
    {
        public static readonly DependencyProperty ModelProperty = DependencyProperty.Register("Model", typeof(ChartViewModel), typeof(ChartView),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public ChartViewModel Model { get { return (ChartViewModel)GetValue(ModelProperty); } set { SetValue(ModelProperty, value); } }

        protected override void OnRender(DrawingContext dc)
        {
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            base.OnRender(dc);
            dc.DrawRectangle(Brushes.White, new Pen(new SolidColorBrush(Color.FromRgb(212, 221, 232)), 1), new Rect(0, 0, ActualWidth, ActualHeight));
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            ChartViewModel model = Model;
            if (model == null || !model.HasData || ActualWidth < 160 || ActualHeight < 120) { Text(dc, "暂无温度曲线数据", 18, 18, Brushes.Gray); return; }
            double left = 64, top = 48, right = Math.Max(left + 10, ActualWidth - 20), bottom = Math.Max(top + 10, ActualHeight - 52);
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            var points = model.Series.SelectMany(s => s.Points).ToList();
            double minX = points.Min(p => p.X), maxX = points.Max(p => p.X), minY = points.Min(p => p.Y), maxY = points.Max(p => p.Y);
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (maxX <= minX) maxX = minX + 1; if (maxY <= minY) { minY -= .5; maxY += .5; }
            double pad = (maxY - minY) * .05; minY -= pad; maxY += pad;
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            var axis = new Pen(new SolidColorBrush(Color.FromRgb(82, 101, 122)), 1);
            dc.DrawLine(axis, new Point(left, top), new Point(left, bottom)); dc.DrawLine(axis, new Point(left, bottom), new Point(right, bottom));
            for (int tick = 0; tick <= 4; tick++)
            {
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                double ratio = tick / 4.0, x = left + ratio * (right - left), y = bottom - ratio * (bottom - top);
                dc.DrawLine(new Pen(Brushes.Gainsboro, 1), new Point(left, y), new Point(right, y));
                // 将外部数据转换为目标类型，并保持约定的表示格式。
                Text(dc, (minY + ratio * (maxY - minY)).ToString("G5", CultureInfo.InvariantCulture), 4, y - 8, Brushes.DimGray);
                Text(dc, (minX + ratio * (maxX - minX)).ToString("G5", CultureInfo.InvariantCulture), x - 12, bottom + 6, Brushes.DimGray);
            }
            // 遍历当前数据集合，逐项完成必要的转换或状态更新。
            foreach (ChartSeriesViewModel series in model.Series)
            // 说明当前文件承载的类型职责，便于维护者快速定位功能边界。
            {
                StreamGeometry geometry = new StreamGeometry();
                using (StreamGeometryContext context = geometry.Open())
                {
                    bool first = true;
                    // 遍历当前数据集合，逐项完成必要的转换或状态更新。
                    foreach (ChartPointViewModel point in series.Points)
                    {
                        Point p = new Point(left + (point.X - minX) / (maxX - minX) * (right - left), bottom - (point.Y - minY) / (maxY - minY) * (bottom - top));
                        // 校验当前条件，仅在满足业务约束时进入该处理分支。
                        if (first) { context.BeginFigure(p, false, false); first = false; } else context.LineTo(p, true, false);
                    }
                }
                geometry.Freeze(); dc.DrawGeometry(null, new Pen(new SolidColorBrush(series.Color), 1.5), geometry);
            }
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            double legendX = left;
            foreach (ChartSeriesViewModel series in model.Series.Take(8))
            {
                // 调用对应组件完成当前步骤，并保留产生的处理结果。
                dc.DrawLine(new Pen(new SolidColorBrush(series.Color), 2), new Point(legendX, 18), new Point(legendX + 18, 18));
                Text(dc, series.Name, legendX + 22, 10, Brushes.DimGray);
                legendX += Math.Min(150, 32 + (series.Name ?? String.Empty).Length * 7);
                // 校验当前条件，仅在满足业务约束时进入该处理分支。
                if (legendX > right - 80) break;
            }
            Text(dc, model.XAxisTitle, (left + right) / 2 - 25, ActualHeight - 20, Brushes.Black);
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            Text(dc, model.YAxisTitle, 4, 2, Brushes.Black);
        }

        private static void Text(DrawingContext dc, string text, double x, double y, Brush brush)
        {
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            dc.DrawText(new FormattedText(text ?? String.Empty, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), 11, brush, 1.0), new Point(x, y));
        }
    }
}
