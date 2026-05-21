using System;
using Volo.Abp.Application.Dtos;

namespace Inventory.Suppliers;

public class SupplierDto : EntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreationTime { get; set; }
}
