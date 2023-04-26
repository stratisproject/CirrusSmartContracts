using Stratis.SmartContracts;
using Stratis.SmartContracts.Standards;

[Deploy]
public class STOContract : SmartContract
{
    public ulong EndBlock
    {
        get => State.GetUInt64(nameof(EndBlock));
        private set => State.SetUInt64(nameof(EndBlock), value);
    }

    public Address TokenAddress
    {
        get => State.GetAddress(nameof(TokenAddress));
        private set => State.SetAddress(nameof(TokenAddress), value);
    }

    public Address KYCAddress
    {
        get => State.GetAddress(nameof(KYCAddress));
        private set => State.SetAddress(nameof(KYCAddress), value);
    }

    public Address MapperAddress
    {
        get => State.GetAddress(nameof(MapperAddress));
        private set => State.SetAddress(nameof(MapperAddress), value);
    }

    public UInt256 TokenBalance
    {
        get => State.GetUInt256(nameof(TokenBalance));
        private set => State.SetUInt256(nameof(TokenBalance), value);
    }

    public bool IsNonFungibleToken
    {
        get => State.GetBool(nameof(IsNonFungibleToken));
        private set => State.SetBool(nameof(IsNonFungibleToken), value);
    }

    public bool SaleOpen => EndBlock >= Block.Number && TokenBalance > 0;

    public Address Owner
    {
        get => State.GetAddress(nameof(Owner));
        private set => State.SetAddress(nameof(Owner), value);
    }

    public SalePeriod[] SalePeriods
    {
        get => State.GetArray<SalePeriod>(nameof(SalePeriods));
        private set => State.SetArray(nameof(SalePeriods), value);
    }

    public STOContract(ISmartContractState smartContractState,
                       Address owner,
                       uint tokenType,
                       UInt256 totalSupply,
                       string name,
                       string symbol,
                       uint decimals,
                       Address kycAddress,
                       Address mapperAddress,
                       byte[] salePeriods) : base(smartContractState)
    {
        Assert(tokenType < 3, $"The {nameof(tokenType)} parameter can be between 0 and 2.");

        Assert(State.IsContract(kycAddress), $"The {nameof(kycAddress)} is not a contract adress.");
        Assert(State.IsContract(mapperAddress), $"The {nameof(mapperAddress)} is not a contract adress.");

        var periods = Serializer.ToArray<SalePeriodInput>(salePeriods);

        ValidatePeriods(periods);
        var tokenTypeEnum = (TokenType)tokenType;
        var result = CreateTokenContract(tokenTypeEnum, totalSupply, name, symbol, decimals);

        Assert(result.Success, "Creating token contract failed.");

        Log(new STOSetupLog { TokenAddress = result.NewContractAddress });

        KYCAddress = kycAddress;
        MapperAddress = mapperAddress;
        TokenAddress = result.NewContractAddress;
        IsNonFungibleToken = tokenTypeEnum == TokenType.NonFungibleToken;
        TokenBalance = IsNonFungibleToken ? (UInt256)ulong.MaxValue : totalSupply;
        Owner = owner;
        SetPeriods(periods);
    }

    private ICreateResult CreateTokenContract(TokenType tokenType, UInt256 totalSupply, string name, string symbol, uint decimals)
    {
        switch (tokenType)
        {
            case TokenType.StandardToken: return Create<StandardToken>(parameters: new object[] { totalSupply, name, symbol, decimals });
            case TokenType.DividendToken: return Create<DividendToken>(parameters: new object[] { totalSupply, name, symbol, decimals });
            default: return Create<NonFungibleToken>(parameters: new object[] { name, symbol });
        }
    }
    public override void Receive() => Invest();

    public bool Invest()
    {
        Assert(SaleOpen, "The STO is completed.");
        Assert(Message.Value > 0, "The amount should be higher than zero");

        EnsureKycVerified();

        var saleInfo = GetSaleInfo();

        var result = IsNonFungibleToken ?
                        Call(TokenAddress, 0, nameof(NonFungibleToken.MintAll), new object[] { Message.Sender, (ulong)saleInfo.TokenAmount }) :
                        Call(TokenAddress, 0, nameof(IStandardToken.TransferTo), new object[] { Message.Sender, saleInfo.TokenAmount });

        Assert(result.Success && (bool)result.ReturnValue, "Token transfer failed.");

        Log(new InvestLog { Sender = Message.Sender, Invested = saleInfo.Invested, TokenAmount = saleInfo.TokenAmount, Refunded = saleInfo.RefundAmount });

        TokenBalance -= saleInfo.TokenAmount;

        if (saleInfo.RefundAmount > 0) // refund over sold amount
        {
            result = Transfer(Message.Sender, saleInfo.RefundAmount);
            Assert(result.Success, "Refund failed.");
        }

        return true;
    }

    private void EnsureKycVerified()
    {
        var result = Call(MapperAddress, 0, "GetSecondaryAddress", new object[] { Message.Sender });

        if (result.Success && result.ReturnValue is Address identityAddress && identityAddress != Address.Zero)
        {
            result = Call(KYCAddress, 0, "GetClaim", new object[] { identityAddress, (uint)3 /*shufti kyc*/ });

            Assert(result.Success && result.ReturnValue is byte[] b && b?.Length > 0, "Your KYC is not verified.");

            return;
        }

        Assert(false, "The address has no mapping.");
    }

    public bool WithdrawFunds()
    {
        Assert(Message.Sender == Owner, "Only contract owner can transfer funds.");
        Assert(!SaleOpen, "STO is not ended yet.");

        var result = Transfer(Owner, Balance);

        return result.Success;
    }


    public bool WithdrawTokens()
    {
        Assert(!IsNonFungibleToken, $"The {nameof(WithdrawTokens)} method is not supported for Non-Fungible Token.");
        Assert(Message.Sender == Owner, "Only contract owner can transfer tokens.");
        Assert(!SaleOpen, "STO is not ended yet.");

        if (TokenBalance == 0)
            return true;

        var result = Call(TokenAddress, 0, nameof(StandardToken.TransferTo), new object[] { Message.Sender, TokenBalance });

        TokenBalance = 0;

        Assert(result.Success && (bool)result.ReturnValue, "Token transfer failed.");

        return true;
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
            blockNumber = checked(blockNumber + input.DurationBlocks);
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
            var spend = (ulong)tokenBalance * period.PricePerToken;
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
        public UInt256 TokenAmount;

    }

    public struct STOSetupLog
    {
        public Address TokenAddress;
    }

    public struct InvestLog
    {
        [Index]
        public Address Sender;
        public ulong Invested;
        public UInt256 TokenAmount;
        public ulong Refunded;
    }
    public enum TokenType : uint
    {
        StandardToken,
        DividendToken,
        NonFungibleToken
    }
}
