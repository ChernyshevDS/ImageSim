namespace ImageSim.ViewModels
{
    public interface IExternalProgressIndicator
    {
        void SetState(ExternalIndicatorState state);
        void SetProgress(double value);
    }
}
