using System;
using System.Collections.Generic;
using System.Text;
 
using GalaSoft.MvvmLight.Ioc;
using ImageSim.Services;

namespace ImageSim.ViewModels
{
    public class ViewModelLocator
    {
        static ViewModelLocator()
        {
            if (GalaSoft.MvvmLight.ViewModelBase.IsInDesignModeStatic)
            {
                SimpleIoc.Default.Register<IFileDataStorage, DummyStorage>();
                SimpleIoc.Default.Register<IImageProvider, DummyImageProvider>();
            }
            else 
            {
                var blob = Akavache.BlobCache.UserAccount;
                var storage = new PersistentStorage(blob);
                SimpleIoc.Default.Register<IFileDataStorage>(() => storage);
                SimpleIoc.Default.Register<IImageProvider, FSImageProvider>();
            }
            
            SimpleIoc.Default.Register<MainVM>();
        }

        public MainVM MainVM => SimpleIoc.Default.GetInstance<MainVM>();
    }
}
