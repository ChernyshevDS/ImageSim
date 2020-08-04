using System;

namespace ImageSim.ViewModels
{
    public class ImageDetailsVM : FileDetailsVM
    {
        private int width;
        private int height;
        private bool isValid;
        private string format;

        public int Width { get => width; set => Set(ref width, value); }
        public int Height { get => height; set => Set(ref height, value); }
        public string Format { get => format; set => Set(ref format, value); }
        public bool IsValid { get => isValid; set => Set(ref isValid, value); }

        public ImageDetailsVM(string path) : base(path)
        {
            try
            {
                using var img = System.Drawing.Image.FromFile(FilePath);
                Width = img.Width;
                Height = img.Height;
                Format = img.RawFormat.ToString();
                IsValid = true;
            }
            catch (Exception)
            {
                Width = 0;
                Height = 0;
                Format = "Unknown format";
                IsValid = false;
            }
        }
    }
}
