using Stratis.SmartContracts;
using Stratis.SmartContracts.Standards;
using System.Linq;
using System;

public class Airdrop : SmartContract 
{
    /// <summary>
    /// Constructor used to create a new Airdrop. Assigns total supply of airdrop, the
    /// contract address for the token being airdropped and the endblock that the
    /// airdrop closes on.
    /// </summary>
    /// <param name="smartContractState">The execution state for the contract.</param>
    /// <param name="totalSupply">The total amount that will be airdropped.</param>
    /// <param name="tokenContractAddress">The smart contract address of the token being airdropped.</param>
    /// <param name="endBlock">The block that ends the sign up period and allows withdrawing.</param>
    public Airdrop(
        ISmartContractState smartContractState,
        ulong totalSupply,
        Address tokenContractAddress,
        ulong endBlock
    ) : base(smartContractState)
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

    public ulong Index
    {
        get => PersistentState.GetUInt64("Index");
        private set => PersistentState.SetUInt64("Index", value);
    }

    public Status GetAccountStatus(Address address)
    {
        return PersistentState.GetStruct<Status>($"Status:{address}");
    }

    private void SetAccountStatus(Address address, Status status)
    {
        PersistentState.SetStruct($"Status:{address}", status);
    }

    /// <summary>
    /// Airdrop signup, validates the account signing up, increments the index
    /// by 1, updates and logs senders new status. 
    /// </summary>
    /// <returns>Boolean Success</returns>
    public bool SignUp()
    {
        if (Block.Number > this.EndBlock) return false;

        if (GetAccountStatus(Message.Sender) != Status.UNAPPROVED) return false;

        this.Index++;

        SetAccountStatus(Message.Sender, Status.APPROVED);

        Log(new StatusLog { Owner = Message.Sender, OldStatus = Status.UNAPPROVED, Status = Status.APPROVED });

        return true;
    }

    /// <summary>
    /// Withdraw funds after sign up period has closed. Validates account status, calculates amount to airdrop,
    /// calls the airdropped tokens contract address to transfer amount to sender. If success, updates and
    /// logs sender status.
    /// </summary>
    /// <returns>Boolean Success</returns>
    public bool Withdraw()
    {
        if (Block.Number < this.EndBlock) return false;

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