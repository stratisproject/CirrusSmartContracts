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

    private void ValidateKYC(Address sender, UInt256 invoiceReference)
    {
        // KYC check. Call Identity contract.
        ITransferResult result = this.Call(IdentityContract, 0, "GetClaim", new object[] { sender, KYCProvider });
        if (!(result?.Success ?? false))
        {
            string reason = "Could not determine KYC status";
            Log(new InvoiceResult() { InvoiceReference = invoiceReference, Success = false, Reason = reason });
            Assert(false, reason);
        }

        // The return value is a json string encoding of a Model.Claim object, represented as a byte array using ascii encoding.
        // The "Key" and "Description" fields of the json-encoded "Claim" object are expected to contain "Identity Approved".
        if (result.ReturnValue == null || !Serializer.ToString((byte[])result.ReturnValue).Contains("Identity Approved"))
        {
            string reason = "Your KYC status is not valid";
            Log(new InvoiceResult() { InvoiceReference = invoiceReference, Success = false, Reason = reason });
            Assert(false, reason);
        }
    }

    /// <inheritdoc />
    public bool CreateInvoice(string symbol, UInt256 amount, UInt256 uniqueNumber)
    {
        Address transactionReference = GetTransactionReference(uniqueNumber);
        var invoiceReference = GetInvoiceReference(transactionReference);

        Assert(GetOutcomeInternal(invoiceReference) == null, "The request has already been processed.");

        var invoiceBytes = RetrieveInvoice(transactionReference, false);
        if (invoiceBytes == null)
        {
            string reason = "Transaction reference already exists";
            Log(new InvoiceResult() { InvoiceReference = invoiceReference, Success = false, Reason = reason });
            Assert(false, reason);
        }

        ValidateKYC(Message.Sender, invoiceReference);

        var invoice = new Invoice() { Symbol = symbol, Amount = amount, To = Message.Sender };

        State.SetStruct($"Invoice:{invoiceReference}", invoice);

        Log(new InvoiceResult() { InvoiceReference = invoiceReference, Success = true });

        return true;
    }

    public UInt256 GetInvoiceReference(Address transactionReference)
    {
        // Hash the transaction reference to get the invoice reference.
        // This avoids the transaction reference being exposed in the SC state.
        return Serializer.ToUInt256(Keccak256(Serializer.Serialize(transactionReference)));
    }

    /// <inheritdoc />
    public byte[] RetrieveInvoice(Address transactionReference, bool recheckKYC)
    {
        var invoiceReference = GetInvoiceReference(transactionReference);

        var invoice = State.GetStruct<Invoice>($"Invoice:{invoiceReference}");

        if (invoice.To == Address.Zero)
            return null;

        // Do another last minute KYC check just in case the KYC was revoked since the invoice was created.
        if (recheckKYC)
            ValidateKYC(invoice.To, invoiceReference);

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

        Assert(GetOutcomeInternal(invoiceReference) == null, "The request has already been processed.");

        var wasAuthorized = State.GetBool($"Authorized:{invoiceReference}");

        State.SetBool($"Authorized:{invoiceReference}", true);

        Log(new ChangeInvoiceAuthorization() { InvoiceReference = invoiceReference, NewAuthorized = true, OldAuthorized = wasAuthorized });

        return true;
    }

    public bool IsAuthorized(Address transactionReference)
    {
        var invoiceReference = GetInvoiceReference(transactionReference);

        Assert(GetOutcomeInternal(invoiceReference) == null, "The request has already been processed.");

        var invoice = State.GetStruct<Invoice>($"Invoice:{invoiceReference}");
        if (invoice.Amount < AuthorizationLimit)
            return true;

        return State.GetBool($"Authorized:{invoiceReference}");
    }

    /// <inheritdoc />
    public void SetAuthorizationLimit(UInt256 newLimit)
    {
        EnsureOwnerOnly();

        Log(new ChangeAuthorizationLimit() { OldLimit = AuthorizationLimit, NewLimit = newLimit });

        AuthorizationLimit = newLimit;
    }

    public void SetOutcome(Address transactionReference, string outcome)
    {
        EnsureOwnerOnly();

        var invoiceReference = GetInvoiceReference(transactionReference);
        State.SetString($"Outcome:{invoiceReference}", outcome);
    }

    public string GetOutcomeInternal(UInt256 invoiceReference)
    {
        return State.GetString($"Outcome:{invoiceReference}");
    }

    public string GetOutcome(Address transactionReference)
    {
        return GetOutcomeInternal(GetInvoiceReference(transactionReference));
    }

    /// <inheritdoc />
    public void SetIdentityContract(Address identityContract)
    {
        EnsureOwnerOnly();

        Log(new ChangeIdentityContract() { OldContract = IdentityContract, NewContract = identityContract });

        IdentityContract = identityContract;
    }

    /// <inheritdoc />
    public void SetKYCProvider(uint kycProvider)
    {
        EnsureOwnerOnly();

        Log(new ChangeKYCProvider() { OldProvider = KYCProvider, NewProvider = kycProvider });

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

    /// <summary>
    /// Provides a record that ownership was transferred from one account to another.
    /// </summary>
    public struct OwnershipTransferred
    {
        [Index] public Address PreviousOwner;
        [Index] public Address NewOwner;
    }

    /// <summary>
    /// Holds the details for the minting operation.
    /// </summary>
    public struct Invoice
    {
        public string Symbol;
        public UInt256 Amount;
        public Address To;
    }

    public struct InvoiceResult
    {
        [Index] public UInt256 InvoiceReference;
        public bool Success;
        public string Reason;
    }

    public struct ChangeAuthorizationLimit
    {
        public UInt256 OldLimit;
        public UInt256 NewLimit;
    }

    public struct ChangeKYCProvider
    {
        public uint OldProvider;
        public uint NewProvider;
    }

    public struct ChangeIdentityContract
    {
        public Address OldContract;
        public Address NewContract;
    }

    public struct ChangeInvoiceAuthorization
    {
        [Index] public UInt256 InvoiceReference;
        public bool OldAuthorized;
        public bool NewAuthorized;
    }
}