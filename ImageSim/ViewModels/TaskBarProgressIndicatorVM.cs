using System.Windows.Shell;
using GalaSoft.MvvmLight;

namespace ImageSim.ViewModels
{
    public class TaskBarProgressIndicatorVM : ViewModelBase, IExternalProgressIndicator
    {
        private TaskbarItemProgressState progressState = TaskbarItemProgressState.None;
        private double progress;

        public TaskbarItemProgressState ProgressState { get => progressState; private set => Set(ref progressState, value); }
        public double Progress { get => progress; private set => Set(ref progress, value); }

        public void SetProgress(double value)
        {
            Progress = value;
        }

        public void SetState(ExternalIndicatorState state)
        {
            switch (state)
            {
                case ExternalIndicatorState.None:           ProgressState = TaskbarItemProgressState.None; break;
                case ExternalIndicatorState.Normal:         ProgressState = TaskbarItemProgressState.Normal; break;
                case ExternalIndicatorState.Indeterminate:  ProgressState = TaskbarItemProgressState.Indeterminate; break;
                case ExternalIndicatorState.Paused:         ProgressState = TaskbarItemProgressState.Paused; break;
                case ExternalIndicatorState.Error:          ProgressState = TaskbarItemProgressState.Error; break;
                default:
                    break;
            }
        }
    }
}
