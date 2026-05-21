using System.ComponentModel.DataAnnotations;

namespace Inventory.Categories;

public class CreateCategoryDto
{
    [Required]
    [StringLength(128)]
    public string Name { get; set; } = null!;

    [StringLength(512)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}
