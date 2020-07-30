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
                SimpleIoc.Default.Register<IFileRegistry, DummyFileRegistry>();
            }
            else 
            {
                var blob = Akavache.BlobCache.UserAccount;
                var storage = new PersistentStorage(blob);
                SimpleIoc.Default.Register<IFileDataStorage>(() => storage);
                SimpleIoc.Default.Register<IFileRegistry, FileRegistry>();
            }
            
            SimpleIoc.Default.Register<MainVM>();
            SimpleIoc.Default.Register<FileListVM>();
        }

        public MainVM MainVM => SimpleIoc.Default.GetInstance<MainVM>();
        public FileListVM FilesVM => SimpleIoc.Default.GetInstance<FileListVM>();
    }
}
