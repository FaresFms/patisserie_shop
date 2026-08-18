using System.ComponentModel.DataAnnotations;

namespace Inventory.Categories;

public class UpdateCategoryDto
{
    [Required]
    [StringLength(128)]
    public string NameAr { get; set; } = null!;

    [Required]
    [StringLength(128)]
    public string NameEn { get; set; } = null!;

    [StringLength(512)]
    public string? DescriptionAr { get; set; }

    [StringLength(512)]
    public string? DescriptionEn { get; set; }

    public bool IsActive { get; set; } = true;
}
