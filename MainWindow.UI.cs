using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows;

namespace Lab1
{
    public partial class MainWindow
    {
        private void GenerateSideMenu(Canvas container, int sidesCount)
        {
            // Получаем доступ к данным фигуры и эллипсу
            ShapeData shapeData = container.Tag as ShapeData;
            var ellipse = container.Children.OfType<Ellipse>().FirstOrDefault();

            SidePropertiesList.Children.Clear();
            _lenBoxes.Clear();   // Очищаем старые ссылки
            _lenSliders.Clear();
            _coordXBoxes.Clear();
            _coordYBoxes.Clear();
            if (_selectedSideIndex == -2)
            {
                GenerateAnchorMenu(container);
                return;
            }
            _activeThickSliders.Clear();
            _activeThickBoxes.Clear();
            double currentMaxDim = Math.Max(shapeData.SizeX, shapeData.SizeY) * 2.0;
            double currentMaxThick = Math.Max(1.0, Math.Min(50.0, currentMaxDim / 15.0));
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

                    double px = shapeData.BasePoints[i].X * shapeData.SizeX + 500;
                    double py = shapeData.BasePoints[i].Y * shapeData.SizeY + 500;
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
                            double normX = (newPx - 500) / shapeData.SizeX; // ИСПОЛЬЗУЕМ SizeX
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
                            double normY = (newPy - 500) / shapeData.SizeY; // ИСПОЛЬЗУЕМ SizeY
                            shapeData.BasePoints[sideIndex] = new Point(shapeData.BasePoints[sideIndex].X, normY);
                            if (shapeData.OriginalBasePoints != null) shapeData.OriginalBasePoints[sideIndex] = new Point(shapeData.OriginalBasePoints[sideIndex].X, normY);
                            UpdatePolygonGeometry(container);
                            SyncMenuLengths(shapeData, -1);
                            _isUpdating = false;
                        }
                    };
                    // ... (где создаются xBox и yBox) ...
                    _coordXBoxes[sideIndex] = xBox;
                    _coordYBoxes[sideIndex] = yBox;
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

                            // ИСПОЛЬЗУЕМ SizeX и SizeY
                            double dx = (p2.X - p1.X) * shapeData.SizeX;
                            double dy = (p2.Y - p1.Y) * shapeData.SizeY;
                            double currentLenPixels = Math.Sqrt(dx * dx + dy * dy);

                            if (currentLenPixels > 0.0001)
                            {
                                double deltaPixels = v - currentLenPixels;

                                // Вычисляем смещение в нормализованных координатах
                                double offsetXNorm = (dx / currentLenPixels) * (deltaPixels / 2.0) / shapeData.SizeX;
                                double offsetYNorm = (dy / currentLenPixels) * (deltaPixels / 2.0) / shapeData.SizeY;

                                shapeData.BasePoints[sideIndex] = new Point(p1.X - offsetXNorm, p1.Y - offsetYNorm);
                                shapeData.BasePoints[next] = new Point(p2.X + offsetXNorm, p2.Y + offsetYNorm);

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
                // --- СТРОКА 4: Толщина (Ползунок, -, Поле, +) ---
                var row4 = new StackPanel { Orientation = Orientation.Horizontal };
                row4.Children.Add(new TextBlock { Text = "Толщ:", Foreground = lightText, FontSize = 10, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 3, 0), Width = 30 });

                double currentThick = shapeData?.CurrentThicknesses != null && i < shapeData.CurrentThicknesses.Length ? shapeData.CurrentThicknesses[i] : (ellipse?.StrokeThickness ?? 2);

                var thickSlider = new Slider { Width = 100, Minimum = 1, Maximum = currentMaxThick, Value = currentThick, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };

