using patisserie_shop.Samples;
using Xunit;

namespace patisserie_shop.EntityFrameworkCore.Domains;

[Collection(patisserie_shopTestConsts.CollectionDefinitionName)]
public class EfCoreSampleDomainTests : SampleDomainTests<patisserie_shopEntityFrameworkCoreTestModule>
{

}
