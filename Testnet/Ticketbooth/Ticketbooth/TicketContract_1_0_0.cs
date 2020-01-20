using Stratis.SmartContracts;

public class TicketContract_1_0_0 : SmartContract
{
    /// <summary>
    /// Creates a new ticketing contract
    /// </summary>
    /// <param name="smartContractState"></param>
    /// <param name="seatsBytes">The serialized array of seats</param>
    /// <param name="venueName">The venue that hosts the contract</param>
    public TicketContract_1_0_0(ISmartContractState smartContractState, byte[] seatsBytes, string venueName)
        : base(smartContractState)
    {
        var seats = Serializer.ToArray<Seat>(seatsBytes);
        var tickets = new Ticket[seats.Length];

        for (int i = 0; i < seats.Length; i++)
        {
            tickets[i] = new Ticket { Seat = seats[i] };
        }

        Log(new Venue { Name = venueName });
        Owner = Message.Sender;
        Tickets = tickets;
    }

    /// <summary>
    /// Stores ticket data for the contract
    /// </summary>
    public Ticket[] Tickets
    {
        get
        {
            return PersistentState.GetArray<Ticket>(nameof(Tickets));
        }
        private set
        {
            PersistentState.SetArray(nameof(Tickets), value);
        }
    }

    private ulong EndOfSale
    {
        get
        {
            return PersistentState.GetUInt64(nameof(EndOfSale));
        }
        set
        {
            PersistentState.SetUInt64(nameof(EndOfSale), value);
        }
    }

    private ulong ReleaseFee
    {
        get
        {
            return PersistentState.GetUInt64(nameof(ReleaseFee));
        }
        set
        {
            PersistentState.SetUInt64(nameof(ReleaseFee), value);
            Log(new TicketReleaseFee { Amount = value });
        }
    }

    private ulong NoRefundBlockCount
    {
        get
        {
            return PersistentState.GetUInt64(nameof(NoRefundBlockCount));
        }
        set
        {
            PersistentState.SetUInt64(nameof(NoRefundBlockCount), value);
            Log(new NoRefundBlocks { Count = value });
        }
    }

    private bool RequireIdentityVerification
    {
        get
        {
            return PersistentState.GetBool(nameof(RequireIdentityVerification));
        }
        set
        {
            PersistentState.SetBool(nameof(RequireIdentityVerification), value);
            Log(new IdentityVerificationPolicy { RequireIdentityVerification = value });
        }
    }

    private Address Owner
    {
        get
        {
            return PersistentState.GetAddress(nameof(Owner));
        }
        set
        {
            PersistentState.SetAddress(nameof(Owner), value);
        }
    }

    private bool SaleOpen
    {
        get
        {
            var endOfSale = EndOfSale;
            return endOfSale != 0 && Block.Number < endOfSale;
        }
    }

    /// <summary>
    /// Starts a ticket sale, when no sale is running
    /// </summary>
    /// <param name="ticketsBytes">The serialized array of tickets</param>
    /// <param name="showName">Name of the event or performance</param>
    /// <param name="organiser">The organiser or artist</param>
    /// <param name="time">Unix time for the event</param>
    /// <param name="endOfSale">The block at which the sale ends</param>
    public void BeginSale(byte[] ticketsBytes, string showName, string organiser, ulong time, ulong endOfSale)
    {
        Assert(Message.Sender == Owner, "Only contract owner can begin a sale");
        Assert(EndOfSale == default(ulong), "Sale currently in progress");
        Assert(Block.Number < endOfSale, "Sale must finish in the future");

        var tickets = Serializer.ToArray<Ticket>(ticketsBytes);
        var copyOfTickets = Tickets;

        Assert(copyOfTickets.Length == tickets.Length, "Seat elements must be equal");
        for (int i = 0; i < copyOfTickets.Length; i++)
        {
            Ticket ticket = default(Ticket);
            for (int y = 0; y < tickets.Length; y++)
            {
                if (SeatsAreEqual(copyOfTickets[i].Seat, tickets[y].Seat))
                {
                    ticket = tickets[y];
                    break;
                }
            }
            Assert(!IsDefaultSeat(ticket.Seat), "Invalid seat provided");
            copyOfTickets[i].Price = tickets[i].Price;
        }

        Tickets = copyOfTickets;
        EndOfSale = endOfSale;

        var show = new Show
        {
            Name = showName,
            Organiser = organiser,
            Time = time,
            EndOfSale = endOfSale
        };
        Log(show);
    }

