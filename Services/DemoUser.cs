namespace MaintenanceSandbox.Models
{
    public class DemoUser
    {
        public string Name { get; set; } = string.Empty;     // display name
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty; // only used in demo auth
        public string Role { get; set; } = string.Empty;
    }
}


