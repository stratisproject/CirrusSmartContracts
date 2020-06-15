using Stratis.SmartContracts;
using Stratis.SmartContracts.Standards;

[Deploy]
public class DividendToken : StandardToken
{
    public ulong Dividends
    {
        get => PersistentState.GetUInt64(nameof(this.Dividends));
        private set => PersistentState.SetUInt64(nameof(this.Dividends), value);
    }

    public Account GetAccount(Address address) => PersistentState.GetStruct<Account>($"Account:{address}");
    private void SetAccount(Address address, Account account) => PersistentState.SetStruct($"Account:{address}", account);

    public DividendToken(ISmartContractState smartContractState, ulong totalSupply, string name, string symbol)
        : base(smartContractState, totalSupply, name, symbol)
    {
    }

    /// <summary>
    /// It is advised that deposit amount should to be evenly divided by total supply, 
    /// otherwise small amount of satoshi may lost
    /// </summary>
    public override void Receive()
    {
        Dividends += Message.Value;
    }

    new public bool TransferTo(Address to, ulong amount)
    {
        UpdateAccount(Message.Sender);

        return base.TransferTo(to, amount);
    }

    new public bool TransferFrom(Address from, Address to, ulong amount)
    {
        UpdateAccount(from);
        UpdateAccount(to);

        return base.TransferFrom(from, to, amount);
    }

    void UpdateAccount(Address address) => GetUpdatedAccount(address);
    private Account GetUpdatedAccount(Address address)
    {
        var account = GetAccount(address);

        var newDividends = Dividends - account.CreditedDividends;

        checked
        {
            var owing = (GetBalance(address) * newDividends) / TotalSupply;

            var hasUpdate = owing != 0 || account.CreditedDividends != Dividends;

            account.Balance += owing;
            account.CreditedDividends = Dividends;


            if (hasUpdate)
            {
                SetAccount(address, account);
            }
        }

        return account;
    }

    public ulong GetDividends() => GetDividends(Message.Sender);

    public ulong GetDividends(Address address)
    {
        var account = GetAccount(address);
        return account.Balance;
    }

    /// <summary>
    /// Withdraws all dividends
    /// </summary>
    public void Withdraw()
    {
        var account = GetUpdatedAccount(Message.Sender);
        Assert(account.Balance > 0, "The account has no dividends.");

        var transfer = Transfer(Message.Sender, account.Balance);

        Assert(transfer.Success, "Transfer failed.");
        account.Balance = 0;

        SetAccount(Message.Sender, account);
    }

    public struct Account
    {
        /// <summary>
        /// Balance for Cirrus dividends
        /// </summary>
        public ulong Balance;

        public ulong CreditedDividends;
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