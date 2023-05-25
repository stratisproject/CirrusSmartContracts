using Stratis.SmartContracts;

/// <summary>
/// A subset of the ERC20Mintable interface used by OpenZeppelin contracts.
/// For simplicity, we assume that the owner and minter of the contract are the sole minters.
/// </summary>
public interface IMintableWithMetadataForNetwork
{
    /// <summary>
    /// The configured owner or minter of the contract can create (mint) new tokens as required.
    /// This increases the total supply of the token.
    /// </summary>
    /// <remarks>Emits a TransferLog event with the 'from' address set to the zero address.</remarks>
    /// <param name="account">The account the newly minted tokens should be credited to.</param>
    /// <param name="amount">The amount of tokens to mint.</param>
    /// <param name="metadata">Additional data to be recorded with the mint.
    /// The structure and interpretation of this data is unspecified here.</param>
    /// <param name="network">The chain containing the account.</param>
    void MintWithMetadataForNetwork(string account, UInt256 amount, string metadata, string network);
}