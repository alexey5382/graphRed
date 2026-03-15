using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows;
using System.Windows.Input;
using System.Windows.Shapes;

namespace Lab1
{
    public partial class MainWindow
    {
        private void AddShape_Click(object sender, RoutedEventArgs e)
        {
            string type = (sender as Button).Tag.ToString();
            if (type == "Custom") { StartDrawingMode(); return; }

            _pendingShapeType = type;
            _pendingSidesCount = type switch { "Tri" => 3, "Pent" => 5, "Circ" => 1, _ => 4 };

            CreationTitle.Text = $"СОЗДАНИЕ: {type.ToUpper()}";

            // Инициализируем комбобокс заливки
            if (CreationFillColorCombo.Items.Count == 0)
            {
                CreationFillColorCombo.Items.Add(new TextBlock { Text = "Прозрачный", Foreground = Brushes.White, FontSize = 11 });
                foreach (var color in _availableColors)
                    CreationFillColorCombo.Items.Add(new Rectangle { Fill = color, Width = 30, Height = 12 });
            }
            // По умолчанию выбираем первый цвет (или оставляем то, что было выбрано ранее)
            if (CreationFillColorCombo.SelectedIndex < 0) CreationFillColorCombo.SelectedIndex = 1;

            GenerateCreationSidesMenu(_pendingSidesCount);

            CreationModal.Visibility = Visibility.Visible;
        }
        private void GenerateCreationSidesMenu(int sidesCount)
        {
            CreationSidesPanel.Children.Clear();
            _creationColorCombos.Clear();
            _creationThickBoxes.Clear();
            _creationThickSliders.Clear();

            Brush darkBg = new SolidColorBrush(Color.FromRgb(37, 37, 38));
            Brush bdr = new SolidColorBrush(Color.FromRgb(85, 85, 85));

            for (int i = 0; i < sidesCount; i++)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
                row.Children.Add(new TextBlock { Text = $"Стор. {i + 1}:", Foreground = Brushes.White, Width = 50, VerticalAlignment = VerticalAlignment.Center });

                var colorCombo = new ComboBox { Width = 50, Background = darkBg, BorderBrush = bdr, Foreground = Brushes.White, Margin = new Thickness(0, 0, 10, 0) };
                foreach (var color in _availableColors) colorCombo.Items.Add(new Rectangle { Fill = color, Width = 30, Height = 12 });
                colorCombo.SelectedIndex = 0;
                _creationColorCombos.Add(colorCombo);
                row.Children.Add(colorCombo);

                row.Children.Add(new TextBlock { Text = "Толщ:", Foreground = Brushes.White, Margin = new Thickness(0, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center });

                var thickSlider = new Slider { Width = 80, Minimum = 1, Maximum = 50, Value = 2, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 5, 0) };
                _creationThickSliders.Add(thickSlider);

                var minusBtn = new Button { Content = "-", Width = 18, Height = 18, Background = darkBg, Foreground = Brushes.White, BorderThickness = new Thickness(0), Margin = new Thickness(0, 0, 2, 0) };
                var thickBox = new TextBox { Text = "2", Width = 25, TextAlignment = TextAlignment.Center, Background = darkBg, Foreground = Brushes.White, BorderBrush = bdr };
                _creationThickBoxes.Add(thickBox);
                var plusBtn = new Button { Content = "+", Width = 18, Height = 18, Background = darkBg, Foreground = Brushes.White, BorderThickness = new Thickness(0), Margin = new Thickness(2, 0, 0, 0) };

                thickSlider.ValueChanged += (s, e) => { thickBox.Text = Math.Round(e.NewValue).ToString(); };
                minusBtn.Click += (s, e) => { if (double.TryParse(thickBox.Text, out double v) && v > 1) thickBox.Text = (v - 1).ToString(); };
                plusBtn.Click += (s, e) => { if (double.TryParse(thickBox.Text, out double v) && v < 50) thickBox.Text = (v + 1).ToString(); };
                thickBox.TextChanged += (s, e) => {
                    if (double.TryParse(thickBox.Text, out double v))
                    {
                        if (v < 1) v = 1; if (v > 50) v = 50;
                        thickSlider.Value = v;
                    }
                };

                row.Children.Add(thickSlider);
                row.Children.Add(minusBtn);
                row.Children.Add(thickBox);
                row.Children.Add(plusBtn);

                CreationSidesPanel.Children.Add(row);
            }
        }
        private (List<Brush>, List<double>) GetCreationProperties()
        {
            var colors = new List<Brush>();
            var thicks = new List<double>();
            for (int i = 0; i < _pendingSidesCount; i++)
            {
                colors.Add(_availableColors[_creationColorCombos[i].SelectedIndex]);
                thicks.Add(double.TryParse(_creationThickBoxes[i].Text, out double t) && t > 0 ? t : 2.0);
            }
            return (colors, thicks);
        }
        private Brush GetCreationFillColor()
        {
            if (CreationFillColorCombo.SelectedIndex > 0)
            {
                var solidColor = (SolidColorBrush)_availableColors[CreationFillColorCombo.SelectedIndex - 1];
                // Делаем заливку полупрозрачной (alpha 100)
                return new SolidColorBrush(Color.FromArgb(100, solidColor.Color.R, solidColor.Color.G, solidColor.Color.B));
            }
            return Brushes.Transparent;
        }
        private void Validation_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox box && double.TryParse(box.Text, out double val))
            {
                if (box.Name.Contains("Width") || box.Name.Contains("Height"))
                {
                    if (val > 1500) { box.Text = "1500"; box.CaretIndex = box.Text.Length; }
                }
                if (box.Name.Contains("Center"))
                {
                    if (val > 2500) { box.Text = "2500"; box.CaretIndex = box.Text.Length; }
                    if (val < -1000) { box.Text = "-1000"; box.CaretIndex = box.Text.Length; }
                }
            }
        }
        private void RandomizeCreation_Click(object sender, RoutedEventArgs e)
        {
            Random rnd = new Random();

            // Рандомизация сторон
            for (int i = 0; i < _pendingSidesCount; i++)
            {
                if (i < _creationColorCombos.Count) _creationColorCombos[i].SelectedIndex = rnd.Next(_availableColors.Count);
                if (i < _creationThickSliders.Count) _creationThickSliders[i].Value = rnd.Next(1, 15);
            }

            // Рандомизация заливки
            if (CreationFillColorCombo.Items.Count > 0)
            {
                CreationFillColorCombo.SelectedIndex = rnd.Next(CreationFillColorCombo.Items.Count);
            }

            // Рандомизация ручных размеров
            ManualWidthBox.Text = rnd.Next(50, 400).ToString();
            ManualHeightBox.Text = rnd.Next(50, 400).ToString();
            ManualCenterXBox.Text = rnd.Next(100, 900).ToString();
            ManualCenterYBox.Text = rnd.Next(100, 700).ToString();
        }
        private void CancelCreation_Click(object sender, RoutedEventArgs e)
        {
            CreationModal.Visibility = Visibility.Collapsed;
        }
        private void ManualCreation_Click(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(ManualWidthBox.Text, out double w) && double.TryParse(ManualHeightBox.Text, out double h) &&
                double.TryParse(ManualCenterXBox.Text, out double cx) && double.TryParse(ManualCenterYBox.Text, out double cy))
            {
                if (w <= 0 || h <= 0) return;

                var (colors, thicks) = GetCreationProperties();
                Brush defaultFill = GetCreationFillColor();
                // Переводим центр из абсолютных координат холста в локальные (минус 500)
                Point centerPoint = new Point(cx - 500, cy - 500);

                MyShape shape = _pendingShapeType switch
                {
                    "Rect" => new MyRectangle(centerPoint, colors, thicks, defaultFill),
                    "Tri" => new MyTriangle(centerPoint, colors, thicks, defaultFill),
                    "Trap" => new MyTrapezoid(centerPoint, colors, thicks, defaultFill),
                    "Pent" => new MyPentagon(centerPoint, colors, thicks, defaultFill),
                    "Circ" => new MyCircle(centerPoint, colors, thicks, defaultFill),
                    _ => null
                };

                if (shape != null)
                {
                    // Делим ширину и высоту пополам, так как SizeX и SizeY - это "радиусы" фигуры от центра
                    var visual = (Canvas)shape.CreateVisual(w / 2, h / 2);
                    Canvas.SetLeft(visual, centerPoint.X);
                    Canvas.SetTop(visual, centerPoint.Y);

                    UpdatePolygonGeometry(visual);
                    MainCanvas.Children.Add(visual);
                    CreationModal.Visibility = Visibility.Collapsed;
                    SelectShape(visual);
                }
            }
        }
        private void StartInteractiveCreation_Click(object sender, RoutedEventArgs e)
        {
            CreationModal.Visibility = Visibility.Collapsed;
            _isInteractiveCreation = true;
            _interactiveFromCenter = DrawModeCenter.IsChecked == true;
            MainCanvas.Cursor = Cursors.Cross;
            DeselectAll();
        }
        private void StartDrawingMode()
        {
            DeselectAll();
            CancelDrawing(); // Очищаем всё перед новым рисованием

            _isDrawingMode = true;
            _drawingPoints.Clear();
            MainCanvas.Cursor = Cursors.Cross;

            // Обязательно добавляем Tag="DrawingTemp"
            _previewLine = new Line { Stroke = Brushes.Gray, StrokeThickness = 2, StrokeDashArray = new DoubleCollection { 4, 4 }, Visibility = Visibility.Collapsed, Tag = "DrawingTemp" };
            MainCanvas.Children.Add(_previewLine);

            _drawingInfoText = new TextBlock { Foreground = Brushes.White, Background = new SolidColorBrush(Color.FromArgb(200, 30, 30, 30)), Padding = new Thickness(4), Visibility = Visibility.Collapsed, Tag = "DrawingTemp" };
            Panel.SetZIndex(_drawingInfoText, 10000);
            MainCanvas.Children.Add(_drawingInfoText);

            // ВОТ ОНО: Забираем фокус клавиатуры у кнопки и отдаем окну, чтобы работал Enter!
            this.Focus();
        }
        private void CancelDrawing()
        {
            _isDrawingMode = false;
            MainCanvas.Cursor = Cursors.Arrow;
            // Теперь мы удаляем абсолютно все временные объекты по тегу
            var tempItems = MainCanvas.Children.OfType<FrameworkElement>().Where(x => x.Tag?.ToString() == "DrawingTemp").ToList();
            foreach (var item in tempItems) MainCanvas.Children.Remove(item);
        }
        private void FinishDrawing(bool isClosed)
        {
            // Убираем случайные двойные клики (точки слишком близко)
            var cleanPoints = new List<Point>();
            foreach (var p in _drawingPoints)
            {
                if (cleanPoints.Count == 0 || (p - cleanPoints.Last()).Length > 2)
                    cleanPoints.Add(p);
            }

            if (cleanPoints.Count < 2) { CancelDrawing(); return; }

            double minX = cleanPoints.Min(p => p.X); double maxX = cleanPoints.Max(p => p.X);
            double minY = cleanPoints.Min(p => p.Y); double maxY = cleanPoints.Max(p => p.Y);

            double width = maxX - minX; double height = maxY - minY;
            double size = Math.Max(width, height) / 2;
            if (size < 1) size = 50;

            double centerX = minX + width / 2;
            double centerY = minY + height / 2;

            Point[] basePoints = new Point[cleanPoints.Count];
            for (int i = 0; i < cleanPoints.Count; i++)
            {
                basePoints[i] = new Point((cleanPoints[i].X - centerX) / size, (cleanPoints[i].Y - centerY) / size);
            }

            int segmentsCount = isClosed ? basePoints.Length : basePoints.Length - 1;

            var defaultColors = Enumerable.Repeat<Brush>(Brushes.Black, segmentsCount).ToList();
            var defaultThicks = Enumerable.Repeat(4.0, segmentsCount).ToList();

            CancelDrawing();

            MyShape shape;
            if (isClosed)
            {
                Brush defaultFill = new SolidColorBrush(Color.FromArgb(100, 200, 200, 200));
                shape = new MyCustomPolygon(new Point(0, 0), basePoints, true, defaultColors, defaultThicks, defaultFill);
            }
            else
            {
                shape = new MyPolyline(new Point(0, 0), basePoints, defaultColors, defaultThicks);
            }

            var visual = (Canvas)shape.CreateVisual(size, size);

            Canvas.SetLeft(visual, centerX - 500);
            Canvas.SetTop(visual, centerY - 500);

            UpdatePolygonGeometry(visual);
            MainCanvas.Children.Add(visual);
            SelectShape(visual);
        }
    }
}
