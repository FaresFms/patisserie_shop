using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace patisserie_shop.Data;

/* This is used if database provider does't define
 * Ipatisserie_shopDbSchemaMigrator implementation.
 */
public class Nullpatisserie_shopDbSchemaMigrator : Ipatisserie_shopDbSchemaMigrator, ITransientDependency
{
    public Task MigrateAsync()
    {
        return Task.CompletedTask;
    }
}
