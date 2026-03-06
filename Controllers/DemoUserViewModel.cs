using System.Collections.Generic;
using MaintenanceSandbox.Models;

namespace MaintenanceSandbox.Controllers
{
    public class DemoUserViewModel
    {
        // What the view is using:
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;

        // Optional: list of demo users (in case the view also loops over them)
        public List<DemoUser> Users { get; set; } = new();
    }
}


