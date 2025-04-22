namespace OpenEdAI.Client.Services
{
    public class NotificationService
    {
        // This event is triggered when a notification is sent
        public event Action<string>? OnNotify;

        // Triggerred for OK/Cancel prompts
        public event Func<string, Task<bool>>? OnPrompt;

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

        // OK/Cancel prompt
        public async Task<bool> ConfirmAsync(string message)
        {
            if (OnPrompt != null)
            {
                return await OnPrompt.Invoke(message);
            }
            return false;
        }

        // This event is triggered when the user acknowledges a notification
        public event Action? OnAcknowledge;
        // Called when the user acknowledges the notification
        public void Acknowledge()
        {
            OnAcknowledge?.Invoke();
        }
    }
}
