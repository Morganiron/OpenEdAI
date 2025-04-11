namespace OpenEdAI.Client.Services
{
    public class LogoutService
    {
        public bool IsLoggingOut { get; private set; }

        public void StartLogout() => IsLoggingOut = true;
        public void Reset() => IsLoggingOut = false;
    }
}
