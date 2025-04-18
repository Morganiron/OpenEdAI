namespace OpenEdAI.Client.Services
{
    public class NotificationService
    {
        // This event is triggered when a notification is sent
        public event Action<string>? OnNotify;

        // This event is triggered when the user acknowledges a notification
        public event Action? OnAcknowledge;

        public Task NotifyAndAwait(string message)
        {
            var tcs = new TaskCompletionSource<bool>();

            // When the layout invokes Ackknowledge, we complete the task
            void Handler()
            {
                OnAcknowledge -= Handler;
                tcs.SetResult(true);
            }
            OnAcknowledge += Handler;
            // Trigger the display
            OnNotify?.Invoke(message);
            return tcs.Task;
        }
        // Called when the user acknowledges the notification
        public void Acknowledge()
        {
            OnAcknowledge?.Invoke();
        }
    }
}
