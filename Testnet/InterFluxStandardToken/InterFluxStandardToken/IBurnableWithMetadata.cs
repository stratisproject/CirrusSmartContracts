using Stratis.SmartContracts;

/// <summary>
/// An extension of the IBurnable functionality that allows additional data (e.g. for tagging) to be recorded.
/// </summary>
public interface IBurnableWithMetadata
{
    /// <summary>
    /// A user can burn tokens if the have the requisite balance available.
    /// Burnt tokens are permanently removed from the total supply and cannot be retrieved.
    /// </summary>
    /// <remarks>Emits a TransferLog event with the 'to' address set to the zero address.
    /// Additionally emits a BurnMetadata event containing the metadata string.</remarks>
    /// <param name="amount">The quantity of tokens to be burnt.</param>
    /// <param name="metadata">Additional data to be recorded with the burn.
    /// The structure and interpretation of this data is unspecified here.</param>
    bool BurnWithMetadata(UInt256 amount, string metadata);
}