                var minusBtn = new Button { Content = "-", Width = 18, Height = 18, Background = darkInputBg, Foreground = Brushes.White, BorderThickness = new Thickness(0), Margin = new Thickness(0, 0, 2, 0) };
                var thickBox = new TextBox { Text = Math.Round(currentThick).ToString(), Width = 30, Background = darkInputBg, Foreground = Brushes.White, BorderBrush = inputBorder, TextAlignment = TextAlignment.Center };
                var plusBtn = new Button { Content = "+", Width = 18, Height = 18, Background = darkInputBg, Foreground = Brushes.White, BorderThickness = new Thickness(0), Margin = new Thickness(2, 0, 0, 0) };

                _activeThickSliders[sideIndex] = thickSlider;
                _activeThickBoxes[sideIndex] = thickBox;

                thickSlider.ValueChanged += (s, e) => {
                    if (!_isUpdating) thickBox.Text = Math.Round(e.NewValue).ToString();
                };

                minusBtn.Click += (s, e) => {
                    if (double.TryParse(thickBox.Text, out double v) && v > 1) thickBox.Text = (v - 1).ToString();
                };

                plusBtn.Click += (s, e) => {
                    if (double.TryParse(thickBox.Text, out double v) && v < thickSlider.Maximum) thickBox.Text = (v + 1).ToString();
                };

                thickBox.TextChanged += (s, e) => {
                    if (!_isUpdating && double.TryParse(thickBox.Text, out double v))
                    {
                        if (v < 1) v = 1;
                        if (v > thickSlider.Maximum) v = thickSlider.Maximum;

                        _isUpdating = true;
                        thickSlider.Value = v;
                        thickBox.Text = Math.Round(v).ToString();
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
        private void GenerateAnchorMenu(Canvas container)
        {
            SidePropertiesList.Children.Clear();
            ShapeData shapeData = container.Tag as ShapeData;
            if (shapeData == null) return;

            var backBtn = new Button { Content = "← Вернуться ко всем сторонам", Foreground = Brushes.White, Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)), BorderThickness = new Thickness(0), Padding = new Thickness(5), Margin = new Thickness(0, 0, 0, 15) };
            backBtn.Click += (s, e) => {
                _selectedSideIndex = -1;
                int sides = container.Children.OfType<Polygon>().Count(p => p.Tag?.ToString() == "Stroke");
                if (sides == 0) sides = 1;
                GenerateSideMenu(container, sides);
            };
            SidePropertiesList.Children.Add(backBtn);

            var centerBtn = new Button { Content = "Сбросить привязку в центр", Foreground = Brushes.White, Background = new SolidColorBrush(Color.FromRgb(0, 122, 204)), BorderThickness = new Thickness(0), Padding = new Thickness(5), Margin = new Thickness(0, 0, 0, 15) };
            centerBtn.Click += (s, e) => {
                shapeData.LocalAnchor = new Point(500, 500);
                var anchor = container.Children.OfType<Ellipse>().FirstOrDefault(a => a.Tag?.ToString() == "Anchor");
                if (anchor != null)
                {
                    Canvas.SetLeft(anchor, 500 - anchor.Width / 2);
                    Canvas.SetTop(anchor, 500 - anchor.Height / 2);
                }
                UpdateCoordinatesUI();
            };
            SidePropertiesList.Children.Add(centerBtn);

            var group = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            group.Children.Add(new TextBlock { Text = "ОБЩИЕ ПАРАМЕТРЫ СТОРОН", FontWeight = FontWeights.Bold, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 10) });

            Brush darkBg = new SolidColorBrush(Color.FromRgb(37, 37, 38));
            Brush bdr = new SolidColorBrush(Color.FromRgb(85, 85, 85));

            // --- ИЗМЕНЕНИЕ ЦВЕТА С ПУНКТОМ "НЕ ВЫБРАН" ---
            var row1 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            row1.Children.Add(new TextBlock { Text = "Цвет:", Foreground = Brushes.White, Width = 55, VerticalAlignment = VerticalAlignment.Center });

