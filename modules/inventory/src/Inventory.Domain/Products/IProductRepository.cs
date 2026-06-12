using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Inventory.Entities;
using Volo.Abp.Domain.Repositories;

namespace Inventory.Products;

public interface IProductRepository : IRepository<AppProduct, Guid>
{
    Task<long> CountFilteredAsync(
        string? filter,
        Guid? categoryId,
        Guid? defaultSupplierId,
        bool? isActive,
        CancellationToken cancellationToken = default);

    Task<List<AppProduct>> GetFilteredListAsync(
        string? filter,
        Guid? categoryId,
        Guid? defaultSupplierId,
        bool? isActive,
        string sorting,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default);

    Task<List<AppProduct>> GetActiveLookupAsync(
        string? filter,
        int maxResults,
        CancellationToken cancellationToken = default);
}
