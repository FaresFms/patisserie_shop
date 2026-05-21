using System;
using Volo.Abp.Application.Dtos;

namespace Inventory.Products;

public class GetProductsInput : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? DefaultSupplierId { get; set; }
    public bool? IsActive { get; set; }
}
