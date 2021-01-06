using Stratis.SmartContracts;
using Stratis.SmartContracts.Standards;

[Deploy]
public class DividendToken : SmartContract, IStandardToken
{

    public ulong Dividends
    {
        get => PersistentState.GetUInt64(nameof(this.Dividends));
        private set => PersistentState.SetUInt64(nameof(this.Dividends), value);
    }

    private Account GetAccount(Address address) => PersistentState.GetStruct<Account>($"Account:{address}");

    private void SetAccount(Address address, Account account) => PersistentState.SetStruct($"Account:{address}", account);


    public DividendToken(ISmartContractState state, ulong totalSupply, string name, string symbol)
        : base(state)
    {
        this.TotalSupply = totalSupply;
        this.Name = name;
        this.Symbol = symbol;
        this.SetBalance(Message.Sender, totalSupply);
    }


    public override void Receive()
    {
        DistributeDividends();
    }

    /// <summary>
    /// It is advised that deposit amount should to be evenly divided by total supply, 
    /// otherwise small amount of satoshi may lost(burn)
    /// </summary>
    public void DistributeDividends()
    {
        Dividends += Message.Value;
    }
    public bool TransferTo(Address to, ulong amount)
    {
        UpdateAccount(Message.Sender);
        UpdateAccount(to);

        return TransferTokensTo(to, amount);
    }

    public bool TransferFrom(Address from, Address to, ulong amount)
    {
        UpdateAccount(from);
        UpdateAccount(to);

        return TransferTokensFrom(from, to, amount);
    }

    private Account UpdateAccount(Address address)
    {
        var account = GetAccount(address);
        var newDividends = GetNewDividends(address, account);

        if (newDividends > 0)
        {
            account.DividendBalance = checked(account.DividendBalance + newDividends);
            account.CreditedDividends = Dividends;
            SetAccount(address, account);
        }

        return account;
    }

    private ulong GetWithdrawableDividends(Address address, Account account)
    {
        return checked(account.DividendBalance + GetNewDividends(address, account)); //Delay divide by TotalSupply to final stage for avoid decimal value loss.
    }

    private ulong GetNewDividends(Address address, Account account)
    {
        checked
        {
            var notCreditedDividends = Dividends - account.CreditedDividends;
            return GetBalance(address) * notCreditedDividends;
        }
    }

    /// <summary>
    /// Get Withdrawable dividends
    /// </summary>
    /// <returns></returns>
    public ulong GetDividends() => GetDividends(Message.Sender);

    /// <summary>
    /// Get Withdrawable dividends
    /// </summary>
    /// <param name="address"></param>
    /// <returns></returns>
    public ulong GetDividends(Address address)
    {
        var account = GetAccount(address);

        return GetWithdrawableDividends(address, account) / TotalSupply;
    }

    /// <summary>
    /// Get the all divididends since beginning (Withdrawable Dividends + Withdrawn Dividends)
    /// </summary>
    /// <returns></returns>
    public ulong GetTotalDividends() => GetTotalDividends(Message.Sender);

    /// <summary>
    /// Get the all divididends since beginning (Withdrawable Dividends + Withdrawn Dividends)
    /// </summary>
    /// <param name="address"></param>
    /// <returns></returns>
    public ulong GetTotalDividends(Address address)
    {
        var account = GetAccount(address);
        return checked(GetWithdrawableDividends(address, account) + account.WithdrawnDividends) / TotalSupply;
    }

    /// <summary>
    /// Withdraws all dividends
    /// </summary>
    public void Withdraw()
    {
        var account = UpdateAccount(Message.Sender);
        var balance = account.DividendBalance / TotalSupply;
        var remainder = account.DividendBalance % TotalSupply;

        Assert(balance > 0, "The account has no dividends.");

        account.WithdrawnDividends = checked(account.WithdrawnDividends + account.DividendBalance - remainder);
        account.DividendBalance = remainder;

        SetAccount(Message.Sender, account);

        var transfer = Transfer(Message.Sender, balance);

        Assert(transfer.Success, "Transfer failed.");
    }

    public struct Account
    {
        /// <summary>
        /// Withdrawable Dividend Balance. Exact value should to divided by <see cref="TotalSupply"/>
        /// </summary>
        public ulong DividendBalance;

        /// <summary>
        /// 
        /// </summary>

        public ulong WithdrawnDividends;

        /// <summary>
        /// Dividends computed and added to <see cref="DividendBalance"/>
        /// </summary>

        public ulong CreditedDividends;
    }

    #region StandardToken code is inlined

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
    private bool TransferTokensTo(Address to, ulong amount)
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
    private bool TransferTokensFrom(Address from, Address to, ulong amount)
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
    #endregion
}