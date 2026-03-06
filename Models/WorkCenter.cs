using SentinelMfgSuite.Web.Models.Base;

namespace SentinelMfgSuite.Web.Models
{
    public class WorkCenter : TenantEntity
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        public Guid AreaId { get; set; }
        public Area Area { get; set; } = null!;

        public int TargetHourlyThroughput { get; set; }
    }
}

