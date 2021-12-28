using Stratis.SmartContracts;

/// <summary>
/// Provides the notion of ownership to the token contract.
/// The owner is able to perform certain privileged operations not available to generic users.
/// </summary>
public interface IOwnable
{
    Address Owner { get; }

    /// <summary>
    /// Secures method access by ensuring that only the owner of the contract is able to call a particular method. 
    /// </summary>
    void OnlyOwner();

    /// <summary>
    /// Checks whether the message sender is the current owner of the contract.
    /// </summary>
    /// <returns>True if the message sender is the contract owner.</returns>
    bool IsOwner();

    /// <summary>
    /// Assign ownership of the contract to the zero address, i.e. no owner.
    /// All functions that require owner-level access then become inaccessible.
    /// Naturally, only the current owner of the contract is able to call this.
    /// </summary>
    void RenounceOwnership();

    /// <summary>
    /// Called by the current owner of the contract in order to grant ownership to a new owner.
    /// </summary>
    /// <param name="newOwner">The address of the new owner.</param>
    void TransferOwnership(Address newOwner);
}

/// <summary>
/// A subset of the ERC20Mintable interface used by OpenZeppelin contracts.
/// For simplicity, we assume that the owner of the contract is the sole minter.
/// </summary>
public interface IMintable
{
    /// <summary>
    /// The owner of the contract can create (mint) new tokens as required.
    /// This increases the total supply of the token.
    /// </summary>
    /// <remarks>Emits a TransferLog event with the 'from' address set to the zero address.</remarks>
    /// <param name="account">The account the newly minted tokens should be credited to.</param>
    /// <param name="amount">The amount of tokens to mint.</param>
    void Mint(Address account, UInt256 amount);
}

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

public class StandardTokenEnhanced : StandardToken, IOwnable, IMintable, IBurnable, IBurnableWithMetadata
{
    public StandardTokenEnhanced(ISmartContractState smartContractState, UInt256 totalSupply, string name, string symbol, byte decimals) : base(smartContractState, totalSupply, name, symbol, decimals)
    {
        this.Owner = Message.Sender;
    }

    /// <inheritdoc />
    public Address Owner
    {
        get => State.GetAddress(nameof(this.Owner));
        private set => State.SetAddress(nameof(this.Owner), value);
    }

    /// <inheritdoc />
    public void OnlyOwner()
    {
        Assert(IsOwner(), "Only the owner can call this method");
    }

    /// <inheritdoc />
    public bool IsOwner()
    {
        return Message.Sender == Owner;
    }

    /// <inheritdoc />
    public void RenounceOwnership()
    {
        OnlyOwner();

        Address previousOwner = this.Owner;

        this.Owner = Address.Zero;

        Log(new OwnershipTransferred { PreviousOwner = previousOwner, NewOwner = Address.Zero });
    }

    /// <inheritdoc />
    public void TransferOwnership(Address newOwner)
    {
        OnlyOwner();

        Address previousOwner = this.Owner;

        this.Owner = newOwner;

        Log(new OwnershipTransferred { PreviousOwner = previousOwner, NewOwner = newOwner });
    }

    /// <inheritdoc />
    public void Mint(Address account, UInt256 amount)
    {
        OnlyOwner();

        UInt256 senderBalance = GetBalance(Message.Sender);

        SetBalance(Message.Sender, senderBalance + amount);

        Log(new TransferLog() { From = Address.Zero, To = account, Amount = amount});

        this.TotalSupply += amount;
    }

    /// <inheritdoc />
    public bool Burn(UInt256 amount)
    {
        if (TransferTo(Address.Zero, amount))
        {
            this.TotalSupply -= amount;

            return true;
        }

        return false;
    }

    /// <inheritdoc />
    public bool BurnWithMetadata(UInt256 amount, string metadata)
    {
        if (Burn(amount))
        {
            Log(new BurnMetadata() { From = Message.Sender, Amount = amount, Metadata = metadata });

            return true;
        }

        return false;
    }

    /// <summary>
    /// Provides a record that ownership was transferred from one account to another.
    /// </summary>
    public struct OwnershipTransferred
    {
        [Index]
        public Address PreviousOwner;

        [Index]
        public Address NewOwner;
    }

    public struct BurnMetadata
    {
        [Index]
        public Address From;

        [Index]
        public string Metadata;

        public UInt256 Amount;
    }
}
