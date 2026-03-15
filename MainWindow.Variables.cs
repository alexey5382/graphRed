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
        // --- Переменные для мульти-выделения и Групп ---
        private List<Canvas> _selectedElements = new List<Canvas>(); // Теперь выделенных элементов может быть много!
        private bool _isSelecting = false;
        private Point _selectionStartPoint;
        private Rectangle _selectionBox;
        // --- Переменные для Групп и глобальной рамки ---
        private List<ShapeGroup> _shapeGroups = new List<ShapeGroup>();
        private Rectangle _globalBoundingBox;
        private Ellipse _globalAnchor;

        private readonly List<Brush> _availableColors = new List<Brush> {
            Brushes.Black, Brushes.Red, Brushes.Orange, Brushes.Yellow, Brushes.Green, Brushes.Blue, Brushes.Purple, Brushes.White
        };
    }
}
