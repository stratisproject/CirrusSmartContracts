using Stratis.SmartContracts;

/// <summary>
/// A Stratis smart contract for running ticket sales (Ticketbooth v1.0.0)
/// </summary>
public class TicketContract_1_0_0 : SmartContract
{
    /// <summary>
    /// Maximum number of seats, necessary while construction cost is lower than cost to begin a sale
    /// </summary>
    public const int MAX_SEATS = 65;

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
        Assert(seats.Length <= MAX_SEATS, $"Cannot handle more than {MAX_SEATS} seats");

        // create tickets
        var tickets = new Ticket[seats.Length];
        for (var i = 0; i < seats.Length; i++)
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
        get => PersistentState.GetArray<Ticket>(nameof(Tickets));
        private set => PersistentState.SetArray(nameof(Tickets), value);
    }

    private ulong EndOfSale
    {
        get => PersistentState.GetUInt64(nameof(EndOfSale));
        set => PersistentState.SetUInt64(nameof(EndOfSale), value);
    }

    private ulong ReleaseFee
    {
        get => PersistentState.GetUInt64(nameof(ReleaseFee));
        set
        {
            PersistentState.SetUInt64(nameof(ReleaseFee), value);
            Log(new TicketReleaseFee { Amount = value });
        }
    }

    private ulong NoRefundBlockCount
    {
        get => PersistentState.GetUInt64(nameof(NoRefundBlockCount));
        set
        {
            PersistentState.SetUInt64(nameof(NoRefundBlockCount), value);
            Log(new NoRefundBlocks { Count = value });
        }
    }

    private bool RequireIdentityVerification
    {
        get => PersistentState.GetBool(nameof(RequireIdentityVerification));
        set
        {
            PersistentState.SetBool(nameof(RequireIdentityVerification), value);
            Log(new IdentityVerificationPolicy { RequireIdentityVerification = value });
        }
    }

    private Address Owner
    {
        get => PersistentState.GetAddress(nameof(Owner));
        set => PersistentState.SetAddress(nameof(Owner), value);
    }

    private bool SaleOpen
    {
        get
        {
            var endOfSale = EndOfSale;
            return endOfSale != default(ulong) && Block.Number < endOfSale;
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

        var pricedTickets = Serializer.ToArray<Ticket>(ticketsBytes);
        var tickets = Tickets;

        Assert(tickets.Length == pricedTickets.Length, "Seat elements must be equal");

        // set ticket prices
        for (var i = 0; i < tickets.Length; i++)
        {
            Ticket ticket = default(Ticket);

            // find matching ticket
            for (var y = 0; y < pricedTickets.Length; y++)
            {
                if (SeatsAreEqual(tickets[i].Seat, pricedTickets[y].Seat))
                {
                    ticket = pricedTickets[y];
                    break;
                }
            }

            Assert(!IsDefaultSeat(ticket.Seat), "Invalid seat provided");
            tickets[i].Price = pricedTickets[i].Price;
        }

        Tickets = tickets;
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
        var tickets = Tickets;
        var reserveIndex = -1;

        // find index of ticket in array
        for (var i = 0; i < tickets.Length; i++)
        {
            if (SeatsAreEqual(tickets[i].Seat, seat))
            {
                reserveIndex = i;
                break;
            }
        }

        Assert(reserveIndex >= 0, "Seat not found");
        Assert(IsAvailable(tickets[reserveIndex]), "Ticket not available");
        Assert(Message.Value < tickets[reserveIndex].Price, "Not enough funds");

        // refund accidental over-payment
        if (Message.Value > tickets[reserveIndex].Price)
        {
            Transfer(Message.Sender, Message.Value - tickets[reserveIndex].Price);
        }

        tickets[reserveIndex].Address = Message.Sender;
        tickets[reserveIndex].Secret = secret;
        tickets[reserveIndex].CustomerIdentifier = customerIdentifier;
        Tickets = tickets;

        Log(tickets[reserveIndex]);
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
        var tickets = Tickets;
        var releaseIndex = -1;

        // find index of ticket in array
        for (var i = 0; i < tickets.Length; i++)
        {
            if (SeatsAreEqual(tickets[i].Seat, seat))
            {
                releaseIndex = i;
                break;
            }
        }

        Assert(releaseIndex >= 0, "Seat not found");
        Assert(Message.Sender == tickets[releaseIndex].Address, "You do not own this ticket");

        if (tickets[releaseIndex].Price > ReleaseFee)
        {
            Transfer(Message.Sender, tickets[releaseIndex].Price - ReleaseFee);
        }

        tickets[releaseIndex].Address = Address.Zero;
        tickets[releaseIndex].Secret = null;
        tickets[releaseIndex].CustomerIdentifier = null;
        Tickets = tickets;

        Log(tickets[releaseIndex]);
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
        for (var i = 0; i < tickets.Length; i++)
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
        /// Unix time (seconds) of the show
        /// </summary>
        public ulong Time;

        /// <summary>
        /// Block height at which the sale ends
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