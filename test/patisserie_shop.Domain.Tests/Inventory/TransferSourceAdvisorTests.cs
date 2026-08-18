using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Inventory.BranchInventory;
using NSubstitute;
using Shouldly;
using Xunit;

namespace patisserie_shop.Inventory;

public class TransferSourceAdvisorTests
{
    [Fact]
    public async Task Should_rank_full_safe_coverage_before_a_larger_partial_stock()
    {
        var destinationId = Guid.NewGuid();
        var productA = Guid.NewGuid();
        var productB = Guid.NewGuid();
        var fullBranch = Guid.NewGuid();
        var partialBranch = Guid.NewGuid();
        var repository = Substitute.For<IBranchInventoryRepository>();

        repository.GetTransferSourceStockAsync(
                destinationId,
                Arg.Any<IReadOnlyCollection<Guid>>(),
                Arg.Any<IReadOnlyCollection<Guid>?>(),
                Arg.Any<CancellationToken>())
            .Returns(new List<TransferSourceStockRow>
            {
                Row(fullBranch, "Full", productA, "A", onHand: 15, minimum: 5),
                Row(fullBranch, "Full", productB, "B", onHand: 8, minimum: 3),
                Row(partialBranch, "Partial", productA, "A", onHand: 30, minimum: 5),
                Row(partialBranch, "Partial", productB, "B", onHand: 4, minimum: 3)
            });

        var advisor = new TransferSourceAdvisor(repository);
        var result = await advisor.RecommendAsync(destinationId, new List<TransferSourceRequestLine>
        {
            new() { ProductId = productA, RequestedQuantity = 6 },
            new() { ProductId = productB, RequestedQuantity = 5 }
        });

        result.Count.ShouldBe(2);
        result[0].BranchId.ShouldBe(fullBranch);
        result[0].CanFulfillAll.ShouldBeTrue();
        result[0].IsRecommended.ShouldBeTrue();
        result[0].CoveragePercent.ShouldBe(100);
        result[1].CanFulfillAll.ShouldBeFalse();
        result[1].Products.ShouldContain(x => x.ProductId == productB && x.MissingQuantity == 4);
    }

    [Fact]
    public async Task Should_ignore_stock_at_or_below_the_safe_minimum()
    {
        var destinationId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var unsafeBranch = Guid.NewGuid();
        var safeBranch = Guid.NewGuid();
        var repository = Substitute.For<IBranchInventoryRepository>();

        repository.GetTransferSourceStockAsync(
                destinationId,
                Arg.Any<IReadOnlyCollection<Guid>>(),
                Arg.Any<IReadOnlyCollection<Guid>?>(),
                Arg.Any<CancellationToken>())
            .Returns(new List<TransferSourceStockRow>
            {
                Row(unsafeBranch, "Unsafe", productId, "A", onHand: 12, minimum: 12),
                Row(safeBranch, "Safe", productId, "A", onHand: 15, minimum: 10)
            });

        var advisor = new TransferSourceAdvisor(repository);
        var result = await advisor.RecommendAsync(destinationId, new List<TransferSourceRequestLine>
        {
            new() { ProductId = productId, RequestedQuantity = 8 }
        });

        result.Count.ShouldBe(1);
        result[0].BranchId.ShouldBe(safeBranch);
        result[0].CanFulfillAll.ShouldBeFalse();
        result[0].TotalMissingQuantity.ShouldBe(3);
        result[0].Products[0].SafeAvailableQuantity.ShouldBe(5);
    }

    private static TransferSourceStockRow Row(
        Guid branchId,
        string branchName,
        Guid productId,
        string productName,
        int onHand,
        int minimum)
        => new()
        {
            BranchId = branchId,
            BranchNameAr = branchName,
            BranchNameEn = branchName,
            ProductId = productId,
            ProductNameAr = productName,
            ProductNameEn = productName,
            QuantityOnHand = onHand,
            MinimumStock = minimum
        };
}
