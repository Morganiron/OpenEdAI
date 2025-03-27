namespace OpenEdAI.Client.Services
{
    public class LoadingService
    {
        public event Action OnLoadingChanged;

        private int _activeOperations = 0;
        public bool IsLoading => _activeOperations > 0;

        public void Show()
        {
            _activeOperations++;
            Notify();
        }

        public void Hide()
        {
            if (_activeOperations > 0)
            {
                _activeOperations--;
            }
            Notify();
        }

        public void Notify()
        {
            OnLoadingChanged?.Invoke();
        }
    }
}
