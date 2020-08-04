using GalaSoft.MvvmLight;
using System.Collections.Generic;
using System.IO;

namespace ImageSim.ViewModels
{
    public static class VMHelper
    {
        private static readonly HashSet<string> image_extensions = new HashSet<string>()
        {
            "JPG", "JPEG", "TIFF", "PNG", "BMP", "EMF", "EXIF", "ICO", "WMF"
        };

        public static bool IsImageExtension(string ext)
        {
            if (string.IsNullOrEmpty(ext))
                return false;
            return image_extensions.Contains(ext.TrimStart('.').ToUpperInvariant());
        }

        public static ViewModelBase GetDetailsVMByPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return new EmptyDetailsVM();
            var ext = Path.GetExtension(path);
            if (IsImageExtension(ext))
                return new ImageDetailsVM(path);
            else
                return new FileDetailsVM(path);
        }
    }
}
