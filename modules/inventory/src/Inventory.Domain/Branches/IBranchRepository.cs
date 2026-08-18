using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Inventory.Entities;
using Volo.Abp.Domain.Repositories;

namespace Inventory.Branches;

public interface IBranchRepository : IRepository<AppBranch, Guid>
{
    Task<long> CountFilteredAsync(
        string? filter,
        bool? isActive,
        CancellationToken cancellationToken = default);

    Task<List<AppBranch>> GetFilteredListAsync(
        string? filter,
        bool? isActive,
        string sorting,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default);

    Task<List<AppBranch>> GetActiveLookupAsync(
        int maxResults,
        CancellationToken cancellationToken = default);
}
