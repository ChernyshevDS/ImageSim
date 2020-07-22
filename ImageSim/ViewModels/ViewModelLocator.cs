using System;
using System.Collections.Generic;
using System.Text;
 
using GalaSoft.MvvmLight.Ioc;

namespace ImageSim.ViewModels
{
    class ViewModelLocator
    {
        static ViewModelLocator()
        {
            SimpleIoc.Default.Register<Services.IImageProvider, Services.FSImageProvider>();
            SimpleIoc.Default.Register<MainVM>();
        }

        public MainVM MainVM => SimpleIoc.Default.GetInstance<MainVM>();
    }
}
