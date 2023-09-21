using System.Linq;
using Moq;
using NBitcoin.DataEncoders;
using NBitcoin;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Serialization;
using Xunit;

namespace MintableTokenTests
{
    public class ChameleonNetwork : Network
    {
        public ChameleonNetwork(byte base58Prefix)
        {
            this.Base58Prefixes = new byte[][] { new byte[] { base58Prefix } };
        }
    }

    /// <summary>
    /// These tests validate the functionality that differs between the original standard token and the extended version.
    /// </summary>
    public class MintableTokenTests
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

        public MintableTokenTests()
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

            var serializer = new Serializer(new ContractPrimitiveSerializerV2(null)); // new SmartContractsPoARegTest()
            this.mockContractState.Setup(x => x.Serializer).Returns(serializer);
        }

        [Fact]
        public void Constructor_Assigns_Owner()
        {
            UInt256 totalSupply = 100_000;
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));

            var standardToken = new MintableToken(this.mockContractState.Object, totalSupply, this.name, this.symbol, "CIRRUS", "Address");

            // Verify that PersistentState was called with the contract owner
            this.mockPersistentState.Verify(s => s.SetAddress($"Owner", this.owner));
        }

        [Fact]
        public void TransferOwnership_Succeeds_For_Owner()
        {
            UInt256 totalSupply = 100_000;
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));

            var standardToken = new MintableToken(this.mockContractState.Object, totalSupply, this.name, this.symbol, "CIRRUS", "Address");

            // Setup the owner of the contract
            this.mockPersistentState.Setup(s => s.GetAddress($"Owner")).Returns(this.owner);

            standardToken.TransferOwnership(this.destination);

            this.mockPersistentState.Setup(s => s.GetAddress($"PendingOwner")).Returns(this.destination);
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.destination, 0));

            standardToken.ClaimOwnership();

            // Verify that PersistentState was called to update the contract owner
            this.mockPersistentState.Verify(s => s.SetAddress($"Owner", this.destination));

            this.mockContractLogger.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new MintableToken.OwnershipTransferredLog() { PreviousOwner = this.owner, NewOwner = this.destination }));
        }

        [Fact]
        public void TransferOwnership_Fails_For_NonOwner()
        {
            UInt256 totalSupply = 100_000;
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));

            var standardToken = new MintableToken(this.mockContractState.Object, totalSupply, this.name, this.symbol, "CIRRUS", "Address");

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

            var mintableToken = new MintableToken(this.mockContractState.Object, 100_000, this.name, this.symbol, "CIRRUS", "Address");

            // Setup the total supply
            this.mockPersistentState.Setup(s => s.GetUInt256($"TotalSupply")).Returns(100_000);

            // Setup the minter of the contract; without this the mint will fail
            this.mockPersistentState.Setup(s => s.GetAddress($"Owner")).Returns(this.sender);

            // Setup the balance of the sender's address in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt256($"Balance:{this.sender}")).Returns(balance);

            mintableToken.MintWithMetadataForCirrus(this.sender, mintAmount, 0, "ExternalId", this.sender);

            this.mockContractLogger.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new MintableToken.TransferLog { From = Address.Zero, To = this.sender, Amount = mintAmount }));

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

            var standardToken = new MintableToken(this.mockContractState.Object, 100_000, this.name, this.symbol, "CIRRUS", "Address");

            // Setup the owner of the contract
            this.mockPersistentState.Setup(s => s.GetAddress($"Owner")).Returns(this.owner);

            // Attempt the mint from a different address
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.sender, 0));

            Assert.ThrowsAny<SmartContractAssertException>(() => standardToken.MintWithMetadataForCirrus(this.sender, mintAmount, 0, "ExternalId", this.sender));
        }

        [Fact]
        public void Burn_Decreases_Balance_And_TotalSupply()
        {
            UInt256 balance = 100;
            UInt256 burnAmount = 20;

            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.sender, 0));

            var standardToken = new MintableToken(this.mockContractState.Object, 100_000, this.name, this.symbol, "CIRRUS", "Address");

            // Setup the total supply
            this.mockPersistentState.Setup(s => s.GetUInt256($"TotalSupply")).Returns(100_000);

            // Setup the balance of the sender's address in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt256($"Balance:{this.sender}")).Returns(balance);

            // Setup the balance of the recipient's address in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt256($"Balance:{Address.Zero}")).Returns(0);

            standardToken.BurnWithMetadata(burnAmount, "ExternalId");

            this.mockContractLogger.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new MintableToken.TransferLog { From = this.sender, To = Address.Zero, Amount = burnAmount }));

            // Verify we queried the balance
            this.mockPersistentState.Verify(s => s.GetUInt256($"Balance:{this.sender}"));

            // Verify we set the sender's balance
            this.mockPersistentState.Verify(s => s.SetUInt256($"Balance:{this.sender}", balance - burnAmount));

            // Verify that the total supply was decreased
            this.mockPersistentState.Verify(s => s.SetUInt256($"TotalSupply", 100_000 - burnAmount));
        }

        [Fact]
        public void Burn_For_Amount_Exceeding_Balance_Fails()
        {
            UInt256 balance = 100;
            UInt256 burnAmount = 120;

            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.sender, 0));

            var standardToken = new MintableToken(this.mockContractState.Object, 100_000, this.name, this.symbol, "CIRRUS", "Address");

            // Setup the total supply
            this.mockPersistentState.Setup(s => s.GetUInt256($"TotalSupply")).Returns(100_000);

            // Setup the balance of the sender's address in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt256($"Balance:{this.sender}")).Returns(balance);

            // Setup the balance of the recipient's address in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt256($"Balance:{Address.Zero}")).Returns(0);

            Assert.False(standardToken.BurnWithMetadata(burnAmount, "ExternalId"));
        }

        [Fact]
        public void BurnWithMetadata_Records_Metadata_In_Log()
        {
            UInt256 balance = 100;
            UInt256 burnAmount = 20;

            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.sender, 0));

            var standardToken = new MintableToken(this.mockContractState.Object, 100_000, this.name, this.symbol, "CIRRUS", "Address");

            // Setup the total supply
            this.mockPersistentState.Setup(s => s.GetUInt256($"TotalSupply")).Returns(100_000);

            // Setup the balance of the sender's address in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt256($"Balance:{this.sender}")).Returns(balance);

            // Setup the balance of the recipient's address in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt256($"Balance:{Address.Zero}")).Returns(0);

            standardToken.BurnWithMetadata(burnAmount, "Hello world");

            // Verify we queried the balance
            this.mockPersistentState.Verify(s => s.GetUInt256($"Balance:{this.sender}"));

            // Verify we set the sender's balance
            this.mockPersistentState.Verify(s => s.SetUInt256($"Balance:{this.sender}", balance - burnAmount));

            // Verify that the total supply was decreased
            this.mockPersistentState.Verify(s => s.SetUInt256($"TotalSupply", 100_000 - burnAmount));

            this.mockContractLogger.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new MintableToken.BurnMetadata() { From = this.sender, Amount = burnAmount, Metadata = "Hello world" }));
        }

        [Fact]
        public void CanPerformDelegatedTransfer()
        {
            UInt256 balance = 100;
            UInt256 burnAmount = 20;

            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.sender, 0));

            var standardToken = new MintableToken(this.mockContractState.Object, 100_000, this.name, this.symbol, "CIRRUS", "Address");

            // Setup the total supply
            this.mockPersistentState.Setup(s => s.GetUInt256($"TotalSupply")).Returns(100_000);

            // Setup the balance of the sender's address in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt256($"Balance:{this.sender}")).Returns(balance);

            // Setup the balance of the recipient's address in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt256($"Balance:{Address.Zero}")).Returns(0);

            var uniqueNumber = new UInt128("1");

            var key = new Key(new HexEncoder().DecodeData("c6edd54dd0671f1415a94ad388265c4465a8b328cc51a0a1fe770d910b48b0d1"));
            var address = key.PubKey.Hash.ToBytes().ToAddress();

            var hexUniqueNumber = Encoders.Hex.EncodeData(uniqueNumber.ToBytes().Reverse().ToArray());

            var contractAddress = this.contract.ToUint160().ToBase58Address(new ChameleonNetwork(1));
            var fromAddress = address.ToUint160().ToBase58Address(new ChameleonNetwork(1));
            var toAddress = this.destination.ToUint160().ToBase58Address(new ChameleonNetwork(1));

            var url = $"webdemo.stratisplatform.com:7167/api/auth?uid={hexUniqueNumber}&from={fromAddress}&to={toAddress}&amount=1.23&metadata=metadata&contract={contractAddress}";
            var signature = key.SignMessage(url);
            byte[] signatureBytes = Encoders.Base64.DecodeData(signature);

            standardToken.DelegatedTransferWithMetadata(url, signatureBytes);
        }
    }
}
