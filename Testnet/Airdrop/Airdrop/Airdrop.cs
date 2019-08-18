using Stratis.SmartContracts;

public class Airdrop : SmartContract
{
    /// <summary>
    /// Constructor used to create a new Airdrop contract. Assigns total supply of airdrop, the
    /// contract address for the token being airdropped and the endblock that the airdrop closes on.
    /// </summary>
    /// <param name="smartContractState">The execution state for the contract.</param>
    /// <param name="tokenContractAddress">The smart contract address of the token being airdropped.</param>
    /// <param name="totalSupply">The total amount that will be airdropped, amount will be divided amongst registrants.</param>
    /// <param name="endBlock">The block that ends the sign up period and allows withdrawing, can use 0 to manually end airdrop using <see cref="CloseRegistration"/>.</param>
    public Airdrop(
        ISmartContractState smartContractState,
        Address tokenContractAddress,
        ulong totalSupply,
        ulong endBlock
    ) : base(smartContractState)
    {
        TotalSupply = totalSupply;
        TokenContractAddress = tokenContractAddress;
        EndBlock = endBlock;
        Owner = Message.Sender;
    }

    /// <summary>The total supply of the token that will be distributed during this airdrop.</summary>
    public ulong TotalSupply
    {
        get => PersistentState.GetUInt64(nameof(TotalSupply));
        private set => PersistentState.SetUInt64(nameof(TotalSupply), value);
    }

    /// <summary>The contract address of the token that will be distributed. This smart contracts address must hold the TotalSupply at this address.</summary>
    public Address TokenContractAddress
    {
        get => PersistentState.GetAddress(nameof(TokenContractAddress));
        private set => PersistentState.SetAddress(nameof(TokenContractAddress), value);
    }

    /// <summary>
    /// Used to automatically close registration based on a specified block number. If the EndBlock is 0, the
    /// registration period for the airdrop must be manually ended by the owner using <see cref="CloseRegistration"/>
    /// </summary>
    public ulong EndBlock
    {
        get => PersistentState.GetUInt64(nameof(EndBlock));
        private set => PersistentState.SetUInt64(nameof(EndBlock), value);
    }

    /// <summary>The total number of registrants for this airdrop.</summary>
    public ulong NumberOfRegistrants
    {
        get => PersistentState.GetUInt64(nameof(NumberOfRegistrants));
        private set => PersistentState.SetUInt64(nameof(NumberOfRegistrants), value);
    }

    /// <summary>Address of the owner of this contract, used to authenticate some methods.</summary>
    public Address Owner
    {
        get => PersistentState.GetAddress(nameof(Owner));
        private set => PersistentState.SetAddress(nameof(Owner), value);
    }

    /// <summary>Calculates and sets the amount to distribute to each registrant.</summary>
    public ulong AmountToDistribute
    {
        get
        {
            ulong amount = PersistentState.GetUInt64(nameof(AmountToDistribute));
            if (amount == 0 && RegistrationIsClosed)
            {
                amount = TotalSupply / NumberOfRegistrants;
                AmountToDistribute = amount;
            }

            return amount;
        }

        private set => PersistentState.SetUInt64(nameof(AmountToDistribute), value);
    }

    /// <summary>Returns whether or not the registration period is closed.</summary>
    public bool RegistrationIsClosed
    {
        get
        {
            bool isClosed = PersistentState.GetBool(nameof(RegistrationIsClosed));
            if (!isClosed && EndBlock > 0)
            {
                isClosed = Block.Number > EndBlock;
                RegistrationIsClosed = isClosed;
            }

            return isClosed;
        }

        private set => PersistentState.SetBool(nameof(RegistrationIsClosed), value);
    }

    /// <summary>Returns the status of any given address.</summary>
    public Status GetAccountStatus(Address address)
    {
        return PersistentState.GetStruct<Status>($"Status:{address}");
    }

    /// <summary>Sets the status for a given address.</summary>
    private void SetAccountStatus(Address address, Status status)
    {
        PersistentState.SetStruct($"Status:{address}", status);
    }

    /// <summary>Validates and registers accounts. See <see cref="AddRegistrantExecute(Address)"/></summary>
    public bool Register()
    {
        return AddRegistrantExecute(Message.Sender);
    }

    /// <summary>Allows owner of the contract to manually add a new registrant. See <see cref="AddRegistrantExecute(Address)"</summary>
    public bool AddRegistrant(Address registrant)
    {
        return Message.Sender == Owner && AddRegistrantExecute(registrant);
    }

    /// <summary>Allows owner to close registration for the airdrop at any time.</summary>
    public bool CloseRegistration()
    {
        if (Message.Sender != Owner)
        {
            return false;
        }

        RegistrationIsClosed = true;

        return true;
    }

    /// <summary>
    /// Withdraw funds after registration period has closed. Validates account status and calls the tokens contract
    /// address that is being airdropped to transfer amount to sender. On success, set senders new status and log it.
    /// </summary>
    public bool Withdraw()
    {
        bool invalidAccountStatus = GetAccountStatus(Message.Sender) != Status.ENROLLED;
        if (invalidAccountStatus || !RegistrationIsClosed)
        {
            return false;
        }

        var transferParams = new object[] { Address, AmountToDistribute };

        ITransferResult result = Call(TokenContractAddress, AmountToDistribute, "TransferTo", transferParams, 10_000);

        if (result == null || !result.Success)
        {
            return false;
        }

        SetAccountStatus(Message.Sender, Status.FUNDED);

        Log(new StatusLog { Registrant = Message.Sender, Status = Status.FUNDED });

        return true;
    }

    /// <summary>Validates and adds a new registrant. Updates the NumberOfRegistrants, account status, and logs result.</summary>
    private bool AddRegistrantExecute(Address registrant)
    {
        bool invalidAddressStatus = GetAccountStatus(registrant) != Status.NOT_ENROLLED;
        if (invalidAddressStatus || RegistrationIsClosed || NumberOfRegistrants >= this.TotalSupply)
        {
            return false;
        }

        NumberOfRegistrants++;

        SetAccountStatus(registrant, Status.ENROLLED);

        Log(new StatusLog { Registrant = Message.Sender, Status = Status.ENROLLED });

        return true;
    }

    public struct StatusLog
    {
        [Index]
        public Address Registrant;

        public Status Status;
    }

    public enum Status
    {
        NOT_ENROLLED = 0,
        ENROLLED = 1,
        FUNDED = 2
    }
}
