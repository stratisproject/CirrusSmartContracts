using Stratis.SmartContracts;

/// <summary>
/// Provides the notion of ownership to the token contract.
/// The owner is able to perform certain privileged operations not available to generic users.
/// </summary>
public interface IPullOwnership
{
    Address Owner { get; }

    /// <summary>
    /// Assign ownership tentatively.
    /// </summary>
    /// <param name="newOwner">The address of the new owner.</param>
    void SetNewOwner(Address address);

    /// <summary>
    /// Called by the new owner of the contract to claim ownership.
    /// </summary>
    void ClaimOwnership();
}