            var colorCombo = new ComboBox { Background = darkBg, Foreground = Brushes.White, BorderBrush = bdr, Width = 90 };
            colorCombo.Items.Add(new TextBlock { Text = "Не выбран", Foreground = Brushes.White, FontSize = 11 }); // Индекс 0
            foreach (var color in _availableColors) colorCombo.Items.Add(new Rectangle { Fill = color, Width = 30, Height = 12 });

            // Проверяем, все ли стороны одинакового цвета
            bool allSameColor = true;
            Brush firstColor = shapeData.CurrentColors?.FirstOrDefault();

            if (shapeData.CurrentColors != null && firstColor != null)
            {
                foreach (var c in shapeData.CurrentColors)
                {
                    if (c.ToString() != firstColor.ToString()) { allSameColor = false; break; }
                }
            }
            else if (shapeData.CurrentColors == null) // Логика для круга
            {
                var ellipse = container.Children.OfType<Ellipse>().FirstOrDefault(x => x.Tag?.ToString() != "Anchor");
                if (ellipse != null) firstColor = ellipse.Stroke;
            }

            // Устанавливаем выбранный пункт
            if (allSameColor && firstColor != null)
            {
                int idx = _availableColors.FindIndex(b => b.ToString() == firstColor.ToString());
                colorCombo.SelectedIndex = idx >= 0 ? idx + 1 : 0; // Сдвиг +1 из-за "Не выбран"
            }
            else
            {
                colorCombo.SelectedIndex = 0; // "Не выбран"
            }

            colorCombo.SelectionChanged += (s, e) => {
                if (_isUpdating || colorCombo.SelectedIndex == 0) return; // Игнорируем, если кликнули на "Не выбран"

                Brush newColor = _availableColors[colorCombo.SelectedIndex - 1];
                if (shapeData.CurrentColors != null)
                {
                    for (int i = 0; i < shapeData.CurrentColors.Length; i++) shapeData.CurrentColors[i] = newColor;
                }
                else
                {
                    var ellipse = container.Children.OfType<Ellipse>().FirstOrDefault(x => x.Tag?.ToString() != "Anchor");
                    if (ellipse != null) ellipse.Stroke = newColor;
                }
                UpdatePolygonGeometry(container);
            };
            row1.Children.Add(colorCombo);
            group.Children.Add(row1);

            // --- ИЗМЕНЕНИЕ ТОЛЩИНЫ ---
            // --- ИЗМЕНЕНИЕ ТОЛЩИНЫ ---
            _activeThickSliders.Clear();
            _activeThickBoxes.Clear();
            double maxDim = Math.Max(shapeData.SizeX, shapeData.SizeY) * 2.0;
            double currentMaxThick = Math.Max(1.0, Math.Min(50.0, maxDim / 15.0));

            var row2 = new StackPanel { Orientation = Orientation.Horizontal };
            row2.Children.Add(new TextBlock { Text = "Толщ:", Foreground = Brushes.White, Width = 55, VerticalAlignment = VerticalAlignment.Center });
            double currentThick = shapeData.CurrentThicknesses != null && shapeData.CurrentThicknesses.Length > 0 ? shapeData.CurrentThicknesses[0] : 2;

