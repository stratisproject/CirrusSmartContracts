using Stratis.SmartContracts;
using Stratis.SmartContracts.Standards;

/// <summary>
/// Implementation of a mintable token contract for the Stratis Platform.
/// </summary>
public class MintableToken : SmartContract, IStandardToken256, IMintableWithMetadata, IBurnableWithMetadata, IPullOwnership
{
    /// <summary>
    /// Constructor used to create a new instance of the token. Assigns the total token supply to the creator of the contract.
    /// </summary>
    /// <param name="smartContractState">The execution state for the contract.</param>
    /// <param name="totalSupply">The total token supply.</param>
    /// <param name="name">The name of the token.</param>
    /// <param name="symbol">The symbol used to identify the token.</param>
    /// <param name="decimals">The amount of decimals for display and calculation purposes.</param>
    /// <param name="minter">The minter address.</param>
    public MintableToken(ISmartContractState smartContractState, UInt256 totalSupply, string name, string symbol, byte decimals, Address minter)
        : base(smartContractState)
    {
        this.TotalSupply = totalSupply;
        this.Name = name;
        this.Symbol = symbol;
        this.Decimals = decimals;
        this.Owner = Message.Sender;
        this.NewOwner = Address.Zero;
        this.SetBalance(Message.Sender, totalSupply);       
        this.SetMinter(minter, true);       
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

    public Address NewOwner
    {
        get => State.GetAddress(nameof(this.NewOwner));
        private set => State.SetAddress(nameof(this.NewOwner), value);
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

    private bool Transfer(Address from, Address to, UInt256 amount, string metadata)
    {
        if (amount == 0)
        {
            Log(new TransferLog { From = from, To = to, Amount = 0, Metadata = metadata });

            return true;
        }

        if (from == Address.Zero)
        {
            TotalSupply = checked(TotalSupply + amount);

            Log(new SupplyChangeLog()
            {
                PreviousSupply = this.TotalSupply - amount,
                TotalSupply = this.TotalSupply,
                Metadata = metadata
            });
        }
        else
        {
            UInt256 fromBalance = GetBalance(from);

            Assert(amount <= fromBalance, "Amount is greater than balance");

            if (from != Message.Sender)
            {
                UInt256 senderAllowance = Allowance(from, Message.Sender);

                Assert(amount <= senderAllowance, "Amount is greater than allowance");

                SetApproval(from, Message.Sender, senderAllowance - amount);
            }

            SetBalance(from, fromBalance - amount);
        }

        if (to == Address.Zero)
        {
            TotalSupply -= amount;

            Log(new SupplyChangeLog()
            {
                PreviousSupply = this.TotalSupply + amount,
                TotalSupply = this.TotalSupply,
                Metadata = metadata
            });
        }
        else
        {
            SetBalance(to, checked(GetBalance(to) + amount));
        }

        Log(new TransferLog { From = from, To = to, Amount = amount, Metadata = metadata });

        return true;
    }

    /// <inheritdoc />
    public bool TransferTo(Address to, UInt256 amount)
    {
        Assert(to != Address.Zero, "Transfer to the zero address");

        return Transfer(Message.Sender, to, amount, string.Empty);
    }

    /// <inheritdoc />
    public bool TransferFrom(Address from, Address to, UInt256 amount)
    {
        Assert(from != Address.Zero, "Transfer from the zero address");
        Assert(to != Address.Zero, "Transfer to the zero address");

        return Transfer(from, to, amount, string.Empty);
    }

    public bool MintWithMetadata(Address account, UInt256 amount, string externalTxId)
    {
        Assert(GetMinter(Message.Sender), "Only a minter can call this method");
        Assert(!GetMinted(externalTxId), "Already minted for this external id");
        
        SetMinted(externalTxId);

        return Transfer(Address.Zero, account, amount, externalTxId);
    }

    public bool BurnWithMetadata(UInt256 amount, string externalTxId)
    {
        Assert(!GetBurned(externalTxId), "Already burned for this external id");

        SetBurned(externalTxId);

        return Transfer(Message.Sender, Address.Zero, amount, externalTxId);
    }

    /// <inheritdoc />
    public bool Approve(Address spender, UInt256 currentAmount, UInt256 amount)
    {
        // Approval not required if sender is spender.
        if (Message.Sender == spender)
        {
            return true;
        }

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
        // Owner can spend the full balance.
        if (owner == spender)
        {
            return GetBalance(owner);
        }

        return State.GetUInt256($"Allowance:{owner}:{spender}");
    }

    /// <inheritdoc />
    public void SetNewOwner(Address address)
    {
        Assert(Message.Sender == Owner, "Only the owner can call this method");

        NewOwner = address;
    }

    /// <inheritdoc />
    public void ClaimOwnership()
    {
        Assert(Message.Sender == NewOwner, "Only the new owner can call this method");

        Owner = NewOwner;

        NewOwner = Address.Zero;
    }

    public bool GetMinter(Address sender)
    {
        return State.GetBool($"Minter:{sender}");
    }

    public void SetMinter(Address sender, bool isMinter)
    {
        Assert(Message.Sender == Owner, "Only the owner can call this method");

        State.SetBool($"Minter:{sender}", isMinter);
    }

    private void SetMinted(string externalTxId)
    {
        State.SetBool($"Minted:{externalTxId}", true);
    }

    private bool GetMinted(string externalTxId)
    {
        return State.GetBool($"Minted:{externalTxId}");
    }

    private void SetBurned(string externalTxId)
    {
        State.SetBool($"Burned:{externalTxId}", true);
    }

    private bool GetBurned(string externalTxId)
    {
        return State.GetBool($"Burned:{externalTxId}");
    }

    public struct TransferLog
    {
        [Index]
        public Address From;

        [Index]
        public Address To;

        public UInt256 Amount;

        public string Metadata;
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

    public struct SupplyChangeLog
    {
        public UInt256 PreviousSupply;

        public UInt256 TotalSupply;

        public string Metadata;
    }
}