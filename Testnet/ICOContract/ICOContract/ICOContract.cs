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

    public bool IsNonFungibleToken
    {
        get => this.PersistentState.GetBool(nameof(IsNonFungibleToken));
        private set => this.PersistentState.SetBool(nameof(IsNonFungibleToken), value);
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
                       ulong totalSupply,
                       string name,
                       string symbol,
                       Address kycAddress,
                       byte[] salePeriods) : base(smartContractState)
    {
        Assert(tokenType < 3, $"The {nameof(tokenType)} parameter can be between 0 and 2");

        Assert(PersistentState.IsContract(kycAddress), "The kycAdress is not a contract adress");

        var periods = Serializer.ToArray<SalePeriodInput>(salePeriods);

        ValidatePeriods(periods);
        var tokenTypeEnum = (TokenType)tokenType;
        var result = CreateTokenContract(tokenTypeEnum, totalSupply, name, symbol);

        Assert(result.Success, "Creating token contract failed.");

        Log(new ICOSetupLog { TokenAddress = result.NewContractAddress });

        KYCAddress = kycAddress;
        TokenAddress = result.NewContractAddress;
        IsNonFungibleToken = tokenTypeEnum == TokenType.NonFungibleToken;
        TokenBalance = IsNonFungibleToken ? ulong.MaxValue : totalSupply;
        Owner = owner;
        SetPeriods(periods);
    }

    private ICreateResult CreateTokenContract(TokenType tokenType, ulong totalSupply, string name, string symbol)
    {
        switch (tokenType)
        {
            case TokenType.StandardToken: return Create<StandardToken>(parameters: new object[] { totalSupply, name, symbol });
            case TokenType.DividendToken: return Create<DividendToken>(parameters: new object[] { totalSupply, name, symbol });
            default: return Create<NonFungibleToken>(parameters: new object[] { name, symbol });
        }
    }
    public override void Receive() => Invest();

    public bool Invest()
    {
        Assert(SaleOpen, "The ICO is completed.");
        Assert(Message.Value > 0, "The amount should be higher than zero");

        EnsureKycVerified();

        var saleInfo = GetSaleInfo();

        var method = IsNonFungibleToken ? nameof(NonFungibleToken.MintAll) : nameof(IStandardToken.TransferTo);
        var result = Call(TokenAddress, 0, method, new object[] { Message.Sender, saleInfo.TokenAmount });

        Assert(result.Success && (bool)result.ReturnValue, "Token transfer failed.");

        Log(new InvestLog { Sender = Message.Sender, Invested = saleInfo.Invested, TokenAmount = saleInfo.TokenAmount, Refunded = saleInfo.RefundAmount });

        TokenBalance = checked(TokenBalance - saleInfo.TokenAmount);

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
        Assert(!IsNonFungibleToken, $"The {nameof(WithdrawTokens)} method is not supported for Non-Fungible Token.");
        Assert(Message.Sender == Owner, "Only contract owner can transfer tokens.");
        Assert(!SaleOpen, "ICO is not ended yet.");

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
        public Address TokenAddress;
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

    public enum TokenType : uint
    {
        StandardToken,
        DividendToken,
        NonFungibleToken
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

public class NonFungibleToken : SmartContract
{
    public struct TransferLog
    {
        [Index]
        public Address From;
        [Index]
        public Address To;
        [Index]
        public ulong TokenId;
    }

    public struct ApprovalLog
    {
        [Index]
        public Address Owner;
        [Index]
        public Address Approved;
        [Index]
        public ulong TokenId;
    }

    public struct ApprovalForAllLog
    {
        [Index]
        public Address Owner;
        [Index]
        public Address Operator;

        public bool Approved;
    }

    public struct OwnershipTransferedLog
    {
        [Index]
        public Address PreviousOwner;

        [Index]
        public Address NewOwner;
    }

    /// <summary>
    /// Get a value indicacting if the interface is supported.
    /// </summary>
    /// <param name="interfaceId">The id of the interface to support.</param>
    /// <returns>A value indicating if the interface is supported.</returns>
    private bool GetSupportedInterfaces(uint interfaceId)
    {
        return this.PersistentState.GetBool($"SupportedInterface:{interfaceId}");
    }

    /// <summary>
    /// Sets the supported interface value.
    /// </summary>
    /// <param name="interfaceId">The interface id.</param>
    /// <param name="value">A value indicating if the interface id is supported.</param>
    private void SetSupportedInterfaces(uint interfaceId, bool value) => this.PersistentState.SetBool($"SupportedInterface:{interfaceId}", value);

    /// <summary>
    /// Gets the key to the persistent state for the owner by NFT ID.
    /// </summary>
    /// <param name="id">The NFT ID.</param>
    /// <returns>The persistent storage key to get or set the NFT owner.</returns>
    private string GetIdToOwnerKey(ulong id) => $"IdToOwner:{id}";

    ///<summary>
    /// Gets the address of the owner of the NFT ID. 
    ///</summary>
    /// <param name="id">The ID of the NFT</param>
    ///<returns>The owner address.</returns>
    private Address GetIdToOwner(ulong id) => this.PersistentState.GetAddress(GetIdToOwnerKey(id));

    /// <summary>
    /// Sets the owner to the NFT ID.
    /// </summary>
    /// <param name="id">The ID of the NFT</param>
    /// <param name="value">The address of the owner.</param>
    private void SetIdToOwner(ulong id, Address value) => this.PersistentState.SetAddress(GetIdToOwnerKey(id), value);

    /// <summary>
    /// Gets the key to the persistent state for the approval address by NFT ID.
    /// </summary>
    /// <param name="id">The NFT ID.</param>
    /// <returns>The persistent storage key to get or set the NFT approval.</returns>
    private string GetIdToApprovalKey(ulong id) => $"IdToApproval:{id}";

    /// <summary>
    /// Getting from NFT ID the approval address.
    /// </summary>
    /// <param name="id">The ID of the NFT</param>
    /// <returns>Address of the approval.</returns>
    private Address GetIdToApproval(ulong id) => this.PersistentState.GetAddress(GetIdToApprovalKey(id));

    /// <summary>
    /// Setting to NFT ID to approval address.
    /// </summary>
    /// <param name="id">The ID of the NFT</param>
    /// <param name="value">The address of the approval.</param>
    private void SetIdToApproval(ulong id, Address value) => this.PersistentState.SetAddress(GetIdToApprovalKey(id), value);

    /// <summary>
    /// Gets the amount of non fungible tokens the owner has.
    /// </summary>
    /// <param name="address">The address of the owner.</param>
    /// <returns>The amount of non fungible tokens.</returns>
    private ulong GetOwnerToNFTokenCount(Address address) => this.PersistentState.GetUInt64($"OwnerToNFTokenCount:{address}");

    /// <summary>
    /// Sets the owner count of this non fungible tokens.
    /// </summary>
    /// <param name="address">The address of the owner.</param>
    /// <param name="value">The amount of tokens.</param>
    private void SetOwnerToNFTokenCount(Address address, ulong value) => this.PersistentState.SetUInt64($"OwnerToNFTokenCount:{address}", value);

    /// <summary>
    /// Gets the permission value of the operator authorization to perform actions on behalf of the owner.
    /// </summary>
    /// <param name="owner">The owner address of the NFT.</param>
    /// <param name="operatorAddress">>Address of the authorized operators</param>
    /// <returns>A value indicating if the operator has permissions to act on behalf of the owner.</returns>
    private bool GetOwnerToOperator(Address owner, Address operatorAddress) => this.PersistentState.GetBool($"OwnerToOperator:{owner}:{operatorAddress}");

    /// <summary>
    /// Sets the owner to operator permission.
    /// </summary>
    /// <param name="owner">The owner address of the NFT.</param>
    /// <param name="operatorAddress">>Address to add to the set of authorized operators.</param>
    /// <param name="value">The permission value.</param>
    private void SetOwnerToOperator(Address owner, Address operatorAddress, bool value) => this.PersistentState.SetBool($"OwnerToOperator:{owner}:{operatorAddress}", value);

    /// <summary>
    /// Owner of the contract is responsible to for minting/burning 
    /// </summary>
    public Address Owner
    {
        get => this.PersistentState.GetAddress(nameof(Owner));
        private set => this.PersistentState.SetAddress(nameof(Owner), value);
    }

    /// <summary>
    /// Name for non-fungible token contract
    /// </summary>
    public string Name
    {
        get => this.PersistentState.GetString(nameof(Name));
        private set => this.PersistentState.SetString(nameof(Name), value);
    }

    /// <summary>
    /// Symbol for non-fungible token contract
    /// </summary>
    public string Symbol
    {
        get => this.PersistentState.GetString(nameof(Symbol));
        private set => this.PersistentState.SetString(nameof(Symbol), value);
    }

    /// <summary>
    /// The next token index which is going to be minted
    /// </summary>
    private ulong NextTokenId
    {
        get => this.PersistentState.GetUInt64(nameof(NextTokenId));
        set => this.PersistentState.SetUInt64(nameof(NextTokenId), value);
    }

    private string GetTokenByIndexKey(ulong index) => $"TokenByIndex:{index}";

    private ulong GetTokenByIndex(ulong index) => this.PersistentState.GetUInt64(GetTokenByIndexKey(index));

    private void SetTokenByIndex(ulong index, ulong token) => this.PersistentState.SetUInt64(GetTokenByIndexKey(index), token);

    private void ClearTokenByIndex(ulong index) => this.PersistentState.Clear(GetTokenByIndexKey(index));

    private string GetIndexByTokenKey(ulong token) => $"IndexByToken:{token}";

    private ulong GetIndexByToken(ulong token) => this.PersistentState.GetUInt64(GetIndexByTokenKey(token));

    private void SetIndexByToken(ulong token, ulong index) => this.PersistentState.SetUInt64(GetIndexByTokenKey(token), index);

    private void ClearIndexByToken(ulong token) => this.PersistentState.Clear(GetIndexByTokenKey(token));

    private string GetTokenOfOwnerByIndexKey(Address address, ulong index) => $"TokenOfOwnerByIndex:{address}:{index}";

    private ulong GetTokenOfOwnerByIndex(Address address, ulong index) => this.PersistentState.GetUInt64(GetTokenOfOwnerByIndexKey(address, index));

    private void SetTokenOfOwnerByIndex(Address owner, ulong index, ulong tokenId) => this.PersistentState.SetUInt64(GetTokenOfOwnerByIndexKey(owner, index), tokenId);

    private void ClearTokenOfOwnerByIndex(Address owner, ulong index) => this.PersistentState.Clear(GetTokenOfOwnerByIndexKey(owner, index));

    private string IndexOfOwnerByTokenKey(Address owner, ulong tokenId) => $"IndexOfOwnerByToken:{owner}:{tokenId}";
    private ulong GetIndexOfOwnerByToken(Address owner, ulong tokenId) => this.PersistentState.GetUInt64(IndexOfOwnerByTokenKey(owner, tokenId));
    private void SetIndexOfOwnerByToken(Address owner, ulong tokenId, ulong index) => this.PersistentState.SetUInt64(IndexOfOwnerByTokenKey(owner, tokenId), index);
    private void ClearIndexOfOwnerByToken(Address owner, ulong tokenId) => this.PersistentState.Clear(IndexOfOwnerByTokenKey(owner, tokenId));
    public ulong TotalSupply
    {
        get => this.PersistentState.GetUInt64(nameof(TotalSupply));
        private set => this.PersistentState.SetUInt64(nameof(TotalSupply), value);
    }

    /// <summary>
    /// Constructor. Initializes the supported interfaces.
    /// </summary>
    /// <param name="state">The smart contract state.</param>
    public NonFungibleToken(ISmartContractState state, string name, string symbol) : base(state)
    {
        // todo: discuss callback handling and supported interface numbering with community.
        this.SetSupportedInterfaces((uint)0x00000001, true); // (ERC165) - ISupportsInterface
        this.SetSupportedInterfaces((uint)0x00000002, true); // (ERC721) - INonFungibleToken,
        this.SetSupportedInterfaces((uint)0x00000003, false); // (ERC721) - INonFungibleTokenReceiver
        this.SetSupportedInterfaces((uint)0x00000004, true); // (ERC721) - INonFungibleTokenMetadata
        this.SetSupportedInterfaces((uint)0x00000005, true); // (ERC721) - IERC721Enumerable

        this.Name = name;
        this.Symbol = symbol;
        this.Owner = Message.Sender;
        this.NextTokenId = 1;
    }

    public ulong TokenByIndex(ulong index)
    {
        Assert(index < TotalSupply, "The index is invalid.");

        return GetTokenByIndex(index);
    }

    public ulong TokenOfOwnerByIndex(Address owner, ulong index)
    {
        Assert(index < GetOwnerToNFTokenCount(owner), "The index is invalid.");

        return GetTokenOfOwnerByIndex(owner, index);
    }

    /// <summary>
    /// Function to check which interfaces are supported by this contract.
    /// </summary>
    /// <param name="interfaceID">Id of the interface.</param>
    /// <returns>True if <see cref="interfaceID"/> is supported, false otherwise.</returns>
    public bool SupportsInterface(uint interfaceID)
    {
        return GetSupportedInterfaces(interfaceID);
    }

    /// <summary>
    /// Transfers the ownership of an NFT from one address to another address. This function can
    /// be changed to payable.
    /// </summary>
    /// <remarks>Throws unless <see cref="Message.Sender"/> is the current owner, an authorized operator, or the
    /// approved address for this NFT.Throws if 'from' is not the current owner.Throws if 'to' is
    /// the zero address.Throws if 'tokenId' is not a valid NFT. When transfer is complete, this
    /// function checks if 'to' is a smart contract. If so, it calls
    /// 'OnNonFungibleTokenReceived' on 'to' and throws if the return value true.</remarks>
    /// <param name="from">The current owner of the NFT.</param>
    /// <param name="to">The new owner.</param>
    /// <param name="tokenId">The NFT to transfer.</param>
    /// <param name="data">Additional data with no specified format, sent in call to 'to'.</param>
    public void SafeTransferFrom(Address from, Address to, ulong tokenId, byte[] data)
    {
        SafeTransferFromInternal(from, to, tokenId, data);
    }

    /// <summary>
    /// Transfers the ownership of an NFT from one address to another address. This function can
    /// be changed to payable.
    /// </summary>
    /// <remarks>This works identically to the other function with an extra data parameter, except this
    /// function just sets data to an empty byte array.</remarks>
    /// <param name="from">The current owner of the NFT.</param>
    /// <param name="to">The new owner.</param>
    /// <param name="tokenId">The NFT to transfer.</param>
    public void SafeTransferFrom(Address from, Address to, ulong tokenId)
    {
        SafeTransferFromInternal(from, to, tokenId, new byte[0]);
    }

    /// <summary>
    /// Throws unless <see cref="Message.Sender"/> is the current owner, an authorized operator, or the approved
    /// address for this NFT.Throws if <see cref="from"/> is not the current owner.Throws if <see cref="to"/> is the zero
    /// address.Throws if <see cref="tokenId"/> is not a valid NFT. This function can be changed to payable.
    /// </summary>
    /// <remarks>The caller is responsible to confirm that <see cref="to"/> is capable of receiving NFTs or else
    /// they maybe be permanently lost.</remarks>
    /// <param name="from">The current owner of the NFT.</param>
    /// <param name="to">The new owner.</param>
    /// <param name="tokenId">The NFT to transfer.</param>
    public void TransferFrom(Address from, Address to, ulong tokenId)
    {
        CanTransfer(tokenId);

        Address tokenOwner = GetIdToOwner(tokenId);
        EnsureAddressIsNotEmpty(tokenOwner);
        EnsureAddressIsNotEmpty(to);
        Assert(tokenOwner == from);

        TransferInternal(to, tokenId);
    }

    /// <summary>
    /// Set or reaffirm the approved address for an NFT. This function can be changed to payable.
    /// </summary>
    /// <remarks>
    /// The zero address indicates there is no approved address. Throws unless <see cref="Message.Sender"/> is
    /// the current NFT owner, or an authorized operator of the current owner.
    /// </remarks>
    /// <param name="approved">Address to be approved for the given NFT ID.</param>
    /// <param name="tokenId">ID of the token to be approved.</param>
    public void Approve(Address approved, ulong tokenId)
    {
        CanOperate(tokenId);
        ValidNFToken(tokenId);

        Address tokenOwner = GetIdToOwner(tokenId);
        Assert(approved != tokenOwner);

        SetIdToApproval(tokenId, approved);
        LogApproval(tokenOwner, approved, tokenId);
    }

    /// <summary>
    /// Enables or disables approval for a third party ("operator") to manage all of
    /// <see cref="Message.Sender"/>'s assets. It also Logs the ApprovalForAll event.
    /// </summary>
    /// <remarks>This works even if sender doesn't own any tokens at the time.</remarks>
    /// <param name="operatorAddress">Address to add to the set of authorized operators.</param>
    /// <param name="approved">True if the operators is approved, false to revoke approval.</param>
    public void SetApprovalForAll(Address operatorAddress, bool approved)
    {
        SetOwnerToOperator(this.Message.Sender, operatorAddress, approved);
        LogApprovalForAll(this.Message.Sender, operatorAddress, approved);
    }

    /// <summary>
    /// Returns the number of NFTs owned by 'owner'. NFTs assigned to the zero address are
    /// considered invalid, and this function throws for queries about the zero address.
    /// </summary>
    /// <param name="owner">Address for whom to query the balance.</param>
    /// <returns>Balance of owner.</returns>
    public ulong BalanceOf(Address owner)
    {
        EnsureAddressIsNotEmpty(owner);
        return GetOwnerToNFTokenCount(owner);
    }

    /// <summary>
    /// Returns the address of the owner of the NFT. NFTs assigned to zero address are considered invalid, and queries about them do throw.
    /// </summary>
    /// <param name="tokenId">The identifier for an NFT.</param>
    /// <returns>Address of tokenId owner.</returns>
    public Address OwnerOf(ulong tokenId)
    {
        Address owner = GetIdToOwner(tokenId);
        EnsureAddressIsNotEmpty(owner);
        return owner;
    }

    /// <summary>
    /// Get the approved address for a single NFT.
    /// </summary>
    /// <remarks>Throws if 'tokenId' is not a valid NFT.</remarks>
    /// <param name="tokenId">ID of the NFT to query the approval of.</param>
    /// <returns>Address that tokenId is approved for. </returns>
    public Address GetApproved(ulong tokenId)
    {
        ValidNFToken(tokenId);

        return GetIdToApproval(tokenId);
    }

    /// <summary>
    /// Checks if 'operator' is an approved operator for 'owner'.
    /// </summary>
    /// <param name="owner">The address that owns the NFTs.</param>
    /// <param name="operatorAddress">The address that acts on behalf of the owner.</param>
    /// <returns>True if approved for all, false otherwise.</returns>
    public bool IsApprovedForAll(Address owner, Address operatorAddress)
    {
        return GetOwnerToOperator(owner, operatorAddress);
    }

    /// <summary>
    /// Actually preforms the transfer.
    /// </summary>
    /// <remarks>Does NO checks.</remarks>
    /// <param name="to">Address of a new owner.</param>
    /// <param name="tokenId">The NFT that is being transferred.</param>
    private void TransferInternal(Address to, ulong tokenId)
    {
        Address from = GetIdToOwner(tokenId);
        ClearApproval(tokenId);

        RemoveNFToken(from, tokenId);
        AddNFToken(to, tokenId);

        LogTransfer(from, to, tokenId);
    }

    /// <summary>
    /// Removes a NFT from owner.
    /// </summary>
    /// <remarks>Use and override this function with caution. Wrong usage can have serious consequences.</remarks>
    /// <param name="from">Address from wich we want to remove the NFT.</param>
    /// <param name="tokenId">Which NFT we want to remove.</param>
    private void RemoveNFToken(Address from, ulong tokenId)
    {
        Assert(GetIdToOwner(tokenId) == from);
        var tokenCount = GetOwnerToNFTokenCount(from);
        SetOwnerToNFTokenCount(from, checked(tokenCount - 1));
        this.PersistentState.Clear(GetIdToOwnerKey(tokenId));

        ulong index = GetIndexOfOwnerByToken(from, tokenId);
        ulong lastIndex = tokenCount - 1;

        if (index != lastIndex)
        {
            ulong lastToken = GetTokenOfOwnerByIndex(from, lastIndex);
            SetIndexOfOwnerByToken(from, lastToken, index);
            SetTokenOfOwnerByIndex(from, index, lastToken);
        }

        ClearTokenOfOwnerByIndex(from, lastIndex);
        ClearIndexOfOwnerByToken(from, tokenId);
    }

    /// <summary>
    /// Assignes a new NFT to owner.
    /// </summary>
    /// <remarks>Use and override this function with caution. Wrong usage can have serious consequences.</remarks>
    /// <param name="to">Address to which we want to add the NFT.</param>
    /// <param name="tokenId">Which NFT we want to add.</param>
    private void AddNFToken(Address to, ulong tokenId)
    {
        Assert(GetIdToOwner(tokenId) == Address.Zero);

        SetIdToOwner(tokenId, to);
        ulong currentTokenAmount = GetOwnerToNFTokenCount(to);
        SetOwnerToNFTokenCount(to, checked(currentTokenAmount + 1));

        var index = currentTokenAmount;
        SetIndexOfOwnerByToken(to, tokenId, index);
        SetTokenOfOwnerByIndex(to, index, tokenId);
    }

    /// <summary>
    /// Actually perform the safeTransferFrom.
    /// </summary>
    /// <param name="from">The current owner of the NFT.</param>
    /// <param name="to">The new owner.</param>
    /// <param name="tokenId">The NFT to transfer.</param>
    /// <param name="data">Additional data with no specified format, sent in call to 'to' if it is a contract.</param>
    private void SafeTransferFromInternal(Address from, Address to, ulong tokenId, byte[] data)
    {
        CanTransfer(tokenId);
        ValidNFToken(tokenId);

        Address tokenOwner = GetIdToOwner(tokenId);
        Assert(tokenOwner == from);
        EnsureAddressIsNotEmpty(to);

        TransferInternal(to, tokenId);

        if (this.PersistentState.IsContract(to))
        {
            ITransferResult result = this.Call(to, 0, "OnNonFungibleTokenReceived", new object[] { this.Message.Sender, from, tokenId, data }, 0);
            Assert((bool)result.ReturnValue);
        }
    }

    /// <summary>
    /// Clears the current approval of a given NFT ID.
    /// </summary>
    /// <param name="tokenId">ID of the NFT to be transferred</param>
    private void ClearApproval(ulong tokenId)
    {
        if (GetIdToApproval(tokenId) != Address.Zero)
        {
            this.PersistentState.Clear(GetIdToApprovalKey(tokenId));
        }
    }

    /// <summary>
    /// This logs when ownership of any NFT changes by any mechanism. This event logs when NFTs are
    /// created('from' == 0) and destroyed('to' == 0). Exception: during contract creation, any
    /// number of NFTs may be created and assigned without logging Transfer.At the time of any
    /// transfer, the approved Address for that NFT (if any) is reset to none.
    /// </summary>
    /// <param name="from">The from address.</param>
    /// <param name="to">The to address.</param>
    /// <param name="tokenId">The NFT ID.</param>
    private void LogTransfer(Address from, Address to, ulong tokenId)
    {
        Log(new TransferLog() { From = from, To = to, TokenId = tokenId });
    }

    /// <summary>
    /// This logs when the approved Address for an NFT is changed or reaffirmed. The zero
    /// Address indicates there is no approved Address. When a Transfer logs, this also
    /// indicates that the approved Address for that NFT (if any) is reset to none.
    /// </summary>
    /// <param name="owner">The owner address.</param>
    /// <param name="operatorAddress">The approved address.</param>
    /// <param name="tokenId">The NFT ID.</ >
    private void LogApproval(Address owner, Address approved, ulong tokenId)
    {
        Log(new ApprovalLog() { Owner = owner, Approved = approved, TokenId = tokenId });
    }

    /// <summary>
    /// This logs when an operator is enabled or disabled for an owner. The operator can manage all NFTs of the owner.
    /// </summary>
    /// <param name="owner">The owner address</param>
    /// <param name="operatorAddress">The operator address.</param>
    /// <param name="approved">A boolean indicating if it has been approved.</param>        
    private void LogApprovalForAll(Address owner, Address operatorAddress, bool approved)
    {
        Log(new ApprovalForAllLog() { Owner = owner, Operator = operatorAddress, Approved = approved });
    }


    /// <summary>
    /// Guarantees that the <see cref="Message.Sender"/> is an owner or operator of the given NFT.
    /// </summary>
    /// <param name="tokenId">ID of the NFT to validate.</param>
    private void CanOperate(ulong tokenId)
    {
        Address tokenOwner = GetIdToOwner(tokenId);
        Assert(tokenOwner == this.Message.Sender || GetOwnerToOperator(tokenOwner, this.Message.Sender));
    }

    /// <summary>
    /// Guarantees that the msg.sender is allowed to transfer NFT.
    /// </summary>
    /// <param name="tokenId">ID of the NFT to transfer.</param>
    private void CanTransfer(ulong tokenId)
    {
        Address tokenOwner = GetIdToOwner(tokenId);
        Assert(
          tokenOwner == this.Message.Sender
          || GetIdToApproval(tokenId) == Message.Sender
          || GetOwnerToOperator(tokenOwner, Message.Sender)
        );
    }

    /// <summary>
    /// Guarantees that tokenId is a valid Token.
    /// </summary>
    /// <param name="tokenId">ID of the NFT to validate.</param>
    private void ValidNFToken(ulong tokenId)
    {
        Address tokenOwner = GetIdToOwner(tokenId);
        EnsureAddressIsNotEmpty(tokenOwner);
    }

    /// <summary>
    /// Sets the contract owner who can mint/bur
    /// </summary>
    /// <param name="owner"></param>
    public void TransferOwnership(Address owner)
    {
        EnsureOwnerOnly();
        Assert(owner != Address.Zero, $"The {nameof(owner)} parameter can not be default(zero) address.");

        Log(new OwnershipTransferedLog { PreviousOwner = this.Owner, NewOwner = owner });

        this.Owner = owner;
    }

    private void EnsureOwnerOnly()
    {
        Assert(Message.Sender == Owner, "Only owner of the contract can set new owner.");
    }

    /// <summary>
    /// Mints new tokens
    /// </summary>
    /// <param name="address">The address that will own the minted NFT</param>
    /// <param name="amount">Number of tokens will be created</param>
    public void MintAll(Address address, ulong amount)
    {
        EnsureOwnerOnly();
        EnsureAddressIsNotEmpty(address);
        Assert(amount > 0, "the amount should be higher than zero");

        var index = TotalSupply;
        var lastIndex = checked(index + amount);
        var tokenId = NextTokenId;

        while (index < lastIndex)
        {
            AddNFToken(address, tokenId);
            SetTokenByIndex(index, tokenId);
            SetIndexByToken(tokenId, index);

            Log(new TransferLog { From = Address.Zero, To = address, TokenId = tokenId });

            checked
            {
                index++;
                tokenId++;
            }
        }

        TotalSupply = checked(TotalSupply + amount);
        NextTokenId = tokenId;
    }

    public void Burn(ulong tokenId)
    {
        Address tokenOwner = GetIdToOwner(tokenId);

        EnsureAddressIsNotEmpty(tokenOwner);

        Assert(tokenOwner == Message.Sender, "Only token owner can burn the token.");

        ClearApproval(tokenId);
        RemoveNFToken(tokenOwner, tokenId);

        //move last token to removed token and delete last token info
        var index = GetIndexByToken(tokenId);
        var lastTokenIndex = checked(--TotalSupply);
        var lastToken = GetTokenByIndex(lastTokenIndex);

        SetTokenByIndex(index, lastToken);
        SetIndexByToken(lastToken, index);

        ClearTokenByIndex(lastTokenIndex);
        ClearIndexByToken(tokenId);

        Log(new TransferLog { From = tokenOwner, To = Address.Zero, TokenId = tokenId });
    }

    public void EnsureAddressIsNotEmpty(Address address)
    {
        Assert(address != Address.Zero, "The address can not be zero.");
    }
}