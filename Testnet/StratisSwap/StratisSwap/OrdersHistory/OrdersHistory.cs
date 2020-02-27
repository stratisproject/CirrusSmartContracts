using Stratis.SmartContracts;

[Deploy]
public class OrdersHistory : SmartContract
{
    /// <summary>
    /// Constructor to create a new orders history contract.
    /// </summary>
    /// <param name="smartContractState">The execution state for the contract.</param>
    public OrdersHistory(ISmartContractState smartContractState)
        : base(smartContractState) { }

    /// <summary>
    /// Add an order receipt to the contract.
    /// </summary>
    /// <param name="order"></param>
    /// <param name="token"></param>
    public void AddOrder(Address order, Address token)
    {
        Assert(PersistentState.IsContract(order)
            && PersistentState.IsContract(token));

        Log(new OrderLog
        {
            Owner = Message.Sender,
            Token = token,
            Order = order,
            Block = Block.Number
        });
    }

    /// <summary>
    /// Add an updated order receipt to the contract.
    /// </summary>
    /// <param name="order">The address of the order contract.</param>
    /// <param name="token">The address of the src token used in the order.</param>
    /// <param name="orderTxHash">The transactionHash of the order transaction.</param>
    public void UpdateOrder(Address order, Address token, string orderTxHash)
    {
        Assert(PersistentState.IsContract(order) 
            && PersistentState.IsContract(token)
            && !string.IsNullOrEmpty(orderTxHash));

        Log(new UpdatedOrderLog
        {
            Token = token,
            Order = order,
            OrderTxHash = orderTxHash,
            Block = Block.Number
        });
    }

    public struct UpdatedOrderLog
    {
        /// <summary>
        /// The address of the src token traded.
        /// </summary>
        [Index]
        public Address Token;

        /// <summary>
        /// The address of the order contract.
        /// </summary>
        [Index]
        public Address Order;

        /// <summary>
        /// The transactionHash of the order transaction.
        /// </summary>
        public string OrderTxHash;

        /// <summary>
        /// The block the update occurred.
        /// </summary>
        public ulong Block;
    }


    public struct OrderLog
    {
        /// <summary>
        /// The owner of the order contract.
        /// </summary>
        [Index]
        public Address Owner;

        /// <summary>
        /// The address of the src token traded.
        /// </summary>
        [Index]
        public Address Token;

        /// <summary>
        /// The address of the order contract.
        /// </summary>
        public Address Order;

        /// <summary>
        /// The block the update occurred.
        /// </summary>
        public ulong Block;
    }
}