            var thickSlider = new Slider { Width = 100, Minimum = 1, Maximum = currentMaxThick, Value = currentThick, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
            var thickBox = new TextBox { Text = Math.Round(currentThick).ToString(), Width = 30, Background = darkBg, Foreground = Brushes.White, BorderBrush = bdr, TextAlignment = TextAlignment.Center };

            _activeThickSliders[-2] = thickSlider;
            _activeThickBoxes[-2] = thickBox;

            thickSlider.ValueChanged += (s, e) => { if (!_isUpdating) thickBox.Text = Math.Round(e.NewValue).ToString(); };
            thickBox.TextChanged += (s, e) => {
                if (!_isUpdating && double.TryParse(thickBox.Text, out double v))
                {
                    if (v < 1) v = 1;
                    if (v > thickSlider.Maximum) v = thickSlider.Maximum;

                    _isUpdating = true;
                    thickSlider.Value = v;
                    thickBox.Text = Math.Round(v).ToString();

                    if (shapeData.CurrentThicknesses != null)
                    {
                        for (int i = 0; i < shapeData.CurrentThicknesses.Length; i++) shapeData.CurrentThicknesses[i] = v;
                    }
                    else
                    {
                        var ellipse = container.Children.OfType<Ellipse>().FirstOrDefault(x => x.Tag?.ToString() != "Anchor");
                        if (ellipse != null) ellipse.StrokeThickness = v;
                    }
                    UpdatePolygonGeometry(container);
                    _isUpdating = false;
                }
            };

            row2.Children.Add(thickSlider);
            row2.Children.Add(thickBox);
            group.Children.Add(row2);
            SidePropertiesList.Children.Add(group);
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
                    var ellipse = container.Children.OfType<Ellipse>().FirstOrDefault(e => e.Tag?.ToString() != "Anchor");
                    if (ellipse != null)
                    {
                        if (color != null) ellipse.Stroke = color;
                        if (thick > 0)
                        {
                            if (data.CurrentThicknesses != null && index < data.CurrentThicknesses.Length)
                                data.CurrentThicknesses[index] = thick;

                            ellipse.StrokeThickness = thick;

                            // ИСПОЛЬЗУЕМ SizeX и SizeY
                            ellipse.Width = data.SizeX * 2;
                            ellipse.Height = data.SizeY * 2;
                            Canvas.SetLeft(ellipse, 500 - data.SizeX);
                            Canvas.SetTop(ellipse, 500 - data.SizeY);
                        }
                        UpdateBoundingBox(container);
                    }
                }
            }
        }
        private void SyncMenuLengths(ShapeData data, int skipIndex)
        {
            if (data.BasePoints == null || data.SideLengths == null) return;

            bool wasUpdating = _isUpdating;
            _isUpdating = true; // БЛОКИРУЕМ вызов событий TextChanged!

            int segmentsCount = data.IsClosed ? data.BasePoints.Length : data.BasePoints.Length - 1;
            for (int i = 0; i < segmentsCount; i++)
            {
                if (i == skipIndex) continue;

                int next = data.IsClosed ? (i + 1) % data.BasePoints.Length : i + 1;
                double dx = (data.BasePoints[next].X - data.BasePoints[i].X) * data.SizeX;
                double dy = (data.BasePoints[next].Y - data.BasePoints[i].Y) * data.SizeY;
                double actualLen = Math.Sqrt(dx * dx + dy * dy);

                data.SideLengths[i] = actualLen;

                if (_lenSliders.ContainsKey(i) && _lenBoxes.ContainsKey(i))
                {
                    _lenSliders[i].Value = actualLen;
                    _lenBoxes[i].Text = Math.Round(actualLen).ToString();
                }
            }
            _isUpdating = wasUpdating;
        }
        private void SyncMenuCoordinates(ShapeData data)
        {
            if (data.BasePoints == null) return;

            bool wasUpdating = _isUpdating;
            _isUpdating = true; // БЛОКИРУЕМ вызов событий TextChanged!

            for (int i = 0; i < data.BasePoints.Length; i++)
            {
                if (_coordXBoxes.ContainsKey(i) && _coordYBoxes.ContainsKey(i))
                {
                    double px = data.BasePoints[i].X * data.SizeX + 500;
                    double py = data.BasePoints[i].Y * data.SizeY + 500;
                    double relX = px - data.LocalAnchor.X;
                    double relY = data.LocalAnchor.Y - py;

                    _coordXBoxes[i].Text = Math.Round(relX).ToString();
                    _coordYBoxes[i].Text = Math.Round(relY).ToString();
                }
            }
            _isUpdating = wasUpdating;
        }
        private void SyncMenuThicknesses(ShapeData data)
        {
            if (data == null) return;

            bool wasUpdating = _isUpdating;
            _isUpdating = true; // БЛОКИРУЕМ вызов событий TextChanged!

            double maxDim = Math.Max(data.SizeX, data.SizeY) * 2.0;
            double maxThick = Math.Max(1.0, Math.Min(50.0, maxDim / 15.0));

            foreach (var kvp in _activeThickSliders)
            {
                int index = kvp.Key;
                Slider slider = kvp.Value;

                slider.Maximum = maxThick; // Устанавливаем новый динамический лимит

                double currentVal = 2;
                if (data.CurrentThicknesses != null)
                {
                    if (index >= 0 && index < data.CurrentThicknesses.Length)
                        currentVal = data.CurrentThicknesses[index];
                    else if (index == -2 && data.CurrentThicknesses.Length > 0)
                        currentVal = data.CurrentThicknesses[0];
                }
                else // Круг
                {
                    var ellipse = _selectedElement?.Children.OfType<Ellipse>().FirstOrDefault(e => e.Tag?.ToString() != "Anchor");
                    if (ellipse != null) currentVal = ellipse.StrokeThickness;
                }

                // Срезаем значение, если оно больше разрешенного
                if (currentVal > maxThick) currentVal = maxThick;

                slider.Value = currentVal;
                if (_activeThickBoxes.ContainsKey(index))
                {
                    _activeThickBoxes[index].Text = Math.Round(currentVal).ToString();
                }
            }
            _isUpdating = wasUpdating;
        }
        private void RandomizeShapeSides_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedElement == null || !(_selectedElement.Tag is ShapeData data)) return;

            Random rnd = new Random();
            _isUpdating = true; // Блокируем лишние срабатывания UI событий при обновлении значений

            // 1. Рандомизируем цвета для каждой стороны независимо
            if (data.CurrentColors != null)
            {
                for (int i = 0; i < data.CurrentColors.Length; i++)
                {
                    data.CurrentColors[i] = _availableColors[rnd.Next(_availableColors.Count)];
                }
            }

            // 2. Рандомизируем толщину с учетом лимита
            double currentMaxDim = Math.Max(data.SizeX, data.SizeY) * 2.0;
            int maxAllowedThick = (int)Math.Max(2.0, Math.Min(50.0, currentMaxDim / 15.0));

            if (data.CurrentThicknesses != null)
            {
                for (int i = 0; i < data.CurrentThicknesses.Length; i++)
                {
                    data.CurrentThicknesses[i] = rnd.Next(1, maxAllowedThick + 1);
                }
            }

            // 3. Применяем изменения к визуалу
            if (data.BasePoints == null) // Если это круг
            {
                var ellipse = _selectedElement.Children.OfType<Ellipse>().FirstOrDefault(x => x.Tag?.ToString() != "Anchor");
                if (ellipse != null)
                {
                    if (data.CurrentColors != null && data.CurrentColors.Length > 0)
                        ellipse.Stroke = data.CurrentColors[0];
                    if (data.CurrentThicknesses != null && data.CurrentThicknesses.Length > 0)
                        ellipse.StrokeThickness = data.CurrentThicknesses[0];
                }
                UpdateBoundingBox(_selectedElement);
            }
            else // Если это многоугольник или ломаная
            {
                UpdatePolygonGeometry(_selectedElement);
            }

            // 4. Обновляем открытое меню, чтобы ползунки и комбобоксы подтянулись
            int sides = _selectedElement.Children.OfType<Polygon>().Count(p => p.Tag?.ToString() == "Stroke");
            if (sides == 0) sides = 1;

            // Если открыто меню якоря (общее), обновляем его. Иначе обновляем меню сторон.
            if (_selectedSideIndex == -2)
                GenerateAnchorMenu(_selectedElement);
            else
                GenerateSideMenu(_selectedElement, sides);

            _isUpdating = false;
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



    }
}
