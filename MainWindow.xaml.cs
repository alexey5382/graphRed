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
        private int _selectedSideIndex = -1; // -1 означает, что выбрана вся фигура
        private Dictionary<int, TextBox> _lenBoxes = new Dictionary<int, TextBox>();
        private Dictionary<int, Slider> _lenSliders = new Dictionary<int, Slider>();
        // --- Переменные для режима рисования ---
        private bool _isDrawingMode = false;
        private List<Point> _drawingPoints = new List<Point>();
        private Line _previewLine;
        private TextBlock _drawingInfoText;

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
            if (type == "Custom") { StartDrawingMode(); return; }

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

            int sides = 1;
            if (_selectedElement.Tag is ShapeData sd && sd.BasePoints != null)
            {
                sides = sd.IsClosed ? sd.BasePoints.Length : sd.BasePoints.Length - 1;
            }
            GenerateSideMenu(_selectedElement, sides);
        }

        private void DeselectAll()
        {
            _selectedSideIndex = -1; // Сбрасываем выбранную сторону
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
                    // Обязательно игнорируем точку Anchor
                    var ellipse = _selectedElement.Children.OfType<Ellipse>().FirstOrDefault(e => e.Tag?.ToString() != "Anchor");
                    if (ellipse != null)
                    {
                        // Просто применяем масштаб
                        ellipse.Width = sizeValue * 2;
                        ellipse.Height = sizeValue * 2;
                        Canvas.SetLeft(ellipse, 500 - sizeValue);
                        Canvas.SetTop(ellipse, 500 - sizeValue);
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
            SidePropertiesList.Children.Clear();
            _lenBoxes.Clear();   // Очищаем старые ссылки
            _lenSliders.Clear();

            // Кнопка возврата ко всем сторонам
            if (_selectedSideIndex != -1)
            {
                var backBtn = new Button { Content = "← Вернуться ко всем сторонам", Foreground = Brushes.White, Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)), BorderThickness = new Thickness(0), Padding = new Thickness(5), Margin = new Thickness(0, 0, 0, 15) };
                backBtn.Click += (s, e) => {
                    _selectedSideIndex = -1;
                    GenerateSideMenu(container, sidesCount);
                    UpdatePolygonGeometry(container); // Убираем пунктир
                };
                SidePropertiesList.Children.Add(backBtn);
            }

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
                if (_selectedSideIndex != -1 && i != _selectedSideIndex) continue;
                int sideIndex = i;
                var group = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };

                // --- СТРОКА 1: Заголовок и Цвет ---
                var row1 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
                row1.Children.Add(new TextBlock { Text = $"СТОР. {i + 1}", FontWeight = FontWeights.Bold, Foreground = Brushes.White, Width = 55, VerticalAlignment = VerticalAlignment.Center });

                var colorCombo = new ComboBox { Background = darkInputBg, Foreground = Brushes.White, BorderBrush = inputBorder, Width = 50 };
                foreach (var color in _availableColors) colorCombo.Items.Add(new Rectangle { Fill = color, Width = 30, Height = 12 });

                Brush currentBrush = shapeData?.CurrentColors != null && i < shapeData.CurrentColors.Length ? shapeData.CurrentColors[i] : (ellipse?.Stroke ?? Brushes.Black);
                colorCombo.SelectedIndex = _availableColors.FindIndex(b => b.ToString() == currentBrush.ToString());
                colorCombo.SelectionChanged += (s, e) => { if (!_isUpdating) UpdateSide(container, sideIndex, _availableColors[colorCombo.SelectedIndex], -1); };
                row1.Children.Add(colorCombo);
                group.Children.Add(row1);

                // --- СТРОКА 2: Координаты X и Y ---
                if (shapeData != null && shapeData.BasePoints != null)
                {
                    var row2 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };

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
                            SyncMenuLengths(shapeData, -1);
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
                            SyncMenuLengths(shapeData, -1);
                            _isUpdating = false;
                        }
                    };

                    group.Children.Add(row2);
                }

                // --- СТРОКА 3: Длина (Слайдер и поле) ---
                if (shapeData?.SideLengths != null && i < shapeData.SideLengths.Length)
                {
                    var row3 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
                    row3.Children.Add(new TextBlock { Text = "Дл:", Foreground = lightText, FontSize = 10, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 3, 0), Width = 25 });

                    double maxLen = 1000;
                    double currentLen = shapeData.SideLengths[i];
                    //алтимшорекитлок
                    var lenSlider = new Slider { Width = 100, Minimum = 10, Maximum = maxLen, Value = currentLen, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
                    var lenBox = new TextBox { Width = 40, Text = Math.Round(currentLen).ToString(), Background = darkInputBg, Foreground = Brushes.White, BorderBrush = inputBorder, TextAlignment = TextAlignment.Center };

                    _lenSliders[sideIndex] = lenSlider;
                    _lenBoxes[sideIndex] = lenBox;

                    lenSlider.ValueChanged += (s, e) => { if (!_isUpdating) lenBox.Text = Math.Round(e.NewValue).ToString(); };

                    lenBox.TextChanged += (s, e) => {
                        if (!_isUpdating && double.TryParse(lenBox.Text, out double v))
                        {
                            if (v > maxLen) { v = maxLen; _isUpdating = true; lenBox.Text = maxLen.ToString(); lenBox.CaretIndex = lenBox.Text.Length; _isUpdating = false; }
                            if (v < 10) v = 10;

                            _isUpdating = true;
                            lenSlider.Value = v;
                            shapeData.SideLengths[sideIndex] = v;

                            int next = (sideIndex + 1) % shapeData.BasePoints.Length;
                            Point p1 = shapeData.BasePoints[sideIndex];
                            Point p2 = shapeData.BasePoints[next];

                            double dx = p2.X - p1.X;
                            double dy = p2.Y - p1.Y;
                            double currentLenNorm = Math.Sqrt(dx * dx + dy * dy);

                            if (currentLenNorm > 0.0001)
                            {
                                double targetLenNorm = v / shapeData.CurrentSize;
                                double deltaNorm = targetLenNorm - currentLenNorm;
                                double offsetX = (dx / currentLenNorm) * (deltaNorm / 2.0);
                                double offsetY = (dy / currentLenNorm) * (deltaNorm / 2.0);

                                shapeData.BasePoints[sideIndex] = new Point(p1.X - offsetX, p1.Y - offsetY);
                                shapeData.BasePoints[next] = new Point(p2.X + offsetX, p2.Y + offsetY);

                                if (shapeData.OriginalBasePoints != null)
                                {
                                    shapeData.OriginalBasePoints[sideIndex] = shapeData.BasePoints[sideIndex];
                                    shapeData.OriginalBasePoints[next] = shapeData.BasePoints[next];
                                }
                            }
                            UpdatePolygonGeometry(container);
                            SyncMenuLengths(shapeData, sideIndex);
                            _isUpdating = false;
                        }
                    };

                    row3.Children.Add(lenSlider);
                    row3.Children.Add(lenBox);
                    group.Children.Add(row3);
                }

                // --- СТРОКА 4: Толщина (Ползунок, -, Поле, +) ---
                var row4 = new StackPanel { Orientation = Orientation.Horizontal };
                row4.Children.Add(new TextBlock { Text = "Толщ:", Foreground = lightText, FontSize = 10, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 3, 0), Width = 30 });

                double currentThick = shapeData?.CurrentThicknesses != null && i < shapeData.CurrentThicknesses.Length ? shapeData.CurrentThicknesses[i] : (ellipse?.StrokeThickness ?? 2);

                // Добавляем ползунок толщины
                var thickSlider = new Slider { Width = 100, Minimum = 1, Maximum = 50, Value = currentThick, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };

                var minusBtn = new Button { Content = "-", Width = 18, Height = 18, Background = darkInputBg, Foreground = Brushes.White, BorderBrush = inputBorder, Margin = new Thickness(0, 0, 2, 0) };
                var thickBox = new TextBox { Text = Math.Round(currentThick).ToString(), Width = 30, Background = darkInputBg, Foreground = Brushes.White, BorderBrush = inputBorder, TextAlignment = TextAlignment.Center };
                var plusBtn = new Button { Content = "+", Width = 18, Height = 18, Background = darkInputBg, Foreground = Brushes.White, BorderBrush = inputBorder, Margin = new Thickness(2, 0, 0, 0) };

                thickSlider.ValueChanged += (s, e) => {
                    if (!_isUpdating) thickBox.Text = Math.Round(e.NewValue).ToString();
                };

                minusBtn.Click += (s, e) => {
                    if (double.TryParse(thickBox.Text, out double v) && v > 1) thickBox.Text = (v - 1).ToString();
                };

                plusBtn.Click += (s, e) => {
                    if (double.TryParse(thickBox.Text, out double v) && v < 50) thickBox.Text = (v + 1).ToString();
                };

                thickBox.TextChanged += (s, e) => {
                    if (!_isUpdating && double.TryParse(thickBox.Text, out double v))
                    {
                        if (v < 1) v = 1;
                        if (v > 50) v = 50;

                        _isUpdating = true;
                        thickSlider.Value = v;
                        UpdateSide(container, sideIndex, null, v);
                        _isUpdating = false;
                    }
                };

                row4.Children.Add(thickSlider);
                row4.Children.Add(minusBtn);
                row4.Children.Add(thickBox);
                row4.Children.Add(plusBtn);
                group.Children.Add(row4);

                // Разделитель
                group.Children.Add(new Separator { Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)), Margin = new Thickness(0, 8, 0, 0) });
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
                    // Ищем круг, игнорируя точку привязки
                    var ellipse = container.Children.OfType<Ellipse>().FirstOrDefault(e => e.Tag?.ToString() != "Anchor");
                    if (ellipse != null)
                    {
                        if (color != null) ellipse.Stroke = color;
                        if (thick > 0)
                        {
                            if (data.CurrentThicknesses != null && index < data.CurrentThicknesses.Length)
                            {
                                data.CurrentThicknesses[index] = thick;
                            }

                            ellipse.StrokeThickness = thick;

                            // Возвращаем чистые размеры (WPF сам нарастит толщину внутрь)
                            double size = data.CurrentSize;
                            ellipse.Width = size * 2;
                            ellipse.Height = size * 2;
                            Canvas.SetLeft(ellipse, 500 - size);
                            Canvas.SetTop(ellipse, 500 - size);
                        }
                        UpdateBoundingBox(container);
                    }
                }
            }
        }

        // --- Логика перетаскивания (MouseDown, MouseMove, MouseUp) ---
        private void MainCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_isDrawingMode)
            {
                Point p = e.GetPosition(MainCanvas);
                if (_drawingPoints.Count > 0 && Keyboard.IsKeyDown(Key.LeftShift)) p = GetSnappedPoint(_drawingPoints.Last(), p);

                _drawingPoints.Add(p);

                // Отрисовка зафиксированной точки
                Ellipse dot = new Ellipse { Width = 8, Height = 8, Fill = Brushes.Cyan, Tag = "DrawingTemp" };
                Canvas.SetLeft(dot, p.X - 4);
                Canvas.SetTop(dot, p.Y - 4);
                MainCanvas.Children.Add(dot);

                if (_drawingPoints.Count == 1)
                {
                    _previewLine.X1 = p.X; _previewLine.Y1 = p.Y;
                    _previewLine.X2 = p.X; _previewLine.Y2 = p.Y;
                    _previewLine.Visibility = Visibility.Visible;
                    _drawingInfoText.Visibility = Visibility.Visible;
                }
                else
                {
                    // Отрисовка зафиксированного отрезка ЧЕРНЫМ цветом
                    Line solidLine = new Line { X1 = _drawingPoints[_drawingPoints.Count - 2].X, Y1 = _drawingPoints[_drawingPoints.Count - 2].Y, X2 = p.X, Y2 = p.Y, Stroke = Brushes.Black, StrokeThickness = 2, Tag = "DrawingTemp" };
                    MainCanvas.Children.Add(solidLine);
                    _previewLine.X1 = p.X; _previewLine.Y1 = p.Y;
                }
                e.Handled = true;
                return;
            }
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
            // 2. Клик по фигуре или её стороне
            // 2. Клик по фигуре или её стороне
            if (e.Source is Shape clickedShape)
            {
                var parentCanvas = VisualTreeHelper.GetParent(clickedShape) as Canvas;

                if (parentCanvas != null && parentCanvas != MainCanvas)
                {
                    // Проверяем, кликнули ли мы точно по обводке (стороне)
                    int clickedSide = -1;
                    if (clickedShape is Polygon poly && poly.Tag?.ToString() == "Stroke")
                    {
                        var strokes = parentCanvas.Children.OfType<Polygon>().Where(p => p.Tag?.ToString() == "Stroke").ToList();
                        clickedSide = strokes.IndexOf(poly);
                    }

                    // Если фигура еще не выбрана ИЛИ мы кликнули по другой грани
                    if (_selectedElement != parentCanvas || _selectedSideIndex != clickedSide)
                    {
                        SelectShape(parentCanvas); // ВНИМАНИЕ: это сбрасывает _selectedSideIndex в -1

                        _selectedSideIndex = clickedSide; // ТЕПЕРЬ устанавливаем индекс выбранной стороны

                        // Перерисовываем меню, чтобы оставить только 1 сторону, и обновляем пунктир
                        int sides = parentCanvas.Children.OfType<Polygon>().Count(p => p.Tag?.ToString() == "Stroke");
                        if (sides == 0) sides = 1;
                        GenerateSideMenu(parentCanvas, sides);
                        UpdatePolygonGeometry(parentCanvas);
                    }

                    // Захватываем мышь для перемещения
                    _draggedElement = parentCanvas;
                    _clickPosition = e.GetPosition(MainCanvas);
                    _draggedElement.CaptureMouse();
                }
            }
            else
            {
                DeselectAll();
            }
        }
        

        private void MainCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDrawingMode && _drawingPoints.Count > 0)
            {
                Point currentPoint = e.GetPosition(MainCanvas);
                Point lastPoint = _drawingPoints.Last();
                if (Keyboard.IsKeyDown(Key.LeftShift)) currentPoint = GetSnappedPoint(lastPoint, currentPoint);

                _previewLine.X2 = currentPoint.X;
                _previewLine.Y2 = currentPoint.Y;

                double len = Math.Round((currentPoint - lastPoint).Length);
                _drawingInfoText.Text = $"X: {Math.Round(currentPoint.X)} Y: {Math.Round(currentPoint.Y)}\nДлина: {len}";
                Canvas.SetLeft(_drawingInfoText, currentPoint.X + 15);
                Canvas.SetTop(_drawingInfoText, currentPoint.Y + 15);
                return;
            }
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
            Point[] pts = new Point[n];
            for (int i = 0; i < n; i++)
                pts[i] = new Point(data.BasePoints[i].X * size + 500, data.BasePoints[i].Y * size + 500);

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
                highlight.X1 = data.BasePoints[_selectedSideIndex].X * size + 500;
                highlight.Y1 = data.BasePoints[_selectedSideIndex].Y * size + 500;
                highlight.X2 = data.BasePoints[next].X * size + 500;
                highlight.Y2 = data.BasePoints[next].Y * size + 500;
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

                if (data.BasePoints != null) // Многоугольник
                {
                    foreach (var bp in data.BasePoints)
                    {
                        double px = bp.X * data.CurrentSize + 500;
                        double py = bp.Y * data.CurrentSize + 500;
                        if (px < minX) minX = px;
                        if (px > maxX) maxX = px;
                        if (py < minY) minY = py;
                        if (py > maxY) maxY = py;
                    }
                }
                else // Окружность
                {
                    minX = 500 - data.CurrentSize;
                    maxX = 500 + data.CurrentSize;
                    minY = 500 - data.CurrentSize;
                    maxY = 500 + data.CurrentSize;
                }

                // Устанавливаем координаты строго по границе (без отступов -2 и +4)
                Canvas.SetLeft(bbox, minX);
                Canvas.SetTop(bbox, minY);
                bbox.Width = Math.Max(0, maxX - minX);
                bbox.Height = Math.Max(0, maxY - minY);
            }
        }
        // Обновляет текстовые поля смежных сторон без потери фокуса
        private void SyncMenuLengths(ShapeData data, int skipIndex)
        {
            if (data.BasePoints == null || data.SideLengths == null) return;

            int segmentsCount = data.IsClosed ? data.BasePoints.Length : data.BasePoints.Length - 1;
            for (int i = 0; i < segmentsCount; i++)
            {
                if (i == skipIndex) continue;

                int next = data.IsClosed ? (i + 1) % data.BasePoints.Length : i + 1;

                double dx = data.BasePoints[next].X - data.BasePoints[i].X;
                double dy = data.BasePoints[next].Y - data.BasePoints[i].Y;
                double actualLen = Math.Sqrt(dx * dx + dy * dy) * data.CurrentSize;

                data.SideLengths[i] = actualLen;

                if (_lenSliders.ContainsKey(i) && _lenBoxes.ContainsKey(i))
                {
                    _lenSliders[i].Value = actualLen;
                    _lenBoxes[i].Text = Math.Round(actualLen).ToString();
                }
            }
        }
        // ЗАМЕНИ свой старый метод OnKeyDown на этот:
        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);

            if (_isDrawingMode)
            {
                if (e.Key == Key.Enter)
                {
                    FinishDrawing(false);      // Enter - открытая ломаная
                    e.Handled = true;          // ВАЖНО: запрещаем кнопке нажиматься от Enter
                }
                else if (e.Key == Key.Tab)
                {
                    FinishDrawing(true);       // Tab - замкнутая фигура
                    e.Handled = true;          // Блокируем перенос фокуса на другие элементы
                }
                else if (e.Key == Key.Escape)
                {
                    CancelDrawing();           // Отмена
                    e.Handled = true;
                }
            }
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

            var visual = (Canvas)shape.CreateVisual(size);

            Canvas.SetLeft(visual, centerX - 500);
            Canvas.SetTop(visual, centerY - 500);

            UpdatePolygonGeometry(visual);
            MainCanvas.Children.Add(visual);
            SelectShape(visual);
        }
        private Point GetSnappedPoint(Point start, Point current)
        {
            Vector v = current - start;
            double angle = Math.Atan2(v.Y, v.X);
            double snapAngle = Math.Round(angle / (Math.PI / 12)) * (Math.PI / 12); // Кратность 15 градусам (Pi / 12)
            double len = v.Length;
            return new Point(start.X + len * Math.Cos(snapAngle), start.Y + len * Math.Sin(snapAngle));
        }
    }
}