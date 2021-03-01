using Stratis.SmartContracts;

namespace DAOContract
{
    [Deploy]
    public class DAOContract : SmartContract
    {
        public ulong Dividends
        {
            get => State.GetUInt64(nameof(Dividends));
            private set => State.SetUInt64(nameof(Dividends), value);
        }

        public DAOContract(ISmartContractState state)
            : base(state)
        {

        }


    }
}