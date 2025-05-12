using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using AForge.Imaging;
using AForge.Imaging.Filters;
using Microsoft.Win32;
using Point = System.Drawing.Point;
using DrawingImage = System.Drawing.Image;
using AForgeImage = AForge.Imaging.Image;

namespace FiltroRomboAforge
{
    public partial class MainWindow : Window
    {
        private Bitmap _originalBitmap;
        private Bitmap _processedBitmap;
        private static readonly Color[] DiamondColors =
        {
            Color.Blue, Color.Pink, Color.Orange, Color.Purple, Color.LightGreen
        };

        public MainWindow()
        {
            InitializeComponent();
        }

        #region Event Handlers
        private void BtnCargarImagen_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog { Filter = "Imágenes|*.jpg;*.png;*.bmp" };
            if (ofd.ShowDialog() == true)
            {
                _originalBitmap?.Dispose();
                _processedBitmap?.Dispose();

                _originalBitmap = LoadBitmap(ofd.FileName);
                ImgOriginal.Source = ConvertToBitmapImage(_originalBitmap);
                ImgResultado.Source = null;
                LblConteo.Content = "Rombos: 0";
            }
        }

        private void BtnSoloVerdes_Click(object sender, RoutedEventArgs e)
        {
            if (_originalBitmap == null) return;

            _processedBitmap?.Dispose();
            _processedBitmap = ExtractGreenChannel(_originalBitmap);
            ImgResultado.Source = ConvertToBitmapImage(_processedBitmap);
        }

        private void BtnRuidoPimienta_Click(object sender, RoutedEventArgs e)
        {
            if (_originalBitmap == null) return;

            _processedBitmap?.Dispose();
            _processedBitmap = RemovePepperNoise(_originalBitmap);
            ImgResultado.Source = ConvertToBitmapImage(_processedBitmap);
        }

        private void BtnQuitarConexiones_Click(object sender, RoutedEventArgs e)
        {
            if (_originalBitmap == null) return;

            _processedBitmap?.Dispose();
            _processedBitmap = RemoveConnections(_originalBitmap);
            ImgResultado.Source = ConvertToBitmapImage(_processedBitmap);
        }

        private void BtnSoloGrandes_Click(object sender, RoutedEventArgs e)
        {
            if (_originalBitmap == null) return;

            _processedBitmap?.Dispose();
            _processedBitmap = KeepMainDiamonds(_originalBitmap, out int count);
            ImgResultado.Source = ConvertToBitmapImage(_processedBitmap);
            LblConteo.Content = $"Rombos: {count}";
        }
        #endregion

        #region Core Image Processing
        private Bitmap LoadBitmap(string filePath)
        {
            using var temp = new Bitmap(filePath);
            return AForgeImage.Clone(temp, PixelFormat.Format24bppRgb);
        }

        private BitmapImage ConvertToBitmapImage(Bitmap bitmap)
        {
            using var memory = new MemoryStream();
            bitmap.Save(memory, ImageFormat.Png);
            memory.Position = 0;

            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.StreamSource = memory;
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.EndInit();
            bitmapImage.Freeze();

            return bitmapImage;
        }

        private Bitmap ExtractGreenChannel(Bitmap source)
        {
            var result = new Bitmap(source.Width, source.Height);

            for (int y = 0; y < source.Height; y++)
            {
                for (int x = 0; x < source.Width; x++)
                {
                    Color pixel = source.GetPixel(x, y);
                    result.SetPixel(x, y, (pixel.G > pixel.R * 1.3 && pixel.G > pixel.B * 1.3) ? pixel : Color.White);
                }
            }
            return result;
        }

        private Bitmap RemovePepperNoise(Bitmap source)
        {
            var grayscale = new Grayscale(0.2125, 0.7154, 0.0721).Apply(source);
            new Threshold(30).ApplyInPlace(grayscale);
            new Invert().ApplyInPlace(grayscale);

            var blobCounter = new BlobCounter
            {
                FilterBlobs = true,
                MinWidth = 3,
                MinHeight = 3
            };

            blobCounter.ProcessImage(grayscale);
            var result = (Bitmap)source.Clone();

            using (var g = Graphics.FromImage(result))
            {
                foreach (var blob in blobCounter.GetObjectsInformation())
                {
                    if (blob.Area <= 100)
                        g.FillRectangle(System.Drawing.Brushes.White, blob.Rectangle);
                }
            }

            grayscale.Dispose();
            return result;
        }

