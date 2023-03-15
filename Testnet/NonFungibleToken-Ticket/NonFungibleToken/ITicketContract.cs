using Stratis.SmartContracts;

namespace TicketContract
{
    public interface ITicketContract
    {
        void MarkAsUsed(UInt256 tokenId);
        bool IsUsed(UInt256 tokenId);
    }
}
