using Lab1;
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
    public partial class MainWindow : Window
    {
        
        private UIElement _draggedElement;
        private Canvas _selectedElement; // Теперь всегда работаем с контейнером Canvas
        private Point _clickPosition;
        private bool _isUpdating = false;
        private Ellipse _draggedAnchor;

        private readonly List<Brush> _availableColors = new List<Brush> {
            Brushes.Black, Brushes.Red, Brushes.Orange, Brushes.Yellow, Brushes.Green, Brushes.Blue, Brushes.Purple, Brushes.White
        };
        public MainWindow()
        {
            InitializeComponent();
            // Заполняем комбобокс заливки
            foreach (var color in _availableColors)
                FillColorCombo.Items.Add(new Rectangle { Fill = color, Width = 40, Height = 14 });
        }
        private void WindowLoaded() { 
        }
        private void UpdateCoordinatesUI()
        {
            if (_selectedElement != null && _selectedElement.Tag is ShapeData data)
            {
                double canvasLeft = Canvas.GetLeft(_selectedElement);
                double canvasTop = Canvas.GetTop(_selectedElement);

                // Абсолютная позиция точки привязки на рабочем поле
                double absX = canvasLeft + data.LocalAnchor.X;
                double absY = canvasTop + data.LocalAnchor.Y;

                CoordXText.Text = $"X: {Math.Round(absX)}";
                CoordYText.Text = $"Y: {Math.Round(absY)}";
            }
        }
        private void AddShape_Click(object sender, RoutedEventArgs e)
        {
            string type = (sender as Button).Tag.ToString();

            // Создаем по центру экрана
            double centerX = MainCanvas.ActualWidth / 2 > 0 ? MainCanvas.ActualWidth / 2 : 400;
            double centerY = MainCanvas.ActualHeight / 2 > 0 ? MainCanvas.ActualHeight / 2 : 300;
            Point centerPoint = new Point(centerX - 500, centerY - 500);

            int sides = type switch { "Tri" => 3, "Pent" => 5, "Circ" => 1, _ => 4 };
            var defaultColors = Enumerable.Repeat<Brush>(Brushes.Black, sides).ToList();
            var defaultThicks = Enumerable.Repeat(2.0, sides).ToList();
            double initialSize = 100;
            Brush defaultFill = new SolidColorBrush(Color.FromArgb(100, 200, 200, 200));

            MyShape shape = type switch
            {
                "Rect" => new MyRectangle(centerPoint, defaultColors, defaultThicks, defaultFill),
                "Tri" => new MyTriangle(centerPoint, defaultColors, defaultThicks, defaultFill),
                "Trap" => new MyTrapezoid(centerPoint, defaultColors, defaultThicks, defaultFill),
                "Pent" => new MyPentagon(centerPoint, defaultColors, defaultThicks, defaultFill),
                "Circ" => new MyCircle(centerPoint, defaultColors, defaultThicks, defaultFill),
                _ => null
            };

            if (shape != null)
            {
                var visual = (Canvas)shape.CreateVisual(initialSize);
                UpdatePolygonGeometry(visual); // Применяем математику стыков при создании
                Canvas.SetLeft(visual, centerPoint.X);
                Canvas.SetTop(visual, centerPoint.Y);

                MainCanvas.Children.Add(visual);
                SelectShape(visual); // Сразу выбираем новую фигуру
            }
        }
        private void SelectShape(Canvas shapeCanvas)
        {
            // Снимаем выделение с предыдущей фигуры
            DeselectAll();

            _selectedElement = shapeCanvas;
            _selectedElement.Opacity = 1;
            PropertiesPanel.Visibility = Visibility.Visible;
            var anchor = _selectedElement.Children.OfType<Ellipse>().FirstOrDefault(a => a.Tag?.ToString() == "Anchor");
            if (anchor != null) anchor.Visibility = Visibility.Visible;

            // --- ЛОГИКА ПУНКТИРНОЙ ОБВОДКИ ---
            var bbox = _selectedElement.Children.OfType<Rectangle>().FirstOrDefault(r => r.Tag?.ToString() == "BoundingBox");
            if (bbox == null)
            {
                bbox = new Rectangle
                {
                    Tag = "BoundingBox",
                    Stroke = Brushes.Black,
                    StrokeThickness = 1.5,
                    StrokeDashArray = new DoubleCollection { 4, 6 }, // Длина штриха и пробела
                    Fill = Brushes.Transparent,
                    IsHitTestVisible = false // Чтобы клики проходили сквозь рамку
                };
                Panel.SetZIndex(bbox, 100); // Поверх самой фигуры
                _selectedElement.Children.Add(bbox);
            }
            bbox.Visibility = Visibility.Visible;
            UpdateBoundingBox(_selectedElement);
            // ---------------------------------

            UpdateCoordinatesUI();

            if (_selectedElement.Tag is ShapeData data)
            {
                _isUpdating = true;
                Brush currentFill = null;
                var hitArea = shapeCanvas.Children.OfType<Polygon>().FirstOrDefault();
                if (hitArea != null) currentFill = hitArea.Fill;
                var ellipse = shapeCanvas.Children.OfType<Ellipse>().FirstOrDefault();
                if (ellipse != null) currentFill = ellipse.Fill;

                if (currentFill != null)
                    FillColorCombo.SelectedIndex = _availableColors.FindIndex(b => b.ToString() == currentFill.ToString());

                GlobalSizeSlider.Value = data.CurrentSize;
                GlobalSizeBox.Text = Math.Round(data.CurrentSize).ToString();

                _isUpdating = false;
            }

            int sides = _selectedElement.Children.OfType<Polygon>().Count(p => p.Tag?.ToString() == "Stroke");
            if (sides == 0) sides = 1;
            GenerateSideMenu(_selectedElement, sides);
        }

        private void DeselectAll()
        {
            var anchor = _selectedElement?.Children.OfType<Ellipse>().FirstOrDefault(a => a.Tag?.ToString() == "Anchor");
            if (anchor != null) anchor.Visibility = Visibility.Collapsed;

            if (_selectedElement != null)
            {
                _selectedElement.Opacity = 1.0;

                // Прячем рамку при снятии выделения
                var bbox = _selectedElement.Children.OfType<Rectangle>().FirstOrDefault(r => r.Tag?.ToString() == "BoundingBox");
                if (bbox != null) bbox.Visibility = Visibility.Collapsed;
            }
            _selectedElement = null;
            PropertiesPanel.Visibility = Visibility.Collapsed;
        }
        private void DeleteShape_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedElement != null)
            {
                MainCanvas.Children.Remove(_selectedElement);
                _selectedElement = null;
                PropertiesPanel.Visibility = Visibility.Collapsed;
            }
        }

        // --- Управление размером фигуры ---
        private void GlobalSize_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Проверяем флаг, чтобы не создать бесконечный цикл обновлений
            if (_isUpdating || _selectedElement == null) return;

            _isUpdating = true;

            // 1. Обновляем текст в поле (округляем для красоты)
            GlobalSizeBox.Text = Math.Round(e.NewValue).ToString();

            // 2. Применяем масштаб к фигуре
            ApplyScale(e.NewValue);

            // 3. Сохраняем новое значение в данные фигуры
            if (_selectedElement.Tag is ShapeData d)
            {
                d.CurrentSize = e.NewValue;
            }

            _isUpdating = false;
        }

        private void GlobalSizeBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdating || _selectedElement == null) return;

            if (double.TryParse(GlobalSizeBox.Text, out double val))
            {
                // Проверка интервала (например, от 10 до 500)
                if (val >= 10 && val <= 500)
                {
                    _isUpdating = true;
                    GlobalSizeSlider.Value = val;
                    ApplyScale(val);

                    // Сохраняем новый размер в данные фигуры
                    if (_selectedElement.Tag is ShapeData d) d.CurrentSize = val;
                    _isUpdating = false;
                }
            }
        }

        private void ApplyScale(double sizeValue)
        {
            if (_selectedElement == null) return;
            if (_selectedElement.Tag is ShapeData data)
            {
                data.CurrentSize = sizeValue;
                if (data.OriginalBasePoints != null)
                {
                    // Вычисляем коэффициент изменения (насколько изменился масштаб)
                    double ratio = sizeValue / data.CurrentSize;

                    // Пропорционально обновляем длины сторон, чтобы они росли вместе с масштабом
                    if (data.SideLengths != null)
                    {
                        for (int i = 0; i < data.SideLengths.Length; i++)
                        {
                            data.SideLengths[i] *= ratio;
                        }
                    }

                    // Сохраняем новый масштаб
                    data.CurrentSize = sizeValue;

                    // ВАЖНО: мы убрали RecalculateBasePoints(data);
                    UpdatePolygonGeometry(_selectedElement);
                    UpdateBoundingBox(_selectedElement);
                }
                else
                {
                    var ellipse = _selectedElement.Children.OfType<Ellipse>().FirstOrDefault();
                    if (ellipse != null)
                    {
                        double t = ellipse.StrokeThickness;
                        ellipse.Width = Math.Max(0, sizeValue * 2 - t);
                        ellipse.Height = Math.Max(0, sizeValue * 2 - t);
                        Canvas.SetLeft(ellipse, 500 - sizeValue + t / 2);
                        Canvas.SetTop(ellipse, 500 - sizeValue + t / 2);
                        UpdateBoundingBox(_selectedElement);
                    }
                }
            }
        }
        private void RecalculateBasePoints(ShapeData data)
        {
            if (data.OriginalBasePoints == null || data.SideLengths == null) return;
            int n = data.OriginalBasePoints.Length;

            for (int i = 0; i < n; i++) data.BasePoints[i] = data.OriginalBasePoints[i];

            for (int i = 0; i < n; i++)
            {
                double targetLen = data.SideLengths[i];
                if (targetLen <= 0) continue;

                int next = (i + 1) % n;
                Point p1 = data.OriginalBasePoints[i];
                Point p2 = data.OriginalBasePoints[next];

                double dx = p2.X - p1.X;
                double dy = p2.Y - p1.Y;
                double origLenBase = Math.Sqrt(dx * dx + dy * dy);
                double currentLenPixels = origLenBase * data.CurrentSize;

                if (currentLenPixels < 0.001) continue;

                // Разница в пикселях между тем что хотим, и тем что есть
                double deltaPixels = targetLen - currentLenPixels;
                double deltaBase = deltaPixels / data.CurrentSize;

                double offsetX = (dx / origLenBase) * (deltaBase / 2.0);
                double offsetY = (dy / origLenBase) * (deltaBase / 2.0);

                // Раздвигаем точки вдоль прямой
                data.BasePoints[i] = new Point(data.BasePoints[i].X - offsetX, data.BasePoints[i].Y - offsetY);
                data.BasePoints[next] = new Point(data.BasePoints[next].X + offsetX, data.BasePoints[next].Y + offsetY);
            }
        }        // --- Динамическое меню сторон ---
        private void GenerateSideMenu(Canvas container, int sidesCount)
        {
            // Очищаем старые элементы в правой панели
            SidePropertiesList.Children.Clear();

            // Определяем кисти для темной темы
            Brush lightText = new SolidColorBrush(Color.FromRgb(200, 200, 200));
            Brush darkInputBg = new SolidColorBrush(Color.FromRgb(37, 37, 38));
            Brush inputBorder = new SolidColorBrush(Color.FromRgb(85, 85, 85));

            // Получаем доступ к данным фигуры и эллипсу
            ShapeData shapeData = container.Tag as ShapeData;
            var ellipse = container.Children.OfType<Ellipse>().FirstOrDefault();

            for (int i = 0; i < sidesCount; i++)
            {
                int sideIndex = i;
                var group = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };

                // --- СТРОКА 1: Заголовок, Цвет, Толщина (с кнопками + и -) ---
                var row1 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
                row1.Children.Add(new TextBlock { Text = $"СТОР. {i + 1}", FontWeight = FontWeights.Bold, Foreground = Brushes.White, Width = 55, VerticalAlignment = VerticalAlignment.Center });

                var colorCombo = new ComboBox { Background = darkInputBg, Foreground = Brushes.White, BorderBrush = inputBorder, Width = 50, Margin = new Thickness(0, 0, 10, 0) };
                foreach (var color in _availableColors) colorCombo.Items.Add(new Rectangle { Fill = color, Width = 30, Height = 12 });

                Brush currentBrush = shapeData?.CurrentColors != null && i < shapeData.CurrentColors.Length ? shapeData.CurrentColors[i] : (ellipse?.Stroke ?? Brushes.Black);
                colorCombo.SelectedIndex = _availableColors.FindIndex(b => b.ToString() == currentBrush.ToString());
                colorCombo.SelectionChanged += (s, e) => { if (!_isUpdating) UpdateSide(container, sideIndex, _availableColors[colorCombo.SelectedIndex], -1); };
                row1.Children.Add(colorCombo);

                row1.Children.Add(new TextBlock { Text = "Толщ:", Foreground = lightText, FontSize = 10, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 5, 0) });

                double currentThick = shapeData?.CurrentThicknesses != null && i < shapeData.CurrentThicknesses.Length ? shapeData.CurrentThicknesses[i] : (ellipse?.StrokeThickness / 2 ?? 2);
                var thickBox = new TextBox { Text = Math.Round(currentThick).ToString(), Width = 30, Background = darkInputBg, Foreground = Brushes.White, BorderBrush = inputBorder, TextAlignment = TextAlignment.Center };

                // Кнопка уменьшения толщины
                var minusBtn = new Button { Content = "-", Width = 20, Height = 20, Background = darkInputBg, Foreground = Brushes.White, BorderBrush = inputBorder, Margin = new Thickness(0, 0, 2, 0) };
                minusBtn.Click += (s, e) => {
                    if (double.TryParse(thickBox.Text, out double v) && v > 1) thickBox.Text = (v - 1).ToString();
                };

                // Кнопка увеличения толщины
                var plusBtn = new Button { Content = "+", Width = 20, Height = 20, Background = darkInputBg, Foreground = Brushes.White, BorderBrush = inputBorder, Margin = new Thickness(2, 0, 0, 0) };
                plusBtn.Click += (s, e) => {
                    if (double.TryParse(thickBox.Text, out double v) && v < 50) thickBox.Text = (v + 1).ToString();
                };

                thickBox.TextChanged += (s, e) => {
                    if (!_isUpdating && double.TryParse(thickBox.Text, out double v) && v >= 1 && v <= 50)
                        UpdateSide(container, sideIndex, null, v);
                };

                row1.Children.Add(minusBtn);
                row1.Children.Add(thickBox);
                row1.Children.Add(plusBtn);
                group.Children.Add(row1);

                // --- СТРОКА 2: Координаты X и Y ---
                if (shapeData != null && shapeData.BasePoints != null)
                {
                    var row2 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };

                    double px = shapeData.BasePoints[i].X * shapeData.CurrentSize + 500;
                    double py = shapeData.BasePoints[i].Y * shapeData.CurrentSize + 500;
                    double relX = px - shapeData.LocalAnchor.X;
                    double relY = shapeData.LocalAnchor.Y - py;

                    row2.Children.Add(new TextBlock { Text = "X:", Foreground = lightText, FontSize = 10, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 3, 0) });
                    var xBox = new TextBox { Width = 40, Text = Math.Round(relX).ToString(), Background = darkInputBg, Foreground = Brushes.White, BorderBrush = inputBorder, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
                    row2.Children.Add(xBox);

                    row2.Children.Add(new TextBlock { Text = "Y:", Foreground = lightText, FontSize = 10, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 3, 0) });
                    var yBox = new TextBox { Width = 40, Text = Math.Round(relY).ToString(), Background = darkInputBg, Foreground = Brushes.White, BorderBrush = inputBorder, TextAlignment = TextAlignment.Center };
                    row2.Children.Add(yBox);

                    xBox.TextChanged += (s, e) => {
                        if (!_isUpdating && double.TryParse(xBox.Text, out double vx))
                        {
                            _isUpdating = true;
                            double newPx = vx + shapeData.LocalAnchor.X;
                            double normX = (newPx - 500) / shapeData.CurrentSize;
                            shapeData.BasePoints[sideIndex] = new Point(normX, shapeData.BasePoints[sideIndex].Y);
                            if (shapeData.OriginalBasePoints != null) shapeData.OriginalBasePoints[sideIndex] = new Point(normX, shapeData.OriginalBasePoints[sideIndex].Y);
                            UpdatePolygonGeometry(container);
                            _isUpdating = false;
                        }
                    };

                    yBox.TextChanged += (s, e) => {
                        if (!_isUpdating && double.TryParse(yBox.Text, out double vy))
                        {
                            _isUpdating = true;
                            double newPy = shapeData.LocalAnchor.Y - vy;
                            double normY = (newPy - 500) / shapeData.CurrentSize;
                            shapeData.BasePoints[sideIndex] = new Point(shapeData.BasePoints[sideIndex].X, normY);
                            if (shapeData.OriginalBasePoints != null) shapeData.OriginalBasePoints[sideIndex] = new Point(shapeData.OriginalBasePoints[sideIndex].X, normY);
                            UpdatePolygonGeometry(container);
                            _isUpdating = false;
                        }
                    };

                    group.Children.Add(row2);

                    // --- СТРОКА 3: Длина (Слайдер и поле с ограничением) ---
                    if (shapeData.SideLengths != null && i < shapeData.SideLengths.Length)
                    {
                        var row3 = new StackPanel { Orientation = Orientation.Horizontal };
                        row3.Children.Add(new TextBlock { Text = "Дл:", Foreground = lightText, FontSize = 10, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 3, 0) });

                        double maxLen = 1000; // Максимально допустимая длина (можете изменить под ваши нужды)
                        double currentLen = shapeData.SideLengths[i];

                        var lenSlider = new Slider { Width = 80, Minimum = 10, Maximum = maxLen, Value = currentLen, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
                        var lenBox = new TextBox { Width = 45, Text = Math.Round(currentLen).ToString(), Background = darkInputBg, Foreground = Brushes.White, BorderBrush = inputBorder, TextAlignment = TextAlignment.Center };

                        lenSlider.ValueChanged += (s, e) => {
                            if (!_isUpdating)
                            {
                                lenBox.Text = Math.Round(e.NewValue).ToString();
                            }
                        };

                        lenBox.TextChanged += (s, e) => {
                            if (!_isUpdating && double.TryParse(lenBox.Text, out double v))
                            {
                                // Проверка и обрезка до максимального/минимального значения
                                if (v > maxLen)
                                {
                                    v = maxLen;
                                    _isUpdating = true;
                                    lenBox.Text = maxLen.ToString();
                                    lenBox.CaretIndex = lenBox.Text.Length; // Оставляем текстовый курсор в конце
                                    _isUpdating = false;
                                }
                                if (v < 10) v = 10;

                                _isUpdating = true;
                                lenSlider.Value = v;
                                shapeData.SideLengths[sideIndex] = v;

                                // Перерасчет геометрии (методы из вашего кода)
                                RecalculateBasePoints(shapeData);
                                ApplyScale(shapeData.CurrentSize);
                                UpdatePolygonGeometry(container);

                                _isUpdating = false;
                            }
                        };

                        row3.Children.Add(lenSlider);
                        row3.Children.Add(lenBox);
                        group.Children.Add(row3);
                    }
                }

                group.Children.Add(new Separator { Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)), Margin = new Thickness(0, 6, 0, 0) });
                SidePropertiesList.Children.Add(group);
            }

        }
        private StackPanel CreateSideControlGroup(int index, Brush currentBrush, double currentThick)
        {
            var group = new StackPanel { Margin = new Thickness(0, 0, 0, 20) };
            group.Children.Add(new TextBlock { Text = $"СТОРОНА {index + 1}", Foreground = Brushes.White, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 5) });

            // ComboBox для цвета
            var colorCombo = new ComboBox { Background = new SolidColorBrush(Color.FromRgb(37, 37, 38)), Foreground = Brushes.White };
            foreach (var br in _availableColors)
                colorCombo.Items.Add(new Rectangle { Fill = br, Width = 40, Height = 14 });

            // Ищем индекс текущего цвета
            colorCombo.SelectedIndex = _availableColors.FindIndex(b => b.ToString() == currentBrush.ToString());
            colorCombo.SelectionChanged += (s, e) => {
                if (!_isUpdating) UpdateSide(_selectedElement, index, _availableColors[colorCombo.SelectedIndex], -1);
            };

            // Ползунок толщины
            var thickSlider = new Slider { Minimum = 1, Maximum = 30, Value = currentThick };
            thickSlider.ValueChanged += (s, e) => {
                if (!_isUpdating) UpdateSide(_selectedElement, index, null, e.NewValue);
            };

            group.Children.Add(colorCombo);
            group.Children.Add(thickSlider);
            return group;
        }

        private void UpdateSide(UIElement visual, int index, Brush color, double thick)
        {
            if (visual is Canvas container && container.Tag is ShapeData data)
            {
                if (data.OriginalBasePoints != null) // Многоугольник
                {
                    if (color != null && index < data.CurrentColors.Length) data.CurrentColors[index] = color;
                    if (thick > 0 && index < data.CurrentThicknesses.Length) data.CurrentThicknesses[index] = thick;
                    UpdatePolygonGeometry(container);
                }
                else // Круг
                {
                    var ellipse = container.Children.OfType<Ellipse>().FirstOrDefault();
                    if (ellipse != null)
                    {
                        if (color != null) ellipse.Stroke = color;
                        if (thick > 0)
                        {
                            ellipse.StrokeThickness = thick;
                            // Пересчитываем позицию и размер, чтобы толщина росла внутрь
                            double size = data.CurrentSize;
                            ellipse.Width = Math.Max(0, size * 2 - thick);
                            ellipse.Height = Math.Max(0, size * 2 - thick);
                            Canvas.SetLeft(ellipse, 500 - size + thick / 2);
                            Canvas.SetTop(ellipse, 500 - size + thick / 2);
                        }
                        UpdateBoundingBox(container);
                    }
                }
            }
        }

        // --- Логика перетаскивания (MouseDown, MouseMove, MouseUp) ---
        private void MainCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.Source == MainCanvas)
            {
                DeselectAll();
                return;
            }
            if (e.Source is Ellipse el && el.Tag?.ToString() == "Anchor")
            {
                _draggedAnchor = el;
                _clickPosition = e.GetPosition(_selectedElement); // Позиция внутри 1000x1000
                el.CaptureMouse();
                e.Handled = true;
                return;
            }
            // 2. Клик по фигуре
            if (e.Source is Shape clickedShape)
            {
                var parentCanvas = VisualTreeHelper.GetParent(clickedShape) as Canvas;

                if (parentCanvas != null && parentCanvas != MainCanvas)
                {
                    // Если фигура ЕЩЕ НЕ выбрана - просто выбираем её (меню обновится само внутри SelectShape)
                    if (_selectedElement != parentCanvas)
                    {
                        SelectShape(parentCanvas);
                    }
                    // Если фигура УЖЕ выбрана - захватываем мышь для перемещения
                    else
                    {
                        _draggedElement = parentCanvas;
                        _clickPosition = e.GetPosition(MainCanvas);
                        _draggedElement.CaptureMouse();
                    }
                }
            }
            else
            {
                DeselectAll();
            }
        }
        

        private void MainCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_draggedAnchor != null && _selectedElement != null)
            {
                Point p = e.GetPosition(_selectedElement);
                Canvas.SetLeft(_draggedAnchor, p.X - _draggedAnchor.Width / 2);
                Canvas.SetTop(_draggedAnchor, p.Y - _draggedAnchor.Height / 2);

                if (_selectedElement.Tag is ShapeData data)
                    data.LocalAnchor = p; // Сохраняем новые координаты привязки

                UpdateCoordinatesUI();
                int sides = _selectedElement.Children.OfType<Polygon>().Count(poly => poly.Tag?.ToString() == "Stroke");
                if (sides == 0) sides = 1;
                GenerateSideMenu(_selectedElement, sides);
            }
            else if (_draggedElement != null)
            {
                Point p = e.GetPosition(MainCanvas);
                double dx = p.X - _clickPosition.X;
                double dy = p.Y - _clickPosition.Y;

                Canvas.SetLeft(_draggedElement, Canvas.GetLeft(_draggedElement) + dx);
                Canvas.SetTop(_draggedElement, Canvas.GetTop(_draggedElement) + dy);

                _clickPosition = p;
                UpdateCoordinatesUI(); // Обновляем координаты при перемещении фигуры
            }
        }

        private void MainCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_draggedAnchor != null)
            {
                _draggedAnchor.ReleaseMouseCapture();
                _draggedAnchor = null;

                // Перерисовываем меню, чтобы обновить цифры координат относительно новой точки
                if (_selectedElement != null)
                {
                    int sides = _selectedElement.Children.OfType<Polygon>().Count(p => p.Tag?.ToString() == "Stroke");
                    if (sides == 0) sides = 1;
                    //GenerateSideMenu(_selectedElement, sides);
                }
            }
            if (_draggedElement != null)
            {
                _draggedElement.ReleaseMouseCapture();
                _draggedElement = null;
                // Фигура остается выбранной, но перестает следовать за мышью
            }
        }
        private void FillColorCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdating || _selectedElement == null || FillColorCombo.SelectedIndex < 0) return;

            Brush selectedColor = _availableColors[FillColorCombo.SelectedIndex];

            // 1. Обновляем Polygon (для многоугольников)
            var hitArea = _selectedElement.Children.OfType<Polygon>().FirstOrDefault();
            if (hitArea != null) hitArea.Fill = selectedColor;

            // 2. Обновляем Ellipse (для круга)
            var ellipse = _selectedElement.Children.OfType<Ellipse>().FirstOrDefault();
            if (ellipse != null) ellipse.Fill = selectedColor;
        }
        private Point GetIntersection(Point p1, Vector dir1, Point p2, Vector dir2, Point fallback)
        {
            double cross = dir1.X * dir2.Y - dir1.Y * dir2.X;
            if (Math.Abs(cross) < 0.0001) return fallback; // Линии параллельны
            Vector diff = p2 - p1;
            double u = (diff.X * dir2.Y - diff.Y * dir2.X) / cross;
            return p1 + dir1 * u;
        }

        private void UpdatePolygonGeometry(Canvas container)
        {
            ShapeData data = container.Tag as ShapeData;
            if (data == null || data.BasePoints == null) return;

            int n = data.BasePoints.Length;
            double size = data.CurrentSize;

            // 1. Координаты в пикселях
            Point[] pts = new Point[n];
            for (int i = 0; i < n; i++)
                pts[i] = new Point(data.BasePoints[i].X * size + 500, data.BasePoints[i].Y * size + 500);

            // 2. Обновляем зону заливки
            var hitArea = container.Children.OfType<Polygon>().FirstOrDefault(p => p.Tag?.ToString() == "HitArea");
            if (hitArea != null) hitArea.Points = new PointCollection(pts);

            Point[] outer = new Point[n];
            Point[] inner = new Point[n];

            // 3. Вычисляем стыки (биссектрисы)
            for (int i = 0; i < n; i++)
            {
                int prev = (i - 1 + n) % n;
                int next = (i + 1) % n;

                Vector dIn = pts[i] - pts[prev]; dIn.Normalize();
                Vector dOut = pts[next] - pts[i]; dOut.Normalize();

                // Нормали наружу (для WPF Y направлен вниз)
                Vector nIn = new Vector(dIn.Y, -dIn.X);
                Vector nOut = new Vector(dOut.Y, -dOut.X);

                double tIn = data.CurrentThicknesses[prev];
                double tOut = data.CurrentThicknesses[i];

                // Находим внешние точки стыка
                // Находим внешние точки стыка (теперь они строго на базовых линиях)
                Point pOuterIn = pts[i];
                Point pOuterOut = pts[i];
                outer[i] = GetIntersection(pOuterIn, dIn, pOuterOut, dOut, pOuterIn);

                // Находим внутренние точки стыка (смещаем на ПОЛНУЮ толщину tIn/tOut)
                Point pInnerIn = pts[i] - nIn * tIn;
                Point pInnerOut = pts[i] - nOut * tOut;
                inner[i] = GetIntersection(pInnerIn, dIn, pInnerOut, dOut, pInnerIn);
            }

            // 4. Применяем формы и цвета к полигонам-сторонам
            var strokePolygons = container.Children.OfType<Polygon>().Where(p => p.Tag?.ToString() == "Stroke").ToList();
            for (int i = 0; i < n; i++)
            {
                if (i < strokePolygons.Count)
                {
                    int next = (i + 1) % n;
                    strokePolygons[i].Points = new PointCollection(new[] { outer[i], outer[next], inner[next], inner[i] });
                    strokePolygons[i].Fill = data.CurrentColors[i];
                }
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

                if (data.BasePoints != null) // Многоугольник
                {
                    double maxThick = data.CurrentThicknesses.Max(); // Учитываем толщину обводки
                    foreach (var bp in data.BasePoints)
                    {
                        double px = bp.X * data.CurrentSize + 500;
                        double py = bp.Y * data.CurrentSize + 500;
                        if (px < minX) minX = px;
                        if (px > maxX) maxX = px;
                        if (py < minY) minY = py;
                        if (py > maxY) maxY = py;
                    }
                    //minX -= maxThick / 2; maxX += maxThick / 2;
                    //minY -= maxThick / 2; maxY += maxThick / 2;
                }
                else // Окружность
                {
                    double thick = container.Children.OfType<Ellipse>().FirstOrDefault()?.StrokeThickness ?? 0;
                    minX = 500 - data.CurrentSize;
                    maxX = 500 + data.CurrentSize;
                    minY = 500 - data.CurrentSize;
                    maxY = 500 + data.CurrentSize;
                }

                // Задаем отступ рамки (2 пикселя)
                Canvas.SetLeft(bbox, minX - 4);
                Canvas.SetTop(bbox, minY - 4);
                bbox.Width = Math.Max(0, maxX - minX + 8);
                bbox.Height = Math.Max(0, maxY - minY + 8);
            }
        }
    }
}