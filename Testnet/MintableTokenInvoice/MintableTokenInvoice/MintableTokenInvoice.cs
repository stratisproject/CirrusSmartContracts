using Stratis.SmartContracts;
using System;

/// <summary>
/// Implementation of a mintable token invoice contract for the Stratis Platform.
/// </summary>
[Deploy]
public class MintableTokenInvoice : SmartContract, IPullOwnership
{
    /// <summary>
    /// Constructor used to create a new instance of the token. Assigns the total token supply to the creator of the contract.
    /// </summary>
    /// <param name="smartContractState">The execution state for the contract.</param>
    /// <param name="authorizationLimit">Any amounts greater or equal to this will require authorization.</param>
    /// <param name="identityContract">The address of the identity contract.</param>
   public MintableTokenInvoice(ISmartContractState smartContractState, UInt256 authorizationLimit, Address identityContract) : base(smartContractState)
   {
        this.Owner = Message.Sender;
        this.NewOwner = Address.Zero;
        this.AuthorizationLimit = authorizationLimit;
        this.IdentityContract = identityContract;
        this.KYCProvider = 3 /* ClaimTopic.Shufti */;
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

    public UInt256 AuthorizationLimit
    {
        get => State.GetUInt256(nameof(this.AuthorizationLimit));
        private set => State.SetUInt256(nameof(this.AuthorizationLimit), value);
    }

    public Address IdentityContract
    {
        get => State.GetAddress(nameof(this.IdentityContract));
        private set => State.SetAddress(nameof(this.IdentityContract), value);
    }

    public uint KYCProvider
    {
        get => State.GetUInt32(nameof(KYCProvider));
        private set => State.SetUInt32(nameof(this.KYCProvider), value);
    }

    private struct TransactionReferenceTemplate
    {
        public UInt256 randomSeed;
        public Address address;
    }

    public Address GetTransactionReference(UInt256 uniqueNumber)
    {
        var template = new TransactionReferenceTemplate() { randomSeed = uniqueNumber, address = Message.Sender };

        var transactionReference = Keccak256(Serializer.Serialize(template));

        Array.Resize(ref transactionReference, 20);

        return Serializer.ToAddress(transactionReference);
    }

    private bool ValidateKYC(Address transactionReference)
    {
        // KYC check. Call Identity contract.
        ITransferResult result = this.Call(IdentityContract, 0, "GetClaim", new object[] { Message.Sender, KYCProvider });
        if (result?.Success ?? false)
        {
            Log(new Execution() { Sender = Message.Sender, TransactionReference = transactionReference });
        }
        else
        {
            Log(new ExecutionFailure() { Sender = Message.Sender, TransactionReference = transactionReference });

            return false;
        }

        if (result.ReturnValue == null)
            return false;

        var claim = Serializer.ToStruct<Claim>((byte[])result.ReturnValue);

        return claim.Key == "Identity Approved" && !claim.IsRevoked;
    }

    /// <inheritdoc />
    public bool CreateInvoice(string symbol, UInt256 amount, UInt256 uniqueNumber)
   {
        Address transactionReference = GetTransactionReference(uniqueNumber);

        var invoiceBytes = RetrieveInvoice(transactionReference);
        Assert(invoiceBytes == null, "Transaction reference already exists");

        Assert(ValidateKYC(transactionReference), "Your KYC status is not valid");

        var invoiceReference = GetInvoiceReference(transactionReference);
        var invoice = new Invoice() { Symbol = symbol, Amount = amount, To = Message.Sender };

        State.SetStruct($"Invoice:{invoiceReference}", invoice);

        return true;
   }

    private UInt256 GetInvoiceReference(Address transactionReference)
    {
        // Hash the transaction reference to get the invoice reference.
        // This avoids the transaction reference being exposed in the SC state.
        return Serializer.ToUInt256(Keccak256(Serializer.Serialize(transactionReference)));
    }

    /// <inheritdoc />
    public byte[] RetrieveInvoice(Address transactionReference)
   {
        var invoiceReference = GetInvoiceReference(transactionReference);

        var invoice = State.GetStruct<Invoice>($"Invoice:{invoiceReference}");

        if (invoice.To == Address.Zero)
            return null;

        return Serializer.Serialize(invoice);
   }

    private void EnsureOwnerOnly()
    {
        Assert(Owner == Message.Sender, "Only the owner can call this method.");
    }

    public bool AuthorizeInvoice(Address transactionReference)
    {
        EnsureOwnerOnly();

        var invoiceReference = GetInvoiceReference(transactionReference);

        var wasAuthorized = State.GetBool($"Authorized:{invoiceReference}");

        State.SetBool($"Authorized:{invoiceReference}", true);

        Log(new Authorize() { InvoiceReference = invoiceReference, NewAuthorized = true, OldAuthorized = wasAuthorized });

        return true;
    }

    public bool IsAuthorized(Address transactionReference)
    {
        var invoiceReference = GetInvoiceReference(transactionReference);
        var invoice = State.GetStruct<Invoice>($"Invoice:{invoiceReference}");
        if (invoice.Amount < AuthorizationLimit)
            return true;

        return State.GetBool($"Authorized:{invoiceReference}");
    }

    /// <inheritdoc />
    public void SetAuthorizationLimit(UInt256 newLimit)
    {
        EnsureOwnerOnly();

        Log(new SetLimit() { OldLimit = AuthorizationLimit, NewLimit = newLimit });

        AuthorizationLimit = newLimit;
    }

    /// <inheritdoc />
    public void SetIdentityContract(Address identityContract)
    {
        EnsureOwnerOnly();

        Log(new SetContract() { OldContract = IdentityContract, NewContract = identityContract });

        IdentityContract = identityContract;
    }

    /// <inheritdoc />
    public void SetKYCProvider(uint kycProvider)
    {
        EnsureOwnerOnly();

        Log(new SetProvider() { OldProvider = KYCProvider, NewProvider = kycProvider });

        KYCProvider = kycProvider;
    }

    /// <inheritdoc />
    public void SetNewOwner(Address address)
    {
        EnsureOwnerOnly();

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

    public struct Claim
    {
        public string Key;
        public string Description;
        public bool IsRevoked;
    }

    /// <summary>
    /// Provides a record that ownership was transferred from one account to another.
    /// </summary>
    public struct OwnershipTransferred
    {
        [Index] public Address PreviousOwner;
        [Index] public Address NewOwner;
    }

    public struct Invoice
    {
        public string Symbol;
        public UInt256 Amount;
        public Address To;
    }

    public struct Execution
    {
        [Index] public Address Sender;
        [Index] public Address TransactionReference;
    }

    public struct ExecutionFailure
    {
        [Index] public Address Sender;
        [Index] public Address TransactionReference;
    }

    public struct SetLimit
    {
        public UInt256 OldLimit;
        public UInt256 NewLimit;
    }

    public struct SetProvider
    {
        public uint OldProvider;
        public uint NewProvider;
    }

    public struct SetContract
    {
        public Address OldContract;
        public Address NewContract;
    }

    public struct Authorize
    {
        [Index] public UInt256 InvoiceReference;
        public bool OldAuthorized;
        public bool NewAuthorized;
    }
}