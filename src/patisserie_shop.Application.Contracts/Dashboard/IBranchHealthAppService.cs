using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace patisserie_shop.Dashboard;

public interface IBranchHealthAppService : IApplicationService
{
    /// <summary>
    /// One health row per active branch the caller can see (admin = all active
    /// branches, branch manager = only their managed branches), ordered by score
    /// descending. Each row carries its full component breakdown.
    /// </summary>
    Task<List<BranchHealthDto>> GetBranchHealthAsync();
}
