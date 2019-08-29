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

    /// <summary>The contract address of the token that will be distributed. This smart contracts
    /// address must be approved to send at least the TotalSupply at this address.</summary>
    public Address TokenContractAddress
    {
        get => PersistentState.GetAddress(nameof(TokenContractAddress));
        private set => PersistentState.SetAddress(nameof(TokenContractAddress), value);
    }

    /// <summary>The total supply of the token that will be distributed during this airdrop.</summary>
    public ulong TotalSupply
    {
        get => PersistentState.GetUInt64(nameof(TotalSupply));
        private set => PersistentState.SetUInt64(nameof(TotalSupply), value);
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

    /// <summary>Address of the owner of this contract, used to authenticate some methods.</summary>
    public Address Owner
    {
        get => PersistentState.GetAddress(nameof(Owner));
        private set => PersistentState.SetAddress(nameof(Owner), value);
    }

    /// <summary>The total number of registrants for this airdrop.</summary>
    public ulong NumberOfRegistrants
    {
        get => PersistentState.GetUInt64(nameof(NumberOfRegistrants));
        private set => PersistentState.SetUInt64(nameof(NumberOfRegistrants), value);
    }

    /// <summary>Gets the AmountToDistribute for each registrant if it has been previously calculated.</summary>
    private ulong AmountToDistribute
    {
        get => PersistentState.GetUInt64(nameof(AmountToDistribute));
        set => PersistentState.SetUInt64(nameof(AmountToDistribute), value);
    }

    /// <summary>Returns whether or not the registration period as been calculated and set to closed.</summary>
    private bool RegistrationIsClosed
    {
        get => PersistentState.GetBool(nameof(RegistrationIsClosed));
        set => PersistentState.SetBool(nameof(RegistrationIsClosed), value);
    }

    /// <summary>Returns the status of any given address.</summary>
    public uint GetAccountStatus(Address address)
    {
        return PersistentState.GetUInt32($"Status:{address}");
    }

    /// <summary>Sets the status for a given address.</summary>
    private void SetAccountStatus(Address address, uint status)
    {
        PersistentState.SetUInt32($"Status:{address}", status);
    }

    /// <summary>Validates and registers accounts. See <see cref="AddRegistrantExecute(Address)"/></summary>
    public bool Register()
    {
        return AddRegistrantExecute(Message.Sender);
    }

    /// <summary>Allows owner of the contract to manually add a new registrant. See <see cref="AddRegistrantExecute(Address)"/></summary>
    public bool AddRegistrant(Address registrant)
    {
        return Message.Sender == Owner && AddRegistrantExecute(registrant);
    }

    /// <summary>Calculate and set the amount to distribute.</summary>
    public ulong GetAmountToDistribute()
    {
        ulong amount = PersistentState.GetUInt64(nameof(AmountToDistribute));
        if (amount == 0 && IsRegistrationClosed())
        {
            amount = TotalSupply / NumberOfRegistrants;
            AmountToDistribute = amount;
        }

        return amount;
    }

    /// <summary>Calculate if the registration period is closed or not.</summary>
    public bool IsRegistrationClosed()
    {
        bool isClosed = PersistentState.GetBool(nameof(RegistrationIsClosed));
        if (!isClosed && EndBlock > 0)
        {
            isClosed = Block.Number > EndBlock;
            RegistrationIsClosed = isClosed;
        }

        return isClosed;
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
    /// The contract address must be approved at the TokenContractAddress to send at least the TotalSupply from the Owners wallet.
    /// </summary>
    public bool Withdraw()
    {
        bool invalidAccountStatus = GetAccountStatus(Message.Sender) != (uint)Status.ENROLLED;
        bool registrationIsClosed = IsRegistrationClosed();
        ulong amountToDistribute = GetAmountToDistribute();

        if (invalidAccountStatus || !registrationIsClosed || amountToDistribute == 0)
        {
            return false;
        }

        var transferParams = new object[] { Message.Sender, amountToDistribute };

        ITransferResult result = Call(TokenContractAddress, amountToDistribute, "TransferTo", transferParams);

        if (result == null || !result.Success)
        {
            return false;
        }

        SetAccountStatus(Message.Sender, (uint)Status.FUNDED);

        Log(new StatusLog { Registrant = Message.Sender, Status = (uint)Status.FUNDED });

        return true;
    }

    /// <summary>Validates and adds a new registrant. Updates the NumberOfRegistrants, account status, and logs result.</summary>
    private bool AddRegistrantExecute(Address registrant)
    {
        if (registrant == Owner)
        {
            return false;
        }

        bool invalidAddressStatus = GetAccountStatus(registrant) != (uint)Status.NOT_ENROLLED;
        bool registrationIsClosed = IsRegistrationClosed();

        if (invalidAddressStatus || registrationIsClosed || NumberOfRegistrants >= this.TotalSupply)
        {
            return false;
        }

        NumberOfRegistrants += 1;

        SetAccountStatus(registrant, (uint)Status.ENROLLED);

        Log(new StatusLog { Registrant = registrant, Status = (uint)Status.ENROLLED });

        return true;
    }

    public struct StatusLog
    {
        [Index]
        public Address Registrant;

        public uint Status;
    }

    public enum Status : uint
    {
        NOT_ENROLLED = 0,
        ENROLLED = 1,
        FUNDED = 2
    }
}