using Stratis.SmartContracts;
using Stratis.SmartContracts.Standards;

/// <summary>
/// Implementation of a standard token contract for the Stratis Platform.
/// </summary>
[Deploy]
public class MintableToken : SmartContract, IStandardToken256, IMintable, IBurnable, IMintableWithMetadata, IMintableWithMetadataForNetwork, IBurnableWithMetadata, IPullOwnership, IInterflux, IBlackListable
{
    /// <summary>
    /// Constructor used to create a new instance of the token. Assigns the total token supply to the creator of the contract.
    /// </summary>
    /// <param name="smartContractState">The execution state for the contract.</param>
    /// <param name="totalSupply">The total token supply.</param>
    /// <param name="name">The name of the token.</param>
    /// <param name="symbol">The symbol used to identify the token.</param>
    /// <param name="decimals">The amount of decimals for display and calculation purposes.</param>
    public MintableToken(ISmartContractState smartContractState, UInt256 totalSupply, string name, string symbol,
         byte decimals) : base(smartContractState)
    {
        this.TotalSupply = totalSupply;
        this.Name = name;
        this.Symbol = symbol;
        this.Decimals = decimals;
        this.Owner = Message.Sender;
        this.NewOwner = Address.Zero;
        this.Interflux = Message.Sender;
        this.SetBalance(Message.Sender, totalSupply);
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
    public void SetNewOwner(Address address)
    {
        Assert(Message.Sender == Owner, "Only the owner can call this method");

        NewOwner = address;
    }

    /// <inheritdoc />
    public void ClaimOwnership()
    {
        Assert(Message.Sender == NewOwner, "Only the new owner can call this method");

        var previousOwner = Owner;

        Owner = NewOwner;

        NewOwner = Address.Zero;

        Log(new OwnershipTransferred() { NewOwner = Message.Sender, PreviousOwner = previousOwner });
    }

    public void SetInterflux(Address interflux)
    {
        Assert(Message.Sender == Owner, "Only the owner can call this method");

        this.Interflux = interflux;
    }

    public void Mint(Address account, UInt256 amount)
    {
        Assert(Message.Sender == Interflux || Message.Sender == Owner, "Only the owner or interflux can call this method");

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

    /// <inheritdoc />
    public void MintWithMetadata(Address account, UInt256 amount, string metadata)
    {
        Mint(account, amount);

        Log(new MintMetadata() { To = account, Amount = amount, Metadata = metadata });
    }

    /// <inheritdoc />
    public void MintWithMetadataForNetwork(Address account, UInt256 amount, string metadata, string destinationAccount, string destinationNetwork)
    {
        Assert(!string.IsNullOrEmpty(destinationAccount) && !string.IsNullOrEmpty(destinationNetwork) && destinationNetwork != "CIRRUS", "Invalid destination");

        MintWithMetadata(account, amount, metadata);

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
        Assert(destinationAccount != Address.Zero, "Invalid destination");

        MintWithMetadata(account, amount, metadata);

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

    public void AddToBlackList(byte[] accounts)
    {
        Assert(Message.Sender == Owner, "Only the owner can call this method");

        foreach (Address address in Serializer.ToArray<Address>(accounts)) 
        {
            SetBlackListed(address, true);
            Log(new BlackListLog() { Account = address, BlackListed = true });
        }
    }

    public void RemoveFromBlackList(Address account)
    {
        Assert(Message.Sender == Owner, "Only the owner can call this method");

        SetBlackListed(account, false);
        Log(new BlackListLog() { Account = account, BlackListed = false });
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

    /// <summary>
    /// Provides a record that ownership was transferred from one account to another.
    /// </summary>
    public struct OwnershipTransferred
    {
        [Index] public Address PreviousOwner;

        [Index] public Address NewOwner;
    }

    public struct MintMetadata
    {
        [Index] public Address To;

        [Index] public string Metadata;

        public UInt256 Amount;
    }

    public struct BurnMetadata
    {
        [Index] public Address From;

        [Index] public string Metadata;

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
        [Index] public string Account;
        [Index] public string Network;
    }
    
    public struct CirrusDestinationLog
    {
        [Index] public Address Account;
    }

    public struct BlackListLog
    {
        [Index] public Address Account;
        public bool BlackListed;
    }
}