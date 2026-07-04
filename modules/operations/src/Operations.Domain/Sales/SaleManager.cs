using System;
using System.Linq;
using System.Threading.Tasks;
using Operations.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace Operations.Sales;

public class SaleManager : DomainService
{
    private readonly IRepository<AppSale, Guid> _saleRepository;

    public SaleManager(IRepository<AppSale, Guid> saleRepository)
    {
        _saleRepository = saleRepository;
    }

    public async Task<AppSale> CreateDraftAsync(
        Guid branchId,
        string? invoiceNumber,
        DateTime saleDate,
        string currency,
        string? notes)
    {
        var normalized = string.IsNullOrWhiteSpace(invoiceNumber)
            ? await GenerateInvoiceNumberAsync(saleDate.Year)
            : invoiceNumber.Trim();

        await EnsureInvoiceNumberIsUniqueAsync(normalized);

        return new AppSale(
            GuidGenerator.Create(),
            branchId,
            normalized,
            saleDate,
            currency,
            notes);
    }

    /// <summary>
    /// INV-YYYY-NNNN, sequence resets per year. Uniqueness is also enforced by
    /// the DB unique index on InvoiceNumber.
    /// </summary>
    public async Task<string> GenerateInvoiceNumberAsync(int year)
    {
        var prefix = $"INV-{year:D4}-";
        var queryable = await _saleRepository.GetQueryableAsync();

        // Max by PARSED numeric tail, not a lexical string Max: past 9999 the wider
        // tail ("INV-2026-10000") sorts before "INV-2026-9999" lexically and would
        // reissue a duplicate. Integer max over the year's suffixes is correct at any
        // width; uniqueness is also enforced by the DB unique index on InvoiceNumber.
        var numbers = await AsyncExecuter.ToListAsync(
            queryable.Where(s => s.InvoiceNumber.StartsWith(prefix)).Select(s => s.InvoiceNumber));

        var maxSeq = 0;
        foreach (var number in numbers)
        {
            if (int.TryParse(number.Substring(prefix.Length), out var n) && n > maxSeq)
            {
                maxSeq = n;
            }
        }
        return $"{prefix}{maxSeq + 1:D4}";
    }

    private async Task EnsureInvoiceNumberIsUniqueAsync(string invoiceNumber)
    {
        var exists = await _saleRepository.AnyAsync(s => s.InvoiceNumber == invoiceNumber);
        if (exists)
        {
            throw new BusinessException(OperationsErrorCodes.DuplicateInvoiceNumber)
                .WithData("InvoiceNumber", invoiceNumber);
        }
    }
}
