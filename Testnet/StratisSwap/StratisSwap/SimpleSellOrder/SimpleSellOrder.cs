using Stratis.SmartContracts;

[Deploy]
public class SimpleSellOrder : SmartContract
{
    /// <summary>
    /// Constructor creating a simple sell order setting the token, price, and amount to sell.
    /// </summary>
    /// <param name="smartContractState">The execution state for the contract.</param>
    /// <param name="token">The address of the src token being sold.</param>
    /// <param name="fullTokenInStratoshis">The number of stratoshis that make up 1 full SRC token.</param>
    /// <param name="price">The price for each src token in stratoshis.</param>
    /// <param name="amount">The amount of src token to sell in full.</param>
    public SimpleSellOrder(
        ISmartContractState smartContractState,
        Address token,
        uint fullTokenInStratoshis,
        ulong price,
        ulong amount) : base (smartContractState)
    {
        Assert(price > 0, "Price must be greater than 0");
        Assert(amount > 0, "Amount must be greater than 0");
        Assert(PersistentState.IsContract(token), "Not a valid token address");

        Token = token;
        FullTokenInStratoshis = fullTokenInStratoshis;
        Price = price;
        Amount = amount;
        Seller = Message.Sender;
        IsActive = true;
    }

    /// <summary>
    /// The contract address of the token being sold.
    /// </summary>
    public Address Token
    {
        get => PersistentState.GetAddress(nameof(Token));
        private set => PersistentState.SetAddress(nameof(Token), value);
    }

    /// <summary>
    /// The number of stratoshis that make up 1 full SRC token.
    /// </summary>
    public uint FullTokenInStratoshis
    {
        get => PersistentState.GetUInt32(nameof(FullTokenInStratoshis));
        private set => PersistentState.SetUInt32(nameof(FullTokenInStratoshis), value);
    }

    /// <summary>
    /// The price in stratoshis of each src token being sold.
    /// </summary>
    public ulong Price
    {
        get => PersistentState.GetUInt64(nameof(Price));
        private set => PersistentState.SetUInt64(nameof(Price), value);
    }

    /// <summary>
    /// The amount of src token being sold in full. 
    /// </summary>
    public ulong Amount
    {
        get => PersistentState.GetUInt64(nameof(Amount));
        private set => PersistentState.SetUInt64(nameof(Amount), value);
    }

    /// <summary>
    /// The seller wallet address.
    /// </summary>
    public Address Seller
    {
        get => PersistentState.GetAddress(nameof(Seller));
        private set => PersistentState.SetAddress(nameof(Seller), value);
    }

    /// <summary>
    /// Status flag for allowing/denying trades.
    /// </summary>
    public bool IsActive
    {
        get => PersistentState.GetBool(nameof(IsActive));
        private set => PersistentState.SetBool(nameof(IsActive), value);
    }

    /// <summary>
    /// Fully or partially fills a sell order.
    /// </summary>
    /// <param name="amountToBuy">The amount of SRC tokens to buy from the seller in full.</param>
    /// <returns>Transaction result with trade details.</returns>
    public Transaction Buy(ulong amountToBuy)
    {
        Assert(IsActive, "Contract is not active.");
        Assert(Message.Sender != Seller, "Sender cannot be owner.");

        amountToBuy = Amount >= amountToBuy ? amountToBuy : Amount;

        var cost = Price * amountToBuy;
        Assert(Message.Value >= cost, "Not enough funds to cover cost.");

        var amountToBuyInStratoshis = amountToBuy * FullTokenInStratoshis;
        var transferResult = Call(Token, 0, "TransferFrom", new object[] { Seller, Message.Sender, amountToBuyInStratoshis });

        Assert((bool)transferResult.ReturnValue == true, "Transfer failure.");

        Transfer(Seller, cost);

        var change = Message.Value - cost;
        if (change > 0)
        {
            Transfer(Message.Sender, change);
        }

        Amount -= amountToBuy;

        if (Amount == 0)
        {
            IsActive = false;
        }

        var txResult = new Transaction
        {
            Buyer = Message.Sender,
            Price = Price,
            Amount = amountToBuy,
            Block = Block.Number
        };

        Log(txResult);

        return txResult;
    }

    /// <summary>
    /// Close the order and prevent further trades against it.
    /// </summary>
    public void CloseOrder()
    {
        Assert(Message.Sender == Seller);

        IsActive = false;
    }

    /// <summary>
    /// Gets the latest details and status of the order.
    /// </summary>
    /// <returns>OrderDetails struct with latest order details.</returns>
    public OrderDetails GetOrderDetails()
    {
        var transferResult = Call(Token, 0, "Allowance", new object[] { Seller, Address });
        var balance = transferResult.Success ? (ulong)transferResult.ReturnValue : 0;

        return new OrderDetails
        {
            Seller = Seller,
            Token = Token,
            Price = Price,
            Amount = Amount,
            OrderType = nameof(SimpleSellOrder),
            IsActive = IsActive,
            Balance = balance,
            FullTokenInStratoshis = FullTokenInStratoshis
        };
    }

    public struct Transaction
    {
        /// <summary>
        /// The address of the buyer.
        /// </summary>
        [Index]
        public Address Buyer;

        /// <summary>
        /// The price in stratoshis per src token.
        /// </summary>
        public ulong Price;

        /// <summary>
        /// The full amount of src tokens traded.
        /// </summary>
        public ulong Amount;

        /// <summary>
        /// The block the transaction occured in.
        /// </summary>
        public ulong Block;
    }

    public struct OrderDetails
    {
        /// <summary>
        /// The address of the seller.
        /// </summary>
        public Address Seller;

        /// <summary>
        /// The address of the token being traded.
        /// </summary>
        public Address Token;

        /// <summary>
        /// The price of each token in stratoshis.
        /// </summary>
        public ulong Price;

        /// <summary>
        /// The amount of src tokens to sell in full.
        /// </summary>
        public ulong Amount;

        /// <summary>
        /// The order type of this trade (SimpleSellOrder)
        /// </summary>
        public string OrderType;

        /// <summary>
        /// Flag describing the status of the contract.
        /// </summary>
        public bool IsActive;

        /// <summary>
        /// The current allowance this contract has of the src token being traded in stratoshis.
        /// </summary>
        public ulong Balance;

        /// <summary>
        /// The number of stratoshis that make up 1 full SRC token being sold.
        /// </summary>
        public ulong FullTokenInStratoshis;
    }
}
