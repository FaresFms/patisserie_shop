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
        var lastNumber = await AsyncExecuter.MaxAsync(
            queryable.Where(s => s.InvoiceNumber.StartsWith(prefix)).Select(s => (string?)s.InvoiceNumber),
            x => x);

        var nextSeq = 1;
        if (!string.IsNullOrEmpty(lastNumber))
        {
            var tail = lastNumber.Substring(prefix.Length);
            if (int.TryParse(tail, out var n))
            {
                nextSeq = n + 1;
            }
        }
        return $"{prefix}{nextSeq:D4}";
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
