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
    /// <param name="totalSupply">The total token supply.</param>
    /// <param name="name">The name of the token.</param>
    /// <param name="symbol">The symbol used to identify the token.</param>
    /// <param name="decimals">The amount of decimals for display and calculation purposes.</param>
   public MintableTokenInvoice(ISmartContractState smartContractState) : base(smartContractState)
   {
        this.Owner = Message.Sender;
        this.NewOwner = Address.Zero;
        this.AuthorizationLimit = 0;
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

    private struct TransactionReferenceTemplate
    {
        public UInt256 randomSeed;
        public Address address;
    }

    public Address GetTransactionReference(UInt256 uniqueNumber)
    {
        var template = new TransactionReferenceTemplate() { randomSeed = uniqueNumber, address = Message.Sender };

        var transactionReference = Serializer.Serialize(template);

        return Serializer.ToAddress(transactionReference);
    }

    /// <inheritdoc />
    public bool CreateInvoice(string symbol, UInt256 amount, UInt256 uniqueNumber)
   {
        Address transactionReference = GetTransactionReference(uniqueNumber);

        var invoice = RetrieveInvoice(transactionReference);
        Assert(invoice.To != Address.Zero, "Transaction reference already exists");

        // TODO: KYC CHECK. Call Identity contract.
        // byte[] GetClaim(Address issuedTo, uint topic)

        // Hash the transaction reference to get the invoice reference.
        // This avoids the transaction reference being exposed in the SC state.
        var invoiceReference = Serializer.ToUInt256(Keccak256(Serializer.Serialize(transactionReference)));
        invoice = new Invoice() { Symbol = symbol, Amount = amount, To = Message.Sender };

        State.SetStruct($"Invoice:{invoiceReference}", invoice);

        return true;
   }

   /// <inheritdoc />
   public Invoice RetrieveInvoice(Address transactionReference)
   {
        // Hash the transaction reference to get the invoice reference.
        // This avoids the transaction reference being exposed in the SC state.
        var invoiceReference = Serializer.ToUInt256(Keccak256(Serializer.Serialize(transactionReference)));

        return State.GetStruct<Invoice>($"Invoice:{invoiceReference}");
   }

    private void EnsureOwnerOnly()
    {
        Assert(Owner == Message.Sender, "The method can be called by only owner.");
    }

    public bool AuthorizeInvoice(Address transactionReference)
    {
        EnsureOwnerOnly();

        // Hash the transaction reference to get the invoice reference.
        // This avoids the transaction reference being exposed in the SC state.
        var invoiceReference = Serializer.ToUInt256(Keccak256(Serializer.Serialize(transactionReference)));

        State.SetBool($"Authorized:{invoiceReference}", true);

        return true;
    }

    public bool IsAuthorized(Address transactionReference)
    {
        Invoice invoice = RetrieveInvoice(transactionReference);
        if (invoice.Amount < AuthorizationLimit)
            return true;

        // Hash the transaction reference to get the invoice reference.
        // This avoids the transaction reference being exposed in the SC state.
        var invoiceReference = Serializer.ToUInt256(Keccak256(Serializer.Serialize(transactionReference)));

        return State.GetBool($"Authorized:{invoiceReference}");
    }

    /// <inheritdoc />
    public void SetAuthorizationLimit(UInt256 newLimit)
    {
        EnsureOwnerOnly();

        AuthorizationLimit = newLimit;
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

    public struct Invoice
    {
        public string Symbol;
        public UInt256 Amount;
        public Address To;
    }
}