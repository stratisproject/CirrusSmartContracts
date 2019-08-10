using System;
using Moq;
using Stratis.SmartContracts.Networks;
using Stratis.SmartContracts.CLR;
using Xunit;

namespace Stratis.SmartContracts.Samples.Tests
{
    public class AirdropTests
    {
        private readonly Mock<ISmartContractState> mockContractState;
        private readonly Mock<IPersistentState> mockPersistentState;
        private readonly Mock<IContractLogger> mockContractLogger;
        private Address owner;
        private Address sender;
        private Address contract;
        private Address spender;
        private Address destination;
        private ulong totalSupply;
        private ulong endBlock;

        public AirdropTests()
        {
            this.mockContractLogger = new Mock<IContractLogger>();
            this.mockPersistentState = new Mock<IPersistentState>();
            this.mockContractState = new Mock<ISmartContractState>();
            this.mockContractState.Setup(s => s.PersistentState).Returns(this.mockPersistentState.Object);
            this.mockContractState.Setup(s => s.ContractLogger).Returns(this.mockContractLogger.Object);
            this.owner = "0x0000000000000000000000000000000000000001".HexToAddress();
            this.sender = "0x0000000000000000000000000000000000000002".HexToAddress();
            this.contract = "0x0000000000000000000000000000000000000003".HexToAddress();
            this.spender = "0x0000000000000000000000000000000000000004".HexToAddress();
            this.destination = "0x0000000000000000000000000000000000000005".HexToAddress();
            this.totalSupply = 100_000;
            this.endBlock = 1_000_000;
        }

        [Fact]
        public void ConstructorSetsTotalSupplyNameSymbolAndEndBlock()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.totalSupply, this.destination, this.endBlock);

            // Verify that PersistentState was called with the total supply, name, symbol and endblock
            this.mockPersistentState.Verify(s => s.SetUInt64(nameof(Airdrop.TotalSupply), this.totalSupply));
            this.mockPersistentState.Verify(s => s.SetUInt64(nameof(Airdrop.EndBlock), this.endBlock));
            this.mockPersistentState.Verify(s => s.SetAddress(nameof(Airdrop.TokenContractAddress), this.destination));
        }

        [Fact]
        public void SignUp()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));
            this.mockContractState.Setup(b => b.Block.Number).Returns(1);
            this.mockPersistentState.Setup(e => e.GetUInt64("EndBlock")).Returns(this.endBlock);

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.totalSupply, this.destination, this.endBlock);

            airdrop.SignUp();

            this.mockPersistentState.Verify(s => s.SetStruct($"Status:{this.owner}", Airdrop.Status.APPROVED));
        }

        [Fact]
        public void UnsetAccountReturnsNull()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.totalSupply, this.destination, this.endBlock);

            var status = airdrop.GetAccountStatus(this.owner);

            Assert.Equal(Airdrop.Status.UNAPPROVED, status);
        }

        [Fact]
        public void SignUpSetsStatusToAPPROVED()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));
            this.mockContractState.Setup(b => b.Block.Number).Returns(1);
            this.mockPersistentState.Setup(e => e.GetUInt64("EndBlock")).Returns(this.endBlock);

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.totalSupply, this.destination, this.endBlock);

            var status = airdrop.SignUp();

            // Assert status succeeds
            Assert.True(status);

            // Verfies that this was run during SignUp
            this.mockPersistentState.Verify(s => s.SetStruct($"Status:{this.owner}", Airdrop.Status.APPROVED));

            // Set the status manually in persistent state
            this.mockPersistentState.Setup(s => s.GetStruct<Airdrop.Status>($"Status:{this.owner}")).Returns(Airdrop.Status.APPROVED);

            // Getting the status returns APPROVED
            Assert.Equal(Airdrop.Status.APPROVED, airdrop.GetAccountStatus(this.owner));
        }

        [Fact]
        public void DuplicateSignUpFails()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));
            this.mockContractState.Setup(b => b.Block.Number).Returns(1);
            this.mockPersistentState.Setup(e => e.GetUInt64("EndBlock")).Returns(this.endBlock);

            // Initialize the smart contract set constructor props
            var airdrop = new Airdrop(this.mockContractState.Object, this.totalSupply, this.destination, this.endBlock);

            var status = airdrop.SignUp();

            // Assert status succeeds
            Assert.True(status);

            // Set the status manually in persistent state
            this.mockPersistentState.Setup(s => s.GetStruct<Airdrop.Status>($"Status:{this.owner}")).Returns(Airdrop.Status.APPROVED);

            status = airdrop.SignUp();

            Assert.False(status);
        }
    }
}
