using Stratis.SmartContracts;
using Stratis.SmartContracts.Standards;
using System.Linq;
using System;

public class Airdrop : SmartContract 
{
    public Airdrop(ISmartContractState smartContractState, ulong totalSupply, Address tokenContractAddress, ulong endBlock)
        : base(smartContractState)
    {
        this.TotalSupply = totalSupply;
        this.TokenContractAddress = tokenContractAddress;
        this.EndBlock = endBlock;
    }

    public ulong TotalSupply
    {
        get => PersistentState.GetUInt64(nameof(this.TotalSupply));
        private set => PersistentState.SetUInt64(nameof(this.TotalSupply), value);
    }

    public Address TokenContractAddress
    {
        get => PersistentState.GetAddress(nameof(this.TokenContractAddress));
        private set => PersistentState.SetAddress(nameof(TokenContractAddress), value);
    }

    public ulong EndBlock
    {
        get => PersistentState.GetUInt64(nameof(this.EndBlock));
        private set => PersistentState.SetUInt64(nameof(this.EndBlock), value);
    }

    private ulong Index
    {
        get => this.PersistentState.GetUInt64("Index");
        set => PersistentState.SetUInt64("Index", value);
    }

    public Status GetAccountStatus(Address address)
    {
        return PersistentState.GetStruct<Status>($"Status:{address}");
    }

    private void SetAccountStatus(Address address, Status status)
    {
        PersistentState.SetStruct($"Status:{address}", status);
    }

    public bool SignUp()
    {
        if (this.Block.Number > this.EndBlock) return false;

        if (GetAccountStatus(Message.Sender) != Status.UNAPPROVED) return false;

        this.Index++;

        SetAccountStatus(Message.Sender, Status.APPROVED);

        Log(new StatusLog { Owner = Message.Sender, OldStatus = Status.UNAPPROVED, Status = Status.APPROVED });

        return true;
    }

    public bool Withdraw()
    {
        if (this.Block.Number < this.EndBlock) return false;

        if (GetAccountStatus(Message.Sender) != Status.APPROVED) return false;

        ulong amount = (ulong)Math.Floor((decimal)this.TotalSupply / this.Index);

        var result = Call(this.TokenContractAddress, 0, "TransferTo", new object[] {
            new { address = Message.Sender},
            new { amount }
        });

        if (!result.Success) return false;

        SetAccountStatus(Message.Sender, Status.FUNDED);

        Log(new StatusLog { Owner = Message.Sender, OldStatus = Status.APPROVED, Status = Status.FUNDED });

        return true;
    }

    public struct StatusLog
    {
        [Index]
        public Address Owner;

        public Status OldStatus;

        public Status Status;
    }

    public enum Status
    {
        UNAPPROVED = 0,
        APPROVED = 1,
        FUNDED = 2
    }
}