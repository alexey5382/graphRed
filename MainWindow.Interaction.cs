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


    }
}
