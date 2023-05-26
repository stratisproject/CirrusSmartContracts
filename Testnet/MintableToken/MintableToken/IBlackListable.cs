using Stratis.SmartContracts;

/// <summary>
/// A subset of the ERC20Mintable interface used by OpenZeppelin contracts.
/// For simplicity, we assume that the owner of the contract is the sole minter.
/// </summary>
public interface IBlackListable
{
    /// <summary>
    /// Blacklists one or more accounts.
    /// </summary>
    /// <param name="accounts">A run-length-encoded (RLE) array of accounts.</param>
    bool AddToBlackList(byte[] accounts);

    /// <summary>
    /// Removes an account from the blacklist.
    /// </summary>
    /// <param name="account">The account to remove from the blacklist.</param>
    /// <returns></returns>
    bool RemoveFromBlackList(Address account);
}
