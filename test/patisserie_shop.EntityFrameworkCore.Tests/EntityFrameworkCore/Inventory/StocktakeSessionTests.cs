using System;
using System.Linq;
using System.Threading.Tasks;
using Inventory;
using Inventory.Entities;
using Inventory.Stocktakes;
using patisserie_shop.EntityFrameworkCore.Testing;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace patisserie_shop.EntityFrameworkCore.Inventory;

[Collection(patisserie_shopTestConsts.CollectionDefinitionName)]
public class StocktakeSessionTests : IntegrationTestBase
{
    [Fact]
    public async Task Start_is_idempotent_and_saved_counts_can_be_resumed()
    {
        var category = await CreateCategoryAsync();
        var product1 = await CreateProductAsync(category.Id);
        var product2 = await CreateProductAsync(category.Id);
        var branch = await CreateBranchAsync();
        var inventory1 = await InitializeInventoryAsync(branch.Id, product1.Id, quantity: 8);
        await InitializeInventoryAsync(branch.Id, product2.Id, quantity: 5);
        var service = GetRequiredService<IStocktakeSessionAppService>();

        var started = await service.StartAsync(new StartStocktakeSessionDto { BranchId = branch.Id });
        var sameDraft = await service.StartAsync(new StartStocktakeSessionDto { BranchId = branch.Id });

        sameDraft.Id.ShouldBe(started.Id);
        started.Status.ShouldBe(StocktakeSessionStatuses.Draft);
        started.TotalLineCount.ShouldBe(2);

        var saved = await service.SaveDraftAsync(new SaveStocktakeSessionDraftDto
        {
            Id = started.Id,
            ConcurrencyStamp = started.ConcurrencyStamp,
            Notes = "First aisle saved",
            Lines = started.Lines.ConvertAll(line => new StocktakeSessionDraftLineDto
            {
                LineId = line.Id,
                CountedQuantity = line.InventoryId == inventory1.Id ? 7 : null,
                Reason = line.InventoryId == inventory1.Id ? StocktakeVarianceReasons.CountingError : null
            })
        });

        saved.CountedLineCount.ShouldBe(1);
        saved.DifferenceLineCount.ShouldBe(1);
        saved.Notes.ShouldBe("First aisle saved");

        var resumed = await service.GetActiveAsync(branch.Id);
        resumed.ShouldNotBeNull();
        resumed.Id.ShouldBe(started.Id);
        resumed.Lines.Single(line => line.InventoryId == inventory1.Id).CountedQuantity.ShouldBe(7);
        resumed.Lines.Single(line => line.InventoryId == inventory1.Id).Reason
            .ShouldBe(StocktakeVarianceReasons.CountingError);
    }

    [Fact]
    public async Task Submit_waits_for_approval_and_approval_updates_stock()
    {
        await DeactivateAllRulesAsync();
        var category = await CreateCategoryAsync();
        var product1 = await CreateProductAsync(category.Id);
        var product2 = await CreateProductAsync(category.Id);
        var branch = await CreateBranchAsync();
        var inventory1 = await InitializeInventoryAsync(branch.Id, product1.Id, quantity: 10);
        var inventory2 = await InitializeInventoryAsync(branch.Id, product2.Id, quantity: 4);
        var service = GetRequiredService<IStocktakeSessionAppService>();
        var started = await service.StartAsync(new StartStocktakeSessionDto { BranchId = branch.Id });

        var submitted = await service.SubmitAsync(new SubmitStocktakeSessionDto
        {
            Id = started.Id,
            ConcurrencyStamp = started.ConcurrencyStamp,
            Notes = "Closing count",
            Lines = started.Lines.ConvertAll(line => new StocktakeSessionDraftLineDto
            {
                LineId = line.Id,
                CountedQuantity = line.InventoryId == inventory1.Id ? 8 : 4,
                Reason = line.InventoryId == inventory1.Id ? StocktakeVarianceReasons.Damaged : null
            })
        });

        submitted.Status.ShouldBe(StocktakeSessionStatuses.PendingReview);
        submitted.SubmittedAt.ShouldNotBeNull();
        (await service.GetActiveAsync(branch.Id)).ShouldNotBeNull();

        var inventoryRepository = GetRequiredService<IRepository<AppBranchInventory, Guid>>();
        (await inventoryRepository.GetAsync(inventory1.Id)).QuantityOnHand.ShouldBe(10);

        var completed = await service.ApproveAsync(new ReviewStocktakeSessionDto
        {
            Id = submitted.Id,
            ConcurrencyStamp = submitted.ConcurrencyStamp,
            ReviewNotes = "Reviewed against the shelf"
        });

        completed.Session.Status.ShouldBe(StocktakeSessionStatuses.Completed);
        completed.Session.MovementReferenceId.ShouldBe(completed.Result.ReferenceId);
        completed.Session.AdjustedLineCount.ShouldBe(1);
        completed.Session.WriteOffLineCount.ShouldBe(1);
        completed.Session.ReviewedAt.ShouldNotBeNull();
        completed.Session.ReviewNotes.ShouldBe("Reviewed against the shelf");
        (await service.GetActiveAsync(branch.Id)).ShouldBeNull();

        var history = await service.GetListAsync(new GetStocktakeSessionsInput
        {
            BranchId = branch.Id,
            MaxResultCount = 10
        });
        var historyItem = history.Items.ShouldHaveSingleItem();
        historyItem.Id.ShouldBe(started.Id);
        historyItem.Status.ShouldBe(StocktakeSessionStatuses.Completed);
        historyItem.ShortageQuantity.ShouldBe(2);

        (await inventoryRepository.GetAsync(inventory1.Id)).QuantityOnHand.ShouldBe(8);
        (await inventoryRepository.GetAsync(inventory2.Id)).QuantityOnHand.ShouldBe(4);
    }

