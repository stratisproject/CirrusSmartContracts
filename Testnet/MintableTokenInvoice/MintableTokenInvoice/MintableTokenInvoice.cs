using Stratis.SCL.Crypto;
using Stratis.SmartContracts;

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

    private void SetInvoice(string invoiceReference, Invoice invoice)
    {
        State.SetStruct($"Invoice:{invoiceReference}", invoice);
    }

    private Invoice GetInvoice(string invoiceReference)
    {
        return State.GetStruct<Invoice>($"Invoice:{invoiceReference}");
    }

    private struct TransactionReferenceTemplate
    {
        public UInt128 uniqueNumber;
        public Address address;
    }

    private string GetTransactionReference(UInt128 uniqueNumber, Address address)
    {
        var template = new TransactionReferenceTemplate() { uniqueNumber = uniqueNumber, address = address };
        var res = Serializer.Serialize(template);

        var temp = Keccak256(res);
        var transactionReference = $"{((Serializer.ToUInt256(temp) % 10000000000) * 97 + 1)}".PadLeft(12, '0');

        return $"REF-{transactionReference.Substring(0, 4)}-{transactionReference.Substring(4, 4)}-{transactionReference.Substring(8, 4)}";
    }

    public string GetInvoiceReference(string transactionReference)
    {
        // Hash the transaction reference to get the invoice reference.
        // This avoids the transaction reference being exposed in the SC state.
        var temp = Keccak256(Serializer.Serialize(transactionReference));
        var invoiceReference = $"{((Serializer.ToUInt256(temp) % 10000000000) * 97 + 42)}".PadLeft(12, '0');

        return $"INV-{invoiceReference.Substring(0, 4)}-{invoiceReference.Substring(4, 4)}-{invoiceReference.Substring(8, 4)}";
    }

    private string ValidateKYC(Address sender)
    {
        // KYC check. Call Identity contract.
        ITransferResult result = this.Call(IdentityContract, 0, "GetClaim", new object[] { sender, KYCProvider });
        if (!(result?.Success ?? false))
        {
            return "Could not determine KYC status";
        }

        // The return value is a json string encoding of a Model.Claim object, represented as a byte array using ascii encoding.
        // The "Key" and "Description" fields of the json-encoded "Claim" object are expected to contain "Identity Approved".
        if (result.ReturnValue == null || !Serializer.ToString((byte[])result.ReturnValue).Contains("Identity Approved"))
        {
            return "Your KYC status is not valid";
        }

        return string.Empty;
    }

    private string CreateInvoiceInternal(Address address, string symbol, UInt256 amount, UInt128 uniqueNumber, string targetAddress, string targetNetwork)
    {
        string transactionReference = GetTransactionReference(uniqueNumber, address);

        var invoiceReference = GetInvoiceReference(transactionReference);

        // Ensure that this method can be called multiple times until all issues are resolved.
        var invoice = GetInvoice(invoiceReference);
        if (invoice.To != Address.Zero)
            // If called with the same unique number then the details should not change.
            Assert(invoice.To != address || invoice.Symbol != symbol && invoice.Amount != amount && invoice.TargetNetwork != targetAddress && invoice.TargetNetwork != targetNetwork, "Transaction reference already exists");
        else
            // Allow the outcome of an invoice to be set when only references have been provided.
            invoice = new Invoice() { Symbol = symbol, Amount = amount, To = address, TargetAddress = targetAddress, TargetNetwork = targetNetwork, Outcome = invoice.Outcome, IsAuthorized = amount < AuthorizationLimit };

        // If the invoice already has an outcome then just return it.
        Assert(string.IsNullOrEmpty(invoice.Outcome), invoice.Outcome);

        string result = ValidateKYC(address);
        Assert(string.IsNullOrEmpty(result), "Obtain KYC verification for this address and then resubmit this request.");

        SetInvoice(invoiceReference, invoice);

        Assert(invoice.IsAuthorized, $"Obtain authorization for this invoice ({invoiceReference}) then resubmit this request.");

        Log(new InvoiceResult() { InvoiceReference = invoiceReference, Success = true });

        // Only provide the transaction reference if all checks pass.
        return transactionReference;
    }

    /// <inheritdoc />
    public string CreateInvoice(string symbol, UInt256 amount, UInt128 uniqueNumber, string targetAddress, string targetNetwork)
    {
        return CreateInvoiceInternal(Message.Sender, symbol, amount, uniqueNumber, targetAddress, targetNetwork);
    }

    private struct SignatureTemplate
    {
        public UInt128 uniqueNumber;
        public string symbol;
        public UInt256 amount;
        public string targetAddress;
        public string targetNetwork;
    }

    public string CreateInvoiceFor(Address address, string symbol, UInt256 amount, UInt128 uniqueNumber, string targetAddress, string targetNetwork, byte[] signature)
    {
        var template = new SignatureTemplate() { uniqueNumber = uniqueNumber, amount = amount, symbol = symbol, targetAddress = targetAddress, targetNetwork = targetNetwork };
        var res = Serializer.Serialize(template);
        Assert(ECRecover.TryGetSigner(res, signature, out Address signer), "Could not resolve signer.");
        Assert(signer == address, "Invalid signature.");

        return CreateInvoiceInternal(address, symbol, amount, uniqueNumber, targetAddress, targetNetwork);
    }
    
    public string CreateInvoiceFromURL(Address address, string url, byte[] signature)
    {
        byte[] arguments = SSAS.ValidateAndParse(address, url, signature, "uid#11,symbol#4,amount#12,targetAddress#4,targetNetwork#4");
        Assert(arguments != null, "Invalid signature.");
        var res = Serializer.ToStruct<SignatureTemplate>(arguments);
        return CreateInvoiceInternal(address, res.symbol, res.amount, res.uniqueNumber, res.targetAddress, res.targetNetwork);
    }
    
    /// <inheritdoc />
    public byte[] RetrieveInvoice(string invoiceReference, bool recheckKYC)
    {
        var invoice = GetInvoice(invoiceReference);

        // Only recheck KYC on invoices that have not yet been processed.
        if (recheckKYC && invoice.To != Address.Zero && string.IsNullOrEmpty(invoice.Outcome))
        {
            // Do another last minute KYC check just in case the KYC was revoked since the invoice was created.
            if (recheckKYC)
                ValidateKYC(invoice.To);
        }

        return Serializer.Serialize(invoice);
    }

    private void EnsureOwnerOnly()
    {
        Assert(Owner == Message.Sender, "Only the owner can call this method.");
    }

    public bool AuthorizeInvoice(string invoiceReference)
    {
        EnsureOwnerOnly();

        var invoice = GetInvoice(invoiceReference);

        Assert(invoice.To != Address.Zero, "The invoice does not exist.");
        Assert(!string.IsNullOrEmpty(invoice.Outcome), "The transaction has already been processed.");

        invoice.IsAuthorized = true;
        SetInvoice(invoiceReference, invoice);

        Log(new ChangeInvoiceAuthorization() { InvoiceReference = invoiceReference, NewAuthorized = true, OldAuthorized = invoice.IsAuthorized });

        return true;
    }

    /// <inheritdoc />
    public void SetAuthorizationLimit(UInt256 newLimit)
    {
        EnsureOwnerOnly();

        Log(new ChangeAuthorizationLimit() { OldLimit = AuthorizationLimit, NewLimit = newLimit });

        AuthorizationLimit = newLimit;
    }

    public void SetOutcome(string transactionReference, string outcome)
    {
        EnsureOwnerOnly();

        var invoiceReference = GetInvoiceReference(transactionReference);
 
        Log(new ChangeOutcome() { InvoiceReference = invoiceReference, Outcome = outcome });

        var invoice = GetInvoice(invoiceReference);
        invoice.Outcome = outcome;
        SetInvoice(invoiceReference, invoice);
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
        public string TargetAddress;
        public string TargetNetwork;
        public string Outcome;
        public bool IsAuthorized;
    }

    public struct InvoiceResult
    {
        [Index] public string InvoiceReference;
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
        [Index] public string InvoiceReference;
        public bool OldAuthorized;
        public bool NewAuthorized;
    }

        public struct ChangeOutcome
    {
        [Index] public string InvoiceReference;
        public string Outcome;
    }
}