using patisserie_shop.Samples;
using Xunit;

namespace patisserie_shop.EntityFrameworkCore.Applications;

[Collection(patisserie_shopTestConsts.CollectionDefinitionName)]
public class EfCoreSampleAppServiceTests : SampleAppServiceTests<patisserie_shopEntityFrameworkCoreTestModule>
{

}
