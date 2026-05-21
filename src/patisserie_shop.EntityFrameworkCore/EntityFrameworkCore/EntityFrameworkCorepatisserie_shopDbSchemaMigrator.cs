using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using patisserie_shop.Data;
using Volo.Abp.DependencyInjection;

namespace patisserie_shop.EntityFrameworkCore;

public class EntityFrameworkCorepatisserie_shopDbSchemaMigrator
    : Ipatisserie_shopDbSchemaMigrator, ITransientDependency
{
    private readonly IServiceProvider _serviceProvider;

    public EntityFrameworkCorepatisserie_shopDbSchemaMigrator(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task MigrateAsync()
    {
        /* We intentionally resolving the patisserie_shopDbContext
         * from IServiceProvider (instead of directly injecting it)
         * to properly get the connection string of the current tenant in the
         * current scope.
         */

        await _serviceProvider
            .GetRequiredService<patisserie_shopDbContext>()
            .Database
            .MigrateAsync();
    }
}
