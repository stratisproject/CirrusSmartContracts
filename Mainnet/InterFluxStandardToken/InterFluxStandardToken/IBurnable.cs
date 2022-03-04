using Stratis.SmartContracts;

/// <summary>
/// A subset of the ERC20Burnable interface used by OpenZeppelin contracts.
/// </summary>
public interface IBurnable
{
    /// <summary>
    /// A user can burn tokens if the have the requisite balance available.
    /// Burnt tokens are permanently removed from the total supply and cannot be retrieved.
    /// </summary>
    /// <remarks>Emits a TransferLog event with the 'to' address set to the zero address.</remarks>
    /// <param name="amount">The quantity of tokens to be burnt.</param>
    bool Burn(UInt256 amount);
}
