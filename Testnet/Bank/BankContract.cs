using Stratis.SmartContracts;

/// <summary>
/// A useless contract that stores the funds you send it until you withdraw them.
/// </summary>
[Deploy]
public class BankContract : SmartContract
{
    public BankContract(ISmartContractState contractState) : base(contractState)
    {
    }

    public ulong GetContractBalance()
    {
        return this.Balance;
    }

    public ulong GetBalance(Address address)
    {
        return this.State.GetUInt64($"Balance:{address}");
    }

    /// <summary>
    /// Test withdrawing deposited funds from a contract.
    /// </summary>
    public void Withdraw()
    {
        var balance = GetBalance(Message.Sender);

        Assert(balance > 0, "Sender does not have a balance");

        this.State.SetUInt64($"Balance:{Message.Sender}", 0);

        var transferResult = Transfer(Message.Sender, balance);

        Assert(transferResult.Success, "Withdrawal transfer failed!");

        Log(new WithdrawLog
        {
            To = Message.Sender,
            Amount = balance
        });
    }

    /// <summary>
    /// Test sending funds to a method.
    /// </summary>
    public void Deposit()
    {
        this.Receive();
    }

    /// <summary>
    /// Test sending funds directly to a contract.
    /// </summary>
    public override void Receive()
    {
        var currentBalance = GetBalance(Message.Sender);
        var newBalance = currentBalance + Message.Value;
        this.State.SetUInt64($"Balance:{Message.Sender}", newBalance);

        this.Log(new ReceiveLog
        {
            From = Message.Sender,
            Amount = Message.Value,
            Balance = newBalance
        });
    }

    /// <summary>
    /// Stores arbitrary string data under a key/value for the sender.
    /// </summary>    
    public void StoreData(string key, string value)
    {
        this.State.SetString($"{Message.Sender}:{key}", value);
    }

    /// <summary>
    /// Returns string data stored under a key for the sender.
    /// </summary>
    public string GetData(string key)
    {
        return this.State.GetString($"{Message.Sender}:{key}");
    }

    public struct ReceiveLog
    {
        [Index]
        public Address From;

        [Index]
        public ulong Amount;

        public ulong Balance;
    }

    public struct WithdrawLog
    {
        [Index]
        public Address To;

        [Index]
        public ulong Amount;
    }
}
