using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Inventory.BranchInventory;
using Inventory.Entities;
using Inventory.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;

namespace Inventory.Stocktakes;

[Authorize(InventoryPermissions.BranchInventory.Default)]
public class StocktakeSessionAppService : InventoryAppService, IStocktakeSessionAppService
{
    private readonly IStocktakeSessionRepository _sessionRepository;
    private readonly StocktakeSessionManager _manager;
    private readonly BranchAccessChecker _branchAccess;

    public StocktakeSessionAppService(
        IStocktakeSessionRepository sessionRepository,
        StocktakeSessionManager manager,
        BranchAccessChecker branchAccess)
    {
        _sessionRepository = sessionRepository;
        _manager = manager;
        _branchAccess = branchAccess;
    }

    public async Task<StocktakeSessionDto?> GetActiveAsync(Guid branchId)
    {
        await _branchAccess.EnsureAccessAsync(branchId);
        var session = await _sessionRepository.FindOpenByBranchAsync(branchId);
        return session is null ? null : MapSession(session);
    }

    public async Task<StocktakeSessionDto> GetAsync(Guid id)
    {
        var session = await _sessionRepository.GetWithLinesAsync(id);
        await _branchAccess.EnsureAccessAsync(session.BranchId);
        return MapSession(session);
    }

    public async Task<PagedResultDto<StocktakeSessionListDto>> GetListAsync(GetStocktakeSessionsInput input)
    {
        await _branchAccess.EnsureAccessAsync(input.BranchId);
        var totalCount = await _sessionRepository.CountFilteredAsync(input.BranchId, input.Status);
        var sessions = await _sessionRepository.GetFilteredListAsync(
            input.BranchId,
            input.Status,
            input.Sorting ?? string.Empty,
            input.SkipCount,
            input.MaxResultCount);

        return new PagedResultDto<StocktakeSessionListDto>(
            totalCount,
            sessions.ConvertAll(session => ObjectMapper.Map<AppStocktakeSession, StocktakeSessionListDto>(session)));
    }

    [Authorize(InventoryPermissions.BranchInventory.Adjust)]
    public async Task<StocktakeSessionDto> StartAsync(StartStocktakeSessionDto input)
    {
        await _branchAccess.EnsureAccessAsync(input.BranchId);
        var start = await _manager.StartAsync(input.BranchId, CurrentUser.Id, CurrentUser.UserName);
        if (start.IsNew)
        {
            await _sessionRepository.InsertAsync(start.Session, autoSave: true);
        }

        return MapSession(start.Session);
    }

    [Authorize(InventoryPermissions.BranchInventory.Adjust)]
    public async Task<StocktakeSessionDto> SaveDraftAsync(SaveStocktakeSessionDraftDto input)
    {
        var session = await _sessionRepository.GetWithLinesAsync(input.Id);
        await _branchAccess.EnsureAccessAsync(session.BranchId);
        _manager.UpdateDraft(session, ToDraftLines(input.Lines), input.Notes, input.ConcurrencyStamp);
        await _sessionRepository.UpdateAsync(session, autoSave: true);
        return MapSession(session);
    }

    [Authorize(InventoryPermissions.BranchInventory.Adjust)]
    public async Task<StocktakeSessionDto> SubmitAsync(SubmitStocktakeSessionDto input)
    {
        var session = await _sessionRepository.GetWithLinesAsync(input.Id);
        await _branchAccess.EnsureAccessAsync(session.BranchId);
        await _manager.SubmitAsync(
            session,
            ToDraftLines(input.Lines),
            input.Notes,
            input.ConcurrencyStamp,
            CurrentUser.Id,
            CurrentUser.UserName);
        await _sessionRepository.UpdateAsync(session, autoSave: true);
        return MapSession(session);
    }

    [Authorize(InventoryPermissions.BranchInventory.ReviewStocktakes)]
    public async Task<ApproveStocktakeSessionResultDto> ApproveAsync(ReviewStocktakeSessionDto input)
    {
        var session = await _sessionRepository.GetWithLinesAsync(input.Id);
        await _branchAccess.EnsureAccessAsync(session.BranchId);
        var result = await _manager.ApproveAsync(
            session,
            input.ConcurrencyStamp,
            CurrentUser.Id,
            CurrentUser.UserName,
            input.ReviewNotes);
        await _sessionRepository.UpdateAsync(session, autoSave: true);

        return new ApproveStocktakeSessionResultDto
        {
            Session = MapSession(session),
            Result = MapResult(result)
        };
    }

    [Authorize(InventoryPermissions.BranchInventory.ReviewStocktakes)]
    public async Task<StocktakeSessionDto> RejectAsync(RejectStocktakeSessionDto input)
    {
        var session = await _sessionRepository.GetWithLinesAsync(input.Id);
        await _branchAccess.EnsureAccessAsync(session.BranchId);
        _manager.Reject(
            session,
            input.ConcurrencyStamp,
            CurrentUser.Id,
            CurrentUser.UserName,
            input.ReviewNotes);
        await _sessionRepository.UpdateAsync(session, autoSave: true);
        return MapSession(session);
    }

    [Authorize(InventoryPermissions.BranchInventory.Adjust)]
    public async Task CancelAsync(CancelStocktakeSessionDto input)
    {
        var session = await _sessionRepository.GetWithLinesAsync(input.Id);
        await _branchAccess.EnsureAccessAsync(session.BranchId);
        _manager.Cancel(session, input.ConcurrencyStamp, CurrentUser.Id);
        await _sessionRepository.UpdateAsync(session, autoSave: true);
    }

    private StocktakeSessionDto MapSession(AppStocktakeSession session)
    {
        var dto = ObjectMapper.Map<AppStocktakeSession, StocktakeSessionDto>(session);
        foreach (var line in session.Lines)
        {
            dto.Lines.Add(ObjectMapper.Map<AppStocktakeLine, StocktakeSessionLineDto>(line));
        }
        dto.Lines.Sort((left, right) => string.Compare(left.ProductName, right.ProductName, StringComparison.CurrentCulture));
        return dto;
    }

    private static List<StocktakeSessionDraftLine> ToDraftLines(List<StocktakeSessionDraftLineDto> lines)
        => lines.ConvertAll(line => new StocktakeSessionDraftLine(
            line.LineId,
            line.CountedQuantity,
            line.Reason,
            line.ReasonNotes,
            line.ProductionDate));

    private static StocktakeResultDto MapResult(StocktakePostingResult result)
        => new()
        {
            ReferenceId = result.ReferenceId,
            CountedLineCount = result.CountedLineCount,
            AdjustedLineCount = result.AdjustedLineCount,
            MatchedLineCount = result.MatchedLineCount,
            WriteOffLineCount = result.WriteOffLineCount,
            ManualAdjustmentLineCount = result.ManualAdjustmentLineCount,
            ShortageQuantity = result.ShortageQuantity,
            OverageQuantity = result.OverageQuantity
        };
}
