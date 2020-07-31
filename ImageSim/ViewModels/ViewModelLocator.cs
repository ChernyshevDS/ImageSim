using System;
using System.Collections.Generic;
using System.Text;
 
using GalaSoft.MvvmLight.Ioc;
using ImageSim.Services;
using MahApps.Metro.Controls.Dialogs;

namespace ImageSim.ViewModels
{
    public class ViewModelLocator
    {
        static ViewModelLocator()
        {
            if (GalaSoft.MvvmLight.ViewModelBase.IsInDesignModeStatic)
            {
                SimpleIoc.Default.Register<IFileDataStorage, DummyStorage>();
                SimpleIoc.Default.Register<IFileService, DummyFileService>();
            }
            else 
            {
                var blob = Akavache.BlobCache.UserAccount;
                var storage = new PersistentStorage(blob);
                SimpleIoc.Default.Register<IFileDataStorage>(() => storage);
                SimpleIoc.Default.Register<IFileService, FileService>();
            }

            SimpleIoc.Default.Register<IDialogCoordinator>(() => DialogCoordinator.Instance);
            SimpleIoc.Default.Register<MainVM>();
            SimpleIoc.Default.Register<FileListVM>();
        }

        public MainVM MainVM => SimpleIoc.Default.GetInstance<MainVM>();
        public FileListVM FilesVM => SimpleIoc.Default.GetInstance<FileListVM>();
    }
}
