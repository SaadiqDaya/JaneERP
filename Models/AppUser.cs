namespace JaneERP.Models
{
    public class AppUser
    {
        public int       UserId       { get; set; }
        public string    Username     { get; set; } = "";
        public string    PasswordHash { get; set; } = "";
        public string    PasswordSalt { get; set; } = "";
        public string    Role         { get; set; } = "Viewer";
        public string    Permissions  { get; set; } = ""; // comma-separated areas for Editor role
        public string    Email        { get; set; } = "";
        public bool      IsActive     { get; set; } = true;
        public DateTime  CreatedAt    { get; set; }
        public DateTime? LastLoginAt       { get; set; }
        public int       FailedLoginCount  { get; set; } = 0;
        public DateTime? LockedUntil       { get; set; }
    }
}
