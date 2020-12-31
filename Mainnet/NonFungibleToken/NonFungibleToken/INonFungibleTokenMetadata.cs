using System;
using System.Collections.Generic;
using System.Text;

namespace NonFungibleTokenContract
{
    public interface INonFungibleTokenMetadata
    {
        string Name { get; set; }
        string Symbol { get; set; }
    }
}
