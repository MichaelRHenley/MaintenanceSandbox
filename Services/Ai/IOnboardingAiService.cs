using System.Threading;
using System.Threading.Tasks;

namespace MaintenanceSandbox.Services.Ai;

public interface IOnboardingAiService
{
    Task<string> GenerateDraftJsonAsync(string facilityDescription, CancellationToken ct = default);
}

