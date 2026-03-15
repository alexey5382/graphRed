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
        private void SelectShape(Canvas shapeCanvas)
        {
            // Снимаем выделение с предыдущей фигуры
            DeselectAll();

            _selectedElement = shapeCanvas;
            _selectedElement.Opacity = 1;
            PropertiesPanel.Visibility = Visibility.Visible;
            CreateHandles(_selectedElement);
            UpdateBoundingBox(_selectedElement);
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

                // --- ИЗМЕНЕНИЯ ЗДЕСЬ (Масштаб 1-100) ---
                double maxDim = Math.Max(data.SizeX, data.SizeY) * 2.0;
                if (maxDim < MIN_SHAPE_SIZE) maxDim = MIN_SHAPE_SIZE;
                if (maxDim > MAX_SHAPE_SIZE) maxDim = MAX_SHAPE_SIZE;

                double scale1to100 = 1.0 + (maxDim - MIN_SHAPE_SIZE) / (MAX_SHAPE_SIZE - MIN_SHAPE_SIZE) * 99.0;

                GlobalSizeSlider.Value = scale1to100;
                GlobalSizeBox.Text = Math.Round(scale1to100).ToString();
                // -----------------------

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

                // НОВОЕ: Удаляем маркеры
                var handles = _selectedElement.Children.OfType<FrameworkElement>().Where(x => x.Tag?.ToString().StartsWith("Vertex_") == true || x.Tag?.ToString().StartsWith("Resize_") == true).ToList();
                foreach (var h in handles) _selectedElement.Children.Remove(h);
            }
            _selectedElement = null;
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
        private void GlobalSize_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Проверяем флаг, чтобы не создать бесконечный цикл обновлений
            if (_isUpdating || _selectedElement == null) return;

            _isUpdating = true;

            // 1. Обновляем текст в поле (округляем для красоты)
            GlobalSizeBox.Text = Math.Round(e.NewValue).ToString();

            // 2. Применяем масштаб к фигуре (ApplyScale сам обновит SizeX и SizeY)
            ApplyScale(e.NewValue);

            _isUpdating = false;
        }
        private void GlobalSizeBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdating || _selectedElement == null) return;

            if (double.TryParse(GlobalSizeBox.Text, out double val))
            {
                if (val >= 1 && val <= 100) // Ограничиваем ввод от 1 до 100
                {
                    _isUpdating = true;
                    GlobalSizeSlider.Value = val;
                    ApplyScale(val);
                    _isUpdating = false;
                }
            }
        }
        private void ApplyScale(double scaleValue)
        {
            if (_selectedElement == null) return;
            if (_selectedElement.Tag is ShapeData data)
            {
                double currentMaxDim = Math.Max(data.SizeX, data.SizeY) * 2.0;
                if (currentMaxDim < 0.001) currentMaxDim = 1.0;

                double targetMaxDim = MIN_SHAPE_SIZE + (scaleValue - 1.0) / 99.0 * (MAX_SHAPE_SIZE - MIN_SHAPE_SIZE);
                double ratio = targetMaxDim / currentMaxDim;

                data.SizeX *= ratio;
                data.SizeY *= ratio;

                if (data.SideLengths != null)
                {
                    for (int i = 0; i < data.SideLengths.Length; i++) data.SideLengths[i] *= ratio;
                }

                // Лимит толщины
                double maxThick = Math.Max(1.0, Math.Min(50.0, targetMaxDim / 15.0));

                if (data.CurrentThicknesses != null)
                {
                    for (int i = 0; i < data.CurrentThicknesses.Length; i++)
                    {
                        if (data.CurrentThicknesses[i] > maxThick) data.CurrentThicknesses[i] = maxThick;
                    }
                }
                else
                {
                    var ellipse = _selectedElement.Children.OfType<Ellipse>().FirstOrDefault(e => e.Tag?.ToString() != "Anchor");
                    if (ellipse != null && ellipse.StrokeThickness > maxThick) ellipse.StrokeThickness = maxThick;
                }

                UpdatePolygonGeometry(_selectedElement);
                UpdateBoundingBox(_selectedElement);

                // --- ВЫЗОВ НАШИХ БЕЗОПАСНЫХ МЕТОДОВ ---
                SyncMenuLengths(data, -1);
                SyncMenuCoordinates(data);
                SyncMenuThicknesses(data);
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
    }
}