        public Bitmap RemoveConnections(Bitmap source)
        {
            // Configuración de parámetros
            const int kernelSize = 5;
            const int pepperThreshold = 20;

            // 1. Inicialización
            int width = source.Width;
            int height = source.Height;
            var result = new Bitmap(width, height);

            // Métodos locales para detección de colores
            bool IsGreen(Color c) => c.G > c.R * 1.5 && c.G > c.B * 1.5;
            bool IsRed(Color c) => c.R > c.G * 1.5 && c.R > c.B * 1.5;
            bool IsBlack(Color c) => c.R < 50 && c.G < 50 && c.B < 50;

            // 2. Identificación de regiones
            var (isBlack, isPepper) = IdentifyRegions(source, width, height, pepperThreshold, IsBlack);

            // 3. Procesamiento morfológico
            var finalMask = ApplyMorphologicalOperations(width, height, kernelSize, isBlack, isPepper);

            // 4. Construcción de la imagen resultante
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color pixel = source.GetPixel(x, y);

                    if (IsRed(pixel) || IsGreen(pixel) || finalMask[x, y])
                    {
                        result.SetPixel(x, y, pixel);
                    }
                    else
                    {
                        result.SetPixel(x, y, Color.White);
                    }
                }
            }

            return result;
        }

        private (bool[,] isBlack, bool[,] isPepper) IdentifyRegions(Bitmap source, int width, int height, int pepperThreshold, Func<Color, bool> isBlack)
        {
            bool[,] blackMap = new bool[width, height];
            bool[,] pepperMap = new bool[width, height];
            bool[,] visited = new bool[width, height];

            // Primera pasada: identificar píxeles negros
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    blackMap[x, y] = isBlack(source.GetPixel(x, y));
                }
            }

            // Segunda pasada: identificar ruido pimienta
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!visited[x, y] && blackMap[x, y])
                    {
                        var region = new List<Point>();
                        FloodFill(blackMap, visited, x, y, width, height, region);

                        if (region.Count <= pepperThreshold)
                        {
                            foreach (var p in region)
                            {
                                pepperMap[p.X, p.Y] = true;
                            }
                        }
                    }
                }
            }

            return (blackMap, pepperMap);
        }

        private bool[,] ApplyMorphologicalOperations(int width, int height, int kernelSize, bool[,] isBlack, bool[,] isPepper)
        {
            bool[,] mask = new bool[width, height];
            int radius = kernelSize / 2;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (isPepper[x, y])
                    {
                        mask[x, y] = true;
                        continue;
                    }

                    // Erosión
                    bool keep = true;
                    for (int dy = -radius; keep && dy <= radius; dy++)
                    {
                        for (int dx = -radius; keep && dx <= radius; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || nx >= width || ny < 0 || ny >= height || !isBlack[nx, ny] || isPepper[nx, ny])
                            {
                                keep = false;
                            }
                        }
                    }

                    // Dilatación
                    if (keep)
                    {
                        for (int dy = -radius; dy <= radius; dy++)
                        {
                            for (int dx = -radius; dx <= radius; dx++)
                            {
                                int nx = x + dx, ny = y + dy;
                                if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                                {
                                    mask[nx, ny] = true;
                                }
                            }
                        }
                    }
                }
            }

            return mask;
        }

        private void FloodFill(bool[,] map, bool[,] visited, int x, int y, int width, int height, List<Point> region)
        {
            var stack = new Stack<Point>();
            stack.Push(new Point(x, y));

            while (stack.Count > 0)
            {
                var p = stack.Pop();
                if (p.X < 0 || p.X >= width || p.Y < 0 || p.Y >= height || visited[p.X, p.Y] || !map[p.X, p.Y])
                    continue;

                visited[p.X, p.Y] = true;
                region.Add(p);

                // 4-vecinos (más eficiente para conexiones)
                stack.Push(new Point(p.X + 1, p.Y));
                stack.Push(new Point(p.X - 1, p.Y));
                stack.Push(new Point(p.X, p.Y + 1));
                stack.Push(new Point(p.X, p.Y - 1));
            }
        }

        private Bitmap KeepMainDiamonds(Bitmap source, out int count)
        {
            var grayscale = new Grayscale(0.2125, 0.7154, 0.0721).Apply(source);
            new Invert().ApplyInPlace(grayscale);
            new Threshold(60).ApplyInPlace(grayscale);

            var se = new short[5, 5];
            for (int i = 0; i < 5; i++)
                for (int j = 0; j < 5; j++)
                    se[i, j] = 1;

            var opened = new Opening(se).Apply(grayscale);
            var median = new Median(5).Apply(opened);

            var blobCounter = new BlobCounter
            {
                FilterBlobs = true,
                MinWidth = 20,
                MinHeight = 20,
                ObjectsOrder = ObjectsOrder.Size
            };

            blobCounter.ProcessImage(median);
            var blobs = blobCounter.GetObjectsInformation();

            var result = new Bitmap(source.Width, source.Height, PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(result))
            {
                g.Clear(Color.White);
                count = Math.Min(5, blobs.Length);

                for (int i = 0; i < count; i++)
                {
                    var rect = blobs[i].Rectangle;
                    var center = new Point(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2);

                    var points = new[]
                    {
                        new Point(center.X, center.Y - rect.Height/2),
                        new Point(center.X + rect.Width/2, center.Y),
                        new Point(center.X, center.Y + rect.Height/2),
                        new Point(center.X - rect.Width/2, center.Y)
                    };

                    using (var brush = new SolidBrush(DiamondColors[i]))
                        g.FillPolygon(brush, points);
                }
            }

            grayscale.Dispose();
            opened.Dispose();
            median.Dispose();

            return result;
        }
        #endregion
    }
} 