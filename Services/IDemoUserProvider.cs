using MaintenanceSandbox.Models;

namespace MaintenanceSandbox.Services
{
    public interface IDemoUserProvider
    {
        /// Returns a DemoUser if the credentials are valid; otherwise null.
            DemoUser? ValidateUser(string email, string password);

            /// Returns a demo user matching the given role name; otherwise null.
            DemoUser? GetByRole(string role);

            /// Returns the current logged-in demo user (from HttpContext/User).
            DemoUser CurrentUser { get; }
    }
}


