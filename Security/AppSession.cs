using JaneERP.Models;

namespace JaneERP.Security
{
    /// <summary>Holds the currently logged-in user for the lifetime of the session.</summary>
    public static class AppSession
    {
        public static AppUser? CurrentUser { get; private set; }
        public static void SetUser(AppUser user) => CurrentUser = user;
        public static void ClearUser()          => CurrentUser = null;
    }
}
