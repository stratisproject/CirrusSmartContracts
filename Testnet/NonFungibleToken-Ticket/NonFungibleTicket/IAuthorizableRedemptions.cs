using Stratis.SmartContracts;

/// <summary>
/// Interface for managing perk redemptions on an NFT
/// </summary>
public interface IAuthorizableRedemptions
{
    /// <summary>
    /// Assigns the redeem role to an address, allowing them to redeem ticket perks
    /// </summary>
    /// <param name="address">Address to assign redeem permissions</param>
    void AssignRedeemRole(Address address);

    /// <summary>
    /// Revokes the redeem role from an address, preventing them from redeeming ticket perks
    /// </summary>
    /// <param name="address">Address to revoke redeem permissions</param>
    void RevokeRedeemRole(Address address);
    
    /// <summary>
    /// Checks if an address has the redeem role and can redeem ticket perks
    /// </summary>
    /// <param name="address">Address to check</param>
    /// <returns>True if the address has a redeem role; otherwise false</returns>
    bool CanRedeemPerks(Address address);
}