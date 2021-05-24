using Stratis.SmartContracts;
using Stratis.SmartContracts.Standards;

[Deploy]
public class NFTExchange : SmartContract
{
    public NFTExchange(ISmartContractState state)
        : base(state)
    {
    }
}