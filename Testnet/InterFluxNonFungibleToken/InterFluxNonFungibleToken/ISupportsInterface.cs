/// <summary>
/// Interface for a class with interface indication support.
/// </summary>
public interface ISupportsInterface
{
    /// <summary>
    /// Function to check which interfaces are supported by this contract.
    /// </summary>
    /// <param name="interfaceID">Id of the interface.</param>
    /// <returns>True if <see cref="interfaceID"/> is supported, false otherwise.</returns>
    bool SupportsInterface(uint interfaceID);
}
