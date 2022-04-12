using Stratis.SmartContracts;

namespace NonFungibleTokenContract
{
    public interface INonFungibleTokenMetadata
    {
        string Name { get; }
        string Symbol { get; }

        string TokenURI(UInt256 tokenId);
    }
}
