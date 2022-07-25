using Stratis.SmartContracts;

public interface IMinter
{
    Address Minter { get; }

    /// <summary>
    /// Sets the minter.
    /// </summary>
    /// <param name="address">The address of the minter.</param>
    void SetMinter(Address address);
}