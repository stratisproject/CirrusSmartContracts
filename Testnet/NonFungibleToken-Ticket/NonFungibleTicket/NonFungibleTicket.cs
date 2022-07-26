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
        return State.GetArray<bool>($"Redemptions:{tokenId}") ?? new bool[ByteSize];
    }

    /// <inheritdoc />
    public bool IsRedeemed(UInt256 tokenId, byte perkIndex)
    {
        return State.GetArray<bool>($"Redemptions:{tokenId}")?[perkIndex] ?? false;
    }

    /// <inheritdoc />
    public void RedeemPerk(UInt256 tokenId, byte perkIndex)
    {
        EnsureOwnerOnly();
        var redemptions = State.GetArray<bool>($"Redemptions:{tokenId}") ?? new bool[ByteSize];
        Assert(!redemptions[perkIndex], "Perk already redeemed.");
        redemptions[perkIndex] = true;
        State.SetArray($"Redemptions:{tokenId}", redemptions);
        Log(new PerkRedeemedLog { NftId = tokenId, PerkIndex = perkIndex });
    }

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