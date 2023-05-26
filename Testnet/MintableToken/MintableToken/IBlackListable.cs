using Stratis.SmartContracts;

/// <summary>
/// Defines the IBlackListable interface.
/// </summary>
public interface IBlackListable
{
    /// <summary>
    /// Blacklists one or more accounts.
    /// </summary>
    /// <param name="accounts">A run-length-encoded (RLE) array of accounts.</param>
    void AddToBlackList(byte[] accounts);

    /// <summary>
    /// Removes an account from the blacklist.
    /// </summary>
    /// <param name="account">The account to remove from the blacklist.</param>
    void RemoveFromBlackList(Address account);
}
