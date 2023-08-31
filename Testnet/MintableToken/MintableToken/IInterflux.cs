using Stratis.SmartContracts;

public interface IInterflux
{
    Address Interflux { get; }

    /// <summary>
    /// Sets the interflux multisig.
    /// </summary>
    /// <param name="address">The address of the minter.</param>
    void SetInterflux(Address address);
}