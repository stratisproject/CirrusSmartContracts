﻿using Stratis.SmartContracts;
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

    private void SetInvoice(UInt128 invoiceReference, Invoice invoice)
    {
        State.SetStruct($"Invoice:{invoiceReference}", invoice);
    }

    private Invoice GetInvoice(UInt128 invoiceReference)
    {
        return State.GetStruct<Invoice>($"Invoice:{invoiceReference}");
    }

    private struct TransactionReferenceTemplate
    {
        public UInt128 uniqueNumber;
        public Address address;
    }

    public Address GetTransactionReference(UInt128 uniqueNumber)
    {
        var template = new TransactionReferenceTemplate() { uniqueNumber = uniqueNumber, address = Message.Sender };

        var res = Serializer.Serialize(template);

        var transactionReference = Keccak256(res);

        Array.Resize(ref transactionReference, 20);

        return Serializer.ToAddress(transactionReference);
    }

    public UInt128 GetInvoiceReference(Address transactionReference)
    {
        // Hash the transaction reference to get the invoice reference.
        // This avoids the transaction reference being exposed in the SC state.
        var invoiceReference = Keccak256(Serializer.Serialize(transactionReference));

        Array.Resize(ref invoiceReference, 16);

        return Serializer.ToUInt128(invoiceReference);
    }

    private string ValidateKYC(Address sender, UInt128 invoiceReference)
    {
        // KYC check. Call Identity contract.
        ITransferResult result = this.Call(IdentityContract, 0, "GetClaim", new object[] { sender, KYCProvider });
        if (!(result?.Success ?? false))
        {
            string reason = "Could not determine KYC status";
            Log(new InvoiceResult() { InvoiceReference = invoiceReference, Success = false, Reason = reason });
            return reason;
        }

        // The return value is a json string encoding of a Model.Claim object, represented as a byte array using ascii encoding.
        // The "Key" and "Description" fields of the json-encoded "Claim" object are expected to contain "Identity Approved".
        if (result.ReturnValue == null || !Serializer.ToString((byte[])result.ReturnValue).Contains("Identity Approved"))
        {
            string reason = "Your KYC status is not valid";
            Log(new InvoiceResult() { InvoiceReference = invoiceReference, Success = false, Reason = reason });
            return reason;
        }

        return string.Empty;
    }

    /// <inheritdoc />
    public Address CreateInvoice(string symbol, UInt256 amount, UInt128 uniqueNumber)
    {
        Address transactionReference = GetTransactionReference(uniqueNumber);

        var invoiceReference = GetInvoiceReference(transactionReference);

        // Ensure that this method can be called multiple times until all issues are resolved.
        var invoice = GetInvoice(invoiceReference);
        if (invoice.To != Address.Zero)
            // If called with the same unique number then the details should not change.
            Assert(invoice.To != Message.Sender || invoice.Symbol != symbol && invoice.Amount != amount, "Transaction reference already exists");
        else
            // Allow the outcome of an invoice to be set when only references have been provided.
            invoice = new Invoice() { Symbol = symbol, Amount = amount, To = Message.Sender, Outcome = invoice.Outcome, IsAuthorized = invoice.Amount < AuthorizationLimit };

        // If the invoice already has an outcome then just return it.
        Assert(string.IsNullOrEmpty(invoice.Outcome), invoice.Outcome);

        string result = ValidateKYC(Message.Sender, invoiceReference);
        Assert(string.IsNullOrEmpty(result), "Obtain KYC verification for this address and then resubmit this request.");

        SetInvoice(invoiceReference, invoice);

        Assert(invoice.IsAuthorized, $"Obtain authorization for this invoice ({invoiceReference}) then resubmit this request.");

        Log(new InvoiceResult() { InvoiceReference = invoiceReference, Success = true });

        // Only provide the transaction reference if all checks pass.
        return transactionReference;
    }

    /// <inheritdoc />
    public byte[] RetrieveInvoice(UInt128 invoiceReference, bool recheckKYC)
    {
        var invoice = GetInvoice(invoiceReference);

        // Only recheck KYC on invoices that have not yet been processed.
        if (recheckKYC && invoice.To != Address.Zero && string.IsNullOrEmpty(invoice.Outcome))
        {
            // Do another last minute KYC check just in case the KYC was revoked since the invoice was created.
            if (recheckKYC)
                ValidateKYC(invoice.To, invoiceReference);
        }

        return Serializer.Serialize(invoice);
    }

    private void EnsureOwnerOnly()
    {
        Assert(Owner == Message.Sender, "Only the owner can call this method.");
    }

    public bool AuthorizeInvoice(UInt128 invoiceReference)
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

    public void SetOutcome(Address transactionReference, string outcome)
    {
        EnsureOwnerOnly();

        var invoiceReference = GetInvoiceReference(transactionReference);

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
        public string Outcome;
        public bool IsAuthorized;
    }

    public struct InvoiceResult
    {
        [Index] public UInt128 InvoiceReference;
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
        [Index] public UInt128 InvoiceReference;
        public bool OldAuthorized;
        public bool NewAuthorized;
    }
}