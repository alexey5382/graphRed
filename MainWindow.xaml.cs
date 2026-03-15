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
        private void SelectShape(Canvas shapeContainer, bool multiSelect = false)
        {
            if (shapeContainer == null) return;

            if (!multiSelect && !Keyboard.IsKeyDown(Key.LeftShift)) DeselectAll();

            if (!_selectedElements.Contains(shapeContainer)) _selectedElements.Add(shapeContainer);

            _selectedElement = shapeContainer;
            _selectedSideIndex = -1;

            var bbox = shapeContainer.Children.OfType<Rectangle>().FirstOrDefault(r => r.Tag?.ToString() == "BoundingBox");
            if (bbox == null)
            {
                bbox = new Rectangle { Stroke = Brushes.Gray, StrokeThickness = 1, StrokeDashArray = new DoubleCollection { 5, 5 }, Tag = "BoundingBox", IsHitTestVisible = false };
                shapeContainer.Children.Add(bbox);
            }
            bbox.Visibility = Visibility.Visible;

            // --- ФИКС: Делаем точку привязки видимой при выделении! ---
            var anchor = shapeContainer.Children.OfType<Ellipse>().FirstOrDefault(e => e.Tag?.ToString() == "Anchor");
            if (anchor != null) anchor.Visibility = Visibility.Visible;

            CreateHandles(shapeContainer);
            UpdateBoundingBox(shapeContainer);

            _isUpdating = true;
            if (shapeContainer.Tag is ShapeData data)
            {
                Brush currentFill = null;
                if (data.BasePoints == null)
                {
                    var ellipse = shapeContainer.Children.OfType<Ellipse>().FirstOrDefault(e => e.Tag?.ToString() != "Anchor");
                    if (ellipse != null) currentFill = ellipse.Fill;
                }
                else
                {
                    var hitArea = shapeContainer.Children.OfType<Polygon>().FirstOrDefault(p => p.Tag?.ToString() == "HitArea");
                    if (hitArea != null) currentFill = hitArea.Fill;
                }

                if (currentFill != null)
                {
                    int fillIndex = _availableColors.FindIndex(b => b.ToString() == currentFill.ToString());
                    if (fillIndex == -1 && currentFill is SolidColorBrush scb)
                    {
                        fillIndex = _availableColors.FindIndex(b => b is SolidColorBrush ab && ab.Color.R == scb.Color.R && ab.Color.G == scb.Color.G && ab.Color.B == scb.Color.B);
                    }
                    if (fillIndex != -1) FillColorCombo.SelectedIndex = fillIndex;
                }

                double maxDim = Math.Max(data.SizeX, data.SizeY) * 2.0;
                if (maxDim < MIN_SHAPE_SIZE) maxDim = MIN_SHAPE_SIZE;
                if (maxDim > MAX_SHAPE_SIZE) maxDim = MAX_SHAPE_SIZE;
                double scale1to100 = 1.0 + (maxDim - MIN_SHAPE_SIZE) / (MAX_SHAPE_SIZE - MIN_SHAPE_SIZE) * 99.0;
                GlobalSizeSlider.Value = scale1to100;
                GlobalSizeBox.Text = Math.Round(scale1to100).ToString();
            }
            _isUpdating = false;

            UpdateGlobalSelectionUI();
            UpdateRightPanelUI();
        }

        private void DeselectAll()
        {
            foreach (var element in _selectedElements)
            {
                var bbox = element.Children.OfType<Rectangle>().FirstOrDefault(r => r.Tag?.ToString() == "BoundingBox");
                if (bbox != null) bbox.Visibility = Visibility.Collapsed;

                // --- ФИКС: Скрываем маркеры и артефакты подсветки ---
                var anchor = element.Children.OfType<Ellipse>().FirstOrDefault(e => e.Tag?.ToString() == "Anchor");
                if (anchor != null) anchor.Visibility = Visibility.Collapsed;

                var highlight = element.Children.OfType<Line>().FirstOrDefault(l => l.Tag?.ToString() == "SideHighlight");
                if (highlight != null) highlight.Visibility = Visibility.Collapsed;

                var handles = element.Children.OfType<FrameworkElement>()
                    .Where(x => x.Tag?.ToString().StartsWith("Vertex_") == true ||
                                x.Tag?.ToString().StartsWith("Resize_") == true).ToList();

                foreach (var h in handles) element.Children.Remove(h);
            }

            _selectedElements.Clear();
            _selectedElement = null;
            _selectedSideIndex = -1;

            if (_globalBoundingBox != null) _globalBoundingBox.Visibility = Visibility.Collapsed;
            if (_globalAnchor != null) _globalAnchor.Visibility = Visibility.Collapsed;

            SidePropertiesList.Children.Clear();
            UpdateRightPanelUI();
        }
        private void DeleteShape_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedElements.Count > 0)
            {
                // Удаляем все выделенные фигуры (одиночные или целые группы)
                foreach (var element in _selectedElements)
                {
                    MainCanvas.Children.Remove(element);
                }

                _selectedElements.Clear();
                _selectedElement = null;
                _selectedSideIndex = -1;

                if (_globalBoundingBox != null) _globalBoundingBox.Visibility = Visibility.Collapsed;
                if (_globalAnchor != null) _globalAnchor.Visibility = Visibility.Collapsed;

                // --- НОВОЕ: Проверяем, не осталось ли групп с 1 или 0 элементов ---
                CleanupEmptyGroups();

                SidePropertiesList.Children.Clear();
                UpdateRightPanelUI();
            }
        }

        // --- НОВЫЙ МЕТОД: Очистка расформированных групп ---
        private void CleanupEmptyGroups()
        {
            // Идем с конца списка, чтобы безопасно удалять элементы в процессе
            for (int i = _shapeGroups.Count - 1; i >= 0; i--)
            {
                var group = _shapeGroups[i];
                var shapesInGroup = MainCanvas.Children.OfType<Canvas>()
                    .Where(c => (c.Tag as ShapeData)?.GroupId == group.Id).ToList();

                // Если в группе осталась 1 фигура (или 0) - снимаем с неё статус группы
                if (shapesInGroup.Count <= 1)
                {
                    foreach (var s in shapesInGroup)
                    {
                        if (s.Tag is ShapeData d) d.GroupId = null;
                    }
                    _shapeGroups.RemoveAt(i);
                }
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