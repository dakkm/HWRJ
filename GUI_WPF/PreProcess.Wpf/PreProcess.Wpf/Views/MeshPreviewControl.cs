using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Input;
using System.Collections.Generic;
using PreProcess.Wpf.Services.Mesh;

namespace PreProcess.Wpf.Views
{
    /// <summary>独立球壳网格的可旋转投影；蓝色线框是球面示意，橙色边是实际网格面。</summary>
    public sealed class MeshPreviewControl : FrameworkElement
    {
        private double yaw = -0.35;
        private double pitch = 0.18;
        private double zoom = 1.0;
        private Point dragStart;
        private double dragYaw;
        private double dragPitch;
        public static readonly DependencyProperty MeshProperty = DependencyProperty.Register(
            "Mesh", typeof(SphericalShellMesh), typeof(MeshPreviewControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public SphericalShellMesh Mesh { get { return (SphericalShellMesh)GetValue(MeshProperty); } set { SetValue(MeshProperty, value); } }

        public MeshPreviewControl()
        {
            Focusable = true;
            MouseLeftButtonDown += OnMouseLeftButtonDown;
            MouseMove += OnMouseMove;
            MouseLeftButtonUp += OnMouseLeftButtonUp;
            MouseWheel += OnMouseWheel;
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            drawingContext.DrawRectangle(Brushes.White, new Pen(new SolidColorBrush(Color.FromRgb(203, 213, 225)), 1), new Rect(0, 0, ActualWidth, ActualHeight));
            if (Mesh == null || Mesh.Nodes == null || Mesh.SurfaceFaces == null || Mesh.SurfaceFaces.Count == 0)
            {
                DrawText(drawingContext, "生成或打开网格后显示表面投影", 12, 12, Brushes.SlateGray);
                return;
            }
            double cx = ActualWidth / 2.0, cy = ActualHeight / 2.0;
            double scale = Math.Max(10, Math.Min(ActualWidth, ActualHeight) * 0.39) * zoom;
            DrawSphereWireframe(drawingContext, cx, cy, scale, 18, 36, 1.0, false);
            DrawSphereWireframe(drawingContext, cx, cy, scale, 18, 36, 0.96, true);
            DrawActualSurfaceEdges(drawingContext, cx, cy, scale);
            DrawText(drawingContext, "蓝色细线=球面示意；橙色粗线=实际网格面 · 拖动旋转/滚轮缩放", 10, 10, Brushes.SlateGray);
        }

        private void DrawActualSurfaceEdges(DrawingContext dc, double cx, double cy, double scale)
        {
            var nodes = new Dictionary<int, MeshNode>();
            foreach (var node in Mesh.Nodes) nodes[node.Id] = node;
            var pen = new Pen(new SolidColorBrush(Color.FromRgb(217, 119, 6)), 2.0);
            foreach (var face in Mesh.SurfaceFaces)
            {
                if (face.Region != "Outer") continue;
                MeshNode a, b, c;
                if (!nodes.TryGetValue(face.A, out a) || !nodes.TryGetValue(face.B, out b) || !nodes.TryGetValue(face.C, out c)) continue;
                Point pa = ProjectActual(a, cx, cy, scale), pb = ProjectActual(b, cx, cy, scale), pc = ProjectActual(c, cx, cy, scale);
                dc.DrawLine(pen, pa, pb); dc.DrawLine(pen, pb, pc); dc.DrawLine(pen, pc, pa);
            }
        }

        private Point ProjectActual(MeshNode node, double cx, double cy, double scale)
        {
            double radius = Math.Max(Mesh.OuterRadius, 1e-9);
            double x = node.X / radius, y = node.Z / radius, z = node.Y / radius;
            double x1 = Math.Cos(yaw) * x + Math.Sin(yaw) * z;
            double z1 = -Math.Sin(yaw) * x + Math.Cos(yaw) * z;
            double y2 = Math.Cos(pitch) * y - Math.Sin(pitch) * z1;
            double z2 = Math.Sin(pitch) * y + Math.Cos(pitch) * z1;
            double perspective = 1.0 / (1.0 + z2 * 0.28);
            return new Point(cx + x1 * scale * perspective, cy - y2 * scale * perspective);
        }

        private void DrawSphereWireframe(DrawingContext dc, double cx, double cy, double scale, int latitudeCount, int longitudeCount, double radiusFactor, bool inner)
        {
            var points = new Point[latitudeCount + 1, longitudeCount + 1];
            var depths = new double[latitudeCount + 1, longitudeCount + 1];
            for (int lat = 0; lat <= latitudeCount; lat++)
            {
                double theta = Math.PI * lat / latitudeCount;
                for (int lon = 0; lon <= longitudeCount; lon++)
                {
                    double phi = 2 * Math.PI * lon / longitudeCount;
                    double x = radiusFactor * Math.Sin(theta) * Math.Cos(phi);
                    double y = radiusFactor * Math.Cos(theta);
                    double z = radiusFactor * Math.Sin(theta) * Math.Sin(phi);
                    double cyaw = Math.Cos(yaw), syaw = Math.Sin(yaw);
                    double x1 = cyaw * x + syaw * z;
                    double z1 = -syaw * x + cyaw * z;
                    double cpitch = Math.Cos(pitch), spitch = Math.Sin(pitch);
                    double y2 = cpitch * y - spitch * z1;
                    double z2 = spitch * y + cpitch * z1;
                    double perspective = 1.0 / (1.0 + z2 * 0.28);
                    points[lat, lon] = new Point(cx + x1 * scale * perspective, cy - y2 * scale * perspective);
                    depths[lat, lon] = z2;
                }
            }
            for (int lat = 1; lat < latitudeCount; lat++)
            {
                for (int lon = 0; lon <= longitudeCount; lon++)
                {
                    DrawSegment(dc, points[lat, lon], points[lat, Math.Min(lon + 1, longitudeCount)], depths[lat, lon], inner);
                }
            }
            for (int lon = 0; lon < longitudeCount; lon += 2)
            {
                for (int lat = 0; lat <= latitudeCount; lat++)
                {
                    DrawSegment(dc, points[lat, lon], points[Math.Min(lat + 1, latitudeCount), lon], depths[lat, lon], inner);
                }
            }
            for (int lat = 1; lat < latitudeCount; lat++)
            {
                for (int lon = 0; lon < longitudeCount; lon += 2)
                {
                    DrawSegment(dc, points[lat, lon], points[lat + 1, Math.Min(lon + 1, longitudeCount)], depths[lat, lon], inner);
                }
            }
        }

        private static void DrawSegment(DrawingContext dc, Point a, Point b, double depth, bool inner)
        {
            byte alpha = (byte)(inner ? 55 : 90 + Math.Max(0, Math.Min(80, (depth + 1) * 35)));
            var pen = new Pen(new SolidColorBrush(Color.FromArgb(alpha, inner ? (byte)100 : (byte)37, inner ? (byte)116 : (byte)99, inner ? (byte)139 : (byte)235)), inner ? 0.55 : 0.8);
            if (inner) pen.DashStyle = DashStyles.Dot;
            dc.DrawLine(pen, a, b);
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Focus(); CaptureMouse(); dragStart = e.GetPosition(this); dragYaw = yaw; dragPitch = pitch; e.Handled = true;
        }
        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!IsMouseCaptured) return;
            Point current = e.GetPosition(this);
            yaw = dragYaw + (current.X - dragStart.X) * 0.012;
            pitch = Math.Max(-1.35, Math.Min(1.35, dragPitch + (current.Y - dragStart.Y) * 0.012));
            InvalidateVisual();
        }
        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e) { if (IsMouseCaptured) ReleaseMouseCapture(); e.Handled = true; }
        private void OnMouseWheel(object sender, MouseWheelEventArgs e) { zoom = Math.Max(0.55, Math.Min(2.2, zoom * (e.Delta > 0 ? 1.1 : 0.9))); InvalidateVisual(); e.Handled = true; }

        private static Point Project(MeshNode node, double cx, double cy, double scale) { return new Point(cx + node.X / MeshRadius(node) * scale, cy - node.Z / MeshRadius(node) * scale); }
        private static double MeshRadius(MeshNode node) { return Math.Max(1e-9, Math.Sqrt(node.X * node.X + node.Y * node.Y + node.Z * node.Z)); }
        private static void DrawText(DrawingContext dc, string text, double x, double y, Brush brush) { dc.DrawText(new FormattedText(text, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 12, brush, 1.0), new Point(x, y)); }

        private sealed class StreamGeometryBuilder
        {
            private readonly PointCollection points;
            public StreamGeometryBuilder(PointCollection value) { points = value; }
            public Geometry Build()
            {
                var geometry = new StreamGeometry();
                using (var context = geometry.Open()) { context.BeginFigure(points[0], true, true); for (int i = 1; i < points.Count; i++) context.LineTo(points[i], true, false); }
                geometry.Freeze(); return geometry;
            }
        }
    }
}
