using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Inventory.Branches;
using Inventory.Categories;
using Inventory.Products;
using patisserie_shop.EntityFrameworkCore.Testing;
using Shouldly;
using Xunit;

namespace patisserie_shop.EntityFrameworkCore.Inventory;

[Collection(patisserie_shopTestConsts.CollectionDefinitionName)]
public class BilingualSetupTests : IntegrationTestBase
{
    [Fact]
    public async Task Product_category_and_branch_titles_follow_the_current_ui_culture()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var categories = GetRequiredService<ICategoryAppService>();
            var products = GetRequiredService<IProductAppService>();
            var branches = GetRequiredService<IBranchAppService>();

            var category = await categories.CreateAsync(new CreateCategoryDto
            {
                NameAr = $"فئة {suffix}",
                NameEn = $"Category {suffix}",
                DescriptionAr = "وصف عربي",
                DescriptionEn = "English description"
            });
            var product = await products.CreateAsync(new CreateProductDto
            {
                CategoryId = category.Id,
                NameAr = $"منتج {suffix}",
                NameEn = $"Product {suffix}",
                SKU = $"BI-{suffix}",
                UnitAr = "قطعة",
                UnitEn = "piece",
                CostPrice = 1m,
                SalePrice = 2m
            });
            var branch = await branches.CreateAsync(new CreateBranchDto
            {
                NameAr = $"فرع {suffix}",
                NameEn = $"Branch {suffix}",
                AddressAr = "عنوان عربي",
                AddressEn = "English address"
            });

            SetCulture("en");
            (await categories.GetAsync(category.Id)).Name.ShouldBe($"Category {suffix}");
            var englishProduct = await products.GetAsync(product.Id);
            englishProduct.Name.ShouldBe($"Product {suffix}");
            englishProduct.Unit.ShouldBe("piece");
            var englishBranch = await branches.GetAsync(branch.Id);
            englishBranch.Name.ShouldBe($"Branch {suffix}");
            englishBranch.Address.ShouldBe("English address");

            SetCulture("ar");
            (await categories.GetAsync(category.Id)).Name.ShouldBe($"فئة {suffix}");
            var arabicProduct = await products.GetAsync(product.Id);
            arabicProduct.Name.ShouldBe($"منتج {suffix}");
            arabicProduct.Unit.ShouldBe("قطعة");
            var arabicBranch = await branches.GetAsync(branch.Id);
            arabicBranch.Name.ShouldBe($"فرع {suffix}");
            arabicBranch.Address.ShouldBe("عنوان عربي");

            var englishSearchInArabic = await products.GetListAsync(new GetProductsInput
            {
                Filter = $"Product {suffix}",
                MaxResultCount = 10
            });
            englishSearchInArabic.Items.Single().Id.ShouldBe(product.Id);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    private static void SetCulture(string name)
    {
        var culture = CultureInfo.GetCultureInfo(name);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }
}
