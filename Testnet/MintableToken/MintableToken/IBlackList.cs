using Stratis.SmartContracts;

/// <summary>
/// Allows contract addresses to be blacklisted.
/// </summary>
public interface IBlackList
{
    void AddBlackList(Address address);

    void RemoveBlackList(Address address);

    void DestroyBlackFunds(Address address);
}
