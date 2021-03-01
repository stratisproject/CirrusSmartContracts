using Stratis.SmartContracts;

public struct Proposal
{
    /// <summary>
    /// The address where the `amount` will go to if the proposal is accepted
    /// </summary>
    public Address Recipient;

    /// <summary>
    /// Requested fund amount. The amount to transfer to `recipient` if the proposal is accepted.
    /// </summary>
    public ulong Amount;
    public string Description;
    public ulong VotingDeadline;
    // True if the proposal's votes have yet to be counted, otherwise False
    public bool Open;
    // True if quorum has been reached, the votes have been counted, and
    // the majority said yes
    public bool ProposalPassed;
    public Address Creator;
}
