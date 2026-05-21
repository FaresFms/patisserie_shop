using Xunit;

namespace patisserie_shop.EntityFrameworkCore;

[CollectionDefinition(patisserie_shopTestConsts.CollectionDefinitionName)]
public class patisserie_shopEntityFrameworkCoreCollection : ICollectionFixture<patisserie_shopEntityFrameworkCoreFixture>
{

}
