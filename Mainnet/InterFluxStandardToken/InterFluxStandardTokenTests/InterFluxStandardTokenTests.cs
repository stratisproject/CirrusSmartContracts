using Moq;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR;
using Xunit;

namespace InterFluxStandardTokenTests
{
    /// <summary>
    /// These tests validate the functionality that differs between the original standard token and the extended version.
    /// </summary>
    public class InterFluxStandardTokenTests
    {
        private readonly Mock<ISmartContractState> mockContractState;
        private readonly Mock<IPersistentState> mockPersistentState;
        private readonly Mock<IContractLogger> mockContractLogger;
        private Address owner;
        private Address sender;
        private Address contract;
        private Address spender;
        private Address destination;
        private string name;
        private string symbol;
        private byte decimals;
        private string nativeChain;
        private string nativeAddress;

        public InterFluxStandardTokenTests()
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
            this.name = "Test Token";
            this.symbol = "TST";
            this.decimals = 8;
            this.nativeChain = "Ethereum";
            this.nativeAddress = "0xa3c22370de5f9544f0c4de126b1e46ceadf0a51b";
        }

        [Fact]
        public void Constructor_Assigns_Owner()
        {
            UInt256 totalSupply = 100_000;
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));

            var standardToken = new InterFluxStandardToken(this.mockContractState.Object, totalSupply, this.name, this.symbol, this.decimals, this.nativeChain, this.nativeAddress);

            // Verify that PersistentState was called with the contract owner
            this.mockPersistentState.Verify(s => s.SetAddress($"Owner", this.owner));
        }

        [Theory]
        [InlineData(" ")]
        [InlineData("")]
        [InlineData("Ethereum")]
        public void Constructor_Assigns_NativeChain(string chain)
        {
            UInt256 totalSupply = 100_000;
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));

            var standardToken = new InterFluxStandardToken(this.mockContractState.Object, totalSupply, this.name, this.symbol, this.decimals, chain, this.nativeAddress);

            // Verify that PersistentState was called with the contract owner
            this.mockPersistentState.Verify(s => s.SetString("NativeChain", chain));
        }

        [Theory]
        [InlineData(" ")]
        [InlineData("")]
        [InlineData("0xa3c22370de5f9544f0c4de126b1e46ceadf0a51b")]
        public void Constructor_Assigns_NativeAddress(string nativeAddress)
        {
            UInt256 totalSupply = 100_000;
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));

            var standardToken = new InterFluxStandardToken(this.mockContractState.Object, totalSupply, this.name, this.symbol, this.decimals, this.nativeChain, nativeAddress);

            // Verify that PersistentState was called with the contract owner
            this.mockPersistentState.Verify(s => s.SetString("NativeAddress", nativeAddress));
        }

        [Fact]
        public void RenounceOwnership_Succeeds_For_Owner()
        {
            UInt256 totalSupply = 100_000;
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));

            var standardToken = new InterFluxStandardToken(this.mockContractState.Object, totalSupply, this.name, this.symbol, this.decimals, this.nativeChain, this.nativeAddress);

            // Setup the owner of the contract
            this.mockPersistentState.Setup(s => s.GetAddress($"Owner")).Returns(this.owner);

            standardToken.RenounceOwnership();

            // Verify that PersistentState was called to update the contract owner
            this.mockPersistentState.Verify(s => s.SetAddress($"Owner", Address.Zero));

            this.mockContractLogger.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new InterFluxStandardToken.OwnershipTransferred() { PreviousOwner = this.owner, NewOwner = Address.Zero}));
        }

        [Fact]
        public void RenounceOwnership_Fails_For_NonOwner()
        {
            UInt256 totalSupply = 100_000;
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));

            var standardToken = new InterFluxStandardToken(this.mockContractState.Object, totalSupply, this.name, this.symbol, this.decimals, this.nativeChain, this.nativeAddress);

            // Setup the owner of the contract
            this.mockPersistentState.Setup(s => s.GetAddress($"Owner")).Returns(this.owner);

            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.sender, 0));

            Assert.ThrowsAny<SmartContractAssertException>(() => standardToken.RenounceOwnership());
        }

        [Fact]
        public void TransferOwnership_Succeeds_For_Owner()
        {
            UInt256 totalSupply = 100_000;
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));

            var standardToken = new InterFluxStandardToken(this.mockContractState.Object, totalSupply, this.name, this.symbol, this.decimals, this.nativeChain, this.nativeAddress);

            // Setup the owner of the contract
            this.mockPersistentState.Setup(s => s.GetAddress($"Owner")).Returns(this.owner);

            standardToken.TransferOwnership(this.destination);

            // Verify that PersistentState was called to update the contract owner
            this.mockPersistentState.Verify(s => s.SetAddress($"Owner", this.destination));

            this.mockContractLogger.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new InterFluxStandardToken.OwnershipTransferred() { PreviousOwner = this.owner, NewOwner = this.destination }));
        }

        [Fact]
        public void TransferOwnership_Fails_For_NonOwner()
        {
            UInt256 totalSupply = 100_000;
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));

            var standardToken = new InterFluxStandardToken(this.mockContractState.Object, totalSupply, this.name, this.symbol, this.decimals, this.nativeChain, this.nativeAddress);

            // Setup the owner of the contract
            this.mockPersistentState.Setup(s => s.GetAddress($"Owner")).Returns(this.owner);

            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.sender, 0));

            Assert.ThrowsAny<SmartContractAssertException>(() => standardToken.TransferOwnership(this.destination));
        }

        [Fact]
        public void Mint_Increases_Balance_And_TotalSupply()
        {
            UInt256 balance = 100;
            UInt256 mintAmount = 20;

            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.sender, 0));

            var standardToken = new InterFluxStandardToken(this.mockContractState.Object, 100_000, this.name, this.symbol, this.decimals, this.nativeChain, this.nativeAddress);

            // Setup the total supply
            this.mockPersistentState.Setup(s => s.GetUInt256($"TotalSupply")).Returns(100_000);

            // Setup the owner of the contract; without this the mint will fail
            this.mockPersistentState.Setup(s => s.GetAddress($"Owner")).Returns(this.sender);

            // Setup the balance of the sender's address in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt256($"Balance:{this.sender}")).Returns(balance);

            standardToken.Mint(this.sender, mintAmount);

            this.mockContractLogger.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new InterFluxStandardToken.TransferLog { From = Address.Zero, To = this.sender, Amount = mintAmount }));

            // Verify we queried the balance
            this.mockPersistentState.Verify(s => s.GetUInt256($"Balance:{this.sender}"));

            // Verify we set the sender's balance
            this.mockPersistentState.Verify(s => s.SetUInt256($"Balance:{this.sender}", balance + mintAmount));

            // Verify that the total supply was increased
            this.mockPersistentState.Verify(s => s.SetUInt256($"TotalSupply", 100_000 + mintAmount));
        }

        [Fact]
        public void Mint_Fails_For_NonOwner()
        {
            UInt256 balance = 100;
            UInt256 mintAmount = 20;

            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));

            var standardToken = new InterFluxStandardToken(this.mockContractState.Object, 100_000, this.name, this.symbol, this.decimals, this.nativeChain, this.nativeAddress);

            // Setup the owner of the contract
            this.mockPersistentState.Setup(s => s.GetAddress($"Owner")).Returns(this.owner);

            // Attempt the mint from a different address
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.sender, 0));

            Assert.ThrowsAny<SmartContractAssertException>(() => standardToken.Mint(this.sender, mintAmount));
        }

        [Fact]
        public void Burn_Decreases_Balance_And_TotalSupply()
        {
            UInt256 balance = 100;
            UInt256 burnAmount = 20;

            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.sender, 0));

            var standardToken = new InterFluxStandardToken(this.mockContractState.Object, 100_000, this.name, this.symbol, this.decimals, this.nativeChain, this.nativeAddress);

            // Setup the total supply
            this.mockPersistentState.Setup(s => s.GetUInt256($"TotalSupply")).Returns(100_000);

            // Setup the balance of the sender's address in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt256($"Balance:{this.sender}")).Returns(balance);

            // Setup the balance of the recipient's address in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt256($"Balance:{Address.Zero}")).Returns(0);

            standardToken.Burn(burnAmount);

            this.mockContractLogger.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new InterFluxStandardToken.TransferLog { From = this.sender, To = Address.Zero, Amount = burnAmount }));

            // Verify we queried the balance
            this.mockPersistentState.Verify(s => s.GetUInt256($"Balance:{this.sender}"));

            // Verify we set the sender's balance
            this.mockPersistentState.Verify(s => s.SetUInt256($"Balance:{this.sender}", balance - burnAmount));

            // Verify we set the receiver's balance (i.e. the zero address)
            this.mockPersistentState.Verify(s => s.SetUInt256($"Balance:{Address.Zero}", burnAmount));

            // Verify that the total supply was decreased
            this.mockPersistentState.Verify(s => s.SetUInt256($"TotalSupply", 100_000 - burnAmount));
        }

        [Fact]
        public void Burn_For_Amount_Exceeding_Balance_Fails()
        {
            UInt256 balance = 100;
            UInt256 burnAmount = 120;

            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.sender, 0));

            var standardToken = new InterFluxStandardToken(this.mockContractState.Object, 100_000, this.name, this.symbol, this.decimals, this.nativeChain, this.nativeAddress);

            // Setup the total supply
            this.mockPersistentState.Setup(s => s.GetUInt256($"TotalSupply")).Returns(100_000);

            // Setup the balance of the sender's address in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt256($"Balance:{this.sender}")).Returns(balance);

            // Setup the balance of the recipient's address in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt256($"Balance:{Address.Zero}")).Returns(0);

            Assert.False(standardToken.Burn(burnAmount));
        }

        [Fact]
        public void BurnWithMetadata_Records_Metadata_In_Log()
        {
            UInt256 balance = 100;
            UInt256 burnAmount = 20;

            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.sender, 0));

            var standardToken = new InterFluxStandardToken(this.mockContractState.Object, 100_000, this.name, this.symbol, this.decimals, this.nativeChain, this.nativeAddress);

            // Setup the total supply
            this.mockPersistentState.Setup(s => s.GetUInt256($"TotalSupply")).Returns(100_000);

            // Setup the balance of the sender's address in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt256($"Balance:{this.sender}")).Returns(balance);

            // Setup the balance of the recipient's address in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt256($"Balance:{Address.Zero}")).Returns(0);

            standardToken.BurnWithMetadata(burnAmount, "Hello world");

            this.mockContractLogger.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new InterFluxStandardToken.TransferLog { From = this.sender, To = Address.Zero, Amount = burnAmount }));

            // Verify we queried the balance
            this.mockPersistentState.Verify(s => s.GetUInt256($"Balance:{this.sender}"));

            // Verify we set the sender's balance
            this.mockPersistentState.Verify(s => s.SetUInt256($"Balance:{this.sender}", balance - burnAmount));

            // Verify we set the receiver's balance (i.e. the zero address)
            this.mockPersistentState.Verify(s => s.SetUInt256($"Balance:{Address.Zero}", burnAmount));

            // Verify that the total supply was decreased
            this.mockPersistentState.Verify(s => s.SetUInt256($"TotalSupply", 100_000 - burnAmount));

            this.mockContractLogger.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new InterFluxStandardToken.BurnMetadata() { From = this.sender, Amount = burnAmount, Metadata = "Hello world" }));
        }
    }
}
