using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Lab1
{
    public abstract class MyShape
    {
        public Point Center { get; set; }
        public List<Brush> SideColors { get; set; }
        public List<double> SideThicknesses { get; set; }
        public Brush FillColor { get; set; } // Новое свойство для заливки
        public abstract int SidesCount { get; }
        public MyShape(Point center, List<Brush> colors, List<double> thicknesses, Brush fill)
        {
            Center = center;
            SideColors = colors;
            SideThicknesses = thicknesses;
            FillColor = fill;
        }

        public abstract FrameworkElement CreateVisual(double size);
    }

    public abstract class MyPolygon : MyShape
    {
        protected Point[] BasePoints;

        public MyPolygon(Point center, List<Brush> colors, List<double> thicknesses, Brush fill)
            : base(center, colors, thicknesses, fill) { }

        public override FrameworkElement CreateVisual(double size)
        {
            int n = this.BasePoints.Length;
            Point[] origPoints = (Point[])this.BasePoints.Clone();
            Point[] currentPoints = (Point[])this.BasePoints.Clone();

            // Вычисляем изначальные длины сторон в пикселях
            double[] initialLengths = new double[n];
            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;
                double dx = origPoints[next].X - origPoints[i].X;
                double dy = origPoints[next].Y - origPoints[i].Y;
                initialLengths[i] = Math.Sqrt(dx * dx + dy * dy) * size;
            }

            Canvas shapeContainer = new Canvas
            {
                Width = 1000,
                Height = 1000,
                Background = null, 
                Tag = new ShapeData
                {
                    OriginalBasePoints = origPoints,
                    BasePoints = currentPoints,
                    CurrentSize = size,
                    SideLengths = initialLengths,
                    CurrentThicknesses = SideThicknesses.ToArray(),
                    CurrentColors = SideColors.ToArray()
                }
            };

            // Подложка-заливка (HitArea)
            Polygon hitArea = new Polygon { Fill = FillColor, Tag = "HitArea" };
            shapeContainer.Children.Add(hitArea);

            // Полигоны, которые заменят линии обводки
            for (int i = 0; i < n; i++)
            {
                shapeContainer.Children.Add(new Polygon { Tag = "Stroke" });
            }

            // Визуальная точка привязки (скрыта по умолчанию)
            Ellipse anchor = new Ellipse
            {
                Width = 14,
                Height = 14,
                Fill = Brushes.Cyan,
                Stroke = Brushes.White,
                StrokeThickness = 2,
                Tag = "Anchor",
                Cursor = Cursors.Cross,
                Visibility = Visibility.Collapsed, // Показываем только при выделении
                IsHitTestVisible = true
            };
            // Центрируем эллипс 14x14 относительно точки 500,500
            Canvas.SetLeft(anchor, 500 - 7);
            Canvas.SetTop(anchor, 500 - 7);
            Panel.SetZIndex(anchor, 9999);
            shapeContainer.Children.Add(anchor);
            
            return shapeContainer;
        }
    }
    public class MyRectangle : MyPolygon
    {
        public override int SidesCount => 4;
        public MyRectangle(Point c, List<Brush> cl, List<double> th, Brush f) : base(c, cl, th, f)
        {
            BasePoints = new Point[] { new Point(-1, -0.6), new Point(1, -0.6), new Point(1, 0.6), new Point(-1, 0.6) };
        }
    }

    public class MyTriangle : MyPolygon
    {
        public override int SidesCount => 3;
        public MyTriangle(Point c, List<Brush> cl, List<double> th, Brush f) : base(c, cl, th, f)
        {
            BasePoints = new Point[] { new Point(0, -1), new Point(1, 0.8), new Point(-1, 0.8) };
        }
    }

    public class MyTrapezoid : MyPolygon
    {
        public override int SidesCount => 4;
        public MyTrapezoid(Point c, List<Brush> cl, List<double> th, Brush f) : base(c, cl, th, f)
        {
            BasePoints = new Point[] { new Point(-0.6, -0.6), new Point(0.6, -0.6), new Point(1, 0.6), new Point(-1, 0.6) };
        }
    }

    public class MyPentagon : MyPolygon
    {
        public override int SidesCount => 5;
        public MyPentagon(Point c, List<Brush> cl, List<double> th, Brush f) : base(c, cl, th, f)
        {
            BasePoints = new Point[5];
            for (int i = 0; i < 5; i++)
            {
                double angle = (i * 72 - 90) * Math.PI / 180;
                BasePoints[i] = new Point(Math.Cos(angle), Math.Sin(angle));
            }
        }
    }

    public class MyCircle : MyShape
    {
        public override int SidesCount => 1;
        public MyCircle(Point c, List<Brush> cl, List<double> th, Brush f) : base(c, cl, th, f) { }
        public override FrameworkElement CreateVisual(double size)
        {
            Canvas shapeContainer = new Canvas
            {
                Width = 1000,
                Height = 1000,
                Background = null,
                Tag = new ShapeData { BasePoints = null, CurrentSize = size }
            };

            Ellipse ellipse = new Ellipse
            {
                Width = size * 2,
                Height = size * 2,
                Stroke = SideColors[0],
                StrokeThickness = SideThicknesses[0],
                Fill = FillColor // Применяем заливку
            };
            Canvas.SetLeft(ellipse, 500 - size);
            Canvas.SetTop(ellipse, 500 - size);
            shapeContainer.Children.Add(ellipse);
            return shapeContainer;
        }
    }
    public class ShapeData
    {
        public Point[] OriginalBasePoints { get; set; }
        public Point[] BasePoints { get; set; }
        public double CurrentSize { get; set; }
        public double[] SideLengths { get; set; }        // Теперь тут длины в пикселях
        public double[] CurrentThicknesses { get; set; } // Текущие толщины сторон
        public Brush[] CurrentColors { get; set; }       // Текущие цвета сторон
        public Point LocalAnchor { get; set; } = new Point(500, 500);

    }
}