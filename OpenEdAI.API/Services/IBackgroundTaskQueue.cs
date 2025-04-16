namespace OpenEdAI.API.Services
{
    public interface IBackgroundTaskQueue
    {
        void EnqueueBackgroundWorkItem(Func<CancellationToken, Task> workItem);
        Task<Func<CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken);
    }
}
