using Moq;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR;
using Xunit;

namespace IdentityContracts.Tests
{
    public class IdentityProviderTests
    {
        private const uint Topic = 1;
        private static readonly byte[] ClaimData = new byte[] { 0, 1, 3, 4 };
        private readonly Mock<ISmartContractState> mockContractState;
        private readonly Mock<IPersistentState> mockPersistentState;
        private readonly Mock<IContractLogger> mockContractLogger;
        private readonly Mock<IMessage> mockMessage;
        private readonly Mock<IInternalTransactionExecutor> mockInternalExecutor;
        private readonly Address owner;
        private readonly Address claimReceiver;
        private readonly Address attacker;
        private readonly Address owner2;

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

            Assert.Throws<SmartContractAssertException>(() => contract.AddClaim(this.claimReceiver, Topic, ClaimData));
            Assert.Throws<SmartContractAssertException>(() => contract.RemoveClaim(this.claimReceiver, Topic));
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
            this.mockPersistentState.Setup(x => x.GetBytes(It.IsAny<string>())).Returns((byte[])null);
            this.mockMessage.Setup(x => x.Sender).Returns(this.owner);

            contract.AddClaim(this.claimReceiver, Topic, ClaimData);

            this.mockPersistentState.Verify(x => x.SetBytes($"Claim[{this.claimReceiver}][{Topic}]", ClaimData));
            this.mockContractLogger.Verify(x => x.Log(It.IsAny<ISmartContractState>(), It.Is<IdentityProvider.ClaimChanged>(y =>
                  y.Topic == Topic
                  && y.Data == ClaimData
                  && y.IssuedTo == this.claimReceiver)));
        }

    }
}
