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
        // --- Переменные для модального окна и интерактивного создания ---
        private string _pendingShapeType;
        private int _pendingSidesCount;
        private List<ComboBox> _creationColorCombos = new List<ComboBox>();
        private List<TextBox> _creationThickBoxes = new List<TextBox>();
        private List<Slider> _creationThickSliders = new List<Slider>();

        private bool _isInteractiveCreation = false;
        private bool _interactiveFromCenter = true;
        private Point _interactiveStartPoint;
        private Rectangle _interactivePreviewBox;
        // --- Переменные для контроля масштаба и толщины ---
        private const double MIN_SHAPE_SIZE = 20.0;   // Минимальный размер (масштаб 1)
        private const double MAX_SHAPE_SIZE = 1000.0; // Максимальный размер (масштаб 100)
        private Dictionary<int, Slider> _activeThickSliders = new Dictionary<int, Slider>();
        private Dictionary<int, TextBox> _activeThickBoxes = new Dictionary<int, TextBox>();
        // --- Переменные для перетаскивания вершин и масштабирования ---
        private int _draggedVertexIndex = -1;
        private string _draggedResizeHandle = null;
        private Point _resizeStartMouse;
        private double _resizeStartSizeX;
        private double _resizeStartSizeY;
        private double _resizeStartLeft;
        private double _resizeStartTop;
        private double _resizeStartWidth;
        private double _resizeStartHeight;
        private double _resizeMinX;
        private double _resizeMaxX;
        private double _resizeMinY;
        private double _resizeMaxY;
        private Dictionary<int, TextBox> _coordXBoxes = new Dictionary<int, TextBox>();
        private Dictionary<int, TextBox> _coordYBoxes = new Dictionary<int, TextBox>();

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


        // Вспомогательный метод для чтения выбранных цветов и толщин
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

        // --- Управление размером фигуры ---
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
        // МЕНЮ УПРАВЛЕНИЯ ЯКОРЕМ И ВСЕМИ СТОРОНАМИ
        // МЕНЮ УПРАВЛЕНИЯ ЯКОРЕМ И ВСЕМИ СТОРОНАМИ
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



        // ПОЛНОСТЬЮ НОВЫЙ МЕТОД ГЕНЕРАЦИИ МЕНЮ СОЗДАНИЯ (с ползунками и + -)
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
        // ВАЛИДАЦИЯ ТЕКСТА
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
        // КНОПКА СЛУЧАЙНЫХ ПАРАМЕТРОВ
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
        // КНОПКА СЛУЧАЙНЫХ ПАРАМЕТРОВ ДЛЯ УЖЕ СУЩЕСТВУЮЩЕЙ ФИГУРЫ
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
        // --- Логика перетаскивания (MouseDown, MouseMove, MouseUp) ---
        private void MainCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 1. РЕЖИМ РИСОВАНИЯ ЛОМАНОЙ (Отрезки)
            if (_isDrawingMode)
            {
                Point p = e.GetPosition(MainCanvas);
                if (_drawingPoints.Count > 0 && Keyboard.IsKeyDown(Key.LeftShift)) p = GetSnappedPoint(_drawingPoints.Last(), p);

                _drawingPoints.Add(p);

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
                    Line solidLine = new Line { X1 = _drawingPoints[_drawingPoints.Count - 2].X, Y1 = _drawingPoints[_drawingPoints.Count - 2].Y, X2 = p.X, Y2 = p.Y, Stroke = Brushes.Black, StrokeThickness = 2, Tag = "DrawingTemp" };
                    MainCanvas.Children.Add(solidLine);
                    _previewLine.X1 = p.X; _previewLine.Y1 = p.Y;
                }
                e.Handled = true;
                return; // Обязательно выходим!
            }

            // 2. ИНТЕРАКТИВНОЕ СОЗДАНИЕ ФИГУРЫ МЫШЬЮ
            if (_isInteractiveCreation)
            {
                _interactiveStartPoint = e.GetPosition(MainCanvas);
                _interactivePreviewBox = new Rectangle
                {
                    Stroke = Brushes.Cyan,
                    StrokeThickness = 2,
                    StrokeDashArray = new DoubleCollection { 4, 4 },
                    Fill = new SolidColorBrush(Color.FromArgb(30, 0, 255, 255))
                };
                Canvas.SetLeft(_interactivePreviewBox, _interactiveStartPoint.X);
                Canvas.SetTop(_interactivePreviewBox, _interactiveStartPoint.Y);
                MainCanvas.Children.Add(_interactivePreviewBox);

                MainCanvas.CaptureMouse(); // Захватываем мышь
                e.Handled = true;
                return; // Важно выйти, чтобы клик не ушел в "пустоту"
            }

            // 3. КЛИК ПО ПУСТОМУ ХОЛСТУ (Снятие выделения)
            if (e.Source == MainCanvas)
            {
                DeselectAll();
                return;
            }
            // 3.5. НОВОЕ: КЛИК ПО ВЕРШИНЕ (Vertex)
            if (e.Source is Ellipse elV && elV.Tag?.ToString().StartsWith("Vertex_") == true)
            {
                _draggedVertexIndex = int.Parse(elV.Tag.ToString().Split('_')[1]);
                elV.CaptureMouse();
                e.Handled = true;
                return;
            }

            // 3.6. НОВОЕ: КЛИК ПО РАМКЕ РАЗМЕРА (Resize)
            if (e.Source is Rectangle rh && rh.Tag?.ToString().StartsWith("Resize_") == true)
            {
                _draggedResizeHandle = rh.Tag.ToString();
                _resizeStartMouse = e.GetPosition(MainCanvas);

                ShapeData data = _selectedElement.Tag as ShapeData;
                _resizeStartSizeX = data.SizeX; _resizeStartSizeY = data.SizeY;
                _resizeStartLeft = Canvas.GetLeft(_selectedElement); _resizeStartTop = Canvas.GetTop(_selectedElement);

                double minX = double.MaxValue, maxX = double.MinValue, minY = double.MaxValue, maxY = double.MinValue;
                if (data.BasePoints != null)
                {
                    foreach (var bp in data.BasePoints)
                    {
                        double px = bp.X * data.SizeX + 500; double py = bp.Y * data.SizeY + 500;
                        if (px < minX) minX = px; if (px > maxX) maxX = px;
                        if (py < minY) minY = py; if (py > maxY) maxY = py;
                    }
                }
                else
                {
                    minX = 500 - data.SizeX; maxX = 500 + data.SizeX;
                    minY = 500 - data.SizeY; maxY = 500 + data.SizeY;
                }
                _resizeMinX = minX; _resizeMaxX = maxX; _resizeMinY = minY; _resizeMaxY = maxY;
                _resizeStartWidth = maxX - minX; _resizeStartHeight = maxY - minY;

                rh.CaptureMouse();
                e.Handled = true;
                return;
            }

            // 4. КЛИК ПО ТОЧКЕ ПРИВЯЗКИ (Anchor)
            if (e.Source is Ellipse el && el.Tag?.ToString() == "Anchor")
            {
                _draggedAnchor = el;
                _clickPosition = e.GetPosition(_selectedElement);
                el.CaptureMouse();

                // НОВОЕ: Включаем меню управления якорем и всеми сторонами
                _selectedSideIndex = -2;
                int sides = _selectedElement.Children.OfType<Polygon>().Count(p => p.Tag?.ToString() == "Stroke");
                if (sides == 0) sides = 1;
                GenerateSideMenu(_selectedElement, sides);

                e.Handled = true;
                return;
            }

            // 5. КЛИК ПО ФИГУРЕ ИЛИ ЕЕ СТОРОНЕ (Выделение и перетаскивание)
            if (e.Source is Shape clickedShape)
            {
                var parentCanvas = VisualTreeHelper.GetParent(clickedShape) as Canvas;

                if (parentCanvas != null && parentCanvas != MainCanvas)
                {
                    int clickedSide = -1;
                    if (clickedShape is Polygon poly && poly.Tag?.ToString() == "Stroke")
                    {
                        var strokes = parentCanvas.Children.OfType<Polygon>().Where(p => p.Tag?.ToString() == "Stroke").ToList();
                        clickedSide = strokes.IndexOf(poly);
                    }

                    if (_selectedElement != parentCanvas || _selectedSideIndex != clickedSide)
                    {
                        SelectShape(parentCanvas);
                        _selectedSideIndex = clickedSide;

                        int sides = parentCanvas.Children.OfType<Polygon>().Count(p => p.Tag?.ToString() == "Stroke");
                        if (sides == 0) sides = 1;
                        GenerateSideMenu(parentCanvas, sides);
                        UpdatePolygonGeometry(parentCanvas);
                    }

                    _draggedElement = parentCanvas;
                    _clickPosition = e.GetPosition(MainCanvas);
                    _draggedElement.CaptureMouse();
                    e.Handled = true;
                }
            }
        }

        private void MainCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            // 1. Движение при рисовании ломаной
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

            // 2. Движение при рисовании рамки новой фигуры
            if (_isInteractiveCreation && e.LeftButton == MouseButtonState.Pressed && _interactivePreviewBox != null)
            {
                Point current = e.GetPosition(MainCanvas);

                double width = Math.Abs(current.X - _interactiveStartPoint.X);
                double height = Math.Abs(current.Y - _interactiveStartPoint.Y);

                if (Keyboard.IsKeyDown(Key.LeftShift))
                {
                    double maxDim = Math.Max(width, height);
                    width = maxDim;
                    height = maxDim;
                }

                double left = _interactiveStartPoint.X;
                double top = _interactiveStartPoint.Y;

                if (_interactiveFromCenter)
                {
                    left = _interactiveStartPoint.X - width;
                    top = _interactiveStartPoint.Y - height;
                    width *= 2;
                    height *= 2;
                }
                else
                {
                    if (current.X < _interactiveStartPoint.X) left = _interactiveStartPoint.X - width;
                    if (current.Y < _interactiveStartPoint.Y) top = _interactiveStartPoint.Y - height;
                }

                Canvas.SetLeft(_interactivePreviewBox, left);
                Canvas.SetTop(_interactivePreviewBox, top);
                _interactivePreviewBox.Width = width;
                _interactivePreviewBox.Height = height;
                return;
            }
            // 2.5 ПЕРЕТАСКИВАНИЕ ВЕРШИНЫ
            if (_draggedVertexIndex != -1 && _selectedElement?.Tag is ShapeData vData)
            {
                Point p = e.GetPosition(_selectedElement);

                double dx = p.X - 500;
                double dy = p.Y - 500;
                double len = Math.Sqrt(dx * dx + dy * dy);
                double offset = 15;

                double realX = p.X; double realY = p.Y;
                if (len > offset)
                {
                    realX -= (dx / len) * offset;
                    realY -= (dy / len) * offset;
                }
                else
                {
                    realX = 500; realY = 500;
                }

                double normX = (realX - 500) / vData.SizeX;
                double normY = (realY - 500) / vData.SizeY;

                vData.BasePoints[_draggedVertexIndex] = new Point(normX, normY);
                if (vData.OriginalBasePoints != null) vData.OriginalBasePoints[_draggedVertexIndex] = new Point(normX, normY);

                UpdatePolygonGeometry(_selectedElement);

                // ТЕПЕРЬ ЭТИ ВЫЗОВЫ АБСОЛЮТНО БЕЗОПАСНЫ (Не вызовут смещения других точек)
                SyncMenuLengths(vData, -1);
                SyncMenuCoordinates(vData);

                return;
            }

            // 2.6 НОВОЕ: МАСШТАБИРОВАНИЕ ЗА РАМКУ (Идеальная математика с якорем противоположного края)
            if (_draggedResizeHandle != null && _selectedElement?.Tag is ShapeData rData)
            {
                Point current = e.GetPosition(MainCanvas);
                double dx = current.X - _resizeStartMouse.X;
                double dy = current.Y - _resizeStartMouse.Y;

                double ratioX = 1.0; double ratioY = 1.0;

                if (_draggedResizeHandle.Contains("E")) ratioX = (_resizeStartWidth + dx) / _resizeStartWidth;
                if (_draggedResizeHandle.Contains("W")) ratioX = (_resizeStartWidth - dx) / _resizeStartWidth;
                if (_draggedResizeHandle.Contains("S")) ratioY = (_resizeStartHeight + dy) / _resizeStartHeight;
                if (_draggedResizeHandle.Contains("N")) ratioY = (_resizeStartHeight - dy) / _resizeStartHeight;

                if (Keyboard.IsKeyDown(Key.LeftShift))
                {
                    double maxRatio = Math.Max(ratioX, ratioY);
                    if (_draggedResizeHandle.Contains("N") || _draggedResizeHandle.Contains("S")) ratioX = ratioY;
                    else if (_draggedResizeHandle.Contains("W") || _draggedResizeHandle.Contains("E")) ratioY = ratioX;
                    else { ratioX = maxRatio; ratioY = maxRatio; }
                }

                if (_resizeStartWidth * ratioX < 10) ratioX = 10 / _resizeStartWidth;
                if (_resizeStartHeight * ratioY < 10) ratioY = 10 / _resizeStartHeight;

                rData.SizeX = _resizeStartSizeX * ratioX;
                rData.SizeY = _resizeStartSizeY * ratioY;

                double newLeft = _resizeStartLeft, newTop = _resizeStartTop;

                // X коррекция позиции
                if (_draggedResizeHandle.Contains("E")) newLeft = (_resizeStartLeft + _resizeMinX) - (500 + (_resizeMinX - 500) * ratioX);
                else if (_draggedResizeHandle.Contains("W")) newLeft = (_resizeStartLeft + _resizeMaxX) - (500 + (_resizeMaxX - 500) * ratioX);
                else if (ratioX != 1.0) { double mX = (_resizeMinX + _resizeMaxX) / 2; newLeft = (_resizeStartLeft + mX) - (500 + (mX - 500) * ratioX); }

                // Y коррекция позиции
                if (_draggedResizeHandle.Contains("S")) newTop = (_resizeStartTop + _resizeMinY) - (500 + (_resizeMinY - 500) * ratioY);
                else if (_draggedResizeHandle.Contains("N")) newTop = (_resizeStartTop + _resizeMaxY) - (500 + (_resizeMaxY - 500) * ratioY);
                else if (ratioY != 1.0) { double mY = (_resizeMinY + _resizeMaxY) / 2; newTop = (_resizeStartTop + mY) - (500 + (mY - 500) * ratioY); }

                Canvas.SetLeft(_selectedElement, newLeft);
                Canvas.SetTop(_selectedElement, newTop);

                // Динамический лимит толщины при масштабировании
                double maxDim = Math.Max(rData.SizeX, rData.SizeY) * 2.0;
                double maxThick = Math.Max(1.0, Math.Min(50.0, maxDim / 15.0));
                if (rData.CurrentThicknesses != null)
                {
                    for (int i = 0; i < rData.CurrentThicknesses.Length; i++)
                    {
                        if (rData.CurrentThicknesses[i] > maxThick) rData.CurrentThicknesses[i] = maxThick;
                    }
                }
                else
                {
                    var ellipse = _selectedElement.Children.OfType<Ellipse>().FirstOrDefault(x => x.Tag?.ToString() != "Anchor");
                    if (ellipse != null && ellipse.StrokeThickness > maxThick) ellipse.StrokeThickness = maxThick;
                }

                UpdatePolygonGeometry(_selectedElement);

                // --- БЕЗОПАСНО ОБНОВЛЯЕМ ВСЕ МЕНЮ СИНХРОННО ---
                SyncMenuLengths(rData, -1);
                SyncMenuCoordinates(rData);
                SyncMenuThicknesses(rData); // <--- Это корректно обновит ползунки толщины при масштабировании мышью!

                // Синхронизация слайдера глобального масштаба
                double scale1to100 = 1.0 + (maxDim - MIN_SHAPE_SIZE) / (MAX_SHAPE_SIZE - MIN_SHAPE_SIZE) * 99.0;
                if (scale1to100 > 100) scale1to100 = 100; if (scale1to100 < 1) scale1to100 = 1;

                _isUpdating = true;
                GlobalSizeSlider.Value = scale1to100;
                GlobalSizeBox.Text = Math.Round(scale1to100).ToString();
                _isUpdating = false;

                return;
            }

            // 3. Перетаскивание точки привязки (Anchor)
            if (_draggedAnchor != null && _selectedElement != null)
            {
                Point p = e.GetPosition(_selectedElement);
                Canvas.SetLeft(_draggedAnchor, p.X - _draggedAnchor.Width / 2);
                Canvas.SetTop(_draggedAnchor, p.Y - _draggedAnchor.Height / 2);

                if (_selectedElement.Tag is ShapeData data)
                    data.LocalAnchor = p;

                UpdateCoordinatesUI();
                int sides = _selectedElement.Children.OfType<Polygon>().Count(poly => poly.Tag?.ToString() == "Stroke");
                if (sides == 0) sides = 1;
                // GenerateSideMenu(_selectedElement, sides); // Убрано, чтобы ползунки не сбрасывались при каждом сдвиге мыши
            }
            // 4. Перетаскивание самой фигуры
            else if (_draggedElement != null)
            {
                Point p = e.GetPosition(MainCanvas);
                double dx = p.X - _clickPosition.X;
                double dy = p.Y - _clickPosition.Y;

                Canvas.SetLeft(_draggedElement, Canvas.GetLeft(_draggedElement) + dx);
                Canvas.SetTop(_draggedElement, Canvas.GetTop(_draggedElement) + dy);

                _clickPosition = p;
                UpdateCoordinatesUI();
            }
        }


        private void MainCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_draggedVertexIndex != -1)
            {
                if (e.Source is UIElement ui) ui.ReleaseMouseCapture();
                _draggedVertexIndex = -1;
                e.Handled = true;
            }
            if (_draggedResizeHandle != null)
            {
                if (e.Source is UIElement ui) ui.ReleaseMouseCapture();
                _draggedResizeHandle = null;
                e.Handled = true;
            }
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
            if (_isInteractiveCreation && _interactivePreviewBox != null)
            {
                MainCanvas.ReleaseMouseCapture();

                double finalW = _interactivePreviewBox.Width;
                double finalH = _interactivePreviewBox.Height;
                double left = Canvas.GetLeft(_interactivePreviewBox);
                double top = Canvas.GetTop(_interactivePreviewBox);

                MainCanvas.Children.Remove(_interactivePreviewBox);
                _interactivePreviewBox = null;
                _isInteractiveCreation = false;
                MainCanvas.Cursor = Cursors.Arrow;

                // Защита от случайного микро-клика
                if (finalW < 10 || finalH < 10) return;

                var (colors, thicks) = GetCreationProperties();
                Brush defaultFill = GetCreationFillColor();

                double sizeX = finalW / 2;
                double sizeY = finalH / 2;
                // Находим истинный центр прямоугольника
                Point centerPoint = new Point(left + sizeX - 500, top + sizeY - 500);

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
                    var visual = (Canvas)shape.CreateVisual(sizeX, sizeY);
                    Canvas.SetLeft(visual, centerPoint.X);
                    Canvas.SetTop(visual, centerPoint.Y);

                    UpdatePolygonGeometry(visual);
                    MainCanvas.Children.Add(visual);
                    SelectShape(visual);
                }
                return;
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

            var visual = (Canvas)shape.CreateVisual(size, size);

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
        // СОЗДАНИЕ МАРКЕРОВ ВЕРШИН И ИЗМЕНЕНИЯ РАЗМЕРА
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
        // 1. СИНХРОНИЗАЦИЯ ДЛИН СТОРОН
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

        // 2. СИНХРОНИЗАЦИЯ КООРДИНАТ ВЕРШИН
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

        // 3. НОВЫЙ МЕТОД: СИНХРОНИЗАЦИЯ ТОЛЩИНЫ (Лимиты + Значения)
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
    }
}