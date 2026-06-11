using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Intelligence.Entities;
using Volo.Abp.Domain.Repositories;

namespace Intelligence.Velocity;

/// <summary>
/// Custom repository for <see cref="AppProductVelocity"/>. Owns the velocity ×
/// product × branch × branch-inventory join, the optional text/branch filters,
/// sort-key translation, and paging — none of which may leak into the app service.
/// </summary>
public interface IProductVelocityRepository : IRepository<AppProductVelocity, Guid>
{
    Task<long> CountFilteredAsync(
        string? filter,
        Guid? branchId,
        IReadOnlyCollection<Guid>? scopedBranchIds,
        CancellationToken cancellationToken = default);

    Task<List<ProductVelocityListRow>> GetFilteredListAsync(
        string? filter,
        Guid? branchId,
        IReadOnlyCollection<Guid>? scopedBranchIds,
        string sorting,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default);
}
