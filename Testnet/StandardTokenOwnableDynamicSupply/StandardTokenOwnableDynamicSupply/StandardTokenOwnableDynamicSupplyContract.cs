using Stratis.SmartContracts;
using Stratis.SmartContracts.Standards;

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

/// <summary>
/// Implementation of a standard token contract for the Stratis Platform.
/// </summary>
[Deploy]
public class StandardTokenOwnableDynamicSupplyContract : SmartContract, IStandardToken256, IOwnable, IMintable, IBurnable, IBurnableWithMetadata
{
    /// <summary>
    /// Constructor used to create a new instance of the token. Assigns the total token supply to the creator of the contract.
    /// </summary>
    /// <param name="smartContractState">The execution state for the contract.</param>
    /// <param name="totalSupply">The total token supply.</param>
    /// <param name="name">The name of the token.</param>
    /// <param name="symbol">The symbol used to identify the token.</param>
    /// <param name="decimals">The amount of decimals for display and calculation purposes.</param>
    public StandardTokenOwnableDynamicSupplyContract(ISmartContractState smartContractState, UInt256 totalSupply, string name, string symbol, byte decimals) : base(smartContractState)
    {
        this.TotalSupply = totalSupply;
        this.Name = name;
        this.Symbol = symbol;
        this.Decimals = decimals;
        this.SetBalance(Message.Sender, totalSupply);
        this.Owner = Message.Sender;
    }

    public string Symbol
    {
        get => State.GetString(nameof(this.Symbol));
        private set => State.SetString(nameof(this.Symbol), value);
    }

    public string Name
    {
        get => State.GetString(nameof(this.Name));
        private set => State.SetString(nameof(this.Name), value);
    }

    /// <inheritdoc />
    public byte Decimals
    {
        get => State.GetBytes(nameof(this.Decimals))[0];
        private set => State.SetBytes(nameof(this.Decimals), new[] { value });
    }

    /// <inheritdoc />
    public UInt256 TotalSupply
    {
        get => State.GetUInt256(nameof(this.TotalSupply));
        private set => State.SetUInt256(nameof(this.TotalSupply), value);
    }

    /// <inheritdoc />
    public Address Owner
    {
        get => State.GetAddress(nameof(this.Owner));
        private set => State.SetAddress(nameof(this.Owner), value);
    }

    /// <inheritdoc />
    public UInt256 GetBalance(Address address)
    {
        return State.GetUInt256($"Balance:{address}");
    }

    private void SetBalance(Address address, UInt256 value)
    {
        State.SetUInt256($"Balance:{address}", value);
    }

    /// <inheritdoc />
    public bool TransferTo(Address to, UInt256 amount)
    {
        if (amount == 0)
        {
            Log(new TransferLog { From = Message.Sender, To = to, Amount = 0 });

            return true;
        }

        UInt256 senderBalance = GetBalance(Message.Sender);

        if (senderBalance < amount)
        {
            return false;
        }

        SetBalance(Message.Sender, senderBalance - amount);

        SetBalance(to, checked(GetBalance(to) + amount));

        Log(new TransferLog { From = Message.Sender, To = to, Amount = amount });

        return true;
    }

    /// <inheritdoc />
    public bool TransferFrom(Address from, Address to, UInt256 amount)
    {
        if (amount == 0)
        {
            Log(new TransferLog { From = from, To = to, Amount = 0 });

            return true;
        }

        UInt256 senderAllowance = Allowance(from, Message.Sender);
        UInt256 fromBalance = GetBalance(from);

        if (senderAllowance < amount || fromBalance < amount)
        {
            return false;
        }

        SetApproval(from, Message.Sender, senderAllowance - amount);

        SetBalance(from, fromBalance - amount);

        SetBalance(to, checked(GetBalance(to) + amount));

        Log(new TransferLog { From = from, To = to, Amount = amount });

        return true;
    }

    /// <inheritdoc />
    public bool Approve(Address spender, UInt256 currentAmount, UInt256 amount)
    {
        if (Allowance(Message.Sender, spender) != currentAmount)
        {
            return false;
        }

        SetApproval(Message.Sender, spender, amount);

        Log(new ApprovalLog { Owner = Message.Sender, Spender = spender, Amount = amount, OldAmount = currentAmount });

        return true;
    }

    private void SetApproval(Address owner, Address spender, UInt256 value)
    {
        State.SetUInt256($"Allowance:{owner}:{spender}", value);
    }

    /// <inheritdoc />
    public UInt256 Allowance(Address owner, Address spender)
    {
        return State.GetUInt256($"Allowance:{owner}:{spender}");
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
        TransferOwnership(Address.Zero);
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

        Log(new TransferLog() { From = Address.Zero, To = account, Amount = amount });

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

    public struct TransferLog
    {
        [Index]
        public Address From;

        [Index]
        public Address To;

        public UInt256 Amount;
    }

    public struct ApprovalLog
    {
        [Index]
        public Address Owner;

        [Index]
        public Address Spender;

        public UInt256 OldAmount;

        public UInt256 Amount;
    }

    /// <summary>
    /// Provides a record that ownership was transferred from one account to another.
    /// </summary>
    public struct OwnershipTransferred
    {
        [Index] public Address PreviousOwner;

        [Index] public Address NewOwner;
    }

    public struct BurnMetadata
    {
        [Index] public Address From;

        [Index] public string Metadata;

        public UInt256 Amount;
    }
}
