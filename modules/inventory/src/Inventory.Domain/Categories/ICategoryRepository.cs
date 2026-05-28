using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Inventory.Entities;
using Volo.Abp.Domain.Repositories;

namespace Inventory.Categories;

public interface ICategoryRepository : IRepository<AppCategory, Guid>
{
    Task<long> CountFilteredAsync(
        string? filter,
        bool? isActive,
        CancellationToken cancellationToken = default);

    Task<List<AppCategory>> GetFilteredListAsync(
        string? filter,
        bool? isActive,
        string sorting,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default);
}
