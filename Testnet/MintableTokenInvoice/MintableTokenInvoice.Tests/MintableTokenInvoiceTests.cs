using Moq;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR;
using Xunit;

namespace MintableTokenInvoiceTests
{
    /// <summary>
    /// These tests validate the functionality that differs between the original standard token and the extended version.
    /// </summary>
    public class MintableTokenInvoiceTests
    {
        private readonly Mock<ISmartContractState> mockContractState;
        private readonly Mock<IPersistentState> mockPersistentState;
        private readonly Mock<IContractLogger> mockContractLogger;
        private Address owner;
        private Address sender;
        private Address contract;
        private Address destination;

        public MintableTokenInvoiceTests()
        {
            this.mockContractLogger = new Mock<IContractLogger>();
            this.mockPersistentState = new Mock<IPersistentState>();
            this.mockContractState = new Mock<ISmartContractState>();
            this.mockContractState.Setup(s => s.PersistentState).Returns(this.mockPersistentState.Object);
            this.mockContractState.Setup(s => s.ContractLogger).Returns(this.mockContractLogger.Object);
            this.owner = "0x0000000000000000000000000000000000000001".HexToAddress();
            this.sender = "0x0000000000000000000000000000000000000002".HexToAddress();
            this.contract = "0x0000000000000000000000000000000000000003".HexToAddress();
            this.destination = "0x0000000000000000000000000000000000000005".HexToAddress();
        }

        [Fact]
        public void Constructor_Assigns_Owner()
        {
            UInt256 totalSupply = 100_000;
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));

            var standardToken = new MintableTokenInvoice(this.mockContractState.Object);

            // Verify that PersistentState was called with the contract owner
            this.mockPersistentState.Verify(s => s.SetAddress($"Owner", this.owner));
        }

        [Fact]
        public void TransferOwnership_Succeeds_For_Owner()
        {
            UInt256 totalSupply = 100_000;
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));

            var standardToken = new MintableTokenInvoice(this.mockContractState.Object);

            // Setup the owner of the contract
            this.mockPersistentState.Setup(s => s.GetAddress($"Owner")).Returns(this.owner);

            standardToken.SetNewOwner(this.destination);

            this.mockPersistentState.Setup(s => s.GetAddress($"NewOwner")).Returns(this.destination);
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.destination, 0));

            standardToken.ClaimOwnership();

            // Verify that PersistentState was called to update the contract owner
            this.mockPersistentState.Verify(s => s.SetAddress($"Owner", this.destination));

            this.mockContractLogger.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new MintableTokenInvoice.OwnershipTransferred() { PreviousOwner = this.owner, NewOwner = this.destination }));
        }

        [Fact]
        public void TransferOwnership_Fails_For_NonOwner()
        {
            UInt256 totalSupply = 100_000;
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));

            var standardToken = new MintableTokenInvoice(this.mockContractState.Object);

            // Setup the owner of the contract
            this.mockPersistentState.Setup(s => s.GetAddress($"Owner")).Returns(this.owner);

            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.sender, 0));

            Assert.ThrowsAny<SmartContractAssertException>(() => standardToken.SetNewOwner(this.destination));
        }
    }
}
