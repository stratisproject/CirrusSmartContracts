using Stratis.SCL.Crypto;
using Stratis.SmartContracts;
using System;

/// <summary>
/// Implementation of a mintable token invoice contract for the Stratis Platform.
/// </summary>
[Deploy]
public class MintableTokenInvoice : SmartContract, IOwnable
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
        this.PendingOwner = Address.Zero;
        this.AuthorizationLimit = authorizationLimit;
        this.IdentityContract = identityContract;
        this.KYCProvider = 3 /* ClaimTopic.Shufti */;
        // Will use SetMintingFee to set the fee (if any).
        this.SetMintingFee(0, 0);
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

    public UInt256 FeeBase
    {
        get => State.GetUInt256(nameof(this.FeeBase));
        private set => State.SetUInt256(nameof(this.FeeBase), value);
    }

    public UInt256 FeeFactor
    {
        get => State.GetUInt256(nameof(this.FeeFactor));
        private set => State.SetUInt256(nameof(this.FeeFactor), value);
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

    public void SetMintingFee(UInt256 feeBase, UInt256 feeFactor)
    {
        Assert(Message.Sender == this.Owner);

        var previousFeeBase = this.FeeBase;
        var previousFeeFactor = this.FeeFactor;

        this.FeeBase = feeBase;
        this.FeeFactor = feeFactor;

        Log(new FeeChangedLog { PreviousFeeBase = previousFeeBase, PreviousFeeFactor = previousFeeFactor, FeeBase = feeBase, FeeFactor = feeFactor });
    }

    public UInt256 CalculateFee(UInt256 amount)
    {
        var fee = amount * this.FeeFactor / 100000000 + this.FeeBase;

        // Round up to 2 decimals.
        fee = ((fee + 999999) / 1000000) * 1000000;

        return fee;
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

    private void EnsureKYCdUserOnly(Address sender)
    {
        // KYC check. Call Identity contract.
        ITransferResult result = this.Call(IdentityContract, 0, "GetClaim", new object[] { sender, KYCProvider });
        Assert(result.Success, "Could not determine KYC status");

        // Only verified users are saved in the Identity contract.
        Assert(result.ReturnValue != null && ((byte[])result.ReturnValue).Length != 0, "Your KYC status is not valid");
    }

    private string CreateInvoiceInternal(Address address, string symbol, UInt256 amount, UInt256 fee, UInt128 uniqueNumber, string targetAddress, string targetNetwork)
    {
        EnsureKYCdUserOnly(address);

        string transactionReference = GetTransactionReference(uniqueNumber, address);

        var invoiceReference = GetInvoiceReference(transactionReference);

        // Ensure that this method can be called multiple times until all issues are resolved.
        var invoice = GetInvoice(invoiceReference);
        if (invoice.To != Address.Zero)
            // If called with the same unique number then the details should not change.
            Assert(invoice.To == address && invoice.Symbol == symbol && invoice.Amount == amount && invoice.Fee == fee && invoice.TargetNetwork == targetAddress && invoice.TargetNetwork == targetNetwork, "Transaction reference already exists");
        else
            // Allow the outcome of an invoice to be set when only references have been provided.
            invoice = new Invoice() { Symbol = symbol, Amount = amount, Fee = fee, To = address, TargetAddress = targetAddress, TargetNetwork = targetNetwork, Outcome = invoice.Outcome, IsAuthorized = amount < AuthorizationLimit };

        // If the invoice already has an outcome then just return it.
        Assert(string.IsNullOrEmpty(invoice.Outcome), invoice.Outcome);

        SetInvoice(invoiceReference, invoice);

        Log(new LogCreateInvoice() { IsAuthorized = invoice.IsAuthorized, InvoiceReference = invoiceReference, Sender = Message.Sender, Account = address, Symbol = symbol, Amount = amount, Fee = fee, UniqueNumber = uniqueNumber, TargetAddress = targetAddress, TargetNetwork = targetNetwork });

        // If the invoice is not authorized then return a message to the user.
        // We don't assert because we want to make the invoice available for approval and allow the user to resubmit the invoice.
        // We don't want to return the transaction reference until the invoice is authorized.
        if (!invoice.IsAuthorized)
        {
            return $"Obtain authorization for this invoice ({invoiceReference}) then resubmit this request.";
        }

        // Only provide the transaction reference if all checks pass.
        return transactionReference;
    }

    /// <inheritdoc />
    public string CreateInvoice(string symbol, UInt256 amount, UInt256 fee, UInt128 uniqueNumber, string targetAddress, string targetNetwork)
    {
        return CreateInvoiceInternal(Message.Sender, symbol, amount, fee, uniqueNumber, targetAddress, targetNetwork);
    }

    public string CreateInvoiceFor(Address address, string symbol, UInt256 amount, UInt256 fee, UInt128 uniqueNumber, string targetAddress, string targetNetwork, byte[] signature)
    {
        Assert(fee == CalculateFee(amount), "The provided to fee does not match the expected fee.");

        // E.g. for amount 110 and fee = 10 we mint 110 tokens and keep 10 minted tokens as fee.
        var template = new SignatureTemplate() { UniqueNumber = uniqueNumber, Amount = amount, Fee = fee, Symbol = symbol, TargetAddress = targetAddress, TargetNetwork = targetNetwork, Contract = this.Address };
        var res = Serializer.Serialize(template);
        Assert(ECRecover.TryGetSigner(res, signature, out Address signer), "Could not resolve signer.");
        Assert(signer == address, "Invalid signature.");

        return CreateInvoiceInternal(address, symbol, amount, fee, uniqueNumber, targetAddress, targetNetwork);
    }
    
    public string CreateInvoiceFromURL(Address address, string url, byte[] signature)
    {
        Assert(SSAS.TryGetSignerSHA256(Serializer.Serialize(url), signature, out Address signer), "Could not resolve signer.");
        Assert(signer == address, "Invalid signature.");

        var args = SSAS.GetURLArguments(url, new string[] { "uid", "symbol", "amount", "fee", "targetAddress", "targetNetwork", "contract" });

        Assert(args != null && args.Length == 7, "Invalid url.");
        Assert(Serializer.ToAddress(SSAS.ParseAddress(args[6], out _)) == this.Address, "Invalid contract address.");

        // Parse amount.
        string amount = args[2];
        int amountDecimalIndex = amount.IndexOf('.');
        int amountDecimals = amountDecimalIndex >= 0 ? amount.Length - amountDecimalIndex - 1 : 0;
        Assert(amountDecimals <= 2, "Too many decimals in amount");

        var amountUInt256 = UInt256.Parse(amount.PadRight(amount.Length + 8 - amountDecimals, '0').Replace(".", ""));

        // Parse fee.
        string fee = args[2];
        int feeDecimalIndex = fee.IndexOf('.');
        int feeDecimals = feeDecimalIndex >= 0 ? fee.Length - feeDecimalIndex - 1 : 0;
        Assert(feeDecimals <= 2, "Too many decimals in fee");

        var feeUInt256 = UInt256.Parse(fee.PadRight(fee.Length + 8 - feeDecimals, '0').Replace(".", ""));

        Assert(feeUInt256 == CalculateFee(amountUInt256), "The agreed to fee does not match the expected fee.");

        var res = new SignatureTemplate
        {
            UniqueNumber = UInt128.Parse($"0x{args[0]}"),
            Symbol = args[1],
            Amount = amountUInt256,
            Fee = feeUInt256,
            TargetAddress = args[4],
            TargetNetwork = args[5],
        };

        Log(new LogCreateInvoiceFromURL() { UniqueNumber = res.UniqueNumber, Account = address, Url = url, Signature = signature });

        return CreateInvoiceInternal(address, res.Symbol, res.Amount, res.Fee, res.UniqueNumber, res.TargetAddress, res.TargetNetwork);
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
                EnsureKYCdUserOnly(invoice.To);
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

        // If the invoice already has an outcome then just return it.
        Assert(string.IsNullOrEmpty(invoice.Outcome), invoice.Outcome);

        Assert(!invoice.IsAuthorized, "The invoice is already authorized.");

        invoice.IsAuthorized = true;
        SetInvoice(invoiceReference, invoice);

        Log(new LogChangeInvoiceAuthorization() { InvoiceReference = invoiceReference, NewAuthorized = true, OldAuthorized = invoice.IsAuthorized });

        return true;
    }

    /// <inheritdoc />
    public void SetAuthorizationLimit(UInt256 newLimit)
    {
        EnsureOwnerOnly();

        Log(new LogChangeAuthorizationLimit() { OldLimit = AuthorizationLimit, NewLimit = newLimit });

        AuthorizationLimit = newLimit;
    }

    public void SetOutcome(string transactionReference, string outcome)
    {
        EnsureOwnerOnly();

        var invoiceReference = GetInvoiceReference(transactionReference);
 
        Log(new LogChangeOutcome() { InvoiceReference = invoiceReference, Outcome = outcome });

        var invoice = GetInvoice(invoiceReference);
        invoice.Outcome = outcome;
        SetInvoice(invoiceReference, invoice);
    }

    /// <inheritdoc />
    public void SetIdentityContract(Address identityContract)
    {
        EnsureOwnerOnly();

        Log(new LogChangeIdentityContract() { OldContract = IdentityContract, NewContract = identityContract });

        IdentityContract = identityContract;
    }

    /// <inheritdoc />
    public void SetKYCProvider(uint kycProvider)
    {
        EnsureOwnerOnly();

        Log(new LogChangeKYCProvider() { OldProvider = KYCProvider, NewProvider = kycProvider });

        KYCProvider = kycProvider;
    }

    /// <inheritdoc />
    public void TransferOwnership(Address address)
    {
        EnsureOwnerOnly();

        PendingOwner = address;

        Log(new OwnershipTransferRequestedLog { CurrentOwner = Owner, PendingOwner = PendingOwner });
    }

    /// <inheritdoc />
    public void ClaimOwnership()
    {
        Assert(Message.Sender == PendingOwner, "Only the new owner can call this method");

        var previousOwner = Owner;

        Owner = PendingOwner;

        PendingOwner = Address.Zero;

        Log(new OwnershipTransferedLog() { NewOwner = Message.Sender, PreviousOwner = previousOwner });
    }

    private struct SignatureTemplate
    {
        public UInt128 UniqueNumber;
        public string Symbol;
        public UInt256 Amount;
        public UInt256 Fee;
        public string TargetAddress;
        public string TargetNetwork;
        public Address Contract;
    }

    /// <summary>
    /// Holds the details for the minting operation.
    /// </summary>
    public struct Invoice
    {
        public string Symbol;
        public UInt256 Amount;
        public UInt256 Fee;
        public Address To;
        public string TargetAddress;
        public string TargetNetwork;
        public string Outcome;
        public bool IsAuthorized;
    }

    public struct LogCreateInvoice
    {
        [Index] public string InvoiceReference;
        [Index] public Address Sender;
        [Index] public Address Account;
        [Index] public string Symbol;
        public UInt256 Amount;
        public UInt256 Fee;
        [Index] public UInt128 UniqueNumber;
        public string TargetAddress;
        public string TargetNetwork;
        public bool IsAuthorized;
    }

    public struct LogCreateInvoiceFromURL
    {
        [Index] public UInt128 UniqueNumber;
        [Index] public Address Account;
        public string Url;
        public byte[] Signature;
    }

    public struct LogChangeAuthorizationLimit
    {
        public UInt256 OldLimit;
        public UInt256 NewLimit;
    }

    public struct LogChangeKYCProvider
    {
        public uint OldProvider;
        public uint NewProvider;
    }

    public struct LogChangeIdentityContract
    {
        public Address OldContract;
        public Address NewContract;
    }

    public struct LogChangeInvoiceAuthorization
    {
        [Index] public string InvoiceReference;
        public bool OldAuthorized;
        public bool NewAuthorized;
    }

    public struct LogChangeOutcome
    {
        [Index] public string InvoiceReference;
        public string Outcome;
    }

    /// <summary>
    /// Provides a record that ownership was transferred from one account to another.
    /// </summary>
    public struct OwnershipTransferedLog
    {
        [Index]
        public Address PreviousOwner;

        [Index]
        public Address NewOwner;
    }

    public struct OwnershipTransferRequestedLog
    {
        [Index]
        public Address CurrentOwner;
        [Index]
        public Address PendingOwner;
    }

    public struct FeeChangedLog
    {
        public UInt256 PreviousFeeBase { get; set; }
        public UInt256 PreviousFeeFactor { get; set; }

        public UInt256 FeeBase { get; set; }
        public UInt256 FeeFactor { get; set; }
    }
}