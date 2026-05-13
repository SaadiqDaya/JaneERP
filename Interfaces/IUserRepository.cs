using JaneERP.Models;

namespace JaneERP.Interfaces
{
    public interface IUserRepository
    {
        int          MaxLoginAttempts { get; }
        bool         HasAnyUsers();
        bool         UsernameExists(string username);
        AppUser?     GetByUsername(string username);
        void         CreateUser(string username, string password, string role = "Viewer", string email = "", string permissions = "");
        List<AppUser> GetAll(bool includeInactive = false);
        void         UpdateUser(AppUser user);
        void         SetPassword(int userId, string newPassword);
        AppUser?     Authenticate(string username, string password);
        void         UnlockUser(int userId);
    }
}
