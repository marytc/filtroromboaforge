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

            var imagenFiltrada = imagenOriginal.Clone();

            var width = imagenFiltrada.Width;
            var height = imagenFiltrada.Height;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var pixel = imagenFiltrada[x, y];
                    if (!(pixel.G > 120 && pixel.R < 100 && pixel.B < 100))
                    {
                        imagenFiltrada[x, y] = new Rgba32(255, 255, 255); // Blanco
                    }
                }
            }

            ImgResultado.Source = CargarBitmap(imagenFiltrada);
        }


        private BitmapImage CargarBitmap(Image<Rgba32> image)
        {
            using (var memoryStream = new MemoryStream())
            {
                image.Save(memoryStream, new PngEncoder());
                memoryStream.Position = 0;
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = memoryStream;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
        }

        private void BtnRuidoPimienta_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Esta función aún no está implementada.");
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