    /// <summary>
    /// Called after the ending of a ticket sale to clear the contract ticket data
    /// </summary>
    public void EndSale()
    {
        Assert(Message.Sender == Owner, "Only contract owner can end sale");
        Assert(EndOfSale != default(ulong), "Sale not currently in progress");
        Assert(Block.Number >= EndOfSale, "Sale contract not fulfilled");

        Tickets = ResetTickets(Tickets);
        EndOfSale = default(ulong);
    }

    /// <summary>
    /// Checks the availability of a seat
    /// </summary>
    /// <param name="seatIdentifierBytes">The serialized seat identifier</param>
    /// <returns>Whether the seat is available</returns>
    public bool CheckAvailability(byte[] seatIdentifierBytes)
    {
        Assert(SaleOpen, "Sale not open");

        var ticket = SelectTicket(seatIdentifierBytes);

        Assert(!IsDefaultSeat(ticket.Seat), "Seat not found");

        return IsAvailable(ticket);
    }

    /// <summary>
    /// Reserves a ticket for the callers address
    /// </summary>
    /// <param name="seatIdentifierBytes">The serialized seat identifier</param>
    /// <param name="secret">The encrypted secret holding ticket ownership</param>
    /// <returns>Whether the seat was successfully reserved</returns>
    public void Reserve(byte[] seatIdentifierBytes, byte[] secret)
    {
        Reserve(seatIdentifierBytes, secret, null);
    }

    /// <summary>
    /// Reserves a ticket for the callers address and with an identifier for the customer
    /// </summary>
    /// <param name="seatIdentifierBytes">The serialized seat identifier</param>
    /// <param name="secret">The encrypted secret holding ticket ownership</param>
    /// <param name="customerIdentifier">An encrypted verifiable identifier for the customer</param>
    /// <returns>Whether the seat was successfully reserved</returns>
    public void Reserve(byte[] seatIdentifierBytes, byte[] secret, byte[] customerIdentifier)
    {
        Assert(secret != null, "Invalid secret");
        Assert(!RequireIdentityVerification || customerIdentifier != null, "Invalid customer identifier");
        Assert(SaleOpen, "Sale not open");

        var seat = Serializer.ToStruct<Seat>(seatIdentifierBytes);
        var copyOfTickets = Tickets;
        Ticket ticket = default(Ticket);
        int ticketIndex = 0;
        for (var i = 0; i < copyOfTickets.Length; i++)
        {
            if (SeatsAreEqual(copyOfTickets[i].Seat, seat))
            {
                ticketIndex = i;
                ticket = copyOfTickets[i];
                break;
            }
        }

        Assert(!IsDefaultSeat(ticket.Seat), "Seat not found");
        Assert(IsAvailable(ticket), "Ticket not available");
        Assert(Message.Value >= ticket.Price, "Not enough funds");

        if (Message.Value > ticket.Price)
        {
            Transfer(Message.Sender, Message.Value - ticket.Price);
        }

        copyOfTickets[ticketIndex].Address = Message.Sender;
        copyOfTickets[ticketIndex].Secret = secret;
        copyOfTickets[ticketIndex].CustomerIdentifier = customerIdentifier;
        Tickets = copyOfTickets;

        Log(copyOfTickets[ticketIndex]);
    }

    /// <summary>
    /// Sets the fee to refund a ticket to the contract
    /// </summary>
    /// <param name="releaseFee">The refund fee</param>
    public void SetTicketReleaseFee(ulong releaseFee)
    {
        Assert(Message.Sender == Owner, "Only contract owner can set release fee");
        Assert(!SaleOpen, "Sale is open");
        ReleaseFee = releaseFee;
    }

    /// <summary>
    /// Sets the block limit for issuing refunds on purchased tickets
    /// </summary>
    /// <param name="noReleaseBlocks">The number of blocks before the end of the contract to disallow refunds</param>
    public void SetNoReleaseBlocks(ulong noReleaseBlocks)
    {
        Assert(Message.Sender == Owner, "Only contract owner can set no release blocks limit");
        Assert(!SaleOpen, "Sale is open");
        NoRefundBlockCount = noReleaseBlocks;
    }

    /// <summary>
    /// Sets the identity verification policy of the venue
    /// </summary>
    /// <param name="requireIdentityVerification">Whether the venue requires identity verification</param>
    public void SetIdentityVerificationPolicy(bool requireIdentityVerification)
    {
        Assert(Message.Sender == Owner, "Only contract owner can set identity verification policy");
        Assert(!SaleOpen, "Sale is open");
        RequireIdentityVerification = requireIdentityVerification;
    }

