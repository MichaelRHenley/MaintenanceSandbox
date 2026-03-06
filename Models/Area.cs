using SentinelMfgSuite.Web.Models.Base;

namespace SentinelMfgSuite.Web.Models
{
    public class Area : TenantEntity
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;

        public Guid SiteId { get; set; }
        public Site Site { get; set; } = null!;

        public ICollection<WorkCenter> WorkCenters { get; set; } = new List<WorkCenter>();
    }
}

