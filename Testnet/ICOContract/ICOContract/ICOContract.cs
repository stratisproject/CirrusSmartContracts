using Stratis.SmartContracts;
using Stratis.SmartContracts.Standards;
using System;

[Deploy]
public class ICOContract : SmartContract
{
    private const ulong Satoshis = 100_000_000;
    private ulong EndBlock
    {
        get => this.PersistentState.GetUInt64(nameof(EndBlock));
        set => this.PersistentState.SetUInt64(nameof(EndBlock), value);
    }

    public Address TokenAddress
    {
        get => this.PersistentState.GetAddress(nameof(TokenAddress));
        private set => this.PersistentState.SetAddress(nameof(TokenAddress), value);
    }

    public ulong TokenBalance
    {
        get => this.PersistentState.GetUInt64(nameof(TokenBalance));
        private set => this.PersistentState.SetUInt64(nameof(TokenBalance), value);
    }

    public bool OnSale => EndBlock >= this.Block.Number && TokenBalance > 0;

    private Address Owner
    {
        get => this.PersistentState.GetAddress(nameof(Owner));
        set => this.PersistentState.SetAddress(nameof(Owner), value);
    }

    public SalePeriod[] SalePeriods
    {
        get => PersistentState.GetArray<SalePeriod>(nameof(SalePeriods));
        private set => PersistentState.SetArray(nameof(SalePeriods), value);
    }

    public ICOContract(ISmartContractState smartContractState,
                        ulong totalSupply,
                        string name,
                        string symbol,
                        byte[] salePeriods
                        ) : base(smartContractState)
    {
        var periods = Serializer.ToArray<SalePeriodInput>(salePeriods);
        

        ValidatePeriods(periods);

        var result = Create<StandardToken>(0, new object[] { totalSupply, name, symbol });

        Log(new StandardTokenCreationLog { Result = result.Success, StandardTokenAddress = result.NewContractAddress });

        Assert(result.Success, "Creating token contract failed.");

        TokenAddress = result.NewContractAddress;
        TokenBalance = totalSupply;
        Owner = Message.Sender;
        SetPeriods(periods);
    }


    public bool Invest()
    {
        Assert(OnSale, "ICO is completed.");

        var saleInfo = GetSaleInfo();

        var result = Call(TokenAddress, 0, nameof(StandardToken.TransferTo), new object[] { Message.Sender, saleInfo.SoldTokenAmount });

        var transferSuccess = result.Success && (bool)result.ReturnValue;

        Log(new TransferLog { Address = Message.Sender, TransferSuccess = transferSuccess, TokenAmount = saleInfo.SoldTokenAmount });

        if (!transferSuccess)
        {
            Transfer(Message.Sender, Message.Value);//refund
            return false;
        }

        TokenBalance -= saleInfo.SoldTokenAmount;

        if (saleInfo.RefundAmount > 0) // refund over sale amount
            Transfer(Message.Sender, saleInfo.RefundAmount);

        return true;
    }

    public bool TransferFunds(Address address)
    {
        Assert(!OnSale, "ICO is not ended yet.");
        Assert(Message.Sender == Owner, "Only contract owner can transfer funds.");

        var result = Transfer(address, Balance);

        return result.Success;
    }

    public ulong GetBalance(Address address)
    {
        var result = Call(TokenAddress, 0, nameof(StandardToken.GetBalance), new object[] { address });

        return (ulong)result.ReturnValue;
    }

    private SalePeriod GetCurrentPeriod()
    {
        foreach (var period in SalePeriods)
        {
            if (period.EndBlock < Block.Number)
                return period;
        }

        return default(SalePeriod);
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
                Multiplier = input.Multiplier
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
        var tokenAmount = checked(Message.Value * period.Multiplier) / Satoshis;
        
        if (tokenAmount > TokenBalance) // refund over sale
        {
            var overSale = tokenAmount - TokenBalance;

            var refund = (overSale * Satoshis) / period.Multiplier;

            return new SaleInfo { RefundAmount = refund, SoldTokenAmount = TokenBalance };
        }

        return new SaleInfo { RefundAmount = 0, SoldTokenAmount = tokenAmount };
    }

    public struct SalePeriodInput
    {
        public ulong DurationBlocks;
        public ulong Multiplier;
    }

    public struct SalePeriod
    {
        public ulong EndBlock;
        public ulong Multiplier;
    }

    public struct SaleInfo
    {
        public ulong RefundAmount;
        public ulong SoldTokenAmount;
    }

    public struct StandardTokenCreationLog
    {
        public bool Result;
        public Address StandardTokenAddress;
    }

    public struct TransferLog
    {
        public Address Address;
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