using Stratis.SmartContracts;

/// <summary>
/// Interface for redeemable ticket perks on an NFT
/// </summary>
/// <remarks>Supports the redemption of up to 255 perks per token</remarks>
public interface IRedeemableTicketPerks
{
    /// <summary>
    /// Retrieves a list of perk redemptions based on a perk
    /// </summary>
    /// <param name="tokenId">Id of the NFT</param>
    /// <returns>A list of values, either true or false, which represent whether the matching perk (attribute) index is redeemed</returns>
    bool[] GetRedemptions(UInt256 tokenId);
    
    /// <summary>
    /// Retrieves the redemption state of a specific perk
    /// </summary>
    /// <param name="tokenId">Id of the NFT</param>
    /// <param name="perkIndex">Index of the perk in the NFT attributes</param>
    /// <returns>True if the perk has been redeemed; otherwise false</returns>
    bool IsRedeemed(UInt256 tokenId, byte perkIndex);

    /// <summary>
    /// Performs the redemption of one or more perks
    /// </summary>
    /// <param name="tokenId">Id of the NFT</param>
    /// <param name="perksIndexes">Indexes of the perks in the NFT attributes</param>
    void RedeemPerks(UInt256 tokenId, byte[] perksIndexes);
}