    /// <summary>
    /// Requests a refund for a ticket, which will be issued if the no refund block limit is not yet reached
    /// </summary>
    /// <param name="seatIdentifierBytes">The serialized seat identifier</param>
    public void ReleaseTicket(byte[] seatIdentifierBytes)
    {
        Assert(SaleOpen, "Sale not open");
        Assert(Block.Number + NoRefundBlockCount < EndOfSale, "Surpassed no refund block limit");

        var seat = Serializer.ToStruct<Seat>(seatIdentifierBytes);
        var copyOfTickets = Tickets;
        Ticket ticket = default(Ticket);
        int ticketIndex = 0;
        for (var i = 0; i < copyOfTickets.Length; i++)
        {
            if (SeatsAreEqual(copyOfTickets[i].Seat, seat))
            {
                ticketIndex = i;
                ticket = copyOfTickets[i];
                break;
            }
        }

        Assert(!IsDefaultSeat(ticket.Seat), "Seat not found");
        Assert(Message.Sender == ticket.Address, "You do not own this ticket");

        if (ticket.Price > ReleaseFee)
        {
            Transfer(Message.Sender, ticket.Price - ReleaseFee);
        }

        copyOfTickets[ticketIndex].Address = Address.Zero;
        copyOfTickets[ticketIndex].Secret = null;
        copyOfTickets[ticketIndex].CustomerIdentifier = null;
        Tickets = copyOfTickets;

        Log(copyOfTickets[ticketIndex]);
    }

    private Ticket SelectTicket(byte[] seatIdentifierBytes)
    {
        var seat = Serializer.ToStruct<Seat>(seatIdentifierBytes);
        foreach (var ticket in Tickets)
        {
            if (SeatsAreEqual(ticket.Seat, seat))
            {
                return ticket;
            }
        }

        return default(Ticket);
    }

    private bool IsAvailable(Ticket ticket)
    {
        return ticket.Address == Address.Zero;
    }

    private bool IsDefaultSeat(Seat seat)
    {
        return seat.Number == default(int) || seat.Letter == default(char);
    }

    private bool SeatsAreEqual(Seat seat1, Seat seat2)
    {
        return seat1.Number == seat2.Number && seat1.Letter == seat2.Letter;
    }

    private Ticket[] ResetTickets(Ticket[] tickets)
    {
        for (int i = 0; i < tickets.Length; i++)
        {
            tickets[i].Price = 0;
            tickets[i].Address = Address.Zero;
            tickets[i].Secret = null;
            tickets[i].CustomerIdentifier = null;
        }

        return tickets;
    }

    /// <summary>
    /// Identifies a specific seat by number and/or letter
    /// </summary>
    public struct Seat
    {
        /// <summary>
        /// A number identifying the seat
        /// </summary>
        public int Number;

        /// <summary>
        /// A letter identifying the seat
        /// </summary>
        public char Letter;
    }

    /// <summary>
    /// Represents a ticket for a specific seat
    /// </summary>
    public struct Ticket
    {
        /// <summary>
        /// The seat the ticket is for
        /// </summary>
        public Seat Seat;

        /// <summary>
        /// Price of the ticket in CRS sats
        /// </summary>
        public ulong Price;

        /// <summary>
        /// The ticket owner
        /// </summary>
        public Address Address;

        /// <summary>
        /// The encrypted ticket secret
        /// </summary>
        public byte[] Secret;

        /// <summary>
        /// Encrypted identifier used by the venue to check identity
        /// </summary>
        public byte[] CustomerIdentifier;
    }

    /// <summary>
    /// Represents the venue or the event organiser
    /// </summary>
    public struct Venue
    {
        /// <summary>
        /// Name of the venue
        /// </summary>
        public string Name;
    }

    /// <summary>
    /// Stores metadata relating to a specific ticket sale
    /// </summary>
    public struct Show
    {
        /// <summary>
        /// Name of the show
        /// </summary>
        public string Name;

        /// <summary>
        /// Organiser of the show
        /// </summary>
        public string Organiser;

        /// <summary>
        /// Unix time of the show
        /// </summary>
        public ulong Time;

        /// <summary>
        /// Block at which the sale ends
        /// </summary>
        public ulong EndOfSale;
    }

    /// <summary>
    /// Represents the fee that is charged if a ticket is released from an address
    /// </summary>
    public struct TicketReleaseFee
    {
        /// <summary>
        /// The release fee, in sats
        /// </summary>
        public ulong Amount;
    }

    /// <summary>
    /// Represents the number of blocks before the end of the contract, where refunds are not allowed
    /// </summary>
    public struct NoRefundBlocks
    {
        /// <summary>
        /// The number of no refund blocks
        /// </summary>
        public ulong Count;
    }

    /// <summary>
    /// Represents the identity verification policy of the venue
    /// </summary>
    public struct IdentityVerificationPolicy
    {
        /// <summary>
        /// Whether the venue requires identity verification
        /// </summary>
        public bool RequireIdentityVerification;
    }
}