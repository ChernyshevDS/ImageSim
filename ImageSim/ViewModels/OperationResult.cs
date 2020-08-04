namespace ImageSim.ViewModels
{
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
}
