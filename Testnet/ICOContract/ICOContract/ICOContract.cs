using Stratis.SmartContracts;
using Stratis.SmartContracts.Standards;
using System;

[Deploy]
public class ICOContract : SmartContract
{
    public ulong EndBlock
    {
        get => this.PersistentState.GetUInt64(nameof(EndBlock));
        private set => this.PersistentState.SetUInt64(nameof(EndBlock), value);
    }

    public Address TokenAddress
    {
        get => this.PersistentState.GetAddress(nameof(TokenAddress));
        private set => this.PersistentState.SetAddress(nameof(TokenAddress), value);
    }

    public Address KYCAddress
    {
        get => this.PersistentState.GetAddress(nameof(KYCAddress));
        private set => this.PersistentState.SetAddress(nameof(KYCAddress), value);
    }

    public ulong TokenBalance
    {
        get => this.PersistentState.GetUInt64(nameof(TokenBalance));
        private set => this.PersistentState.SetUInt64(nameof(TokenBalance), value);
    }

    public bool SaleOpen => EndBlock >= this.Block.Number && TokenBalance > 0;

    public Address Owner
    {
        get => this.PersistentState.GetAddress(nameof(Owner));
        private set => this.PersistentState.SetAddress(nameof(Owner), value);
    }

    public SalePeriod[] SalePeriods
    {
        get => PersistentState.GetArray<SalePeriod>(nameof(SalePeriods));
        private set => PersistentState.SetArray(nameof(SalePeriods), value);
    }

    private const ulong Satoshis = 100_000_000;

    public ICOContract(ISmartContractState smartContractState,
                       Address owner,
                       uint tokenType,
                       Address kycAddress,
                       ulong totalSupply,
                       string name,
                       string symbol,
                       byte[] salePeriods) : base(smartContractState)
    {
        Assert(tokenType < 2, $"The {nameof(tokenType)} parameter can be 0 or 1 only");

        Assert(PersistentState.IsContract(kycAddress), "The kycAdress is not a contract adress");

        var periods = Serializer.ToArray<SalePeriodInput>(salePeriods);

        ValidatePeriods(periods);

        var parameters = new object[] { totalSupply, name, symbol };

        var result = tokenType == 0 ? Create<StandardToken>(parameters: parameters)
                                    : Create<DividendToken>(parameters: parameters);

        Assert(result.Success, "Creating token contract failed.");

        Log(new ICOSetupLog { StandardTokenAddress = result.NewContractAddress });

        KYCAddress = kycAddress;
        TokenAddress = result.NewContractAddress;
        TokenBalance = totalSupply;
        Owner = owner;
        SetPeriods(periods);
    }

    public override void Receive() => Invest();

    public bool Invest()
    {
        Assert(SaleOpen, "The ICO is completed.");
        Assert(Message.Value > 0, "The amount should be higher than zero");

        EnsureKycVerified();

        var saleInfo = GetSaleInfo();

        var result = Call(TokenAddress, 0, nameof(StandardToken.TransferTo), new object[] { Message.Sender, saleInfo.TokenAmount });

        Assert(result.Success && (bool)result.ReturnValue, "Token transfer failed.");

        Log(new InvestLog { Sender = Message.Sender, Invested = saleInfo.Invested, TokenAmount = saleInfo.TokenAmount, Refunded = saleInfo.RefundAmount });

        TokenBalance -= saleInfo.TokenAmount;

        if (saleInfo.RefundAmount > 0) // refund over sold amount
            Transfer(Message.Sender, saleInfo.RefundAmount);

        return true;
    }

    private void EnsureKycVerified()
    {
        var result = Call(KYCAddress, 0, "GetClaim", new object[] { Message.Sender, 3 /*shufti kyc*/ });

        Assert(result.Success && result.ReturnValue != null, "Your KYC is not verified.");
    }

    public bool WithdrawFunds()
    {
        Assert(Message.Sender == Owner, "Only contract owner can transfer funds.");
        Assert(!SaleOpen, "ICO is not ended yet.");

        var result = Transfer(this.Owner, Balance);

        return result.Success;
    }

