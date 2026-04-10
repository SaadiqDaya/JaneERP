using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace JaneERP.Security
{
    public static class SecureStore
    {
        private static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "JaneERP");
        private static void EnsureFolder() => Directory.CreateDirectory(Folder);

        public static void SaveSecret(string name, string secret)
        {
            EnsureFolder();
            var path = Path.Combine(Folder, name + ".bin");
            var plain = Encoding.UTF8.GetBytes(secret ?? "");
            var encrypted = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(path, encrypted);
        }

        public static string? GetSecret(string name)
        {
            var path = Path.Combine(Folder, name + ".bin");
            if (!File.Exists(path)) return null;
            try
            {
                var encrypted = File.ReadAllBytes(path);
                var plain = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plain);
            }
            catch
            {
                return null;
            }
        }

        public static void DeleteSecret(string name)
        {
            var path = Path.Combine(Folder, name + ".bin");
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
