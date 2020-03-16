using Moq;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR;
using Xunit;

namespace IdentityContracts.Tests
{
    public class IdentityProviderTests
    {
        private readonly Mock<ISmartContractState> mockContractState;
        private readonly Mock<IPersistentState> mockPersistentState;
        private readonly Mock<IContractLogger> mockContractLogger;
        private readonly Mock<IMessage> mockMessage;
        private readonly Mock<IInternalTransactionExecutor> mockInternalExecutor;
        private readonly Address owner;
        private readonly Address claimReceiver;
        private readonly Address attacker;
        private readonly Address owner2;
        private readonly Address contractAddress;
        private const uint Topic1 = 1;
        private static readonly byte[] ClaimData = new byte[]{ 0, 1, 3, 4 };

        public IdentityProviderTests()
        {
            this.mockContractLogger = new Mock<IContractLogger>();
            this.mockPersistentState = new Mock<IPersistentState>();
            this.mockContractState = new Mock<ISmartContractState>();
            this.mockMessage = new Mock<IMessage>();
            this.mockInternalExecutor = new Mock<IInternalTransactionExecutor>();
            this.mockContractState.Setup(x => x.PersistentState).Returns(this.mockPersistentState.Object);
            this.mockContractState.Setup(x => x.ContractLogger).Returns(this.mockContractLogger.Object);
            this.mockContractState.Setup(x => x.Message).Returns(this.mockMessage.Object);
            this.mockContractState.Setup(x => x.InternalTransactionExecutor).Returns(this.mockInternalExecutor.Object);
            this.owner = "0x0000000000000000000000000000000000000001".HexToAddress();
            this.claimReceiver = "0x0000000000000000000000000000000000000002".HexToAddress();
            this.attacker = "0x0000000000000000000000000000000000000003".HexToAddress();
            this.owner2 = "0x0000000000000000000000000000000000000004".HexToAddress();
            this.contractAddress = "0x0000000000000000000000000000000000000005".HexToAddress();
        }

        [Fact]
        public void OwnerGetsSet()
        {
            this.mockMessage.Setup(x => x.Sender).Returns(this.owner);

            var contract = new IdentityProvider(this.mockContractState.Object);

            this.mockPersistentState.Verify(x => x.SetAddress("Owner", this.owner));
        }

        [Fact]
        public void NonOwnerCantCallStateUpdateMethods()
        {
            var contract = new IdentityProvider(this.mockContractState.Object);
            this.mockPersistentState.Setup(x => x.GetAddress("Owner")).Returns(this.owner);

            this.mockMessage.Setup(x => x.Sender).Returns(this.attacker);

            Assert.Throws<SmartContractAssertException>(() => contract.AddClaim(this.claimReceiver, Topic1, ClaimData));
            Assert.Throws<SmartContractAssertException>(() => contract.RemoveClaim(this.claimReceiver, Topic1));
            Assert.Throws<SmartContractAssertException>(() => contract.ChangeOwner(this.attacker));
        }

        [Fact]
        public void ChangeOwnerSucceeds()
        {
            var contract = new IdentityProvider(this.mockContractState.Object);
            this.mockPersistentState.Setup(x => x.GetAddress("Owner")).Returns(this.owner);
            this.mockMessage.Setup(x => x.Sender).Returns(this.owner);

            contract.ChangeOwner(this.owner2);

            this.mockPersistentState.Verify(x => x.SetAddress("Owner", this.owner2));
        }

        [Fact]
        public void SetClaimSucceeds()
        {
            var contract = new IdentityProvider(this.mockContractState.Object);
            this.mockPersistentState.Setup(x => x.GetAddress("Owner")).Returns(this.owner);
            this.mockPersistentState.Setup(x => x.GetBytes(It.IsAny<string>())).Returns((byte[]) null);
            this.mockMessage.Setup(x => x.Sender).Returns(this.owner);

            contract.AddClaim(this.claimReceiver, Topic1, ClaimData);

            this.mockPersistentState.Verify(x=>x.SetBytes($"Claim[{this.claimReceiver}][{Topic1}]", ClaimData));
            this.mockContractLogger.Verify(x=>x.Log(It.IsAny<ISmartContractState>(), It.Is<IdentityProvider.ClaimAdded>(y =>
                y.Topic == Topic1 
                && y.Data == ClaimData
                && y.IssuedTo == this.claimReceiver)));
        }

    }
}
