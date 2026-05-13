using Microsoft.Extensions.DependencyInjection;

namespace JaneERP.Infrastructure
{
    /// <summary>
    /// Static service locator for WinForms.
    /// WinForms creates forms with <c>new FormXxx()</c>, so we expose the container
    /// through a static accessor rather than constructor-injecting IServiceProvider
    /// into every form. All registrations are done once at startup via
    /// <see cref="ServiceRegistration.AddJaneERPServices"/>.
    /// </summary>
    public static class AppServices
    {
        private static IServiceProvider? _provider;

        /// <summary>Called once from Program.Main after building the service collection.</summary>
        internal static void Initialize(IServiceProvider provider)
            => _provider = provider;

        /// <summary>Resolves a required service. Throws if not registered or not initialized.</summary>
        public static T Get<T>() where T : notnull
        {
            if (_provider == null)
                throw new InvalidOperationException(
                    "AppServices has not been initialized. Call ServiceRegistration.Build() in Program.Main.");
            return _provider.GetRequiredService<T>();
        }

        /// <summary>Resolves an optional service; returns null if not registered.</summary>
        public static T? GetOptional<T>() where T : class
            => _provider?.GetService<T>();
    }
}
