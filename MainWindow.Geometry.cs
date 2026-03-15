using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Lab1
{
    public partial class MainWindow
    {
        private void UpdatePolygonGeometry(Canvas container)
        {
            ShapeData data = container.Tag as ShapeData;
            if (data == null) return;

            // --- ИСПРАВЛЕНИЕ: Обработка масштабирования круга ---
            if (data.BasePoints == null)
            {
                var ellipse = container.Children.OfType<Ellipse>().FirstOrDefault(e => e.Tag?.ToString() != "Anchor");
                if (ellipse != null)
                {
                    ellipse.Width = data.SizeX * 2;
                    ellipse.Height = data.SizeY * 2;
                    Canvas.SetLeft(ellipse, 500 - data.SizeX);
                    Canvas.SetTop(ellipse, 500 - data.SizeY);
                }
                UpdateBoundingBox(container);
                return;
            }

            int n = data.BasePoints.Length;
            Point[] pts = new Point[n];
            for (int i = 0; i < n; i++)
                pts[i] = new Point(data.BasePoints[i].X * data.SizeX + 500, data.BasePoints[i].Y * data.SizeY + 500); // ИСПОЛЬЗУЕМ SizeX и SizeY!

            var hitArea = container.Children.OfType<Polygon>().FirstOrDefault(p => p.Tag?.ToString() == "HitArea");
            if (hitArea != null) hitArea.Points = new PointCollection(pts);

            Point[] outer = new Point[n];
            Point[] inner = new Point[n];

            if (data.IsClosed)
            {
                // ЛОГИКА ДЛЯ ЗАМКНУТЫХ ФИГУР (Биссектрисы)
                for (int i = 0; i < n; i++)
                {
                    int prev = (i - 1 + n) % n;
                    int next = (i + 1) % n;

                    Vector dIn = pts[i] - pts[prev]; dIn.Normalize();
                    Vector dOut = pts[next] - pts[i]; dOut.Normalize();
                    Vector nIn = new Vector(dIn.Y, -dIn.X);
                    Vector nOut = new Vector(dOut.Y, -dOut.X);

                    double tIn = data.CurrentThicknesses[prev];
                    double tOut = data.CurrentThicknesses[i];

                    outer[i] = GetIntersection(pts[i], dIn, pts[i], dOut, pts[i]);
                    inner[i] = GetIntersection(pts[i] - nIn * tIn, dIn, pts[i] - nOut * tOut, dOut, pts[i] - nIn * tIn);
                }
            }
            else
            {
                // ЛОГИКА ДЛЯ НЕЗАМКНУТОЙ ЛОМАНОЙ (Прямые концы и перпендикуляры)
                for (int i = 0; i < n; i++)
                {
                    if (i == 0) // Торец начала
                    {
                        Vector d = pts[1] - pts[0]; d.Normalize();
                        Vector norm = new Vector(d.Y, -d.X);
                        outer[0] = pts[0];
                        inner[0] = pts[0] - norm * data.CurrentThicknesses[0];
                    }
                    else if (i == n - 1) // Торец конца
                    {
                        Vector d = pts[n - 1] - pts[n - 2]; d.Normalize();
                        Vector norm = new Vector(d.Y, -d.X);
                        outer[n - 1] = pts[n - 1];
                        inner[n - 1] = pts[n - 1] - norm * data.CurrentThicknesses[n - 2]; // толщина последнего реального отрезка
                    }
                    else // Внутренние стыки
                    {
                        Vector dIn = pts[i] - pts[i - 1]; dIn.Normalize();
                        Vector dOut = pts[i + 1] - pts[i]; dOut.Normalize();
                        Vector nIn = new Vector(dIn.Y, -dIn.X);
                        Vector nOut = new Vector(dOut.Y, -dOut.X);

                        double tIn = data.CurrentThicknesses[i - 1];
                        double tOut = data.CurrentThicknesses[i];

                        outer[i] = GetIntersection(pts[i], dIn, pts[i], dOut, pts[i]);
                        inner[i] = GetIntersection(pts[i] - nIn * tIn, dIn, pts[i] - nOut * tOut, dOut, pts[i] - nIn * tIn);
                    }
                }
            }

            // Применяем геометрию к полигонам (цикл строго до segmentsCount, чтобы избежать выхода за пределы массива)
            int segmentsCount = data.IsClosed ? n : n - 1;
            var strokePolygons = container.Children.OfType<Polygon>().Where(p => p.Tag?.ToString() == "Stroke").ToList();

            for (int i = 0; i < strokePolygons.Count; i++)
            {
                if (i < segmentsCount)
                {
                    int next = data.IsClosed ? (i + 1) % n : i + 1;
                    strokePolygons[i].Points = new PointCollection(new[] { outer[i], outer[next], inner[next], inner[i] });
                    strokePolygons[i].Fill = data.CurrentColors[i];
                    strokePolygons[i].Visibility = Visibility.Visible;
                }
                else
                {
                    strokePolygons[i].Visibility = Visibility.Collapsed;
                }
            }

            // Логика подсветки выбранной стороны (SideHighlight)
            var highlight = container.Children.OfType<Line>().FirstOrDefault(l => l.Tag?.ToString() == "SideHighlight");
            if (container == _selectedElement && _selectedSideIndex >= 0 && _selectedSideIndex < segmentsCount && data.CurrentThicknesses != null)
            {
                if (highlight == null)
                {
                    highlight = new Line { Tag = "SideHighlight", Stroke = Brushes.White, StrokeThickness = 3, StrokeDashArray = new DoubleCollection { 3, 3 }, IsHitTestVisible = false };
                    Panel.SetZIndex(highlight, 999);
                    container.Children.Add(highlight);
                }
                highlight.Visibility = Visibility.Visible;

                int next = data.IsClosed ? (_selectedSideIndex + 1) % n : _selectedSideIndex + 1;
                highlight.X1 = data.BasePoints[_selectedSideIndex].X * data.SizeX + 500;
                highlight.Y1 = data.BasePoints[_selectedSideIndex].Y * data.SizeY + 500;
                highlight.X2 = data.BasePoints[next].X * data.SizeX + 500;
                highlight.Y2 = data.BasePoints[next].Y * data.SizeY + 500;
            }
            else if (highlight != null)
            {
                highlight.Visibility = Visibility.Collapsed;
            }

            UpdateBoundingBox(container);
        }
        private void UpdateBoundingBox(Canvas container)
        {
            var bbox = container.Children.OfType<Rectangle>().FirstOrDefault(r => r.Tag?.ToString() == "BoundingBox");
            if (bbox == null || bbox.Visibility != Visibility.Visible) return;

            if (container.Tag is ShapeData data)
            {
                double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;

                if (data.BasePoints != null)
                {
                    foreach (var bp in data.BasePoints)
                    {
                        double px = bp.X * data.SizeX + 500;
                        double py = bp.Y * data.SizeY + 500;
                        if (px < minX) minX = px; if (px > maxX) maxX = px;
                        if (py < minY) minY = py; if (py > maxY) maxY = py;
                    }
                }
                else
                {
                    minX = 500 - data.SizeX; maxX = 500 + data.SizeX;
                    minY = 500 - data.SizeY; maxY = 500 + data.SizeY;
                }

                Canvas.SetLeft(bbox, minX);
                Canvas.SetTop(bbox, minY);
                bbox.Width = Math.Max(0, maxX - minX);
                bbox.Height = Math.Max(0, maxY - minY);

                // --- НОВОЕ: Размещение маркеров масштабирования ---
                var resizeHandles = container.Children.OfType<Rectangle>().Where(r => r.Tag?.ToString().StartsWith("Resize_") == true);
                foreach (var rh in resizeHandles)
                {
                    double midX = (minX + maxX) / 2; double midY = (minY + maxY) / 2;
                    string tag = rh.Tag.ToString();
                    double x = 0, y = 0;

                    if (tag.Contains("W")) x = minX; else if (tag.Contains("E")) x = maxX; else x = midX;
                    if (tag.Contains("N")) y = minY; else if (tag.Contains("S")) y = maxY; else y = midY;

                    Canvas.SetLeft(rh, x - rh.Width / 2); Canvas.SetTop(rh, y - rh.Height / 2);
                }

                // --- Размещение маркеров вершин (С ОТСТУПОМ НАРУЖУ) ---
                var vertexHandles = container.Children.OfType<Ellipse>().Where(e => e.Tag?.ToString().StartsWith("Vertex_") == true);
                foreach (var vh in vertexHandles)
                {
                    int idx = int.Parse(vh.Tag.ToString().Split('_')[1]);
                    if (data.BasePoints != null && idx < data.BasePoints.Length)
                    {
                        double px = data.BasePoints[idx].X * data.SizeX + 500;
                        double py = data.BasePoints[idx].Y * data.SizeY + 500;

                        // Сдвигаем маркер на 15 пикселей от центра (500, 500)
                        double dx = px - 500;
                        double dy = py - 500;
                        double len = Math.Sqrt(dx * dx + dy * dy);
                        double offset = 15;

                        if (len > 0.001)
                        {
                            px += (dx / len) * offset;
                            py += (dy / len) * offset;
                        }

                        Canvas.SetLeft(vh, px - vh.Width / 2);
                        Canvas.SetTop(vh, py - vh.Height / 2);
                    }
                }
            }
        }
        private Point GetIntersection(Point p1, Vector dir1, Point p2, Vector dir2, Point fallback)
        {
            double cross = dir1.X * dir2.Y - dir1.Y * dir2.X;
            if (Math.Abs(cross) < 0.0001) return fallback; // Линии параллельны
            Vector diff = p2 - p1;
            double u = (diff.X * dir2.Y - diff.Y * dir2.X) / cross;
            return p1 + dir1 * u;
        }
        private Point GetSnappedPoint(Point start, Point current)
        {
            Vector v = current - start;
            double angle = Math.Atan2(v.Y, v.X);
            double snapAngle = Math.Round(angle / (Math.PI / 12)) * (Math.PI / 12); // Кратность 15 градусам (Pi / 12)
            double len = v.Length;
            return new Point(start.X + len * Math.Cos(snapAngle), start.Y + len * Math.Sin(snapAngle));
        }
        private void CreateHandles(Canvas container)
        {
            // Очищаем старые маркеры, если есть
            var existing = container.Children.OfType<FrameworkElement>().Where(x => x.Tag?.ToString().StartsWith("Vertex_") == true || x.Tag?.ToString().StartsWith("Resize_") == true).ToList();
            foreach (var h in existing) container.Children.Remove(h);

            ShapeData data = container.Tag as ShapeData;
            if (data == null) return;

            // 1. Маркеры вершин
            if (data.BasePoints != null)
            {
                for (int i = 0; i < data.BasePoints.Length; i++)
                {
                    Ellipse vh = new Ellipse { Width = 10, Height = 10, Fill = Brushes.Yellow, Stroke = Brushes.Black, StrokeThickness = 1, Tag = "Vertex_" + i, Cursor = Cursors.Hand };
                    Panel.SetZIndex(vh, 999);
                    container.Children.Add(vh);
                }
            }

            // 2. Маркеры масштабирования (углы и стороны)
            string[] tags = { "Resize_NW", "Resize_N", "Resize_NE", "Resize_W", "Resize_E", "Resize_SW", "Resize_S", "Resize_SE" };
            foreach (var tag in tags)
            {
                Cursor cur = Cursors.Arrow;
                if (tag == "Resize_NW" || tag == "Resize_SE") cur = Cursors.SizeNWSE;
                if (tag == "Resize_NE" || tag == "Resize_SW") cur = Cursors.SizeNESW;
                if (tag == "Resize_N" || tag == "Resize_S") cur = Cursors.SizeNS;
                if (tag == "Resize_W" || tag == "Resize_E") cur = Cursors.SizeWE;

                Rectangle rh = new Rectangle { Width = 8, Height = 8, Fill = Brushes.White, Stroke = Brushes.Black, StrokeThickness = 1, Tag = tag, Cursor = cur };
                Panel.SetZIndex(rh, 1000);
                container.Children.Add(rh);
            }
        }
        // Создание и обновление единой рамки для мульти-выделения и групп
        private void UpdateGlobalSelectionUI()
        {
            if (_globalBoundingBox == null)
            {
                _globalBoundingBox = new Rectangle { StrokeThickness = 2, StrokeDashArray = new DoubleCollection { 4, 4 }, IsHitTestVisible = false };
                Panel.SetZIndex(_globalBoundingBox, 9998);
                MainCanvas.Children.Add(_globalBoundingBox);
            }
            if (_globalAnchor == null)
            {
                _globalAnchor = new Ellipse { Width = 14, Height = 14, Fill = Brushes.Orange, Stroke = Brushes.White, StrokeThickness = 2, Tag = "GlobalAnchor", Cursor = Cursors.Cross };
                Panel.SetZIndex(_globalAnchor, 9999);
                MainCanvas.Children.Add(_globalAnchor);
            }

            if (_selectedElements.Count <= 1)
            {
                _globalBoundingBox.Visibility = Visibility.Collapsed;
                _globalAnchor.Visibility = Visibility.Collapsed;
                return;
            }

            // --- ФИКС: Черный цвет для мульти-выбора, синий для групп ---
            string firstGroupId = (_selectedElements[0].Tag as ShapeData)?.GroupId;
            bool isFormalGroup = !string.IsNullOrEmpty(firstGroupId) && _selectedElements.All(e => (e.Tag as ShapeData)?.GroupId == firstGroupId);
            _globalBoundingBox.Stroke = isFormalGroup ? Brushes.DodgerBlue : Brushes.Black;

            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;

            foreach (var container in _selectedElements)
            {
                var bbox = container.Children.OfType<Rectangle>().FirstOrDefault(r => r.Tag?.ToString() == "BoundingBox");
                if (bbox != null)
                {
                    double left = Canvas.GetLeft(bbox) + Canvas.GetLeft(container);
                    double top = Canvas.GetTop(bbox) + Canvas.GetTop(container);
                    if (left < minX) minX = left;
                    if (left + bbox.Width > maxX) maxX = left + bbox.Width;
                    if (top < minY) minY = top;
                    if (top + bbox.Height > maxY) maxY = top + bbox.Height;
                }
                if (bbox != null) bbox.Visibility = Visibility.Collapsed;
                var handles = container.Children.OfType<FrameworkElement>().Where(x => x.Tag?.ToString().StartsWith("Vertex_") == true || x.Tag?.ToString().StartsWith("Resize_") == true || x.Tag?.ToString() == "Anchor").ToList();
                foreach (var h in handles) h.Visibility = Visibility.Collapsed;
            }

            Canvas.SetLeft(_globalBoundingBox, minX);
            Canvas.SetTop(_globalBoundingBox, minY);
            _globalBoundingBox.Width = maxX - minX;
            _globalBoundingBox.Height = maxY - minY;
            _globalBoundingBox.Visibility = Visibility.Visible;

            Point anchorPt = new Point((minX + maxX) / 2, (minY + maxY) / 2);
            if (isFormalGroup)
            {
                var group = _shapeGroups.FirstOrDefault(g => g.Id == firstGroupId);
                if (group != null) anchorPt = group.GroupAnchor;
            }

            Canvas.SetLeft(_globalAnchor, anchorPt.X - 7);
            Canvas.SetTop(_globalAnchor, anchorPt.Y - 7);
            _globalAnchor.Visibility = Visibility.Visible;
        }

    }
}
