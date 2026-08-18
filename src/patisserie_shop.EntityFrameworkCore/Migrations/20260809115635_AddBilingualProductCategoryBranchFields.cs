using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace patisserie_shop.Migrations
{
    /// <inheritdoc />
    public partial class AddBilingualProductCategoryBranchFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Unit",
                table: "InventoryProducts",
                newName: "UnitAr");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "InventoryProducts",
                newName: "NameAr");

            migrationBuilder.RenameColumn(
                name: "Description",
                table: "InventoryProducts",
                newName: "DescriptionAr");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "InventoryCategories",
                newName: "NameAr");

            migrationBuilder.RenameColumn(
                name: "Description",
                table: "InventoryCategories",
                newName: "DescriptionAr");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "InventoryBranches",
                newName: "NameAr");

            migrationBuilder.RenameColumn(
                name: "Address",
                table: "InventoryBranches",
                newName: "AddressAr");

            migrationBuilder.AddColumn<string>(
                name: "DescriptionEn",
                table: "InventoryProducts",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameEn",
                table: "InventoryProducts",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UnitEn",
                table: "InventoryProducts",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DescriptionEn",
                table: "InventoryCategories",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameEn",
                table: "InventoryCategories",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AddressEn",
                table: "InventoryBranches",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameEn",
                table: "InventoryBranches",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            // Existing installations stored Arabic presentation data in the legacy
            // single-language columns. Preserve it as Arabic and provide a safe English
            // fallback for every user-created row before applying known seed translations.
            migrationBuilder.Sql(
                """
                UPDATE "InventoryCategories"
                SET "NameEn" = "NameAr",
                    "DescriptionEn" = "DescriptionAr";

                UPDATE "InventoryProducts"
                SET "NameEn" = "NameAr",
                    "DescriptionEn" = "DescriptionAr",
                    "UnitEn" = "UnitAr";

                UPDATE "InventoryBranches"
                SET "NameEn" = "NameAr",
                    "AddressEn" = "AddressAr";
                """);

            // Upgrade the original presentation catalog to the approved Syrian bakery
            // names. Custom products are untouched because this mapping is SKU-specific.
            migrationBuilder.Sql(
                """
                UPDATE "InventoryProducts"
                SET "NameAr" = CASE "SKU"
                    WHEN 'VN-001' THEN 'كرواسون بالجبنة'
                    WHEN 'VN-002' THEN 'كرواسون بالشوكولا'
                    WHEN 'VN-003' THEN 'كرواسون بالزعتر'
                    WHEN 'VN-004' THEN 'معروك بالتمر'
                    WHEN 'CT-001' THEN 'قطعة كاتو شوكولا'
                    WHEN 'CT-002' THEN 'قطعة كاتو فانيلا وفواكه'
                    WHEN 'CT-003' THEN 'تارت بالفراولة'
                    WHEN 'CT-004' THEN 'تشيز كيك لوتس'
                    WHEN 'CT-005' THEN 'قالب كاتو شوكولا'
                    WHEN 'BR-001' THEN 'خبز صمون'
                    WHEN 'BR-002' THEN 'خبز فرنسي'
                    WHEN 'BR-003' THEN 'خبز نخالة'
                    WHEN 'BR-004' THEN 'خبز بالحليب'
                    WHEN 'PF-001' THEN 'علبة برازق شامية'
                    WHEN 'PF-002' THEN 'تشكيلة معمول'
                    WHEN 'PF-003' THEN 'علبة غريبة شامية'
                    WHEN 'PF-004' THEN 'علبة بيتيفور مشكل'
                    WHEN 'PF-005' THEN 'عش البلبل بالفستق'
                    WHEN 'CB-001' THEN 'علبة بسكويت باليانسون'
                    WHEN 'CB-002' THEN 'علبة كوكيز بالشوكولا'
                    WHEN 'CB-003' THEN 'سابليه بالمربى'
                    WHEN 'SS-001' THEN 'علبة معمول العيد بالتمر'
                    WHEN 'SS-002' THEN 'معروك رمضان بالقشطة'
                    WHEN 'SS-003' THEN 'علبة قطايف بالجوز'
                    ELSE "NameAr" END,
                    "NameEn" = CASE "SKU"
                    WHEN 'VN-001' THEN 'Cheese Croissant'
                    WHEN 'VN-002' THEN 'Chocolate Croissant'
                    WHEN 'VN-003' THEN 'Zaatar Croissant'
                    WHEN 'VN-004' THEN 'Date Maarouk'
                    WHEN 'CT-001' THEN 'Chocolate Gateau Slice'
                    WHEN 'CT-002' THEN 'Vanilla Fruit Gateau Slice'
                    WHEN 'CT-003' THEN 'Strawberry Tart'
                    WHEN 'CT-004' THEN 'Lotus Cheesecake'
                    WHEN 'CT-005' THEN 'Whole Chocolate Gateau'
                    WHEN 'BR-001' THEN 'Samoon Bread'
                    WHEN 'BR-002' THEN 'French Bread'
                    WHEN 'BR-003' THEN 'Bran Bread'
                    WHEN 'BR-004' THEN 'Milk Bread'
                    WHEN 'PF-001' THEN 'Damascene Barazek Box'
                    WHEN 'PF-002' THEN 'Assorted Maamoul'
                    WHEN 'PF-003' THEN 'Damascene Ghraybeh Box'
                    WHEN 'PF-004' THEN 'Assorted Petit Four Box'
                    WHEN 'PF-005' THEN 'Pistachio Osh El Bulbul'
                    WHEN 'CB-001' THEN 'Anise Biscuit Box'
                    WHEN 'CB-002' THEN 'Chocolate Cookie Box'
                    WHEN 'CB-003' THEN 'Jam Sable Biscuit'
                    WHEN 'SS-001' THEN 'Eid Date Maamoul Box'
                    WHEN 'SS-002' THEN 'Ramadan Cream Maarouk'
                    WHEN 'SS-003' THEN 'Walnut Qatayef Box'
                    ELSE "NameEn" END,
                    "ShelfLifeDays" = CASE "SKU"
                    WHEN 'VN-001' THEN 2 WHEN 'VN-002' THEN 2 WHEN 'VN-003' THEN 2 WHEN 'VN-004' THEN 3
                    WHEN 'CT-001' THEN 3 WHEN 'CT-002' THEN 3 WHEN 'CT-003' THEN 2 WHEN 'CT-004' THEN 3 WHEN 'CT-005' THEN 3
                    WHEN 'BR-001' THEN 1 WHEN 'BR-002' THEN 2 WHEN 'BR-003' THEN 3 WHEN 'BR-004' THEN 2
                    WHEN 'PF-001' THEN 14 WHEN 'PF-002' THEN 14 WHEN 'PF-003' THEN 14 WHEN 'PF-004' THEN 7 WHEN 'PF-005' THEN 10
                    WHEN 'CB-001' THEN 14 WHEN 'CB-002' THEN 10 WHEN 'CB-003' THEN 14
                    WHEN 'SS-001' THEN 21 WHEN 'SS-002' THEN 2 WHEN 'SS-003' THEN 2
                    ELSE "ShelfLifeDays" END
                WHERE "SKU" IN (
                    'VN-001','VN-002','VN-003','VN-004','CT-001','CT-002','CT-003','CT-004','CT-005',
                    'BR-001','BR-002','BR-003','BR-004','PF-001','PF-002','PF-003','PF-004','PF-005',
                    'CB-001','CB-002','CB-003','SS-001','SS-002','SS-003');
                """);

            migrationBuilder.Sql(
                """
                UPDATE "InventoryCategories"
                SET "NameEn" = CASE "NameAr"
                    WHEN 'المعجنات المورّقة' THEN 'Viennoiseries'
                    WHEN 'الكيك والتارت' THEN 'Cakes and Tarts'
                    WHEN 'الخبز' THEN 'Bread'
                    WHEN 'الحلويات الصغيرة' THEN 'Petit Fours'
                    WHEN 'الكوكيز والبسكويت' THEN 'Cookies and Biscuits'
                    WHEN 'الأصناف الموسمية' THEN 'Seasonal Items'
                    ELSE "NameEn" END,
                    "DescriptionEn" = CASE "NameAr"
                    WHEN 'المعجنات المورّقة' THEN 'Laminated pastries such as croissants and pain au chocolat'
                    WHEN 'الكيك والتارت' THEN 'Whole cakes, individual slices, and fruit tarts'
                    WHEN 'الخبز' THEN 'Daily artisan bread and baguettes'
                    WHEN 'الحلويات الصغيرة' THEN 'Bite-sized pastries, macarons, and mini eclairs'
                    WHEN 'الكوكيز والبسكويت' THEN 'Butter cookies, sable biscuits, and biscotti'
                    WHEN 'الأصناف الموسمية' THEN 'Rotating seasonal and celebration items'
                    ELSE "DescriptionEn" END;

                UPDATE "InventoryProducts"
                SET "NameEn" = CASE "SKU"
                    WHEN 'VN-001' THEN 'Cheese Croissant'
                    WHEN 'VN-002' THEN 'Chocolate Croissant'
                    WHEN 'VN-003' THEN 'Zaatar Croissant'
                    WHEN 'VN-004' THEN 'Date Maarouk'
                    WHEN 'CT-001' THEN 'Chocolate Gateau Slice'
                    WHEN 'CT-002' THEN 'Vanilla Fruit Gateau Slice'
                    WHEN 'CT-003' THEN 'Strawberry Tart'
                    WHEN 'CT-004' THEN 'Lotus Cheesecake'
                    WHEN 'CT-005' THEN 'Whole Chocolate Gateau'
                    WHEN 'BR-001' THEN 'Samoon Bread'
                    WHEN 'BR-002' THEN 'French Bread'
                    WHEN 'BR-003' THEN 'Bran Bread'
                    WHEN 'BR-004' THEN 'Milk Bread'
                    WHEN 'PF-001' THEN 'Damascene Barazek Box'
                    WHEN 'PF-002' THEN 'Assorted Maamoul'
                    WHEN 'PF-003' THEN 'Damascene Ghraybeh Box'
                    WHEN 'PF-004' THEN 'Assorted Petit Four Box'
                    WHEN 'PF-005' THEN 'Pistachio Osh El Bulbul'
                    WHEN 'CB-001' THEN 'Anise Biscuit Box'
                    WHEN 'CB-002' THEN 'Chocolate Cookie Box'
                    WHEN 'CB-003' THEN 'Jam Sable Biscuit'
                    WHEN 'SS-001' THEN 'Eid Date Maamoul Box'
                    WHEN 'SS-002' THEN 'Ramadan Cream Maarouk'
                    WHEN 'SS-003' THEN 'Walnut Qatayef Box'
                    ELSE "NameEn" END,
                    "UnitEn" = CASE "UnitAr"
                    WHEN 'علبة' THEN 'box'
                    WHEN 'طقم' THEN 'set'
                    WHEN 'قطعة' THEN 'piece'
                    ELSE "UnitEn" END;

                UPDATE "InventoryBranches"
                SET "NameEn" = CASE "NameAr"
                    WHEN 'فرع المزة' THEN 'Mazzeh Branch'
                    WHEN 'فرع المالكي' THEN 'Malki Branch'
                    WHEN 'فرع أبو رمانة' THEN 'Abu Rummaneh Branch'
                    WHEN 'فرع كفرسوسة' THEN 'Kafr Sousa Branch'
                    WHEN 'فرع الشعلان' THEN 'Shaalan Branch'
                    WHEN 'فرع مشروع دمر' THEN 'Dummar Project Branch'
                    WHEN 'فرع باب توما' THEN 'Bab Touma Branch'
                    WHEN 'فرع جرمانا' THEN 'Jaramana Branch'
                    WHEN 'المطبخ المركزي' THEN 'Central Kitchen'
                    ELSE "NameEn" END,
                    "AddressEn" = CASE "NameAr"
                    WHEN 'فرع المزة' THEN 'Mazzeh Highway, beside Al Jalaa City, Damascus'
                    WHEN 'فرع المالكي' THEN 'Abdel Moneim Riad Street, Malki, Damascus'
                    WHEN 'فرع أبو رمانة' THEN 'Nazem Pasha Avenue, Abu Rummaneh, Damascus'
                    WHEN 'فرع كفرسوسة' THEN 'Al Baraem Street, Kafr Sousa, Damascus'
                    WHEN 'فرع الشعلان' THEN 'Al Hamra Street, Shaalan, Damascus'
                    WHEN 'فرع مشروع دمر' THEN 'Sixth Island, Dummar Project, Damascus'
                    WHEN 'فرع باب توما' THEN 'Bab Touma Square, Old Damascus'
                    WHEN 'فرع جرمانا' THEN 'Municipality Street, Jaramana, Rural Damascus'
                    WHEN 'المطبخ المركزي' THEN 'Industrial Area, Southern Damascus Entrance'
                    ELSE "AddressEn" END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DescriptionEn",
                table: "InventoryProducts");

            migrationBuilder.DropColumn(
                name: "NameEn",
                table: "InventoryProducts");

            migrationBuilder.DropColumn(
                name: "UnitEn",
                table: "InventoryProducts");

            migrationBuilder.DropColumn(
                name: "DescriptionEn",
                table: "InventoryCategories");

            migrationBuilder.DropColumn(
                name: "NameEn",
                table: "InventoryCategories");

            migrationBuilder.DropColumn(
                name: "AddressEn",
                table: "InventoryBranches");

            migrationBuilder.DropColumn(
                name: "NameEn",
                table: "InventoryBranches");

            migrationBuilder.RenameColumn(
                name: "UnitAr",
                table: "InventoryProducts",
                newName: "Unit");

            migrationBuilder.RenameColumn(
                name: "NameAr",
                table: "InventoryProducts",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "DescriptionAr",
                table: "InventoryProducts",
                newName: "Description");

            migrationBuilder.RenameColumn(
                name: "NameAr",
                table: "InventoryCategories",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "DescriptionAr",
                table: "InventoryCategories",
                newName: "Description");

            migrationBuilder.RenameColumn(
                name: "NameAr",
                table: "InventoryBranches",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "AddressAr",
                table: "InventoryBranches",
                newName: "Address");
        }
    }
}