    [Fact]
    public async Task Stale_submission_changes_neither_stock_nor_saved_draft()
    {
        await DeactivateAllRulesAsync();
        var category = await CreateCategoryAsync();
        var product = await CreateProductAsync(category.Id);
        var branch = await CreateBranchAsync();
        var inventory = await InitializeInventoryAsync(branch.Id, product.Id, quantity: 10);
        var service = GetRequiredService<IStocktakeSessionAppService>();
        var started = await service.StartAsync(new StartStocktakeSessionDto { BranchId = branch.Id });

        await AdjustStockAsync(inventory.Id, newQuantity: 9);

        var exception = await Should.ThrowAsync<BusinessException>(() => service.SubmitAsync(
            new SubmitStocktakeSessionDto
            {
                Id = started.Id,
                ConcurrencyStamp = started.ConcurrencyStamp,
                Notes = "Stale saved session",
                Lines = started.Lines.ConvertAll(line => new StocktakeSessionDraftLineDto
                {
                    LineId = line.Id,
                    CountedQuantity = 8,
                    Reason = StocktakeVarianceReasons.CountingError
                })
            }));

        exception.Code.ShouldBe(InventoryErrorCodes.BranchInventoryConcurrency);
        var active = await service.GetActiveAsync(branch.Id);
        active.ShouldNotBeNull();
        active.Status.ShouldBe(StocktakeSessionStatuses.Draft);
        active.CountedLineCount.ShouldBe(0);

        var inventoryRepository = GetRequiredService<IRepository<AppBranchInventory, Guid>>();
        (await inventoryRepository.GetAsync(inventory.Id)).QuantityOnHand.ShouldBe(9);
    }

    [Fact]
    public async Task Cancel_closes_the_saved_draft_without_changing_stock()
    {
        var category = await CreateCategoryAsync();
        var product = await CreateProductAsync(category.Id);
        var branch = await CreateBranchAsync();
        var inventory = await InitializeInventoryAsync(branch.Id, product.Id, quantity: 6);
        var service = GetRequiredService<IStocktakeSessionAppService>();
        var started = await service.StartAsync(new StartStocktakeSessionDto { BranchId = branch.Id });
        var saved = await service.SaveDraftAsync(new SaveStocktakeSessionDraftDto
        {
            Id = started.Id,
            ConcurrencyStamp = started.ConcurrencyStamp,
            Lines = started.Lines.ConvertAll(line => new StocktakeSessionDraftLineDto
            {
                LineId = line.Id,
                CountedQuantity = 5,
                Reason = StocktakeVarianceReasons.CountingError
            })
        });

        await service.CancelAsync(new CancelStocktakeSessionDto
        {
            Id = saved.Id,
            ConcurrencyStamp = saved.ConcurrencyStamp
        });

        (await service.GetActiveAsync(branch.Id)).ShouldBeNull();
        var history = await service.GetListAsync(new GetStocktakeSessionsInput
        {
            BranchId = branch.Id,
            MaxResultCount = 10
        });
        var cancelled = history.Items.ShouldHaveSingleItem();
        cancelled.Status.ShouldBe(StocktakeSessionStatuses.Cancelled);
        cancelled.CountedLineCount.ShouldBe(1);

        var inventoryRepository = GetRequiredService<IRepository<AppBranchInventory, Guid>>();
        (await inventoryRepository.GetAsync(inventory.Id)).QuantityOnHand.ShouldBe(6);
    }

    [Fact]
    public async Task Reject_closes_the_review_without_changing_stock_and_allows_a_new_session()
    {
        var category = await CreateCategoryAsync();
        var product = await CreateProductAsync(category.Id);
        var branch = await CreateBranchAsync();
        var inventory = await InitializeInventoryAsync(branch.Id, product.Id, quantity: 6);
        var service = GetRequiredService<IStocktakeSessionAppService>();
        var started = await service.StartAsync(new StartStocktakeSessionDto { BranchId = branch.Id });

        var submitted = await service.SubmitAsync(new SubmitStocktakeSessionDto
        {
            Id = started.Id,
            ConcurrencyStamp = started.ConcurrencyStamp,
            Notes = "Review this count",
            Lines = started.Lines.ConvertAll(line => new StocktakeSessionDraftLineDto
            {
                LineId = line.Id,
                CountedQuantity = 5,
                Reason = StocktakeVarianceReasons.Complimentary
            })
        });

        var rejected = await service.RejectAsync(new RejectStocktakeSessionDto
        {
            Id = submitted.Id,
            ConcurrencyStamp = submitted.ConcurrencyStamp,
            ReviewNotes = "Please recount the display shelf"
        });

        rejected.Status.ShouldBe(StocktakeSessionStatuses.Rejected);
        rejected.ReviewNotes.ShouldBe("Please recount the display shelf");
        rejected.ReviewedAt.ShouldNotBeNull();
        (await service.GetActiveAsync(branch.Id)).ShouldBeNull();

        var inventoryRepository = GetRequiredService<IRepository<AppBranchInventory, Guid>>();
        (await inventoryRepository.GetAsync(inventory.Id)).QuantityOnHand.ShouldBe(6);
        (await service.StartAsync(new StartStocktakeSessionDto { BranchId = branch.Id })).Id.ShouldNotBe(started.Id);
    }
}
