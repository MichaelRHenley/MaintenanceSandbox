namespace MaintenanceSandbox.Models
{
    
        // Simple "Identity-lite" user model
        public class AppUser
        {
            public int Id { get; set; }

            // Display name shown in UI
            public string Name { get; set; } = "";

            // Unique login identifier
            public string Email { get; set; } = "";

            // Hashed password (we’ll handle hashing when we do login)
            public string PasswordHash { get; set; } = "";

            // Role controls what they can do:
            // "Operator", "Tech", "Supervisor", "Admin"
            public string Role { get; set; } = "";

            // 🔹 Multi-tenant hook:
            // Which customer / site this user belongs to
            public int TenantId { get; set; } = 1;   // 1 = Sentinel Demo Plant
        }
    }


