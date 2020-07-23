using System;
using System.Collections.Generic;
using System.Text;
 
using GalaSoft.MvvmLight.Ioc;
using ImageSim.Services;

namespace ImageSim.ViewModels
{
    class ViewModelLocator
    {
        static ViewModelLocator()
        {
            var blob = Akavache.BlobCache.UserAccount;
            var storage = new PersistentStorage(blob);

            SimpleIoc.Default.Register<IImageProvider, FSImageProvider>();
            SimpleIoc.Default.Register<IFileDataStorage>(() => storage);
            SimpleIoc.Default.Register<MainVM>();
        }

        public MainVM MainVM => SimpleIoc.Default.GetInstance<MainVM>();
    }
}
