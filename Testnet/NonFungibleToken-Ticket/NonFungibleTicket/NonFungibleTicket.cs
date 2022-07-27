using Stratis.SmartContracts;

[Deploy]
public class NonFungibleTicket : NonFungibleToken, IRedeemableTicketPerks
{
    private const int ByteSize = 256;
    
    public NonFungibleTicket(ISmartContractState state, string name, string symbol)
        : base(state, name, symbol, true)
    {
        SetSupportedInterfaces(TokenInterface.IRedeemableTicketPerks, true); // Redeemable ticket perks
    }

    /// <inheritdoc />
    public bool[] GetRedemptions(UInt256 tokenId)
    {
        EnsureTokenHasBeenMinted(tokenId);
        return GetRedemptionsExecute(tokenId);
    }

    /// <inheritdoc />
    public bool IsRedeemed(UInt256 tokenId, byte perkIndex)
    {
        EnsureTokenHasBeenMinted(tokenId);
        return GetRedemptionsExecute(tokenId)?[perkIndex] ?? false;
    }

    /// <inheritdoc />
    public void RedeemPerks(UInt256 tokenId, byte[] perkIndexes)
    {
        EnsureOwnerOnly();
        EnsureTokenHasBeenMinted(tokenId);
        Assert(perkIndexes.Length > 0, "Must provide at least one perk to redeem.");
        var redemptions = GetRedemptionsExecute(tokenId);
        foreach (var perkIndex in perkIndexes)
        {
            Assert(!redemptions[perkIndex], $"Perk at index {perkIndex} already redeemed.");
            redemptions[perkIndex] = true;
            Log(new PerkRedeemedLog { NftId = tokenId, PerkIndex = perkIndex });
        }
        State.SetArray($"Redemptions:{tokenId}", redemptions);
    }

    /// <summary>
    /// Retrieves redemptions from state if set; otherwise falls back to the zero-redemptions value
    /// </summary>
    /// <param name="tokenId">Id of the NFT</param>
    /// <returns>An array of redemption values corresponding to metadata attributes</returns>
    private bool[] GetRedemptionsExecute(UInt256 tokenId)
    {
        var redemptions = State.GetArray<bool>($"Redemptions:{tokenId}");
        return redemptions.Length != 0 ? redemptions : new bool[ByteSize];
    }
    
    private void EnsureTokenHasBeenMinted(UInt256 tokenId) => Assert(TokenIdCounter >= tokenId, "Token id does not exist");

    /// <summary>
    /// A log that is omitted when a perk is redeemed
    /// </summary>
    public struct PerkRedeemedLog
    {
        /// <summary>
        /// Id of the NFT
        /// </summary>
        [Index] public UInt256 NftId;
        
        /// <summary>
        /// Index of the perk in the NFT attributes
        /// </summary>
        [Index] public byte PerkIndex;
    }
}