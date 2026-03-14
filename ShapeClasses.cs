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
        public Brush FillColor { get; set; }
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
                    IsClosed = true, // Переопределится ниже в MyCustomPolygon
                    OriginalBasePoints = origPoints,
                    BasePoints = currentPoints,
                    CurrentSize = size,
                    SideLengths = initialLengths,
                    CurrentThicknesses = SideThicknesses.ToArray(),
                    CurrentColors = SideColors.ToArray(),
                    LocalAnchor = new Point(500, 500)
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

            // Визуальная точка привязки
            Ellipse anchor = new Ellipse
            {
                Width = 14,
                Height = 14,
                Fill = Brushes.Cyan,
                Stroke = Brushes.White,
                StrokeThickness = 2,
                Tag = "Anchor",
                Cursor = Cursors.Cross,
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = true
            };
            Canvas.SetLeft(anchor, 500 - 7);
            Canvas.SetTop(anchor, 500 - 7);
            Panel.SetZIndex(anchor, 9999);
            shapeContainer.Children.Add(anchor);

            return shapeContainer;
        }
    }

    // БАЗОВЫЙ КЛАСС ДЛЯ ВСЕХ ФИГУР-ЛОМАНЫХ И МНОГОУГОЛЬНИКОВ
    public class MyCustomPolygon : MyPolygon
    {
        public override int SidesCount => BasePoints.Length;
        private bool _isClosed;

        // Конструктор принимает точки и флаг замкнутости
        public MyCustomPolygon(Point center, Point[] points, bool isClosed, List<Brush> cl, List<double> th, Brush f)
            : base(center, cl, th, f)
        {
            BasePoints = points;
            _isClosed = isClosed;
        }

        public override FrameworkElement CreateVisual(double size)
        {
            var visual = base.CreateVisual(size);
            if (visual is Canvas c && c.Tag is ShapeData d)
            {
                d.IsClosed = _isClosed; // Передаем флаг в ShapeData
            }
            return visual;
        }
    }

    public class MyRectangle : MyCustomPolygon
    {
        public MyRectangle(Point c, List<Brush> cl, List<double> th, Brush f)
            : base(c, new Point[] { new Point(-1, -0.6), new Point(1, -0.6), new Point(1, 0.6), new Point(-1, 0.6) }, true, cl, th, f) { }
    }

    public class MyTriangle : MyCustomPolygon
    {
        public MyTriangle(Point c, List<Brush> cl, List<double> th, Brush f)
            : base(c, new Point[] { new Point(0, -1), new Point(1, 0.8), new Point(-1, 0.8) }, true, cl, th, f) { }
    }

    public class MyTrapezoid : MyCustomPolygon
    {
        public MyTrapezoid(Point c, List<Brush> cl, List<double> th, Brush f)
            : base(c, new Point[] { new Point(-0.6, -0.6), new Point(0.6, -0.6), new Point(1, 0.6), new Point(-1, 0.6) }, true, cl, th, f) { }
    }

    public class MyPentagon : MyCustomPolygon
    {
        public MyPentagon(Point c, List<Brush> cl, List<double> th, Brush f)
            : base(c, GetPentagonPoints(), true, cl, th, f) { }

        // Вынес генерацию точек пятиугольника в отдельный статический метод для удобства передачи в base()
        private static Point[] GetPentagonPoints()
        {
            Point[] pts = new Point[5];
            for (int i = 0; i < 5; i++)
            {
                double angle = (i * 72 - 90) * Math.PI / 180;
                pts[i] = new Point(Math.Cos(angle), Math.Sin(angle));
            }
            return pts;
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
                Tag = new ShapeData
                {
                    IsClosed = true,
                    BasePoints = null,
                    CurrentSize = size,
                    CurrentThicknesses = SideThicknesses.ToArray(),
                    CurrentColors = SideColors.ToArray(),
                    LocalAnchor = new Point(500, 500)
                }
            };

            double t = SideThicknesses[0];
            Ellipse ellipse = new Ellipse
            {
                Width = size * 2,
                Height = size * 2,
                Stroke = SideColors[0],
                StrokeThickness = t,
                Fill = FillColor
            };

            Canvas.SetLeft(ellipse, 500 - size);
            Canvas.SetTop(ellipse, 500 - size);
            shapeContainer.Children.Add(ellipse);

            Ellipse anchor = new Ellipse
            {
                Width = 14,
                Height = 14,
                Fill = Brushes.Cyan,
                Stroke = Brushes.White,
                StrokeThickness = 2,
                Tag = "Anchor",
                Cursor = Cursors.Cross,
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = true
            };
            Canvas.SetLeft(anchor, 500 - 7);
            Canvas.SetTop(anchor, 500 - 7);
            Panel.SetZIndex(anchor, 9999);
            shapeContainer.Children.Add(anchor);

            return shapeContainer;
        }
    }
    // КЛАСС ДЛЯ НЕЗАМКНУТЫХ ЛОМАНЫХ (ОТРЕЗКОВ)
    public class MyPolyline : MyPolygon
    {
        public override int SidesCount => BasePoints.Length - 1; // У незамкнутой линии на 1 сторону меньше

        public MyPolyline(Point center, Point[] points, List<Brush> cl, List<double> th)
            : base(center, cl, th, null) // Заливка всегда null
        {
            BasePoints = points;
        }

        public override FrameworkElement CreateVisual(double size)
        {
            int pointsCount = BasePoints.Length;
            int segmentsCount = pointsCount - 1;

            Point[] origPoints = (Point[])BasePoints.Clone();
            Point[] currentPoints = (Point[])BasePoints.Clone();

            // Вычисляем изначальные длины только для существующих сегментов
            double[] initialLengths = new double[segmentsCount];
            for (int i = 0; i < segmentsCount; i++)
            {
                double dx = origPoints[i + 1].X - origPoints[i].X;
                double dy = origPoints[i + 1].Y - origPoints[i].Y;
                initialLengths[i] = Math.Sqrt(dx * dx + dy * dy) * size;
            }

            Canvas shapeContainer = new Canvas
            {
                Width = 1000,
                Height = 1000,
                Background = null,
                Tag = new ShapeData
                {
                    IsClosed = false, // ВАЖНО: флаг незамкнутости
                    OriginalBasePoints = origPoints,
                    BasePoints = currentPoints,
                    CurrentSize = size,
                    SideLengths = initialLengths,
                    CurrentThicknesses = SideThicknesses.ToArray(),
                    CurrentColors = SideColors.ToArray(),
                    LocalAnchor = new Point(500, 500)
                }
            };

            // Добавляем пустую подложку, чтобы не ломать логику поиска в других местах
            Polygon hitArea = new Polygon { Tag = "HitArea", Fill = null };
            shapeContainer.Children.Add(hitArea);

            // Создаем полигоны строго по количеству отрезков (N - 1)
            for (int i = 0; i < segmentsCount; i++)
            {
                shapeContainer.Children.Add(new Polygon { Tag = "Stroke" });
            }

            Ellipse anchor = new Ellipse
            {
                Width = 14,
                Height = 14,
                Fill = Brushes.Cyan,
                Stroke = Brushes.White,
                StrokeThickness = 2,
                Tag = "Anchor",
                Cursor = Cursors.Cross,
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = true
            };
            Canvas.SetLeft(anchor, 500 - 7); Canvas.SetTop(anchor, 500 - 7);
            Panel.SetZIndex(anchor, 9999);
            shapeContainer.Children.Add(anchor);

            return shapeContainer;
        }
    }

    public class ShapeData
    {
        public bool IsClosed { get; set; } = true;
        public Point[] OriginalBasePoints { get; set; }
        public Point[] BasePoints { get; set; }
        public double CurrentSize { get; set; }
        public double[] SideLengths { get; set; }
        public double[] CurrentThicknesses { get; set; }
        public Brush[] CurrentColors { get; set; }
        public Point LocalAnchor { get; set; } = new Point(500, 500);
    }
}