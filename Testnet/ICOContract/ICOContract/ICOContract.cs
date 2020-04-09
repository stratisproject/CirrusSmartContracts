using Stratis.SmartContracts;
using Stratis.SmartContracts.Standards;

[Deploy]
public class ICOContract : SmartContract
{
    private ulong Rate
    {
        get => this.PersistentState.GetUInt64(nameof(Rate));
        set => this.PersistentState.SetUInt64(nameof(Rate), value);
    }

    private ulong EndBlock
    {
        get => this.PersistentState.GetUInt64(nameof(EndBlock));
        set => this.PersistentState.SetUInt64(nameof(EndBlock), value);
    }

    private Address StandardTokenAddress
    {
        get => this.PersistentState.GetAddress(nameof(StandardTokenAddress));
        set => this.PersistentState.SetAddress(nameof(StandardTokenAddress), value);
    }

    private ulong TokenBalance
    {
        get => this.PersistentState.GetUInt64(nameof(TokenBalance));
        set => this.PersistentState.SetUInt64(nameof(TokenBalance), value);
    }

    private const ulong Satoshis = 100_000_000;
    public bool OnSale => EndBlock >= this.Block.Number && TokenBalance > 0;
    private ulong GetTokenAmount(ulong amount) => checked(amount * Rate) / Satoshis;

    public ICOContract(ISmartContractState smartContractState,
                        ulong totalSupply,
                        string name,
                        string symbol,
                        ulong endBlockDuration,
                        ulong rate
                        ) : base(smartContractState)
    {

        var result = Create<StandardToken>(0, new object[] { totalSupply, name, symbol });

        Log(new StandardTokenCreationLog { Result = result.Success, StandardTokenAddress = result.NewContractAddress });

        Assert(result.Success, "Creating token contract failed.");

        EndBlock = Block.Number + endBlockDuration;
        Rate = rate;
        StandardTokenAddress = result.NewContractAddress;
        TokenBalance = totalSupply;

    }

    public bool Invest()
    {
        Assert(OnSale, "ICO is completed.");

        var (refundAmount, soldTokenAmount) = GetOverSale();

        var result = Call(StandardTokenAddress, 0, nameof(StandardToken.TransferTo), new object[] { Message.Sender, soldTokenAmount });

        var transferSuccess = result.Success && (bool)result.ReturnValue;

        Log(new TransferLog { Address = Message.Sender, TransferSuccess = transferSuccess, TokenAmount = soldTokenAmount });

        if (transferSuccess)
        {
            if (refundAmount > 0) // refund over sale
                Transfer(Message.Sender, refundAmount);
        }
        else
        {
            //refund
            _ = Transfer(Message.Sender, Message.Value);

            return false;
        }

        return true;
    }

    private (ulong refundAmount, ulong soldTokenAmount) GetOverSale()
    {
        var tokenAmount = GetTokenAmount(Message.Value);

        if (tokenAmount > TokenBalance) // refund over sale
        {
            var overSale = tokenAmount - TokenBalance;

            var refund = (overSale * Satoshis) / Rate;

            return (refundAmount: refund, soldTokenAmount: TokenBalance);
        }

        return (refundAmount: 0, soldTokenAmount: tokenAmount);
    }

    public struct StandardTokenCreationLog
    {
        public bool Result;
        public Address StandardTokenAddress;
    }

    public struct TransferLog
    {
        public Address Address;
        public bool CallSuccess;
        public bool TransferSuccess;
        public ulong TokenAmount;
    }
}

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
    public StandardToken(ISmartContractState smartContractState, ulong totalSupply, string name, string symbol)
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
    public ulong TotalSupply
    {
        get => PersistentState.GetUInt64(nameof(this.TotalSupply));
        private set => PersistentState.SetUInt64(nameof(this.TotalSupply), value);
    }

    /// <inheritdoc />
    public ulong GetBalance(Address address)
    {
        return PersistentState.GetUInt64($"Balance:{address}");
    }

    private void SetBalance(Address address, ulong value)
    {
        PersistentState.SetUInt64($"Balance:{address}", value);
    }

    /// <inheritdoc />
    public bool TransferTo(Address to, ulong amount)
    {
        if (amount == 0)
        {
            Log(new TransferLog { From = Message.Sender, To = to, Amount = 0 });

            return true;
        }

        ulong senderBalance = GetBalance(Message.Sender);

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
    public bool TransferFrom(Address from, Address to, ulong amount)
    {
        if (amount == 0)
        {
            Log(new TransferLog { From = from, To = to, Amount = 0 });

            return true;
        }

        ulong senderAllowance = Allowance(from, Message.Sender);
        ulong fromBalance = GetBalance(from);

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
    public bool Approve(Address spender, ulong currentAmount, ulong amount)
    {
        if (Allowance(Message.Sender, spender) != currentAmount)
        {
            return false;
        }

        SetApproval(Message.Sender, spender, amount);

        Log(new ApprovalLog { Owner = Message.Sender, Spender = spender, Amount = amount, OldAmount = currentAmount });

        return true;
    }

    private void SetApproval(Address owner, Address spender, ulong value)
    {
        PersistentState.SetUInt64($"Allowance:{owner}:{spender}", value);
    }

    /// <inheritdoc />
    public ulong Allowance(Address owner, Address spender)
    {
        return PersistentState.GetUInt64($"Allowance:{owner}:{spender}");
    }

    public struct TransferLog
    {
        [Index]
        public Address From;

        [Index]
        public Address To;

        public ulong Amount;
    }

    public struct ApprovalLog
    {
        [Index]
        public Address Owner;

        [Index]
        public Address Spender;

        public ulong OldAmount;

        public ulong Amount;
    }
}
