namespace JaneERP.Security
{
    /// <summary>
    /// Roles: Admin (everything), Viewer (read-only), Editor (read + write in permitted areas).
    /// Areas: "Inventory", "SalesOrders", "Log"
    /// </summary>
    public static class PermissionHelper
    {
        public static bool IsAdmin() =>
            AppSession.CurrentUser?.Role == "Admin";

        public static bool CanEdit(string area)
        {
            var user = AppSession.CurrentUser;
            if (user == null)       return false;
            if (user.Role == "Admin")  return true;
            if (user.Role == "Viewer") return false;
            if (user.Role == "Editor")
            {
                var parts = (user.Permissions ?? "")
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                return parts.Any(p => p.Equals(area, StringComparison.OrdinalIgnoreCase));
            }
            return false;
        }
    }
}
