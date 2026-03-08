using System.Security.Claims;
using MaintenanceSandbox.Models;

namespace MaintenanceSandbox.Services
{
    public class DemoUserProvider : IDemoUserProvider
    {
        private readonly IHttpContextAccessor _http;

        public DemoUserProvider(IHttpContextAccessor http)
        {
            _http = http;
        }

        // Hard-coded demo users
        private static readonly List<DemoUser> _users =
        [
            new DemoUser
            {
                Name = "Supervisor",
                Email = "supervisor@sentinel-demo.local",
                Password = "sentineldemo",
                Role = "Supervisor"
            },
            new DemoUser
            {
                Name = "Operator",
                Email = "operator@sentinel-demo.local",
                Password = "sentineldemo",
                Role = "Operator"
            },
            new DemoUser
            {
                Name = "Maintenance Tech",
                Email = "tech@sentinel-demo.local",
                Password = "sentineldemo",
                Role = "Tech"
            }
        ];

        public DemoUser? ValidateUser(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                return null;

            return _users.FirstOrDefault(u =>
                string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase) &&
                u.Password == password);
        }

        public DemoUser? GetByRole(string role) =>
            _users.FirstOrDefault(u => string.Equals(u.Role, role, StringComparison.OrdinalIgnoreCase));

        public DemoUser CurrentUser
        {
            get
            {
                var user = _http.HttpContext?.User;

                if (user?.Identity?.IsAuthenticated != true)
                {
                    // Fallback - shouldn't happen on [Authorize] actions
                    return new DemoUser
                    {
                        Name = "Guest",
                        Email = "",
                        Role = "Operator"
                    };
                }

                var email = user.Identity?.Name ?? "";
                var role = user.FindFirst(ClaimTypes.Role)?.Value ?? "Operator";

                // Try to map back to our demo list to get a nicer Name if possible
                var match = _users.FirstOrDefault(u =>
                    string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase));

                if (match is not null)
                    return match;

                // Default: use email as name
                return new DemoUser
                {
                    Name = string.IsNullOrWhiteSpace(email) ? "User" : email,
                    Email = email,
                    Role = role
                };
            }
        }
    }
}