    public bool WithdrawTokens()
    {
        Assert(Message.Sender == Owner, "Only contract owner can transfer tokens.");
        Assert(!SaleOpen, "ICO is not ended yet.");

        var result = Call(TokenAddress, 0, nameof(StandardToken.TransferTo), new object[] { Message.Sender, TokenBalance });

        Assert(result.Success && (bool)result.ReturnValue, "Token transfer failed.");

        return true;
    }

    public ulong GetBalance(Address address)
    {
        var result = Call(TokenAddress, 0, nameof(StandardToken.GetBalance), new object[] { address });

        return (ulong)result.ReturnValue;
    }

    private SalePeriod GetCurrentPeriod()
    {
        var result = default(SalePeriod);

        foreach (var period in SalePeriods)
        {
            if (period.EndBlock >= Block.Number)
            {
                result = period;
                break;
            }
        }

        return result;
    }
    private void SetPeriods(SalePeriodInput[] periods)
    {
        var salePeriods = ConvertSalePeriodInputs(periods);

        SalePeriods = salePeriods;
        EndBlock = salePeriods[salePeriods.Length - 1].EndBlock;
    }

    private SalePeriod[] ConvertSalePeriodInputs(SalePeriodInput[] periods)
    {
        var result = new SalePeriod[periods.Length];
        var blockNumber = Block.Number;
        for (int i = 0; i < periods.Length; i++)
        {
            var input = periods[i];
            blockNumber += input.DurationBlocks;
            result[i] = new SalePeriod
            {
                EndBlock = blockNumber,
                PricePerToken = input.PricePerToken
            };
        }

        return result;
    }

    private void ValidatePeriods(SalePeriodInput[] periods)
    {
        Assert(periods.Length > 0, "Please provide at least 1 sale period");

        foreach (var period in periods)
        {
            Assert(period.DurationBlocks > 0, "DurationBlocks should higher than zero");
        }
    }

    private SaleInfo GetSaleInfo()
    {
        var period = GetCurrentPeriod();

        var tokenAmount = Message.Value / period.PricePerToken;

        var tokenBalance = TokenBalance;
        if (tokenAmount > tokenBalance) // refund over sold amount
        {
            var spend = checked(tokenBalance * period.PricePerToken);
            var refund = Message.Value - spend;

            return new SaleInfo { Invested = spend, RefundAmount = refund, TokenAmount = tokenBalance };
        }

        return new SaleInfo { Invested = Message.Value, TokenAmount = tokenAmount };
    }

    public struct SalePeriodInput
    {
        public ulong DurationBlocks;
        public ulong PricePerToken;
    }

    public struct SalePeriod
    {
        public ulong EndBlock;
        public ulong PricePerToken;
    }

    public struct SaleInfo
    {
        public ulong Invested;
        public ulong RefundAmount;
        public ulong TokenAmount;

    }

    public struct ICOSetupLog
    {
        public Address StandardTokenAddress;
    }

    public struct InvestLog
    {
        [Index]
        public Address Sender;
        public ulong Invested;
        public ulong TokenAmount;
        public ulong Refunded;
    }

    public struct Claim
    {
        public string Key;

        public string Description;

        public bool IsRevoked;
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

    /// <summary>
    /// It is advised that deposit amount should to be evenly divided by total supply, 
    /// otherwise small amount of satoshi may lost(burn)
    /// </summary>
    public override void Receive()
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

    Account UpdateAccount(Address address)
    {
        var account = GetAccount(address);
        var newDividends = GetWithdrawableDividends(address, account);

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
        var newDividends = Dividends - account.CreditedDividends;
        var notCreditedDividends = checked(GetBalance(address) * newDividends);

        return checked(account.DividendBalance + notCreditedDividends); //Delay divide by TotalSupply to final stage for avoid decimal value loss.
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

        var transfer = Transfer(Message.Sender, balance);

        Assert(transfer.Success, "Transfer failed.");

        account.WithdrawnDividends = checked(account.WithdrawnDividends + account.DividendBalance - remainder);
        account.DividendBalance = remainder;

        SetAccount(Message.Sender, account);
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