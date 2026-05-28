using Operations.Entities;

namespace Operations.Sales;

/// <summary>
/// Typed read-model returned by <see cref="ISaleRepository"/> list queries.
/// Carries the sale aggregate plus its line-item count, computed in the
/// query so the app service never touches a queryable to aggregate it.
/// </summary>
public class SaleListRow
{
    public AppSale Sale { get; set; } = null!;
    public int ItemCount { get; set; }
}
