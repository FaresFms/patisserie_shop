using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Inventory.Entities;
using Volo.Abp.Domain.Repositories;

namespace Inventory.Suppliers;

public interface ISupplierRepository : IRepository<AppSupplier, Guid>
{
    Task<long> CountFilteredAsync(
        string? filter,
        bool? isActive,
        CancellationToken cancellationToken = default);

    Task<List<AppSupplier>> GetFilteredListAsync(
        string? filter,
        bool? isActive,
        string sorting,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default);
}
