﻿using Stratis.SmartContracts;

namespace NonFungibleTokenContract
{
    public interface IRoyaltyInfo
    {
        /// <summary>
        /// Returns an object representing the royaltyInfo for the given contract.
        /// The object[] is expected to include at least the receiver as the first param, and the royalty Amount as the second.
        /// </summary>
        /// <param name="tokenId"></param>
        /// <param name="salePrice"></param>
        /// <returns></returns>
         object[] RoyaltyInfo(UInt256 tokenId, ulong salePrice);
    }
}
