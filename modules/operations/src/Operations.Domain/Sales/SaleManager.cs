using System;
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
    /// Generates INV-YYYY-XXXXXXXX. The GUID suffix avoids duplicate invoices when
    /// multiple users record sales at the same time.
    /// </summary>
    public Task<string> GenerateInvoiceNumberAsync(int year)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        return Task.FromResult($"INV-{year:D4}-{suffix}");
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
