using Stratis.SmartContracts;

public interface IOwnable
{
    /// <summary>
    /// Gets the address of the current owner.
    /// </summary>
    Address Owner { get; }

    /// <summary>
    /// Gets the address of the current owner.
    /// </summary>
    Address PendingOwner { get; }

    /// <summary>
    /// Allows the current owner to set a new owner in a pending state.
    /// </summary>
    /// <param name="pendingOwner">The address to set as the pending owner.</param>
    void TransferOwnership(Address pendingOwner);

    /// <summary>
    /// Allows the pending owner to claim the ownership.
    /// </summary>
    void ClaimOwnership();
}
