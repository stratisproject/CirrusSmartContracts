using Stratis.SmartContracts;

[Deploy]
public class SimpleBuyOrder : SmartContract
{
    /// <summary>
    /// Constructor creating a simple buy order setting the token, price, and amount to buy.
    /// </summary>
    /// <param name="smartContractState">The execution state for the contract.</param>
    /// <param name="token">The address of the src token being bought.</param>
    /// <param name="price">The price for each src token in stratoshis.</param>
    /// <param name="amount">The amount of src token to buy in full.</param>
    public SimpleBuyOrder(
        ISmartContractState smartContractState, 
        Address token,
        ulong price,
        ulong amount) : base (smartContractState)
    {
        Assert(price > 0, "Price must be greater than 0");
        Assert(amount > 0, "Amount must be greater than 0");
        Assert(Message.Value >= amount * price, "Balance is not enough to cover cost");
        Assert(PersistentState.IsContract(token), "Not a valid token address");

        Token = token;
        Price = price;
        Amount = amount;
        Buyer = Message.Sender;
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
    /// The price in stratoshis of each src token being bought.
    /// </summary>
    public ulong Price
    {
        get => PersistentState.GetUInt64(nameof(Price));
        private set => PersistentState.SetUInt64(nameof(Price), value);
    }

    /// <summary>
    /// The amount of src token being bought in full. 
    /// </summary>
    public ulong Amount
    {
        get => PersistentState.GetUInt64(nameof(Amount));
        private set => PersistentState.SetUInt64(nameof(Amount), value);
    }

    /// <summary>
    /// The buyer wallet address.
    /// </summary>
    public Address Buyer
    {
        get => PersistentState.GetAddress(nameof(Buyer));
        private set => PersistentState.SetAddress(nameof(Buyer), value);
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
    /// Fully or partially fills a buy order.
    /// </summary>
    /// <param name="amountToSell">The amount of SRC tokens to sell to the buyer in full.</param>
    /// <returns>Transaction result with trade details.</returns>
    public Transaction Sell(ulong amountToSell)
    {
        Assert(IsActive, "Contract is not active.");
        Assert(Message.Sender != Buyer, "Sender cannot be owner.");

        amountToSell = Amount >= amountToSell ? amountToSell : Amount;

        var cost = Price * amountToSell;
        Assert(Balance >= cost, "Not enough funds to cover cost.");

        var amountInStratoshis = amountToSell * 100_000_000;
        var transferResult = Call(Token, 0, "TransferFrom", new object[] { Message.Sender, Buyer, amountInStratoshis });

        Assert((bool)transferResult.ReturnValue == true, "Transfer failure.");

        Transfer(Message.Sender, cost);

        Amount -= amountToSell;

        if (Amount == 0)
        {
            CloseOrderExecute();
        }

        var txResult = new Transaction
        {
            Seller = Message.Sender,
            Price = Price,
            Amount = amountToSell,
            Block = Block.Number
        };

        Log(txResult);

        return txResult;
    }

    /// <summary>
    /// Closes the order and returns any CRS balance on the contract
    /// </summary>
    public void CloseOrder()
    {
        Assert(Message.Sender == Buyer);

        CloseOrderExecute();
    }

    private void CloseOrderExecute()
    {
        if (Balance > 0)
        {
            Transfer(Buyer, Balance);
        }

        IsActive = false;
    }

    /// <summary>
    /// Gets the latest details and status of the order.
    /// </summary>
    /// <returns>OrderDetails struct with latest order details.</returns>
    public OrderDetails GetOrderDetails()
    {
        return new OrderDetails
        {
            Buyer = Buyer,
            Token = Token,
            Price = Price,
            Amount = Amount,
            OrderType = nameof(SimpleBuyOrder),
            IsActive = IsActive,
            Balance = Balance
        };
    }

    public struct Transaction
    {
        /// <summary>
        /// The address of the buyer.
        /// </summary>
        [Index]
        public Address Seller;

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
        /// The address of the buyer.
        /// </summary>
        public Address Buyer;

        /// <summary>
        /// The address of the token being traded.
        /// </summary>
        public Address Token;

        /// <summary>
        /// The price of each token in stratoshis.
        /// </summary>
        public ulong Price;

        /// <summary>
        /// The amount of src tokens to buy in full.
        /// </summary>
        public ulong Amount;

        /// <summary>
        /// The order type of this trade (SimpleBuyOrder)
        /// </summary>
        public string OrderType;

        /// <summary>
        /// Flag describing the status of the contract.
        /// </summary>
        public bool IsActive;

        /// <summary>
        /// The current CRS balance of the contract in stratoshis.
        /// </summary>
        public ulong Balance;
    }
}
