namespace NonFungibleTokenContract
{
    using Stratis.SmartContracts;

    /// <summary>
    /// Interface for a non-fungible token receiver.
    /// </summary>
    public interface INonFungibleTokenReceiver
    {
        /// <summary>
        /// Handle the receipt of a NFT. The smart contract calls this function on the
        /// recipient after a transfer. This function MAY throw or return false to revert and reject the transfer.
        /// Return true if the transfer is ok.
        /// </summary>
        /// <param name="operatorAddress">The address which called safeTransferFrom function.</param>
        /// <param name="fromAddress">The address which previously owned the token.</param>
        /// <param name="tokenId">The NFT identifier which is being transferred.</param>
        /// <param name="data">Additional data with no specified format.</param>
        /// <returns>A bool indicating the resulting operation.</returns>
        bool OnNonFungibleTokenReceived(Address operatorAddress, Address fromAddress, ulong tokenId, byte[] data);
    }
}
