using Stratis.SmartContracts;
using Stratis.SmartContracts.Standards;
using Amount = Stratis.SmartContracts.UInt256;

/// <summary>
/// Implementation of a standard token contract for the Stratis Platform.
/// </summary>
public class StandardToken : SmartContract, IStandardToken
{
    /// <summary>
    /// Constructor used to create a new instance of the token. Assigns the total token supply to the creator of the contract.
    /// </summary>
    /// <param name="smartContractState">The execution state for the contract.</param>
    /// <param name="totalSupply">The total token supply.</param>
    /// <param name="name">The name of the token.</param>
    /// <param name="symbol">The symbol used to identify the token.</param>
    public StandardToken(ISmartContractState smartContractState, Amount totalSupply, string name, string symbol)
        : base(smartContractState)
    {
        this.TotalSupply = totalSupply;
        this.Name = name;
        this.Symbol = symbol;
        this.SetBalance(Message.Sender, totalSupply);
    }

    public string Symbol
    {
        get => PersistentState.GetString(nameof(this.Symbol));
        private set => PersistentState.SetString(nameof(this.Symbol), value);
    }

    public string Name
    {
        get => PersistentState.GetString(nameof(this.Name));
        private set => PersistentState.SetString(nameof(this.Name), value);
    }

    /// <inheritdoc />
    public Amount TotalSupply
    {
        get => PersistentState.GetUInt256(nameof(this.TotalSupply));
        private set => PersistentState.SetUInt256(nameof(this.TotalSupply), value);
    }

    /// <inheritdoc />
    public uint GetDecimals()
    {
        return PersistentState.GetUInt32($"Decimals");
    }

    private void SetDecimals(uint decimals)
    {
        PersistentState.SetUInt32($"Decimals", decimals);
    }

    /// <inheritdoc />
    public Amount GetBalance(Address address)
    {
        return PersistentState.GetUInt256($"Balance:{address}");
    }

    private void SetBalance(Address address, Amount value)
    {
        PersistentState.SetUInt256($"Balance:{address}", value);
    }

    /// <inheritdoc />
    public bool TransferTo(Address to, Amount amount)
    {
        if (amount == 0)
        {
            Log(new TransferLog { From = Message.Sender, To = to, Amount = 0 });

            return true;
        }

        Amount senderBalance = GetBalance(Message.Sender);

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
    public bool TransferFrom(Address from, Address to, Amount amount)
    {
        if (amount == 0)
        {
            Log(new TransferLog { From = from, To = to, Amount = 0 });

            return true;
        }

        Amount senderAllowance = Allowance(from, Message.Sender);
        Amount fromBalance = GetBalance(from);

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
    public bool Approve(Address spender, Amount currentAmount, Amount amount)
    {
        if (Allowance(Message.Sender, spender) != currentAmount)
        {
            return false;
        }

        SetApproval(Message.Sender, spender, amount);

        Log(new ApprovalLog { Owner = Message.Sender, Spender = spender, Amount = amount, OldAmount = currentAmount });

        return true;
    }

    private void SetApproval(Address owner, Address spender, Amount value)
    {
        PersistentState.SetUInt256($"Allowance:{owner}:{spender}", value);
    }

    /// <inheritdoc />
    public Amount Allowance(Address owner, Address spender)
    {
        return PersistentState.GetUInt256($"Allowance:{owner}:{spender}");
    }

    public struct TransferLog
    {
        [Index]
        public Address From;

        [Index]
        public Address To;

        public Amount Amount;
    }

    public struct ApprovalLog
    {
        [Index]
        public Address Owner;

        [Index]
        public Address Spender;

        public Amount OldAmount;

        public Amount Amount;
    }
}