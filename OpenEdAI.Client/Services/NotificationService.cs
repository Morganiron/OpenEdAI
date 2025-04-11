namespace OpenEdAI.Client.Services
{
    public class NotificationService
    {
        public event Action<string>? OnNotify;

        public void Notify(string message)
        {
            OnNotify?.Invoke(message);
        }
    }
}
