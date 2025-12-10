using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;



namespace LabelSnip
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        public static extern int StrCmpLogicalW(string x, string y);


        private List<string> _imageFiles;
        private int _currentIndex;
        private string _imagesFolderPath;
        private string _labelsFolderPath;
        private string _saveFolderPath;
        private string _deleteImagePath;
        private BitmapImage _bitmapImage;

        private Point? _startPoint = null; // 鼠标点击的起始点
        private Rectangle _currentRectangle; // 当前正在绘制的矩形
        private bool _isDrawing = false; // 是否正在绘制矩形

        public ICommand PrevImageCommand { get; }
        public ICommand NextImageCommand { get; }
        public ICommand DeleteCurrImageCommand { get; }
        public MainWindow()
        {
            InitializeComponent();
            _imageFiles = new List<string>();
            // 初始化命令并包装点击事件为不带参数的方法
            PrevImageCommand = new RelayCommand(_ => PrevImage_Click(null, null));
            NextImageCommand = new RelayCommand(_ => NextImage_Click(null, null));
            DeleteCurrImageCommand = new RelayCommand(_ => DeleteCurrImage());

            // 绑定数据上下文（通常是 ViewModel，但在这里直接绑定窗口本身）
            DataContext = this;
        }

        private void OpenImage_Click(object sender, RoutedEventArgs e)
        {
            // 创建 OpenFileDialog 实例
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择图片",
                Filter = "图片文件 (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|所有文件 (*.*)|*.*"
            };

            // 显示对话框并获取用户选择的文件
            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    // 获取选中的图片文件路径
                    string filePath = openFileDialog.FileName;
                    _imageFiles.Add(filePath);
                    _currentIndex = 0;
                    using (FileStream stream = File.OpenRead(filePath))
                    {
                        _bitmapImage = new BitmapImage();
                        _bitmapImage.BeginInit();
                        _bitmapImage.CacheOption = BitmapCacheOption.OnLoad; // 缓存到内存
                        _bitmapImage.StreamSource = stream;
                        _bitmapImage.EndInit();
                        _bitmapImage.Freeze(); // 可选：冻结对象以便跨线程使用
                    }
                    ImageBrush myImageBrush = new ImageBrush(_bitmapImage);
                    MainCanvas.Background = myImageBrush;
                    SnapCanvas.Children.Clear();
                    UpdateStatusBar();
                    // 读取标签文件
                    DisplayLabels(filePath);
                    if (string.IsNullOrEmpty(_saveFolderPath))
                    {
                        _saveFolderPath = System.IO.Path.GetDirectoryName(filePath);
                    }
                    if (string.IsNullOrEmpty(_labelsFolderPath))
                    {
                        _labelsFolderPath = System.IO.Path.GetDirectoryName(filePath);
                    }
                }
                catch (Exception ex)
                {
                    // 弹出错误信息
                    System.Windows.MessageBox.Show($"加载图片失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        private void OpenImageFolder_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new CommonOpenFileDialog())
            {
                dialog.IsFolderPicker = true;

                if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    _imagesFolderPath = dialog.FileName;
                    _imageFiles = Directory.GetFiles(_imagesFolderPath, "*.*", SearchOption.TopDirectoryOnly)
                                   .Where(file => file.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                                   file.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                                                   file.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                                   file.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase))
                                   .OrderBy(f => f, Comparer<string>.Create((x, y) => StrCmpLogicalW(x, y)))
                                   .ToList();
                    _currentIndex = 0;

                    if (_imageFiles != null && _imageFiles.Count > 0)
                    {
                        string filePath = _imageFiles[_currentIndex];
                        using (FileStream stream = File.OpenRead(filePath))
                        {
                            _bitmapImage = new BitmapImage();
                            _bitmapImage.BeginInit();
                            _bitmapImage.CacheOption = BitmapCacheOption.OnLoad; // 缓存到内存
                            _bitmapImage.StreamSource = stream;
                            _bitmapImage.EndInit();
                            _bitmapImage.Freeze(); // 可选：冻结对象以便跨线程使用
                        }
                        ImageBrush myImageBrush = new ImageBrush(_bitmapImage);
                        MainCanvas.Background = myImageBrush;
                        SnapCanvas.Children.Clear();
                        UpdateStatusBar();
                        // 读取标签文件
                        DisplayLabels(filePath);
                    }

                    if (string.IsNullOrEmpty(_saveFolderPath))
                    {
                        _saveFolderPath = _imagesFolderPath;
                    }
                    if (string.IsNullOrEmpty(_labelsFolderPath))
                    {
                        _labelsFolderPath = _imagesFolderPath;
                    }
                }
            }
        }

        private void OpenLabelsFolder_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new CommonOpenFileDialog())
            {
                dialog.IsFolderPicker = true;

                if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    _labelsFolderPath = dialog.FileName;
                    if (_imageFiles != null && _imageFiles.Count > 0)
                    {
                        string filePath = _imageFiles[_currentIndex];
                        // 读取标签文件
                        DisplayLabels(filePath);
                    }
                }
            }
        }

        // 定义颜色表（按 classId 循环使用）
        private List<Brush> classColors = new List<Brush>
        {
            Brushes.DarkGreen,
            Brushes.Red,
            Brushes.Lime,
            Brushes.Blue,
            Brushes.Orange,
            Brushes.Magenta,
            Brushes.Cyan,
            Brushes.Yellow,
            Brushes.Gray,
            Brushes.DarkCyan
        };
        private void DisplayLabels(string imagePath)
        {
            if (string.IsNullOrEmpty(_labelsFolderPath))
            {
                return; // 没有标签文件夹路径，跳过
            }
            // 读取标签文件
            string labelPath = System.IO.Path.Combine(_labelsFolderPath, GetLabelFileName(imagePath));
            // 显示标签
            if (!File.Exists(labelPath))
            {
                return;
            }
            SnapCanvas.Children.Clear();
            var shapes = ParseYoloLabel(labelPath);
            foreach (var shape in shapes)
            {
                int classId = shape.ClassId;
                shape.ShapeUI.Stroke = classColors[classId % classColors.Count]; ;
                shape.ShapeUI.StrokeThickness = 2;
                SnapCanvas.Children.Add(shape.ShapeUI);
            }
        }
        private enum YoloShapeType
        {
            Rectangle,
            Polygon
        }
        private class YoloShape
        {
            public int ClassId { get; set; }
            public Shape ShapeUI { get; set; }
            public YoloShapeType ShapeType { get; set; }
        }

        private List<YoloShape> ParseYoloLabel(string labelPath)
        {
            List<YoloShape> shapes = new List<YoloShape>();
            if (!File.Exists(labelPath))
                return shapes;

            double imgW = _bitmapImage.PixelWidth;
            double imgH = _bitmapImage.PixelHeight;

            double canvasW = MainCanvas.ActualWidth;
            double canvasH = MainCanvas.ActualHeight;

            double scaleX = canvasW / imgW;
            double scaleY = canvasH / imgH;

            var lines = File.ReadAllLines(labelPath);

            foreach (var line in lines)
            {
                var parts = line.Trim().Split(' ');
                if (parts.Length < 5)
                    continue;

                int classId = int.Parse(parts[0]);

                // -------------------------------------------------------------------
                // YOLO-DET: 5字段 → class cx cy w h
                // -------------------------------------------------------------------
                if (parts.Length == 5)
                {
                    double cx = double.Parse(parts[1]);
                    double cy = double.Parse(parts[2]);
                    double w = double.Parse(parts[3]);
                    double h = double.Parse(parts[4]);

                    double x = (cx - w / 2) * imgW * scaleX;
                    double y = (cy - h / 2) * imgH * scaleY;
                    double ww = w * imgW * scaleX;
                    double hh = h * imgH * scaleY;

                    Rectangle rect = new Rectangle
                    {
                        Width = ww,
                        Height = hh,
                        Stroke = Brushes.Red,
                        StrokeThickness = 2
                    };

                    Canvas.SetLeft(rect, x);
                    Canvas.SetTop(rect, y);

                    shapes.Add(new YoloShape
                    {
                        ClassId = classId,
                        ShapeUI = rect,
                        ShapeType = YoloShapeType.Rectangle
                    });

                    continue;
                }

                // -------------------------------------------------------------------
                // YOLO-OBB / YOLO-Seg: 多边形 → class x1 y1 x2 y2 ... xn yn
                // -------------------------------------------------------------------
                // parts.Length = 1 + 2*N
                if ((parts.Length - 1) % 2 == 0)
                {
                    Polygon polygon = new Polygon
                    {
                        Stroke = Brushes.Lime,
                        StrokeThickness = 2,
                        Fill = Brushes.Transparent
                    };

                    for (int i = 1; i < parts.Length; i += 2)
                    {
                        double px = double.Parse(parts[i]);
                        double py = double.Parse(parts[i + 1]);

                        double sx = px * imgW * scaleX;
                        double sy = py * imgH * scaleY;

                        polygon.Points.Add(new Point(sx, sy));
                    }

                    shapes.Add(new YoloShape
                    {
                        ClassId = classId,
                        ShapeUI = polygon,
                        ShapeType = YoloShapeType.Polygon
                    });
                }
            }

            return shapes;
        }


        private List<Tuple<int, Rectangle>> GetBboxes(string labelPath)
        {
            List<Tuple<int, Rectangle>> bboxes = new List<Tuple<int, Rectangle>>();
            if (!File.Exists(labelPath))
                return bboxes;

            var labels = File.ReadAllLines(labelPath);
            double imageOriginalWidth = _bitmapImage.PixelWidth;
            double imageOriginalHeight = _bitmapImage.PixelHeight;
            double canvasWidth = MainCanvas.ActualWidth;
            double canvasHeight = MainCanvas.ActualHeight;
            double scaleX = canvasWidth / imageOriginalWidth;
            double scaleY = canvasHeight / imageOriginalHeight;

            foreach (var label in labels)
            {
                var parts = label.Split(' ');
                if (parts.Length == 5)
                {
                    int classId = int.Parse(parts[0]);
                    double centerX = double.Parse(parts[1]);
                    double centerY = double.Parse(parts[2]);
                    double boxWidth = double.Parse(parts[3]);
                    double boxHeight = double.Parse(parts[4]);

                    double rectX = (centerX - boxWidth / 2) * imageOriginalWidth * scaleX;
                    double rectY = (centerY - boxHeight / 2) * imageOriginalHeight * scaleY;
                    double rectWidth = boxWidth * imageOriginalWidth * scaleX;
                    double rectHeight = boxHeight * imageOriginalHeight * scaleY;

                    var rect = new Rectangle
                    {
                        Width = rectWidth,
                        Height = rectHeight
                    };

                    Canvas.SetLeft(rect, rectX);
                    Canvas.SetTop(rect, rectY);

                    bboxes.Add(Tuple.Create(classId, rect));
                }
            }

            return bboxes;
        }

        private void PrevImage_Click(object sender, RoutedEventArgs e)
        {
            if (_imageFiles != null && _imageFiles.Count > 0)
            {
                int newIndex = _currentIndex - 1;
                if (newIndex >= 0)
                {
                    string filePath = _imageFiles[newIndex];
                    using (FileStream stream = File.OpenRead(filePath))
                    {
                        _bitmapImage = new BitmapImage();
                        _bitmapImage.BeginInit();
                        _bitmapImage.CacheOption = BitmapCacheOption.OnLoad; // 缓存到内存
                        _bitmapImage.StreamSource = stream;
                        _bitmapImage.EndInit();
                        _bitmapImage.Freeze(); // 可选：冻结对象以便跨线程使用
                    }
                    ImageBrush myImageBrush = new ImageBrush(_bitmapImage);
                    MainCanvas.Background = myImageBrush;
                    SnapCanvas.Children.Clear();
                    _currentIndex = newIndex;
                    UpdateStatusBar();
                    // 读取标签文件
                    DisplayLabels(filePath);
                }
            }
        }
        private void NextImage_Click(object sender, RoutedEventArgs e)
        {
            if (_imageFiles != null && _imageFiles.Count > 0)
            {
                int newIndex = _currentIndex + 1;
                if (newIndex < _imageFiles.Count)
                {
                    string filePath = _imageFiles[newIndex];
                    using (FileStream stream = File.OpenRead(filePath))
                    {
                        _bitmapImage = new BitmapImage();
                        _bitmapImage.BeginInit();
                        _bitmapImage.CacheOption = BitmapCacheOption.OnLoad; // 缓存到内存
                        _bitmapImage.StreamSource = stream;
                        _bitmapImage.EndInit();
                        _bitmapImage.Freeze(); // 可选：冻结对象以便跨线程使用
                    }
                    ImageBrush myImageBrush = new ImageBrush(_bitmapImage);
                    MainCanvas.Background = myImageBrush;
                    SnapCanvas.Children.Clear();
                    _currentIndex = newIndex;
                    UpdateStatusBar();
                    // 读取标签文件
                    DisplayLabels(filePath);
                }
            }
        }
        private void DeleteCurrImage()
        {
            if (_imageFiles != null && _imageFiles.Count > 0)
            {
                string imageFullPath = _imageFiles[_currentIndex];

                _imageFiles.RemoveAt(_currentIndex);
                if (_currentIndex == _imageFiles.Count)
                {
                    _currentIndex -= 1;
                }
                if (_currentIndex >= 0)
                {

                    string filePath = _imageFiles[_currentIndex];
                    using (FileStream stream = File.OpenRead(filePath))
                    {
                        _bitmapImage = new BitmapImage();
                        _bitmapImage.BeginInit();
                        _bitmapImage.CacheOption = BitmapCacheOption.OnLoad; // 缓存到内存
                        _bitmapImage.StreamSource = stream;
                        _bitmapImage.EndInit();
                        _bitmapImage.Freeze(); // 可选：冻结对象以便跨线程使用
                    }
                    ImageBrush myImageBrush = new ImageBrush(_bitmapImage);
                    MainCanvas.Background = myImageBrush;
                    SnapCanvas.Children.Clear();
                    UpdateStatusBar();
                    // 读取标签文件
                    DisplayLabels(filePath);
                }
                else
                {
                    ImageBrush myImageBrush = new ImageBrush();
                    MainCanvas.Background = myImageBrush;
                    SnapCanvas.Children.Clear();
                    ImageInfoText.Text = "";
                }

                DirectoryInfo directoryInfo = Directory.GetParent(_saveFolderPath);
                if (directoryInfo != null)
                {
                    _deleteImagePath = directoryInfo.FullName + "\\_delete";
                    Directory.CreateDirectory(_deleteImagePath);
                }

                string imageFileName = System.IO.Path.GetFileName(imageFullPath);
                string newImagePath = GetUniqueFileName(System.IO.Path.Combine(_deleteImagePath, imageFileName));
                File.Move(imageFullPath, newImagePath);

                string labelFullPath = System.IO.Path.Combine(_labelsFolderPath, GetLabelFileName(imageFullPath));
                string labelFileName = System.IO.Path.GetFileName(labelFullPath);
                string newLabelPath = GetUniqueFileName(System.IO.Path.Combine(_deleteImagePath, labelFileName));
                if (File.Exists(labelFullPath))
                {
                    File.Move(labelFullPath, newLabelPath);
                }
            }
        }

        private void ChangedSaveFolder_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new CommonOpenFileDialog())
            {
                dialog.IsFolderPicker = true;

                if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    _saveFolderPath = dialog.FileName;
                }
            }
        }


        private void MainCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            Point currentPosition = e.GetPosition(MainCanvas);

            if (_isDrawing && _startPoint.HasValue)
            {

                double x = Math.Min(_startPoint.Value.X, currentPosition.X);
                double y = Math.Min(_startPoint.Value.Y, currentPosition.Y);
                double width = Math.Abs(_startPoint.Value.X - currentPosition.X);
                double height = Math.Abs(_startPoint.Value.Y - currentPosition.Y);

                _currentRectangle.Width = width;
                _currentRectangle.Height = height;

                Canvas.SetLeft(_currentRectangle, x);
                Canvas.SetTop(_currentRectangle, y);
            }

            // 更新状态栏文本
            StatusBarText.Text = $"({(int)currentPosition.X}, {(int)currentPosition.Y})";

            // 绘制新的十字虚线
            DrawCrosshair(currentPosition);
        }
        private void MainCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Point clickPosition = e.GetPosition(MainCanvas);

                if (_startPoint == null)
                {
                    // 第一次点击，记录起始点
                    _startPoint = clickPosition;
                    _currentRectangle = new Rectangle
                    {
                        Stroke = Brushes.Red,
                        StrokeThickness = 2,
                        Fill = new SolidColorBrush(Color.FromArgb(128, 0, 0, 255))
                    };

                    // 将矩形添加到 Canvas
                    SnapCanvas.Children.Add(_currentRectangle);
                    _isDrawing = true;
                }
                else
                {
                    // 第二次点击，绘制矩形
                    Point endPoint = clickPosition;

                    double x = Math.Min(_startPoint.Value.X, endPoint.X);
                    double y = Math.Min(_startPoint.Value.Y, endPoint.Y);
                    double width = Math.Abs(_startPoint.Value.X - endPoint.X);
                    double height = Math.Abs(_startPoint.Value.Y - endPoint.Y);
                    if (width < 1 || height < 1)
                        return;

                    _currentRectangle.Width = width;
                    _currentRectangle.Height = height;

                    Canvas.SetLeft(_currentRectangle, x);
                    Canvas.SetTop(_currentRectangle, y);

                    // 重置状态
                    _startPoint = null;
                    _isDrawing = false;
                    SaveScreenshotAndLabel(_saveFolderPath);
                }
            }
        }

        private void DrawCrosshair(Point position)
        {
            CrosshairCanvas.Children.Clear();
            // 获取 Canvas 的宽高
            double canvasWidth = CrosshairCanvas.ActualWidth;
            double canvasHeight = CrosshairCanvas.ActualHeight;

            // 定义虚线样式
            var dashedLine = new DoubleCollection { 4, 4 }; // 4px 线段，4px 间隔

            // 绘制水平虚线
            var horizontalLine = new Line
            {
                X1 = 0,
                Y1 = position.Y,
                X2 = canvasWidth,
                Y2 = position.Y,
                Stroke = Brushes.Green,
                StrokeThickness = 1,
                StrokeDashArray = dashedLine
            };

            // 绘制垂直虚线
            var verticalLine = new Line
            {
                X1 = position.X,
                Y1 = 0,
                X2 = position.X,
                Y2 = canvasHeight,
                Stroke = Brushes.Green,
                StrokeThickness = 1,
                StrokeDashArray = dashedLine
            };

            // 将虚线添加到 Canvas
            CrosshairCanvas.Children.Add(horizontalLine);
            CrosshairCanvas.Children.Add(verticalLine);
        }
        private void UpdateStatusBar()
        {
            if (_imageFiles == null || _imageFiles.Count == 0)
                return;
            string fileName = _imageFiles[_currentIndex];
            int totalImages = _imageFiles.Count;
            int currentIndex = _currentIndex + 1; // 序号从1开始
            ImageInfoText.Text = $"Image {currentIndex}/{totalImages}: {fileName}";
        }



        private string GetLabelFileName(string imageFileName)
        {
            return System.IO.Path.GetFileNameWithoutExtension(imageFileName) + ".txt";
        }
        private void SaveScreenshotAndLabel(string targetPath)
        {
            if (_currentRectangle == null || _currentRectangle.Width <= 0 || _currentRectangle.Height <= 0)
                return;
            if (_imageFiles == null || _imageFiles.Count == 0)
                return;

            // 获取矩形区域的坐标和尺寸
            double rectX = Canvas.GetLeft(_currentRectangle);
            double rectY = Canvas.GetTop(_currentRectangle);
            double rectWidth = _currentRectangle.Width;
            double rectHeight = _currentRectangle.Height;

            string imageFileName = System.IO.Path.GetFileName(_imageFiles[_currentIndex]);
            string newImagePath = GetUniqueFileName(System.IO.Path.Combine(targetPath, imageFileName));
            // 截取图像
            SaveCanvasScreenshot(rectX, rectY, rectWidth, rectHeight, newImagePath);

            // 获取标签文件名
            string originalLabelFile = GetLabelFileName(imageFileName);
            string originalLabelPath = System.IO.Path.Combine(_labelsFolderPath, originalLabelFile);

            List<YoloShape> shapes = ParseYoloLabel(originalLabelPath);
            if (shapes == null || shapes.Count == 0)
                return;//没有标签
            List<string> newLabels = new List<string>();

            // 矩形区域内的比例
            double imageOriginalWidth = _bitmapImage.PixelWidth;
            double imageOriginalHeight = _bitmapImage.PixelHeight;

            foreach (var shape in shapes)
            {
                int classId = shape.ClassId;
                Shape shapeUI = shape.ShapeUI;
                YoloShapeType shapeType = shape.ShapeType;

                if (shapeType == YoloShapeType.Rectangle)
                {
                    Rectangle rect = (Rectangle)shapeUI;
                    // 计算中心点和宽高在矩形区域中的新坐标
                    double x1 = Canvas.GetLeft(rect);
                    double y1 = Canvas.GetTop(rect);
                    double x2 = x1 + rect.Width;
                    double y2 = y1 + rect.Height;

                    double centerX = x1 + rect.Width / 2;
                    double centerY = y1 + rect.Height / 2;
                    double boxWidth = rect.Width;
                    double boxHeight = rect.Height;

                    // 将中心点和尺寸转换到矩形区域内
                    double newX1 = (x1 - rectX) / rectWidth;
                    double newY1 = (y1 - rectY) / rectHeight;
                    double newX2 = (x2 - rectX) / rectWidth;
                    double newY2 = (y2 - rectY) / rectHeight;


                    double newCenterX = (centerX - rectX) / rectWidth;
                    double newCenterY = (centerY - rectY) / rectHeight;
                    double newBoxWidth = boxWidth / rectWidth;
                    double newBoxHeight = boxHeight / rectHeight;

                    // 检查是否在矩形区域内
                    if (newX1 >= 0 && newX1 <= 1 && newY1 >= 0 && newY1 <= 1 && newX2 >= 0 && newX2 <= 1 && newY2 >= 0 && newY2 <= 1)
                    {
                        // 保存新的标签
                        newLabels.Add($"{classId} {newCenterX} {newCenterY} {newBoxWidth} {newBoxHeight}");
                    }
                    else if (newCenterX >= 0 && newCenterX <= 1 && newCenterY >= 0 && newCenterY <= 1)
                    {
                        newX1 = Math.Max(newX1, 0);
                        newY1 = Math.Max(newY1, 0);
                        newX2 = Math.Min(newX2, 1);
                        newY2 = Math.Min(newY2, 1);

                        newCenterX = (newX1 + newX2) / 2;
                        newCenterY = (newY1 + newY2) / 2;
                        newBoxWidth = newX2 - newX1;
                        newBoxHeight = newY2 - newY1;
                        newLabels.Add($"{classId} {newCenterX} {newCenterY} {newBoxWidth} {newBoxHeight}");
                    }
                }
                else
                {
                    Polygon poly = (Polygon)shapeUI;
                    List<(double, double)> newPts = new List<(double, double)>();

                    foreach (var pt in poly.Points)
                    {
                        double px = pt.X;
                        double py = pt.Y;

                        // 转换为相对截图的坐标
                        double nx = (px - rectX) / rectWidth;
                        double ny = (py - rectY) / rectHeight;
                        newPts.Add((nx, ny));
                    }
                    bool anyOutSide = false;
                    foreach (var (nx, ny) in newPts)
                    {
                        if (nx < 0 || nx > 1 || ny < 0 || ny > 1)
                        {
                            anyOutSide = true;
                            break;
                        }
                    }

                    if (anyOutSide)
                        continue; // 有任意一个点超出就舍弃这个多边形

                    StringBuilder sb = new StringBuilder();
                    sb.Append(shape.ClassId);
                    foreach (var (nx, ny) in newPts)
                        sb.Append($" {nx} {ny}");

                    newLabels.Add(sb.ToString());
                }


            }

            // 保存新的标签文件
            string newLabelFileName = GetLabelFileName(newImagePath);
            string newLabelPath = System.IO.Path.Combine(targetPath, newLabelFileName);
            File.WriteAllLines(newLabelPath, newLabels);
        }
        private void SaveScreenshotAndLabel_bak(string targetPath)
        {
            if (_currentRectangle == null || _currentRectangle.Width <= 0 || _currentRectangle.Height <= 0)
                return;
            if (_imageFiles == null || _imageFiles.Count == 0)
                return;

            // 获取矩形区域的坐标和尺寸
            double rectX = Canvas.GetLeft(_currentRectangle);
            double rectY = Canvas.GetTop(_currentRectangle);
            double rectWidth = _currentRectangle.Width;
            double rectHeight = _currentRectangle.Height;

            string imageFileName = System.IO.Path.GetFileName(_imageFiles[_currentIndex]);
            string newImagePath = GetUniqueFileName(System.IO.Path.Combine(targetPath, imageFileName));
            // 截取图像
            SaveCanvasScreenshot(rectX, rectY, rectWidth, rectHeight, newImagePath);

            // 获取标签文件名
            string originalLabelFile = GetLabelFileName(imageFileName);
            string originalLabelPath = System.IO.Path.Combine(_labelsFolderPath, originalLabelFile);

            List<Tuple<int, Rectangle>> bboxes = GetBboxes(originalLabelPath); // 读取标签文件中的目标
            if (bboxes == null || bboxes.Count == 0)
                return;//没有标签
            List<string> newLabels = new List<string>();

            // 矩形区域内的比例
            double imageOriginalWidth = _bitmapImage.PixelWidth;
            double imageOriginalHeight = _bitmapImage.PixelHeight;

            foreach (var bbox in bboxes)
            {
                int classId = bbox.Item1;
                Rectangle rect = bbox.Item2;

                // 计算中心点和宽高在矩形区域中的新坐标
                double x1 = Canvas.GetLeft(rect);
                double y1 = Canvas.GetTop(rect);
                double x2 = x1 + rect.Width;
                double y2 = y1 + rect.Height;

                double centerX = x1 + rect.Width / 2;
                double centerY = y1 + rect.Height / 2;
                double boxWidth = rect.Width;
                double boxHeight = rect.Height;

                // 将中心点和尺寸转换到矩形区域内
                double newX1 = (x1 - rectX) / rectWidth;
                double newY1 = (y1 - rectY) / rectHeight;
                double newX2 = (x2 - rectX) / rectWidth;
                double newY2 = (y2 - rectY) / rectHeight;


                double newCenterX = (centerX - rectX) / rectWidth;
                double newCenterY = (centerY - rectY) / rectHeight;
                double newBoxWidth = boxWidth / rectWidth;
                double newBoxHeight = boxHeight / rectHeight;

                // 检查是否在矩形区域内
                if (newX1 >= 0 && newX1 <= 1 && newY1 >= 0 && newY1 <= 1 && newX2 >= 0 && newX2 <= 1 && newY2 >= 0 && newY2 <= 1)
                {
                    // 保存新的标签
                    newLabels.Add($"{classId} {newCenterX} {newCenterY} {newBoxWidth} {newBoxHeight}");
                }
                else if (newCenterX >= 0 && newCenterX <= 1 && newCenterY >= 0 && newCenterY <= 1)
                {
                    newX1 = Math.Max(newX1, 0);
                    newY1 = Math.Max(newY1, 0);
                    newX2 = Math.Min(newX2, 1);
                    newY2 = Math.Min(newY2, 1);

                    newCenterX = (newX1 + newX2) / 2;
                    newCenterY = (newY1 + newY2) / 2;
                    newBoxWidth = newX2 - newX1;
                    newBoxHeight = newY2 - newY1;
                    newLabels.Add($"{classId} {newCenterX} {newCenterY} {newBoxWidth} {newBoxHeight}");
                }
            }

            // 保存新的标签文件
            string newLabelFileName = GetLabelFileName(newImagePath);
            string newLabelPath = System.IO.Path.Combine(targetPath, newLabelFileName);
            File.WriteAllLines(newLabelPath, newLabels);
        }
        private void SaveCanvasScreenshot(double x, double y, double width, double height, string newImagePath)
        {
            // 创建一个 RenderTargetBitmap，用于渲染 Canvas 的特定区域
            RenderTargetBitmap renderBitmap = new RenderTargetBitmap(
                (int)width,
                (int)height,
                96, 96,
                PixelFormats.Pbgra32  // 使用 Pbgra32 来包含 RGB 数据
            );

            // 创建一个新的 DrawingVisual
            DrawingVisual drawingVisual = new DrawingVisual();
            using (DrawingContext drawingContext = drawingVisual.RenderOpen())
            {
                // 绘制 Canvas 的指定区域
                drawingContext.DrawRectangle(
                    new VisualBrush(MainCanvas),
                    null,
                    new Rect(-x, -y, MainCanvas.ActualWidth, MainCanvas.ActualHeight)
                );
            }

            // 渲染 DrawingVisual 到 RenderTargetBitmap
            renderBitmap.Render(drawingVisual);

            // 创建一个 JpegBitmapEncoder 用于保存图像
            JpegBitmapEncoder encoder = new JpegBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(renderBitmap));

            // 将图像保存到目标路径
            using (FileStream fileStream = new FileStream(newImagePath, FileMode.Create))
            {
                encoder.Save(fileStream);
            }
        }
        public static string GetUniqueFileName(string fileName)
        {
            // 分离文件名和扩展名
            string directory = System.IO.Path.GetDirectoryName(fileName);
            string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(fileName);
            string extension = System.IO.Path.GetExtension(fileName);

            // 生成新的文件名
            int counter = 1;
            string newFileName;
            do
            {
                // 修改文件名，添加后缀
                newFileName = System.IO.Path.Combine(directory, $"{fileNameWithoutExtension}-{counter}{extension}");
                counter++;
            } while (File.Exists(newFileName));

            return newFileName;
        }
    }
}
