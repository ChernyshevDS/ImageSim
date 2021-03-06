﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace ImageSim
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            Akavache.Registrations.Start("ImageSim");
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            this.Exit += App_Exit;
        }

        private void App_Exit(object sender, ExitEventArgs e)
        {
            Akavache.BlobCache.Shutdown().Wait();
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine(e.Exception.Message);
            System.Diagnostics.Debugger.Break();
            e.Handled = true;
        }
    }
}
