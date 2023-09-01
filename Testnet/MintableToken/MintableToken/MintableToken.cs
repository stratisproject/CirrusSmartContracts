using Stratis.SmartContracts;
using Stratis.SmartContracts.Standards;

/// <summary>
/// Implementation of a standard token contract for the Stratis Platform.
/// </summary>
[Deploy]
public class MintableToken : SmartContract, IStandardToken256, IMintable, IBurnable, IMintableWithMetadata, IMintableWithMetadataForNetwork, IBurnableWithMetadata, IOwnable, IInterflux, IBlackList
{
    /// <summary>
    /// Constructor used to create a new instance of the token. Assigns the total token supply to the creator of the contract.
    /// </summary>
    /// <param name="smartContractState">The execution state for the contract.</param>
    /// <param name="totalSupply">The total token supply.</param>
    /// <param name="name">The name of the token.</param>
    /// <param name="symbol">The symbol used to identify the token.</param>
    /// <param name="nativeChain">The blockchain name of the token's native chain.</param>
    /// <param name="nativeAddress">The contract address of the token's native blockchain if available.</param>    
    public MintableToken(ISmartContractState smartContractState, UInt256 totalSupply, string name, string symbol,
         string nativeChain, string nativeAddress) : base(smartContractState)
    {
        this.TotalSupply = totalSupply;
        this.Name = name;
        this.Symbol = symbol;
        this.Owner = Message.Sender;
        this.PendingOwner = Address.Zero;
        this.NativeChain = nativeChain;
        this.NativeAddress = nativeAddress;
        this.Interflux = Message.Sender;
        this.Decimals = 8;
        this.SetBalance(Message.Sender, totalSupply);

        Log(new OwnershipTransferredLog { PreviousOwner = Address.Zero, NewOwner = Message.Sender });
        Log(new InterfluxChangedLog { PreviousInterflux = Address.Zero, NewInterflux = Message.Sender });
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

    public Address PendingOwner
    {
        get => State.GetAddress(nameof(this.PendingOwner));
        private set => State.SetAddress(nameof(this.PendingOwner), value);
    }

    public string NativeChain
    {
        get => State.GetString(nameof(this.NativeChain));
        private set => State.SetString(nameof(this.NativeChain), value);
    }

    public string NativeAddress
    {
        get => State.GetString(nameof(this.NativeAddress));
        private set => State.SetString(nameof(this.NativeAddress), value);
    }

    public Address Interflux
    {
        get => State.GetAddress(nameof(this.Interflux));
        private set => State.SetAddress(nameof(this.Interflux), value);
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

    private bool GetBlackListed(Address address)
    {
        return State.GetBool($"BlackListed:{address}");
    }

    private void SetBlackListed(Address address, bool value)
    {
        State.SetBool($"BlackListed:{address}", value);
    }

    /// <inheritdoc />
    public bool TransferTo(Address to, UInt256 amount)
    {
        BeforeTokenTransfer(Message.Sender, to);

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
        BeforeTokenTransfer(from, to);

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

    private void OnlyOwner()
    {
        Assert(Message.Sender == Owner, "Only the owner can call this method");
    }

    public void AddBlackList(Address address)
    {
        OnlyOwner();

        SetBlackListed(address, true);

        Log(new AddedBlackListLog() { BlackListedUser = address });
    }

    public void RemoveBlackList(Address address)
    {
        OnlyOwner();

        SetBlackListed(address, false);

        Log(new RemovedBlackListLog() { BlackListedUser = address });
    }

    public void DestroyBlackFunds(Address address)
    {
        OnlyOwner();

        Assert(GetBlackListed(address));

        UInt256 dirtyFunds = GetBalance(address);
        SetBalance(address, UInt256.Zero);
        TotalSupply -= dirtyFunds;

        Log(new DestroyBlackFundsLog() { BlackListedUser = address, DirtyFunds = dirtyFunds });
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
    public void TransferOwnership(Address pendingOwner)
    {
        OnlyOwner();

        PendingOwner = pendingOwner;

        Log(new OwnershipTransferRequestedLog { CurrentOwner = Owner, PendingOwner = PendingOwner });
    }

    /// <inheritdoc />
    public void ClaimOwnership()
    {
        Assert(Message.Sender == PendingOwner, "Only the new owner can call this method");

        var previousOwner = Owner;

        Owner = PendingOwner;

        PendingOwner = Address.Zero;

        Log(new OwnershipTransferredLog { PreviousOwner = previousOwner, NewOwner = Message.Sender });
    }

    public void SetInterflux(Address interflux)
    {
        OnlyOwner();

        var previousInterflux = Interflux;

        this.Interflux = interflux;

        Log(new InterfluxChangedLog { PreviousInterflux = previousInterflux, NewInterflux = Message.Sender });
    }

    private void InternalMint(Address account, UInt256 amount)
    {
        Assert(!GetBlackListed(account), "This address is blacklisted");

        UInt256 startingBalance = GetBalance(account);

        SetBalance(account, startingBalance + amount);

        Log(new TransferLog() { From = Address.Zero, To = account, Amount = amount });

        this.TotalSupply += amount;

        Log(new SupplyChangeLog()
        {
            PreviousSupply = (this.TotalSupply - amount),
            TotalSupply = this.TotalSupply
        });
    }   

    public void Mint(Address account, UInt256 amount)
    {
        Assert(Message.Sender == Interflux, "Only Interflux can call this method");

        InternalMint(account, amount);
    }

    /// <inheritdoc />
    public void MintWithMetadata(Address account, UInt256 amount, string metadata)
    {
        OnlyOwner();

        InternalMint(account, amount);

        Log(new MintMetadata() { To = account, Amount = amount, Metadata = metadata });
    }

    /// <inheritdoc />
    public void MintWithMetadataForNetwork(Address account, UInt256 amount, string metadata, string destinationAccount, string destinationNetwork)
    {
        OnlyOwner();

        Assert(Interflux != Address.Zero, "Interflux address not set");
        Assert(!string.IsNullOrEmpty(destinationAccount) && !string.IsNullOrEmpty(destinationNetwork) && !string.Equals(destinationNetwork, NativeChain, System.StringComparison.OrdinalIgnoreCase), "Invalid destination");

        InternalMint(account, amount);

        Log(new MintMetadata() { To = account, Amount = amount, Metadata = metadata });

        if (TransferFrom(account, Interflux, amount))
        {
            Log(new CrosschainLog()
            {
                Account = destinationAccount,
                Network = destinationNetwork
            });
        }
    }

    /// <inheritdoc />
    public void MintWithMetadataForCirrus(Address account, UInt256 amount, string metadata, Address destinationAccount)
    {
        OnlyOwner();

        Assert(destinationAccount != Address.Zero, "Invalid destination");

        InternalMint(account, amount);

        Log(new MintMetadata() { To = account, Amount = amount, Metadata = metadata });

        if (account != destinationAccount)
        {
            TransferFrom(account, destinationAccount, amount);
        }
                
        Log(new CirrusDestinationLog()
        {
            Account = destinationAccount,
        });
    }

    public bool Burn(UInt256 amount)
    {
        if (TransferTo(Address.Zero, amount))
        {
            this.TotalSupply -= amount;

            Log(new SupplyChangeLog()
            {
                PreviousSupply = (this.TotalSupply + amount),
                TotalSupply = this.TotalSupply
            });

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

    private void BeforeTokenTransfer(Address from, Address to)
    {
        Assert(!GetBlackListed(from) && !GetBlackListed(to), "This address is blacklisted");
    }

    public struct TransferLog
    {
        [Index]
        public Address From;

        [Index]
        public Address To;

        public UInt256 Amount;
    }

    /// <summary>
    /// Provides a record of an approval (change) to spend a certain number of coins.
    /// </summary>
    public struct ApprovalLog
    {
        [Index]
        public Address Owner;

        [Index]
        public Address Spender;

        public UInt256 OldAmount;

        public UInt256 Amount;
    }
 
    public struct MintMetadata
    {
        [Index] 
        public Address To;

        [Index] 
        public string Metadata;

        public UInt256 Amount;
    }

    public struct BurnMetadata
    {
        [Index] 
        public Address From;

        [Index] 
        public string Metadata;

        public UInt256 Amount;
    }

    /// <summary>
    /// Provides a record that the total supply changed.
    /// </summary>
    public struct SupplyChangeLog
    {
        public UInt256 PreviousSupply;

        public UInt256 TotalSupply;
    }

    public struct CrosschainLog
    {
        [Index] 
        public string Account;

        [Index] 
        public string Network;
    }
    
    public struct CirrusDestinationLog
    {
        [Index] 
        public Address Account;
    }

    public struct AddedBlackListLog
    {
        [Index] 
        public Address BlackListedUser;
    }

    public struct RemovedBlackListLog
    {
        [Index] 
        public Address BlackListedUser;
    }

    public struct DestroyBlackFundsLog
    {
        [Index] 
        public Address BlackListedUser;

        public UInt256 DirtyFunds;
    }


    /// <summary>
    /// Provides a record that ownership was transferred from one account to another.
    /// </summary>
    public struct OwnershipTransferredLog
    {
        [Index]
        public Address PreviousOwner;

        [Index]
        public Address NewOwner;
    }

    public struct InterfluxChangedLog
    {
        [Index]
        public Address PreviousInterflux;

        [Index]
        public Address NewInterflux;
    }

    public struct OwnershipTransferRequestedLog
    {
        [Index]
        public Address CurrentOwner;
        [Index]
        public Address PendingOwner;
    }
}