using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace WpfApp1;

public static class CrosshairRenderer
{
    public static void Draw(Canvas cv, Crosshair c, double cx, double cy)
    {
        cv.Children.Clear();
        Color col;
        try { col = (Color)ColorConverter.ConvertFromString(c.Color); } catch { col = Colors.White; }

        var root = new Canvas { IsHitTestVisible = false };
        if (c.Glow) root.Effect = new DropShadowEffect { Color = col, BlurRadius = 10, ShadowDepth = 0, Opacity = 0.9 };
        var fill = new SolidColorBrush(col);

        foreach (var outline in c.Outline ? new[] { true, false } : new[] { false })
        {
            Brush b = outline ? Brushes.Black : fill;
            double t = c.Thickness + (outline ? 2 : 0), h = c.Size / 2, g = c.Gap, ex = outline ? 1 : 0;

            void L(double x1, double y1, double x2, double y2) =>
                root.Children.Add(new Line { X1 = cx + x1, Y1 = cy + y1, X2 = cx + x2, Y2 = cy + y2, Stroke = b, StrokeThickness = t });

            void Arms(double from, double to)
            {
                L(from - ex, 0, to + ex, 0);   L(-from + ex, 0, -to - ex, 0);
                L(0, from - ex, 0, to + ex);   L(0, -from + ex, 0, -to - ex);
            }

            void Ring(double d)
            {
                var e = new Ellipse { Width = d, Height = d, Stroke = b, StrokeThickness = t };
                Canvas.SetLeft(e, cx - d / 2); Canvas.SetTop(e, cy - d / 2);
                root.Children.Add(e);
            }

            void Dot(double d)
            {
                if (outline) d += 2;
                var e = new Ellipse { Width = d, Height = d, Fill = b };
                Canvas.SetLeft(e, cx - d / 2); Canvas.SetTop(e, cy - d / 2);
                root.Children.Add(e);
            }

            switch (c.Shape)
            {
                case "Dot": Dot(Math.Max(2, c.Size / 2)); break;
                case "Circle": Ring(c.Size); break;
                case "Diamond":
                    root.Children.Add(new Polygon
                    {
                        Points = new PointCollection { new Point(cx, cy - h), new Point(cx + h, cy), new Point(cx, cy + h), new Point(cx - h, cy) },
                        Stroke = b, StrokeThickness = t
                    });
                    break;
                case "Sniper": Ring(c.Size); Arms(Math.Max(g, 2), h * 1.6); break;
                default: Arms(g, h); break;
            }

            if (c.CenterDot) Dot(Math.Max(2, c.Thickness + 1));
        }
        cv.Children.Add(root);
    }
}
