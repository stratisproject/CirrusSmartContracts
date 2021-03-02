using Stratis.SmartContracts;

public struct Proposal
{
    public ulong RequestedAmount;

    public Address Recipient;
    
    public string Description;
    
    public bool VotingOpen;

    public bool VotingSucceed;
    
    public Address Creator;
}
