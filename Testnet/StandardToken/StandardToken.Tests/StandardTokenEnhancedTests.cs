using System;
using Moq;
using Stratis.SmartContracts.CLR;
using Xunit;

namespace Stratis.SmartContracts.Samples.Tests
{
    public class StandardTokenEnhancedTests
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

        public StandardTokenEnhancedTests()
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
        }

        [Fact]
        public void Constructor_Assigns_Owner()
        {
            UInt256 totalSupply = 100_000;
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));

            var standardToken = new StandardTokenEnhanced(this.mockContractState.Object, totalSupply, this.name, this.symbol, this.decimals);

            // Verify that PersistentState was called with the contract owner
            this.mockPersistentState.Verify(s => s.SetAddress($"Owner", this.owner));
        }

        [Fact]
        public void IsOwner_Returns_True_For_Owner()
        {
            UInt256 totalSupply = 100_000;
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));

            var standardToken = new StandardTokenEnhanced(this.mockContractState.Object, totalSupply, this.name, this.symbol, this.decimals);

            // Setup the owner of the contract
            this.mockPersistentState.Setup(s => s.GetAddress($"Owner")).Returns(this.owner);

            Assert.True(standardToken.IsOwner());
        }

        [Fact]
        public void IsOwner_Returns_False_For_NonOwner()
        {
            UInt256 totalSupply = 100_000;
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));

            var standardToken = new StandardTokenEnhanced(this.mockContractState.Object, totalSupply, this.name, this.symbol, this.decimals);

            // Setup the owner of the contract
            this.mockPersistentState.Setup(s => s.GetAddress($"Owner")).Returns(this.owner);

            // Call the IsOwner method from a different address
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.sender, 0));

            Assert.False(standardToken.IsOwner());
        }

        [Fact]
        public void RenounceOwnership_Succeeds_For_Owner()
        {
            UInt256 totalSupply = 100_000;
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));

            var standardToken = new StandardTokenEnhanced(this.mockContractState.Object, totalSupply, this.name, this.symbol, this.decimals);

            // Setup the owner of the contract
            this.mockPersistentState.Setup(s => s.GetAddress($"Owner")).Returns(this.owner);

            standardToken.RenounceOwnership();

            // Verify that PersistentState was called to update the contract owner
            this.mockPersistentState.Verify(s => s.SetAddress($"Owner", Address.Zero));

            this.mockContractLogger.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new StandardTokenEnhanced.OwnershipTransferred() { PreviousOwner = this.owner, NewOwner = Address.Zero}));
        }

        [Fact]
        public void RenounceOwnership_Fails_For_NonOwner()
        {
            UInt256 totalSupply = 100_000;
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));

            var standardToken = new StandardTokenEnhanced(this.mockContractState.Object, totalSupply, this.name, this.symbol, this.decimals);

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

            var standardToken = new StandardTokenEnhanced(this.mockContractState.Object, totalSupply, this.name, this.symbol, this.decimals);

            // Setup the owner of the contract
            this.mockPersistentState.Setup(s => s.GetAddress($"Owner")).Returns(this.owner);

            standardToken.TransferOwnership(this.destination);

            // Verify that PersistentState was called to update the contract owner
            this.mockPersistentState.Verify(s => s.SetAddress($"Owner", this.destination));

            this.mockContractLogger.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new StandardTokenEnhanced.OwnershipTransferred() { PreviousOwner = this.owner, NewOwner = this.destination }));
        }

        [Fact]
        public void TransferOwnership_Fails_For_NonOwner()
        {
            UInt256 totalSupply = 100_000;
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));

            var standardToken = new StandardTokenEnhanced(this.mockContractState.Object, totalSupply, this.name, this.symbol, this.decimals);

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

            var standardToken = new StandardTokenEnhanced(this.mockContractState.Object, 100_000, this.name, this.symbol, this.decimals);

            // Setup the total supply
            this.mockPersistentState.Setup(s => s.GetUInt256($"TotalSupply")).Returns(100_000);

            // Setup the owner of the contract; without this the mint will fail
            this.mockPersistentState.Setup(s => s.GetAddress($"Owner")).Returns(this.sender);

            // Setup the balance of the sender's address in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt256($"Balance:{this.sender}")).Returns(balance);

            standardToken.Mint(this.sender, mintAmount);

            this.mockContractLogger.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new StandardTokenEnhanced.TransferLog { From = Address.Zero, To = this.sender, Amount = mintAmount }));

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

            var standardToken = new StandardTokenEnhanced(this.mockContractState.Object, 100_000, this.name, this.symbol, this.decimals);

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

            var standardToken = new StandardTokenEnhanced(this.mockContractState.Object, 100_000, this.name, this.symbol, this.decimals);

            // Setup the total supply
            this.mockPersistentState.Setup(s => s.GetUInt256($"TotalSupply")).Returns(100_000);

            // Setup the balance of the sender's address in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt256($"Balance:{this.sender}")).Returns(balance);

            // Setup the balance of the recipient's address in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt256($"Balance:{Address.Zero}")).Returns(0);

            standardToken.Burn(burnAmount);

            this.mockContractLogger.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new StandardTokenEnhanced.TransferLog { From = this.sender, To = Address.Zero, Amount = burnAmount }));

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

            var standardToken = new StandardTokenEnhanced(this.mockContractState.Object, 100_000, this.name, this.symbol, this.decimals);

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

            var standardToken = new StandardTokenEnhanced(this.mockContractState.Object, 100_000, this.name, this.symbol, this.decimals);

            // Setup the total supply
            this.mockPersistentState.Setup(s => s.GetUInt256($"TotalSupply")).Returns(100_000);

            // Setup the balance of the sender's address in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt256($"Balance:{this.sender}")).Returns(balance);

            // Setup the balance of the recipient's address in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt256($"Balance:{Address.Zero}")).Returns(0);

            standardToken.BurnWithMetadata(burnAmount, "Hello world");

            this.mockContractLogger.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new StandardTokenEnhanced.TransferLog { From = this.sender, To = Address.Zero, Amount = burnAmount }));

            // Verify we queried the balance
            this.mockPersistentState.Verify(s => s.GetUInt256($"Balance:{this.sender}"));

            // Verify we set the sender's balance
            this.mockPersistentState.Verify(s => s.SetUInt256($"Balance:{this.sender}", balance - burnAmount));

            // Verify we set the receiver's balance (i.e. the zero address)
            this.mockPersistentState.Verify(s => s.SetUInt256($"Balance:{Address.Zero}", burnAmount));

            // Verify that the total supply was decreased
            this.mockPersistentState.Verify(s => s.SetUInt256($"TotalSupply", 100_000 - burnAmount));

            this.mockContractLogger.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new StandardTokenEnhanced.BurnMetadata() { From = this.sender, Amount = burnAmount, Metadata = "Hello world" }));
        }

        #region StandardToken compatibility
        [Fact]
        public void Constructor_Sets_TotalSupply()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));

            UInt256 totalSupply = 100_000;
            var standardToken = new StandardTokenEnhanced(this.mockContractState.Object, totalSupply, this.name, this.symbol, this.decimals);

            // Verify that PersistentState was called with the total supply
            this.mockPersistentState.Verify(s => s.SetUInt256(nameof(StandardTokenEnhanced.TotalSupply), totalSupply));
        }

        [Fact]
        public void Constructor_Assigns_TotalSupply_To_Owner()
        {
            UInt256 totalSupply = 100_000;
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));

            var standardToken = new StandardTokenEnhanced(this.mockContractState.Object, totalSupply, this.name, this.symbol, this.decimals);

            // Verify that PersistentState was called with the total supply
            this.mockPersistentState.Verify(s => s.SetUInt256($"Balance:{this.owner}", totalSupply));
        }

        [Fact]
        public void GetBalance_Returns_Correct_Balance()
        {
            UInt256 balance = 100;

            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));

            var standardToken = new StandardTokenEnhanced(this.mockContractState.Object, 100_000, this.name, this.symbol, this.decimals);

            // Setup the balance of the address in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt256($"Balance:{this.spender}")).Returns(balance);

            Assert.Equal(balance, standardToken.GetBalance(this.spender));
        }

        [Fact]
        public void Approve_Sets_Approval_Correctly()
        {
            UInt256 approval = 1000;

            // Setup the Message.Sender address
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));

            var standardToken = new StandardTokenEnhanced(this.mockContractState.Object, 100_000, this.name, this.symbol, this.decimals);

            standardToken.Approve(this.spender, 0, approval);

            this.mockPersistentState.Verify(s => s.SetUInt256($"Allowance:{this.owner}:{this.spender}", approval));

            this.mockContractLogger.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new StandardTokenEnhanced.ApprovalLog { Owner = this.owner, Spender = this.spender, Amount = approval, OldAmount = 0 }));
        }

        [Fact]
        public void Approve_Sets_Approval_Correctly_When_NonZero()
        {
            UInt256 approval = 1000;
            UInt256 newApproval = 2000;

            // Setup the Message.Sender address
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));

            var standardToken = new StandardTokenEnhanced(this.mockContractState.Object, 100_000, this.name, this.symbol, this.decimals);

            // Set up an existing allowance
            this.mockPersistentState.Setup(s => s.GetUInt256($"Allowance:{this.owner}:{this.spender}")).Returns(approval);

            standardToken.Approve(this.spender, approval, newApproval);

            this.mockPersistentState.Verify(s => s.SetUInt256($"Allowance:{this.owner}:{this.spender}", newApproval));

            this.mockContractLogger.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new StandardTokenEnhanced.ApprovalLog { Owner = this.owner, Spender = this.spender, Amount = newApproval, OldAmount = approval }));
        }

        [Fact]
        public void Approve_Does_Not_Set_Approval_If_Different()
        {
            ulong approval = 1000;
            ulong newApproval = 2000;

            // Setup the Message.Sender address
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));

            var standardToken = new StandardTokenEnhanced(this.mockContractState.Object, 100_000, this.name, this.symbol, this.decimals);

            // Set up an existing allowance
            this.mockPersistentState.Setup(s => s.GetUInt64($"Allowance:{this.owner}:{this.spender}")).Returns(approval);

            // Attempt to set the new approval for a different earlier approval
            var differentApproval = approval + 1;

            Assert.False(standardToken.Approve(this.spender, differentApproval, newApproval));

            this.mockPersistentState.Verify(s => s.SetUInt64($"Allowance:{this.owner}:{this.spender}", It.IsAny<ulong>()), Times.Never);
        }

        [Fact]
        public void Allowance_Returns_Correct_Allowance()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.sender, 0));

            var standardToken = new StandardTokenEnhanced(this.mockContractState.Object, 100_000, this.name, this.symbol, this.decimals);

            standardToken.Allowance(this.owner, this.spender);

            this.mockPersistentState.Verify(s => s.GetUInt256($"Allowance:{this.owner}:{this.spender}"));
        }

        [Fact]
        public void TransferTo_0_Returns_True()
        {
            ulong amount = 0;

            // Setup the Message.Sender address
            this.mockContractState.Setup(m => m.Message)
                .Returns(new Message(this.contract, this.sender, 0));

            var standardToken = new StandardTokenEnhanced(this.mockContractState.Object, 100_000, this.name, this.symbol, this.decimals);

            Assert.True(standardToken.TransferTo(this.destination, amount));
            this.mockContractLogger.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new StandardTokenEnhanced.TransferLog { From = this.sender, To = this.destination, Amount = amount }));
        }

        [Fact]
        public void TransferTo_Full_Balance_Returns_True()
        {
            UInt256 balance = 10000;
            UInt256 amount = balance;
            UInt256 destinationBalance = 123;

            // Setup the Message.Sender address
            this.mockContractState.Setup(m => m.Message)
                .Returns(new Message(this.contract, this.sender, 0));

            var standardToken = new StandardTokenEnhanced(this.mockContractState.Object, 100_000, this.name, this.symbol, this.decimals);

            // Setup the balance of the sender's address in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt256($"Balance:{this.sender}")).Returns(balance);

            // Setup the balance of the recipient's address in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt256($"Balance:{this.destination}")).Returns(destinationBalance);

            Assert.True(standardToken.TransferTo(this.destination, amount));

            // Verify we queried the balance
            this.mockPersistentState.Verify(s => s.GetUInt256($"Balance:{this.sender}"));

            // Verify we set the sender's balance
            this.mockPersistentState.Verify(s => s.SetUInt256($"Balance:{this.sender}", balance - amount));

            // Verify we set the receiver's balance
            this.mockPersistentState.Verify(s => s.SetUInt256($"Balance:{this.destination}", destinationBalance + amount));
        }

        [Fact]
        public void TransferTo_Greater_Than_Balance_Returns_False()
        {
            UInt256 balance = 0;
            UInt256 amount = balance + 1;

            // Setup the Message.Sender address
            this.mockContractState.Setup(m => m.Message)
                .Returns(new Message(this.contract, this.sender, 0));

            var standardToken = new StandardTokenEnhanced(this.mockContractState.Object, 100_000, this.name, this.symbol, this.decimals);

            // Setup the balance of the address in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt256($"Balance:{this.sender}")).Returns(balance);

            Assert.False(standardToken.TransferTo(this.destination, amount));

            // Verify we queried the balance
            this.mockPersistentState.Verify(s => s.GetUInt256($"Balance:{this.sender}"));
        }

        [Fact]
        public void TransferTo_Destination_With_Balance_Greater_Than_uint_MaxValue_Returns_False()
        {
            UInt256 destinationBalance = UInt256.MaxValue;
            UInt256 senderBalance = 100;
            UInt256 amount = senderBalance - 1; // Transfer less than the balance

            // Setup the Message.Sender address
            this.mockContractState.Setup(m => m.Message)
                .Returns(new Message(this.contract, this.sender, 0));

            var standardToken = new StandardTokenEnhanced(this.mockContractState.Object, 100_000, this.name, this.symbol, this.decimals);

            // Setup the balance of the address in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt256($"Balance:{this.sender}")).Returns(senderBalance);

            // Setup the destination's balance to be ulong.MaxValue
            this.mockPersistentState.Setup(s => s.GetUInt256($"Balance:{this.destination}")).Returns(destinationBalance);

            Assert.ThrowsAny<OverflowException>(() => standardToken.TransferTo(this.destination, amount));

            // Verify we queried the sender's balance
            this.mockPersistentState.Verify(s => s.GetUInt256($"Balance:{this.sender}"));

            // Verify we queried the destination's balance
            this.mockPersistentState.Verify(s => s.GetUInt256($"Balance:{this.destination}"));
        }

        [Fact]
        public void TransferTo_Destination_Success_Returns_True()
        {
            UInt256 destinationBalance = 400_000;
            UInt256 senderBalance = 100;
            UInt256 amount = senderBalance - 1; // Transfer less than the balance

            // Setup the Message.Sender address
            this.mockContractState.Setup(m => m.Message)
                .Returns(new Message(this.contract, this.sender, 0));

            var standardToken = new StandardTokenEnhanced(this.mockContractState.Object, 100_000, this.name, this.symbol, this.decimals);

            int callOrder = 1;

            // Setup the balance of the address in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt256($"Balance:{this.sender}")).Returns(senderBalance)
                .Callback(() => Assert.Equal(1, callOrder++));

            // Setup the sender's balance
            this.mockPersistentState.Setup(s => s.SetUInt256($"Balance:{this.sender}", It.IsAny<UInt256>()))
                .Callback(() => Assert.Equal(2, callOrder++));

            // Setup the destination's balance
            this.mockPersistentState.Setup(s => s.GetUInt256($"Balance:{this.destination}")).Returns(destinationBalance)
                .Callback(() => Assert.Equal(3, callOrder++));

            // Setup the destination's balance. Important that this happens AFTER setting the sender's balance
            this.mockPersistentState.Setup(s => s.SetUInt256($"Balance:{this.destination}", It.IsAny<UInt256>()))
                .Callback(() => Assert.Equal(4, callOrder++));

            Assert.True(standardToken.TransferTo(this.destination, amount));

            // Verify we queried the sender's balance
            this.mockPersistentState.Verify(s => s.GetUInt256($"Balance:{this.sender}"));

            // Verify we queried the destination's balance
            this.mockPersistentState.Verify(s => s.GetUInt256($"Balance:{this.destination}"));

            // Verify we set the sender's balance
            this.mockPersistentState.Verify(s => s.SetUInt256($"Balance:{this.sender}", senderBalance - amount));

            // Verify we set the receiver's balance
            this.mockPersistentState.Verify(s => s.SetUInt256($"Balance:{this.destination}", destinationBalance + amount));

            this.mockContractLogger.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new StandardTokenEnhanced.TransferLog { From = this.sender, To = this.destination, Amount = amount }));
        }

        [Fact]
        public void TransferFrom_0_Returns_True()
        {
            ulong amount = 0;

            // Setup the Message.Sender address
            this.mockContractState.Setup(m => m.Message)
                .Returns(new Message(this.contract, this.sender, 0));

            var standardToken = new StandardTokenEnhanced(this.mockContractState.Object, 100_000, this.name, this.symbol, this.decimals);

            Assert.True(standardToken.TransferFrom(this.owner, this.destination, amount));

            this.mockContractLogger.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new StandardTokenEnhanced.TransferLog { From = this.owner, To = this.destination, Amount = amount }));
        }

        [Fact]
        public void TransferFrom_Full_Balance_Returns_True()
        {
            UInt256 allowance = 1000;
            UInt256 amount = allowance;
            UInt256 balance = amount; // Balance should be the same as the amount we are trying to send

            // Setup the Message.Sender address
            this.mockContractState.Setup(m => m.Message)
                .Returns(new Message(this.contract, this.sender, 0));

            var standardToken = new StandardTokenEnhanced(this.mockContractState.Object, 100_000, this.name, this.symbol, this.decimals);

            // Setup the balance of the owner in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt256($"Balance:{this.owner}")).Returns(balance);

            // Setup the balance of the address in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt256($"Allowance:{this.owner}:{this.sender}")).Returns(allowance);

            Assert.True(standardToken.TransferFrom(this.owner, this.destination, amount));

            // Verify we queried the sender's allowance
            this.mockPersistentState.Verify(s => s.GetUInt256($"Allowance:{this.owner}:{this.sender}"));

            // Verify we queried the owner's balance
            this.mockPersistentState.Verify(s => s.GetUInt256($"Balance:{this.owner}"));

            // Verify we set the sender's allowance
            this.mockPersistentState.Verify(s => s.SetUInt256($"Allowance:{this.owner}:{this.sender}", allowance - amount));

            // Verify we set the owner's balance
            this.mockPersistentState.Verify(s => s.SetUInt256($"Balance:{this.owner}", balance - amount));

            // Verify we set the destination's balance
            this.mockPersistentState.Verify(s => s.SetUInt256($"Balance:{this.destination}", amount));
        }

        [Fact]
        public void TransferFrom_Greater_Than_Senders_Allowance_Returns_False()
        {
            UInt256 allowance = 0;
            UInt256 amount = allowance + 1;
            UInt256 balance = amount + 1; // Balance should be more than amount we are trying to send

            // Setup the Message.Sender address
            this.mockContractState.Setup(m => m.Message)
                .Returns(new Message(this.contract, this.sender, 0));

            var standardToken = new StandardTokenEnhanced(this.mockContractState.Object, 100_000, this.name, this.symbol, this.decimals);

            // Setup the balance of the owner in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt256($"Balance:{this.owner}")).Returns(balance);

            // Setup the balance of the address in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt256($"Allowance:{this.owner}:{this.sender}")).Returns(allowance);

            Assert.False(standardToken.TransferFrom(this.owner, this.destination, amount));

            // Verify we queried the sender's allowance
            this.mockPersistentState.Verify(s => s.GetUInt256($"Allowance:{this.owner}:{this.sender}"));

            // Verify we queried the owner's balance
            this.mockPersistentState.Verify(s => s.GetUInt256($"Balance:{this.owner}"));
        }

        [Fact]
        public void TransferFrom_Greater_Than_Owners_Balance_Returns_False()
        {
            UInt256 balance = 0; // Balance should be less than amount we are trying to send
            UInt256 amount = balance + 1;
            UInt256 allowance = amount + 1; // Allowance should be more than amount we are trying to send

            // Setup the Message.Sender address
            this.mockContractState.Setup(m => m.Message)
                .Returns(new Message(this.contract, this.sender, 0));

            var standardToken = new StandardTokenEnhanced(this.mockContractState.Object, 100_000, this.name, this.symbol, this.decimals);

            // Setup the balance of the owner in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt256($"Balance:{this.owner}")).Returns(balance);

            // Setup the allowance of the sender in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt256($"Allowance:{this.owner}:{this.sender}")).Returns(allowance);

            Assert.False(standardToken.TransferFrom(this.owner, this.destination, amount));

            // Verify we queried the sender's allowance
            this.mockPersistentState.Verify(s => s.GetUInt256($"Allowance:{this.owner}:{this.sender}"));

            // Verify we queried the owner's balance
            this.mockPersistentState.Verify(s => s.GetUInt256($"Balance:{this.owner}"));
        }

        [Fact]
        public void TransferFrom_To_Destination_With_Balance_Greater_Than_Amount_MaxValue_Returns_False()
        {
            UInt256 destinationBalance = UInt256.MaxValue; // Destination balance should be ulong.MaxValue
            UInt256 amount = 1;
            UInt256 allowance = amount + 1; // Allowance should be more than amount we are trying to send
            UInt256 ownerBalance = allowance + 1; // Owner balance should be more than allowance

            // Setup the Message.Sender address
            this.mockContractState.Setup(m => m.Message)
                .Returns(new Message(this.contract, this.sender, 0));

            var standardToken = new StandardTokenEnhanced(this.mockContractState.Object, 100_000, this.name, this.symbol, this.decimals);

            // Setup the balance of the owner in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt256($"Balance:{this.owner}")).Returns(ownerBalance);

            // Setup the balance of the destination in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt256($"Balance:{this.destination}")).Returns(destinationBalance);

            // Setup the allowance of the sender in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt256($"Allowance:{this.owner}:{this.sender}")).Returns(allowance);

            Assert.ThrowsAny<OverflowException>(() => standardToken.TransferFrom(this.owner, this.destination, amount));

            // Verify we queried the sender's allowance
            this.mockPersistentState.Verify(s => s.GetUInt256($"Allowance:{this.owner}:{this.sender}"));

            // Verify we queried the owner's balance
            this.mockPersistentState.Verify(s => s.GetUInt256($"Balance:{this.owner}"));
        }

        [Fact]
        public void TransferFrom_To_Destination_Success_Returns_True()
        {
            UInt256 destinationBalance = 100;
            UInt256 amount = 1;
            UInt256 allowance = amount + 1; // Allowance should be more than amount we are trying to send
            UInt256 ownerBalance = allowance + 1; // Owner balance should be more than allowance

            // Setup the Message.Sender address
            this.mockContractState.Setup(m => m.Message)
                .Returns(new Message(this.contract, this.sender, 0));

            var standardToken = new StandardTokenEnhanced(this.mockContractState.Object, 100_000, this.name, this.symbol, this.decimals);

            int callOrder = 1;

            // Setup the allowance of the sender in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt256($"Allowance:{this.owner}:{this.sender}")).Returns(allowance)
                .Callback(() => Assert.Equal(1, callOrder++));

            // Setup the balance of the owner in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt256($"Balance:{this.owner}")).Returns(ownerBalance)
                .Callback(() => Assert.Equal(2, callOrder++));

            // Set the sender's new allowance
            this.mockPersistentState.Setup(s => s.SetUInt256($"Allowance:{this.owner}:{this.sender}", It.IsAny<UInt256>()))
                .Callback(() => Assert.Equal(3, callOrder++));

            // Set the owner's new balance
            this.mockPersistentState.Setup(s => s.SetUInt256($"Balance:{this.owner}", It.IsAny<UInt256>()))
                .Callback(() => Assert.Equal(4, callOrder++));

            // Setup the balance of the destination in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt256($"Balance:{this.destination}")).Returns(destinationBalance)
                .Callback(() => Assert.Equal(5, callOrder++));

            // Setup the destination's balance. Important that this happens AFTER setting the owner's balance
            this.mockPersistentState.Setup(s => s.SetUInt256($"Balance:{this.destination}", It.IsAny<UInt256>()))
                .Callback(() => Assert.Equal(6, callOrder++));

            Assert.True(standardToken.TransferFrom(this.owner, this.destination, amount));

            // Verify we queried the sender's allowance
            this.mockPersistentState.Verify(s => s.GetUInt256($"Allowance:{this.owner}:{this.sender}"));

            // Verify we queried the owner's balance
            this.mockPersistentState.Verify(s => s.GetUInt256($"Balance:{this.owner}"));

            // Verify we queried the destination's balance
            this.mockPersistentState.Verify(s => s.GetUInt256($"Balance:{this.destination}"));

            // Verify we set the sender's allowance
            this.mockPersistentState.Verify(s => s.SetUInt256($"Allowance:{this.owner}:{this.sender}", allowance - amount));

            // Verify we set the owner's balance
            this.mockPersistentState.Verify(s => s.SetUInt256($"Balance:{this.owner}", ownerBalance - amount));

            // Verify we set the receiver's balance
            this.mockPersistentState.Verify(s => s.SetUInt256($"Balance:{this.destination}", destinationBalance + amount));

            this.mockContractLogger.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new StandardTokenEnhanced.TransferLog { From = this.owner, To = this.destination, Amount = amount }));
        }

        [Fact]
        public void TransferTo_Self()
        {
            UInt256 balance = 100;
            UInt256 amount = 27;

            Address subject = this.sender;

            this.mockContractState.Setup(m => m.Message)
                .Returns(new Message(this.contract, subject, 0));

            var standardToken = new StandardTokenEnhanced(this.mockContractState.Object, 100_000, this.name, this.symbol, this.decimals);

            // Setup the balance of the owner in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt256($"Balance:{subject}")).Returns(balance);

            // If a call is made to set the subject's balance to amount, make sure we update the state
            this.mockPersistentState.Setup(s => s.SetUInt256($"Balance:{subject}", balance - amount))
                .Callback(() =>
                    this.mockPersistentState.Setup(s => s.GetUInt256($"Balance:{subject}")).Returns(balance - amount));

            // Transfer all of the subject's tokens
            Assert.True(standardToken.TransferTo(subject, amount));

            // Verify we set the subject's balance to the difference between the amounts
            this.mockPersistentState.Verify(s => s.SetUInt256($"Balance:{subject}", balance - amount));

            // Verify we get the subject's balance
            this.mockPersistentState.Verify(s => s.GetUInt256($"Balance:{subject}"));

            // Verify we set the subject's balance back to the initial same amount
            this.mockPersistentState.Verify(s => s.SetUInt256($"Balance:{subject}", balance));

            this.mockContractLogger.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new StandardTokenEnhanced.TransferLog { From = subject, To = subject, Amount = amount }));
        }

        [Fact]
        public void TransferFrom_Self()
        {
            UInt256 balance = 100;
            UInt256 amount = 27;

            Address subject = this.sender;

            this.mockContractState.Setup(m => m.Message)
                .Returns(new Message(this.contract, subject, 0));

            var standardToken = new StandardTokenEnhanced(this.mockContractState.Object, 100_000, this.name, this.symbol, this.decimals);

            // Setup the balance of the owner in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt256($"Balance:{subject}")).Returns(balance);

            // Setup the allowance of the owner in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt256($"Allowance:{subject}:{subject}")).Returns(balance);

            // If a call is made to set the subject's balance to amount, make sure we update the state
            this.mockPersistentState.Setup(s => s.SetUInt256($"Balance:{subject}", balance - amount))
                .Callback(() =>
                    this.mockPersistentState.Setup(s => s.GetUInt256($"Balance:{subject}")).Returns(balance - amount));

            // If a call is made to set the subject's allowance to amount, make sure we update the state
            this.mockPersistentState.Setup(s => s.SetUInt256($"Allowance:{subject}:{subject}", balance - amount))
                .Callback(() =>
                    this.mockPersistentState.Setup(s => s.GetUInt256($"Allowance:{subject}:{subject}")).Returns(balance - amount));

            // Transfer all of the subject's tokens
            Assert.True(standardToken.TransferFrom(subject, subject, amount));

            // Verify we set the subject's balance to the difference between the amounts
            this.mockPersistentState.Verify(s => s.SetUInt256($"Balance:{subject}", balance - amount));

            // Verify we get the subject's balance
            this.mockPersistentState.Verify(s => s.GetUInt256($"Balance:{subject}"));

            // Verify we set the subject's balance back to the initial same amount
            this.mockPersistentState.Verify(s => s.SetUInt256($"Balance:{subject}", balance));

            // Verify we get the subject's allowance
            this.mockPersistentState.Verify(s => s.GetUInt256($"Allowance:{subject}:{subject}"));

            // Verify we set the subject's allowance to the difference between the amounts
            this.mockPersistentState.Verify(s => s.SetUInt256($"Allowance:{subject}:{subject}", balance - amount));

            this.mockContractLogger.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new StandardTokenEnhanced.TransferLog { From = subject, To = subject, Amount = amount }));
        }

        [Fact]
        public void Constructor_Sets_Name_And_Symbol_And_Decimals()
        {
            Address subject = this.sender;

            this.mockContractState.Setup(m => m.Message)
                .Returns(new Message(this.contract, subject, 0));

            var standardToken = new StandardTokenEnhanced(this.mockContractState.Object, 100_000, this.name, this.symbol, this.decimals);

            // Verify we set the name and the symbol
            this.mockPersistentState.Verify(s => s.SetString(nameof(standardToken.Name), this.name));
            this.mockPersistentState.Verify(s => s.SetString(nameof(standardToken.Symbol), this.symbol));
            this.mockPersistentState.Verify(s => s.SetBytes(nameof(standardToken.Decimals), new[] { this.decimals }));

            // Verify name property returns name.
            this.mockPersistentState.Setup(s => s.GetString(nameof(standardToken.Name))).Returns(this.name);
            Assert.Equal(this.name, standardToken.Name);

            // Verify symbol property returns symbol.
            this.mockPersistentState.Setup(s => s.GetString(nameof(standardToken.Symbol))).Returns(this.symbol);
            Assert.Equal(this.symbol, standardToken.Symbol);

            // Verify decimals property returns decimals.
            this.mockPersistentState.Setup(s => s.GetBytes(nameof(standardToken.Decimals))).Returns(new[] { this.decimals });
            Assert.Equal(this.decimals, standardToken.Decimals);
        }
#endregion
    }
}
