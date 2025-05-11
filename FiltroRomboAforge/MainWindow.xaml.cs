using AForge.Imaging.Filters;
using Microsoft.Win32;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Png;
using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Image = SixLabors.ImageSharp.Image;
using System.Drawing;
using AForge.Imaging;

namespace FiltroRomboAforge
{
    public partial class MainWindow : Window
    {
        private string rutaImagenOriginal;
        private Image<Rgba32> imagenOriginal;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void BtnCargarImagen_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFile = new OpenFileDialog();
            openFile.Filter = "Archivos de imagen|*.png;*.jpg;*.jpeg;*.bmp";
            if (openFile.ShowDialog() == true)
            {
                rutaImagenOriginal = openFile.FileName;
                imagenOriginal = Image.Load<Rgba32>(rutaImagenOriginal);
                ImgOriginal.Source = CargarBitmap(imagenOriginal);
                ImgResultado.Source = null;
            }
        }

        private void BtnSoloVerdes_Click(object sender, RoutedEventArgs e)
        {
            if (imagenOriginal == null) return;

            // 1. Crear máscara verde (binaria)
            var mask = new Image<Rgba32>(imagenOriginal.Width, imagenOriginal.Height);
            for (int y = 0; y < imagenOriginal.Height; y++)
            {
                for (int x = 0; x < imagenOriginal.Width; x++)
                {
                    var pixel = imagenOriginal[x, y];
                    bool esVerde = pixel.G > 120 && pixel.R < 100 && pixel.B < 100;
                    mask[x, y] = esVerde ? new Rgba32(0, 0, 0) : new Rgba32(255, 255, 255);
                }
            }

            // 2. Convertir a Bitmap para usar AForge
            using var ms = new MemoryStream();
            mask.SaveAsBmp(ms);
            ms.Seek(0, SeekOrigin.Begin);
            System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(ms);

            // 3. Convertir a imagen en escala de grises
            Grayscale grayscale = new Grayscale(0.2125, 0.7154, 0.0721);
            Bitmap grayBmp = grayscale.Apply(bmp);

            // 4. Aplicar Closing (relleno de bordes) + Erosion (suaviza bordes)
            Closing closeFilter = new Closing();
            Bitmap closed = closeFilter.Apply(grayBmp);
            Erosion erosion = new Erosion();
            Bitmap eroded = erosion.Apply(closed);

            // 5. Aplicar binarización con umbral automático
            Threshold threshold = new Threshold(100);
            threshold.ApplyInPlace(eroded);

            // 6. Contar blobs (figuras negras)
            BlobCounter blobCounter = new BlobCounter
            {
                FilterBlobs = true,
                MinWidth = 10,
                MinHeight = 10,
                ObjectsOrder = ObjectsOrder.Size
            };
            blobCounter.ProcessImage(eroded);
            var blobs = blobCounter.GetObjectsInformation();

            // 7. Construir nueva imagen resaltando solo zonas verdes originales
            var finalImage = imagenOriginal.Clone();
            for (int y = 0; y < finalImage.Height; y++)
            {
                for (int x = 0; x < finalImage.Width; x++)
                {
                    var c = eroded.GetPixel(x, y);
                    if (c.R == 0)
                    {
                        // Mantener color verde original
                    }
                    else
                    {
                        finalImage[x, y] = new Rgba32(255, 255, 255);
                    }
                }
            }

            // 8. Mostrar resultado y conteo
            ImgResultado.Source = CargarBitmap(finalImage);
            LblConteo.Content = $"Rombos verdes detectados: {blobs.Length}";
        }




        private BitmapSource CargarBitmap(Image<Rgba32> image)
        {
            using var ms = new MemoryStream();
            image.SaveAsBmp(ms);
            ms.Seek(0, SeekOrigin.Begin);

            var bitmap = new System.Windows.Media.Imaging.BmpBitmapDecoder(
                ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            return bitmap.Frames[0];
        }


        private void BtnRuidoPimienta_Click(object sender, RoutedEventArgs e)
        {
            if (imagenOriginal == null) return;

            // 1. Convertir imagen a Bitmap para AForge
            using var ms = new MemoryStream();
            imagenOriginal.SaveAsBmp(ms);
            ms.Seek(0, SeekOrigin.Begin);
            Bitmap bmp = new Bitmap(ms);

            // 2. Convertir a escala de grises
            Grayscale grayFilter = new Grayscale(0.2125, 0.7154, 0.0721);
            Bitmap grayBmp = grayFilter.Apply(bmp);

            // 3. Invertir la imagen: así el ruido negro se vuelve blanco y puede contarse como blobs
            Invert invertFilter = new Invert();
            invertFilter.ApplyInPlace(grayBmp);

            // 4. Aplicar umbral para binarizar
            Threshold threshold = new Threshold(50); // Ajusta si es necesario
            threshold.ApplyInPlace(grayBmp);

            // 5. Detectar blobs
            BlobCounter blobCounter = new BlobCounter
            {
                FilterBlobs = true,
                MinWidth = 1,
                MinHeight = 1,
                ObjectsOrder = ObjectsOrder.None
            };
            blobCounter.ProcessImage(grayBmp);
            var blobs = blobCounter.GetObjectsInformation();

            // 6. Clonar la imagen original
            var resultado = imagenOriginal.Clone();

            int eliminados = 0;

            // 7. Eliminar blobs pequeños
            foreach (var blob in blobs)
            {
                int area = blob.Rectangle.Width * blob.Rectangle.Height;
                if (area < 200) // Umbral ajustable
                {
                    eliminados++;
                    for (int y = blob.Rectangle.Top; y < blob.Rectangle.Bottom; y++)
                    {
                        for (int x = blob.Rectangle.Left; x < blob.Rectangle.Right; x++)
                        {
                            if (x >= 0 && y >= 0 && x < resultado.Width && y < resultado.Height)
                            {
                                resultado[x, y] = new Rgba32(255, 255, 255); // Pintamos blanco
                            }
                        }
                    }
                }
            }

            // 8. Mostrar resultado
            ImgResultado.Source = CargarBitmap(resultado);
            LblConteo.Content = $"Ruido pimienta eliminado: {eliminados}";
        }




        private void BtnQuitarConexiones_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Esta función aún no está implementada.");
        }

        private void BtnSoloGrandes_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Esta función aún no está implementada.");
        }
    }
}
