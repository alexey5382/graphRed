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
        // ВАЖНО: Теперь передаем SizeX и SizeY
        public abstract FrameworkElement CreateVisual(double sizeX, double sizeY);
    }
    public abstract class MyPolygon : MyShape
    {
        protected Point[] BasePoints;

        public MyPolygon(Point center, List<Brush> colors, List<double> thicknesses, Brush fill)
            : base(center, colors, thicknesses, fill) { }

        public override FrameworkElement CreateVisual(double sizeX, double sizeY)
        {
            int n = this.BasePoints.Length;
            Point[] origPoints = (Point[])this.BasePoints.Clone();
            Point[] currentPoints = (Point[])this.BasePoints.Clone();

            double[] initialLengths = new double[n];
            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;
                double dx = (origPoints[next].X - origPoints[i].X) * sizeX;
                double dy = (origPoints[next].Y - origPoints[i].Y) * sizeY;
                initialLengths[i] = Math.Sqrt(dx * dx + dy * dy);
            }

            Canvas shapeContainer = new Canvas
            {
                Width = 1000,
                Height = 1000,
                Background = null,
                Tag = new ShapeData
                {
                    IsClosed = true,
                    OriginalBasePoints = origPoints,
                    BasePoints = currentPoints,
                    SizeX = sizeX,  // Новая ось X
                    SizeY = sizeY,  // Новая ось Y
                    SideLengths = initialLengths,
                    CurrentThicknesses = SideThicknesses.ToArray(),
                    CurrentColors = SideColors.ToArray(),
                    LocalAnchor = new Point(500, 500)
                }
            };

            Polygon hitArea = new Polygon { Fill = FillColor, Tag = "HitArea" };
            shapeContainer.Children.Add(hitArea);

            for (int i = 0; i < n; i++) shapeContainer.Children.Add(new Polygon { Tag = "Stroke" });

            Ellipse anchor = new Ellipse { Width = 14, Height = 14, Fill = Brushes.Cyan, Stroke = Brushes.White, StrokeThickness = 2, Tag = "Anchor", Cursor = Cursors.Cross, Visibility = Visibility.Collapsed, IsHitTestVisible = true };
            Canvas.SetLeft(anchor, 500 - 7); Canvas.SetTop(anchor, 500 - 7);
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

        public override FrameworkElement CreateVisual(double sizeX, double sizeY)
        {
            var visual = base.CreateVisual(sizeX, sizeY);
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
            // ИСПРАВЛЕНИЕ: Y теперь от -1 до 1 (идеально вписывается в нарисованную рамку)
            : base(c, new Point[] { new Point(-1, -1), new Point(1, -1), new Point(1, 1), new Point(-1, 1) }, true, cl, th, f) { }
    }

    public class MyTriangle : MyCustomPolygon
    {
        public MyTriangle(Point c, List<Brush> cl, List<double> th, Brush f)
            // ИСПРАВЛЕНИЕ: Y теперь от -1 до 1
            : base(c, new Point[] { new Point(0, -1), new Point(1, 1), new Point(-1, 1) }, true, cl, th, f) { }
    }

    public class MyTrapezoid : MyCustomPolygon
    {
        public MyTrapezoid(Point c, List<Brush> cl, List<double> th, Brush f)
            // ИСПРАВЛЕНИЕ: Y теперь от -1 до 1
            : base(c, new Point[] { new Point(-0.6, -1), new Point(0.6, -1), new Point(1, 1), new Point(-1, 1) }, true, cl, th, f) { }
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

        // ВНИМАНИЕ: Здесь теперь sizeX и sizeY
        public override FrameworkElement CreateVisual(double sizeX, double sizeY)
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
                    SizeX = sizeX, // ЗАПИСЫВАЕМ НОВЫЕ ОСИ
                    SizeY = sizeY,
                    CurrentThicknesses = SideThicknesses.ToArray(),
                    CurrentColors = SideColors.ToArray(),
                    LocalAnchor = new Point(500, 500)
                }
            };

            double t = SideThicknesses[0];
            Ellipse ellipse = new Ellipse
            {
                Width = sizeX * 2,   // ИСПОЛЬЗУЕМ SizeX
                Height = sizeY * 2,  // ИСПОЛЬЗУЕМ SizeY
                Stroke = SideColors[0],
                StrokeThickness = t,
                Fill = FillColor
            };

            Canvas.SetLeft(ellipse, 500 - sizeX); // ИСПОЛЬЗУЕМ SizeX
            Canvas.SetTop(ellipse, 500 - sizeY);  // ИСПОЛЬЗУЕМ SizeY
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
    }    // КЛАСС ДЛЯ НЕЗАМКНУТЫХ ЛОМАНЫХ (ОТРЕЗКОВ)
    public class MyPolyline : MyPolygon
    {
        public override int SidesCount => BasePoints.Length - 1;

        public MyPolyline(Point center, Point[] points, List<Brush> cl, List<double> th)
            : base(center, cl, th, null)
        {
            BasePoints = points;
        }

        // ВНИМАНИЕ: Здесь теперь sizeX и sizeY
        public override FrameworkElement CreateVisual(double sizeX, double sizeY)
        {
            int pointsCount = BasePoints.Length;
            int segmentsCount = pointsCount - 1;

            Point[] origPoints = (Point[])BasePoints.Clone();
            Point[] currentPoints = (Point[])BasePoints.Clone();

            double[] initialLengths = new double[segmentsCount];
            for (int i = 0; i < segmentsCount; i++)
            {
                // ИСПРАВЛЕННЫЙ РАСЧЕТ ДЛИНЫ: умножаем каждую ось на свой размер
                double dx = (origPoints[i + 1].X - origPoints[i].X) * sizeX;
                double dy = (origPoints[i + 1].Y - origPoints[i].Y) * sizeY;
                initialLengths[i] = Math.Sqrt(dx * dx + dy * dy);
            }

            Canvas shapeContainer = new Canvas
            {
                Width = 1000,
                Height = 1000,
                Background = null,
                Tag = new ShapeData
                {
                    IsClosed = false,
                    OriginalBasePoints = origPoints,
                    BasePoints = currentPoints,
                    SizeX = sizeX, // ЗАПИСЫВАЕМ НОВЫЕ ОСИ
                    SizeY = sizeY,
                    SideLengths = initialLengths,
                    CurrentThicknesses = SideThicknesses.ToArray(),
                    CurrentColors = SideColors.ToArray(),
                    LocalAnchor = new Point(500, 500)
                }
            };

            Polygon hitArea = new Polygon { Tag = "HitArea", Fill = null };
            shapeContainer.Children.Add(hitArea);

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
        public string GroupId { get; set; } // <--- НОВОЕ СВОЙСТВО
        public bool IsClosed { get; set; } = true;
        public Point[] OriginalBasePoints { get; set; }
        public Point[] BasePoints { get; set; }
        // Убрали CurrentSize, добавили две оси
        public double SizeX { get; set; }
        public double SizeY { get; set; }
        public double[] SideLengths { get; set; }
        public double[] CurrentThicknesses { get; set; }
        public Brush[] CurrentColors { get; set; }
        public Point LocalAnchor { get; set; } = new Point(500, 500);
    }
    public class ShapeGroup
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "Новая группа";
        // Единая точка привязки для всей группы (в абсолютных координатах холста)
        public Point GroupAnchor { get; set; }
    }
}