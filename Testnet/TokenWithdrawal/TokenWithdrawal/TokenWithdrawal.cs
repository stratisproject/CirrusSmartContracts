using Stratis.SmartContracts;

[Deploy]
public class TokenWithdrawal : SmartContract
{
    public TokenWithdrawal(ISmartContractState smartContractState) 
        : base(smartContractState)
    {
        // Initialize your state variables if needed
    }

    public enum WithdrawalStatus { Requested, Pending, Executed, Failed, Refunded }

    public uint WithdrawalSequence()
    {
        return this.State.GetUInt32("WithdrawalSequence");
    }

    public Withdrawal GetWithdrawal(uint seqNumber)
    {
        return this.State.GetStruct<Withdrawal>($"Withdrawal:{seqNumber}");
    }

    public Withdrawal GetWithdrawal(UInt128 uniqueNumber)
    {
        uint seqNumber = this.GetWithdrawalIndex(uniqueNumber);
        return this.GetWithdrawal(seqNumber);
    }

    public uint GetWithdrawalIndex(UInt128 uniqueNumber)
    {
        return this.State.GetUInt32($"WithdrawalIndex:{uniqueNumber}");
    }

    /// <summary>
    /// Called by the minting daemons after detecting transfers to this contract.
    /// </summary>
    /// <remarks>
    /// The daemons use the metadata associated with the transfer to look up the
    /// following additional transfer details:<list type="bullet">
    /// <item>Unique Number</item>
    /// <item>Cirrus Identity</item>
    /// <item>Fee</item>
    /// </list>
    /// </remarks>
    public void RegisterWithdrawal(UInt128 uniqueNumber, Address cirrusIdentity, string metadata, UInt256 amount, UInt256 fee)
    {
        //Assert(Message.Sender == /*Your multisig contract address*/);
        var withdrawal = new Withdrawal() { UniqueNumber = uniqueNumber, CirrusIdentity = cirrusIdentity, Metadata = metadata, Amount = amount, Fee = fee };
        this.SetWithdrawal(uniqueNumber, withdrawal);
        this.Log(new WithdrawalRequestedLog { UniqueNumber = uniqueNumber, CirrusIdentity = cirrusIdentity, Metadata = metadata, Amount = amount, Fee = fee });
    }

    private void ChangeStatus(UInt128 uniqueNumber, WithdrawalStatus newStatus)
    {
        var withdrawal = this.GetWithdrawal(uniqueNumber);
        var prevStatus = withdrawal.Status;
        withdrawal.Status = newStatus;
        this.SetWithdrawal(uniqueNumber, withdrawal);
        this.Log(new WithdrawalStatusChangedLog { UniqueNumber = uniqueNumber, PrevStatus = prevStatus, NewStatus = newStatus });
    }

    /// <summary>
    /// Called by "Payment Multisig" (initially Payment Operator (Web Demo UI)).
    /// </summary>
    /// <param name="uniqueNumber"></param>
    public void MarkAsPending(UInt128 uniqueNumber)
    {
        this.Assert(this.Message.Sender == /*Your Web Demo UI address*/);

        this.ChangeStatus(uniqueNumber, WithdrawalStatus.Pending);
    }

    /// <summary>
    /// Called by "Minting Nodes" (via Multisig).
    /// </summary>
    /// <param name="uniqueNumber"></param>
    public void MarkAsExecuted(UInt128 uniqueNumber)
    {
        this.Assert(Message.Sender == /*Your multisig contract address*/);

        this.ChangeStatus(uniqueNumber, WithdrawalStatus.Executed);
    }

    /// <summary>
    /// Called by "Payment Multisig" (initially Payment Operator (Web Demo UI)) or "Minting Nodes" (via Multisig).
    /// </summary>
    /// <param name="uniqueNumber"></param>
    public void MarkAsFailed(UInt128 uniqueNumber)
    {
        this.ChangeStatus(uniqueNumber, WithdrawalStatus.Failed);
    }

    private void SetWithdrawal(UInt128 uniqueNumber, Withdrawal withdrawal)
    {
        uint seqNumber = this.GetWithdrawalIndex(uniqueNumber);
        if (seqNumber == 0)
        {
            seqNumber = this.GetNextWithdrawalIndex();
            this.SetWithdrawalIndex(uniqueNumber, seqNumber);
            this.SetWithdrawal(seqNumber, withdrawal);
        }
    }

    private uint GetNextWithdrawalIndex()
    {
        var seqNumber = this.WithdrawalSequence();
        this.State.SetUInt32("WithdrawalSequence", seqNumber + 1);
        return seqNumber;
    }

    private void SetWithdrawal(uint seqNumber, Withdrawal withdrawal)
    {
        this.State.SetStruct($"Withdrawal:{seqNumber}", withdrawal);
    }

    private void SetWithdrawalIndex(UInt128 uniqueNumber, uint seqNumber)
    {
        this.State.SetUInt32($"WithdrawalIndex:{uniqueNumber}", seqNumber);
    }

    public struct Withdrawal
    {
        public UInt128 UniqueNumber;
        public Address CirrusIdentity;
        public string Metadata;
        public UInt256 Amount;
        public UInt256 Fee;
        public WithdrawalStatus Status;
    }

    public struct WithdrawalRequestedLog
    {
        public UInt128 UniqueNumber;
        public UInt256 Amount;
        public UInt256 Fee;
        public Address CirrusIdentity;
        public string Metadata;
    }

    public struct WithdrawalStatusChangedLog
    {
        public UInt128 UniqueNumber;
        public WithdrawalStatus PrevStatus;
        public WithdrawalStatus NewStatus;
    }
}
