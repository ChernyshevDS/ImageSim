using GalaSoft.MvvmLight.Command;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ImageSim
{
    /// <summary>
    /// Логика взаимодействия для ProgressWindow.xaml
    /// </summary>
    public partial class ProgressWindow : Window
    {
        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register("Message", typeof(string), typeof(ProgressWindow),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty ProgressPercentageProperty =
            DependencyProperty.Register("ProgressPercentage", typeof(double), typeof(ProgressWindow),
                new PropertyMetadata(0.0));

        public static readonly DependencyProperty HasProgressPercentageProperty =
            DependencyProperty.Register("HasProgressPercentage", typeof(bool), typeof(ProgressWindow), new PropertyMetadata(true));

        public static readonly DependencyProperty OperationFinishedProperty =
            DependencyProperty.Register("OperationFinished", typeof(bool), typeof(ProgressWindow), 
                new PropertyMetadata(false, HandleOperationFinishedStatic));

        private static void HandleOperationFinishedStatic(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if(e.NewValue is bool finished && finished)
                ((ProgressWindow)d).HandleOperationFinished();
        }

        public static readonly DependencyProperty CanCancelProperty =
            DependencyProperty.Register("CanCancel", typeof(bool), typeof(ProgressWindow), 
                new PropertyMetadata(false, HandleCanCancelChanged));

        private static void HandleCanCancelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var pw = (ProgressWindow)d;
            var cmd = (RelayCommand)pw.CancelCommand;
            cmd.RaiseCanExecuteChanged();
        }

        public string Message
        {
            get { return (string)GetValue(MessageProperty); }
            set { SetValue(MessageProperty, value); }
        }

        public double ProgressPercentage
        {
            get { return (double)GetValue(ProgressPercentageProperty); }
            set { SetValue(ProgressPercentageProperty, value); }
        }

        public bool HasProgressPercentage
        {
            get { return (bool)GetValue(HasProgressPercentageProperty); }
            set { SetValue(HasProgressPercentageProperty, value); }
        }

        public bool OperationFinished
        {
            get { return (bool)GetValue(OperationFinishedProperty); }
            set { SetValue(OperationFinishedProperty, value); }
        }

        public bool CanCancel
        {
            get { return (bool)GetValue(CanCancelProperty); }
            set { SetValue(CanCancelProperty, value); }
        }

        private RelayCommand cancelCmd;
        public ICommand CancelCommand => cancelCmd ??= new RelayCommand(HandleCancel, HandleCanCancel);

        public IProgress<ProgressArgs> Progress { get; }

        public event EventHandler<EventArgs> CancelRequestedEvent;

        public ProgressWindow()
        {
            InitializeComponent();
            Progress = new Progress<ProgressArgs>(this.HandleReport);

            this.Closing += ProgressWindow_Closing;
        }

        private void RaiseCancelRequested()
        {
            CancelRequestedEvent?.Invoke(this, EventArgs.Empty);
        }

        private void ProgressWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if(!OperationFinished)
                e.Cancel = true;
        }

        private bool HandleCanCancel()
        {
            return CanCancel;
        }

        private void HandleCancel()
        {
            if(!OperationFinished && CanCancel)
                RaiseCancelRequested();
        }

        private void HandleOperationFinished()
        {
            this.Close();
        }

        public static OperationResult<T> RunTaskAsync<T>(Func<IProgress<ProgressArgs>, CancellationToken, Task<T>> task, 
            string title = null, bool canCancel = false)
        {
            using var tokenSource = new CancellationTokenSource();
            var pw = new ProgressWindow() { Owner = Application.Current.MainWindow, CanCancel = canCancel };

            if (!string.IsNullOrEmpty(title))
                pw.Title = title;

            OperationResult<T> result = new OperationResult<T>(true, default);

            pw.Loaded += async (s, e) =>
            {
                try
                {
                    var taskResult = await task(pw.Progress, tokenSource.Token);
                    result = new OperationResult<T>(false, taskResult);
                    pw.OperationFinished = true;
                }
                catch (OperationCanceledException)
                {
                    pw.Progress.Report(new ProgressArgs("Operation cancelled", 100));
                    pw.OperationFinished = true;
                }
            };
            pw.CancelRequestedEvent += (s, e) => tokenSource.Cancel();

            pw.ShowDialog();
            return result;
        }

        public void HandleReport(ProgressArgs value)
        {
            this.Message = value.Message;
            this.HasProgressPercentage = value.Percentage.HasValue;
            if(HasProgressPercentage)
                this.ProgressPercentage = value.Percentage.Value;
        }
    }

    public struct OperationResult<T>
    {
        public T Result { get; }
        public bool IsCancelled { get; }

        public OperationResult(bool isCancelled, T result)
        {
            IsCancelled = isCancelled;
            Result = result;
        }
    }

    public class ProgressArgs
    {
        public ProgressArgs(string message, double? percentage)
        {
            Percentage = percentage;
            Message = message;
        }

        public double? Percentage { get; }
        public string Message { get; }
    }
}
