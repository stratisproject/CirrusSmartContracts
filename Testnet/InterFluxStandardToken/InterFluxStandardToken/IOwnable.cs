using Stratis.SmartContracts;

/// <summary>
/// Provides the notion of ownership to the token contract.
/// The owner is able to perform certain privileged operations not available to generic users.
/// </summary>
public interface IOwnable
{
    Address Owner { get; }

    /// <summary>
    /// Assign ownership of the contract to the zero address, i.e. no owner.
    /// All functions that require owner-level access then become inaccessible.
    /// Naturally, only the current owner of the contract is able to call this.
    /// </summary>
    void RenounceOwnership();

    /// <summary>
    /// Called by the current owner of the contract in order to grant ownership to a new owner.
    /// </summary>
    /// <param name="newOwner">The address of the new owner.</param>
    void TransferOwnership(Address newOwner);
}
