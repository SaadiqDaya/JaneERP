using JaneERP.Models;

namespace JaneERP.Security
{
    /// <summary>Holds the currently logged-in user for the lifetime of the session.
    /// All reads/writes are lock-protected so background threads (e.g. SyncService) are safe.</summary>
    public static class AppSession
    {
        private static readonly object _lock = new();
        private static AppUser? _currentUser;

        public static AppUser? CurrentUser
        {
            get { lock (_lock) { return _currentUser; } }
        }

        public static void SetUser(AppUser user)
        {
            lock (_lock) { _currentUser = user; }
        }

        public static void ClearUser()
        {
            lock (_lock) { _currentUser = null; }
        }
    }
}
