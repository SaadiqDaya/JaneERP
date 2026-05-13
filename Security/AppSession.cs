using JaneERP.Models;

namespace JaneERP.Security
{
    /// <summary>Holds the currently logged-in user for the lifetime of the session.
    /// All reads/writes are lock-protected so background threads (e.g. SyncService) are safe.</summary>
    public static class AppSession
    {
        private static readonly object _lock = new();
        private static AppUser? _currentUser;
        private static DateTime _lastActivityTime = DateTime.Now;

        public static AppUser? CurrentUser
        {
            get { lock (_lock) { return _currentUser; } }
        }

        /// <summary>Tracks the last time the user interacted with any form (mouse or keyboard).</summary>
        public static DateTime LastActivityTime
        {
            get { lock (_lock) { return _lastActivityTime; } }
        }

        /// <summary>Called by the global message filter on any mouse/keyboard message.</summary>
        public static void UpdateActivityTime()
        {
            lock (_lock) { _lastActivityTime = DateTime.Now; }
        }

        public static void SetUser(AppUser user)
        {
            lock (_lock) { _currentUser = user; _lastActivityTime = DateTime.Now; }
        }

        public static void ClearUser()
        {
            lock (_lock) { _currentUser = null; }
        }
    }

    /// <summary>Application-wide message filter that updates <see cref="AppSession.LastActivityTime"/>
    /// on any mouse or keyboard activity across ALL open forms.</summary>
    internal sealed class GlobalActivityFilter : IMessageFilter
    {
        private const int WM_KEYDOWN     = 0x0100;
        private const int WM_KEYUP       = 0x0101;
        private const int WM_MOUSEMOVE   = 0x0200;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_MBUTTONDOWN = 0x0207;
        private const int WM_MOUSEWHEEL  = 0x020A;

        public bool PreFilterMessage(ref Message m)
        {
            switch (m.Msg)
            {
                case WM_KEYDOWN:
                case WM_KEYUP:
                case WM_MOUSEMOVE:
                case WM_LBUTTONDOWN:
                case WM_RBUTTONDOWN:
                case WM_MBUTTONDOWN:
                case WM_MOUSEWHEEL:
                    AppSession.UpdateActivityTime();
                    break;
            }
            return false; // never consume — just observe
        }
    }
}
