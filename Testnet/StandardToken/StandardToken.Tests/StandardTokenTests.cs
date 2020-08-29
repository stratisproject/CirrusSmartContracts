using System;
using Moq;
using Stratis.SmartContracts.Networks;
using Stratis.SmartContracts.CLR;
using Xunit;

namespace Stratis.SmartContracts.Samples.Tests
{
    public class StandardTokenTests
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
        private uint decimals;

        public StandardTokenTests()
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
        public void Constructor_Sets_TotalSupply()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));

            ulong totalSupply = 100_000;
            var standardToken = new StandardToken(this.mockContractState.Object, totalSupply, this.name, this.symbol, this.decimals);

            // Verify that PersistentState was called with the total supply
            this.mockPersistentState.Verify(s => s.SetUInt64(nameof(StandardToken.TotalSupply), totalSupply));
        }

        [Fact]
        public void Constructor_Assigns_TotalSupply_To_Owner()
        {
            ulong totalSupply = 100_000;
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));

            var standardToken = new StandardToken(this.mockContractState.Object, totalSupply, this.name, this.symbol, this.decimals);

            // Verify that PersistentState was called with the total supply
            this.mockPersistentState.Verify(s => s.SetUInt64($"Balance:{this.owner}", totalSupply));
        }

        [Fact]
        public void GetBalance_Returns_Correct_Balance()
        {
            ulong balance = 100;

            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));

            var standardToken = new StandardToken(this.mockContractState.Object, 100_000, this.name, this.symbol, this.decimals);

            // Setup the balance of the address in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt64($"Balance:{this.spender}")).Returns(balance);

            Assert.Equal(balance, standardToken.GetBalance(this.spender));
        }

        [Fact]
        public void GetDecimals_Returns_Correct_Amount()
        {
            uint decimalAmount = 4;

            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));

            var standardToken = new StandardToken(this.mockContractState.Object, 100_000, this.name, this.symbol, decimalAmount);

            // Setup the decimals amount in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt32("Decimals")).Returns(decimalAmount);

            Assert.Equal(decimalAmount, standardToken.GetDecimals());
        }

        [Fact]
        public void Approve_Sets_Approval_Correctly()
        {
            ulong approval = 1000;

            // Setup the Message.Sender address
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));

            var standardToken = new StandardToken(this.mockContractState.Object, 100_000, this.name, this.symbol, this.decimals);

            standardToken.Approve(this.spender, 0, approval);

            this.mockPersistentState.Verify(s => s.SetUInt64($"Allowance:{this.owner}:{this.spender}", approval));

            this.mockContractLogger.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new StandardToken.ApprovalLog { Owner = this.owner, Spender = this.spender, Amount = approval, OldAmount = 0 }));
        }

        [Fact]
        public void Approve_Sets_Approval_Correctly_When_NonZero()
        {
            ulong approval = 1000;
            ulong newApproval = 2000;

            // Setup the Message.Sender address
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));

            var standardToken = new StandardToken(this.mockContractState.Object, 100_000, this.name, this.symbol, this.decimals);

            // Set up an existing allowance
            this.mockPersistentState.Setup(s => s.GetUInt64($"Allowance:{this.owner}:{this.spender}")).Returns(approval);

            standardToken.Approve(this.spender, approval, newApproval);

            this.mockPersistentState.Verify(s => s.SetUInt64($"Allowance:{this.owner}:{this.spender}", newApproval));

            this.mockContractLogger.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new StandardToken.ApprovalLog { Owner = this.owner, Spender = this.spender, Amount = newApproval, OldAmount = approval }));
        }

        [Fact]
        public void Approve_Does_Not_Set_Approval_If_Different()
        {
            ulong approval = 1000;
            ulong newApproval = 2000;

            // Setup the Message.Sender address
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));

            var standardToken = new StandardToken(this.mockContractState.Object, 100_000, this.name, this.symbol, this.decimals);

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

            var standardToken = new StandardToken(this.mockContractState.Object, 100_000, this.name, this.symbol, this.decimals);

            standardToken.Allowance(this.owner, this.spender);

            this.mockPersistentState.Verify(s => s.GetUInt64($"Allowance:{this.owner}:{this.spender}"));
        }

        [Fact]
        public void TransferTo_0_Returns_True()
        {
            ulong amount = 0;

            // Setup the Message.Sender address
            this.mockContractState.Setup(m => m.Message)
                .Returns(new Message(this.contract, this.sender, 0));

            var standardToken = new StandardToken(this.mockContractState.Object, 100_000, this.name, this.symbol, this.decimals);

            Assert.True(standardToken.TransferTo(this.destination, amount));
            this.mockContractLogger.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new StandardToken.TransferLog { From = this.sender, To = this.destination, Amount = amount }));
        }

        [Fact]
        public void TransferTo_Full_Balance_Returns_True()
        {
            ulong balance = 10000;
            ulong amount = balance;
            ulong destinationBalance = 123;

            // Setup the Message.Sender address
            this.mockContractState.Setup(m => m.Message)
                .Returns(new Message(this.contract, this.sender, 0));

            var standardToken = new StandardToken(this.mockContractState.Object, 100_000, this.name, this.symbol, this.decimals);

            // Setup the balance of the sender's address in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt64($"Balance:{this.sender}")).Returns(balance);

            // Setup the balance of the recipient's address in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt64($"Balance:{this.destination}")).Returns(destinationBalance);

            Assert.True(standardToken.TransferTo(this.destination, amount));

            // Verify we queried the balance
            this.mockPersistentState.Verify(s => s.GetUInt64($"Balance:{this.sender}"));

            // Verify we set the sender's balance
            this.mockPersistentState.Verify(s => s.SetUInt64($"Balance:{this.sender}", balance - amount));

            // Verify we set the receiver's balance
            this.mockPersistentState.Verify(s => s.SetUInt64($"Balance:{this.destination}", destinationBalance + amount));
        }

        [Fact]
        public void TransferTo_Greater_Than_Balance_Returns_False()
        {
            ulong balance = 0;
            ulong amount = balance + 1;

            // Setup the Message.Sender address
            this.mockContractState.Setup(m => m.Message)
                .Returns(new Message(this.contract, this.sender, 0));

            var standardToken = new StandardToken(this.mockContractState.Object, 100_000, this.name, this.symbol, this.decimals);

            // Setup the balance of the address in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt64($"Balance:{this.sender}")).Returns(balance);

            Assert.False(standardToken.TransferTo(this.destination, amount));

            // Verify we queried the balance
            this.mockPersistentState.Verify(s => s.GetUInt64($"Balance:{this.sender}"));
        }

        [Fact]
        public void TransferTo_Destination_With_Balance_Greater_Than_uint_MaxValue_Returns_False()
        {
            ulong destinationBalance = ulong.MaxValue;
            ulong senderBalance = 100;
            ulong amount = senderBalance - 1; // Transfer less than the balance

            // Setup the Message.Sender address
            this.mockContractState.Setup(m => m.Message)
                .Returns(new Message(this.contract, this.sender, 0));

            var standardToken = new StandardToken(this.mockContractState.Object, 100_000, this.name, this.symbol, this.decimals);

            // Setup the balance of the address in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt64($"Balance:{this.sender}")).Returns(senderBalance);

            // Setup the destination's balance to be ulong.MaxValue
            this.mockPersistentState.Setup(s => s.GetUInt64($"Balance:{this.destination}")).Returns(destinationBalance);

            Assert.ThrowsAny<OverflowException>(() => standardToken.TransferTo(this.destination, amount));

            // Verify we queried the sender's balance
            this.mockPersistentState.Verify(s => s.GetUInt64($"Balance:{this.sender}"));

            // Verify we queried the destination's balance
            this.mockPersistentState.Verify(s => s.GetUInt64($"Balance:{this.destination}"));
        }

        [Fact]
        public void TransferTo_Destination_Success_Returns_True()
        {
            ulong destinationBalance = 400_000;
            ulong senderBalance = 100;
            ulong amount = senderBalance - 1; // Transfer less than the balance

            // Setup the Message.Sender address
            this.mockContractState.Setup(m => m.Message)
                .Returns(new Message(this.contract, this.sender, 0));

            var standardToken = new StandardToken(this.mockContractState.Object, 100_000, this.name, this.symbol, this.decimals);

            int callOrder = 1;

            // Setup the balance of the address in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt64($"Balance:{this.sender}")).Returns(senderBalance)
                .Callback(() => Assert.Equal(1, callOrder++));

            // Setup the destination's balance
            this.mockPersistentState.Setup(s => s.GetUInt64($"Balance:{this.destination}")).Returns(destinationBalance)
                .Callback(() => Assert.Equal(2, callOrder++));

            // Setup the sender's balance
            this.mockPersistentState.Setup(s => s.SetUInt64($"Balance:{this.sender}", It.IsAny<uint>()))
                .Callback(() => Assert.Equal(3, callOrder++));

            // Setup the destination's balance. Important that this happens AFTER setting the sender's balance
            this.mockPersistentState.Setup(s => s.SetUInt64($"Balance:{this.destination}", It.IsAny<uint>()))
                .Callback(() => Assert.Equal(4, callOrder++));

            Assert.True(standardToken.TransferTo(this.destination, amount));

            // Verify we queried the sender's balance
            this.mockPersistentState.Verify(s => s.GetUInt64($"Balance:{this.sender}"));

            // Verify we queried the destination's balance
            this.mockPersistentState.Verify(s => s.GetUInt64($"Balance:{this.destination}"));

            // Verify we set the sender's balance
            this.mockPersistentState.Verify(s => s.SetUInt64($"Balance:{this.sender}", senderBalance - amount));

            // Verify we set the receiver's balance
            this.mockPersistentState.Verify(s => s.SetUInt64($"Balance:{this.destination}", destinationBalance + amount));

            this.mockContractLogger.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new StandardToken.TransferLog { From = this.sender, To = this.destination, Amount = amount }));
        }

        [Fact]
        public void TransferFrom_0_Returns_True()
        {
            ulong amount = 0;

            // Setup the Message.Sender address
            this.mockContractState.Setup(m => m.Message)
                .Returns(new Message(this.contract, this.sender, 0));

            var standardToken = new StandardToken(this.mockContractState.Object, 100_000, this.name, this.symbol, this.decimals);

            Assert.True(standardToken.TransferFrom(this.owner, this.destination, amount));

            this.mockContractLogger.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new StandardToken.TransferLog { From = this.owner, To = this.destination, Amount = amount }));
        }

        [Fact]
        public void TransferFrom_Full_Balance_Returns_True()
        {
            ulong allowance = 1000;
            ulong amount = allowance;
            ulong balance = amount; // Balance should be the same as the amount we are trying to send

            // Setup the Message.Sender address
            this.mockContractState.Setup(m => m.Message)
                .Returns(new Message(this.contract, this.sender, 0));

            var standardToken = new StandardToken(this.mockContractState.Object, 100_000, this.name, this.symbol, this.decimals);

            // Setup the balance of the owner in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt64($"Balance:{this.owner}")).Returns(balance);

            // Setup the balance of the address in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt64($"Allowance:{this.owner}:{this.sender}")).Returns(allowance);

            Assert.True(standardToken.TransferFrom(this.owner, this.destination, amount));

            // Verify we queried the sender's allowance
            this.mockPersistentState.Verify(s => s.GetUInt64($"Allowance:{this.owner}:{this.sender}"));

            // Verify we queried the owner's balance
            this.mockPersistentState.Verify(s => s.GetUInt64($"Balance:{this.owner}"));

            // Verify we set the sender's allowance
            this.mockPersistentState.Verify(s => s.SetUInt64($"Allowance:{this.owner}:{this.sender}", allowance - amount));

            // Verify we set the owner's balance
            this.mockPersistentState.Verify(s => s.SetUInt64($"Balance:{this.owner}", balance - amount));

            // Verify we set the destination's balance
            this.mockPersistentState.Verify(s => s.SetUInt64($"Balance:{this.destination}", amount));
        }

        [Fact]
        public void TransferFrom_Greater_Than_Senders_Allowance_Returns_False()
        {
            ulong allowance = 0;
            ulong amount = allowance + 1;
            ulong balance = amount + 1; // Balance should be more than amount we are trying to send

            // Setup the Message.Sender address
            this.mockContractState.Setup(m => m.Message)
                .Returns(new Message(this.contract, this.sender, 0));

            var standardToken = new StandardToken(this.mockContractState.Object, 100_000, this.name, this.symbol, this.decimals);

            // Setup the balance of the owner in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt64($"Balance:{this.owner}")).Returns(balance);

            // Setup the balance of the address in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt64($"Allowance:{this.owner}:{this.sender}")).Returns(allowance);

            Assert.False(standardToken.TransferFrom(this.owner, this.destination, amount));

            // Verify we queried the sender's allowance
            this.mockPersistentState.Verify(s => s.GetUInt64($"Allowance:{this.owner}:{this.sender}"));

            // Verify we queried the owner's balance
            this.mockPersistentState.Verify(s => s.GetUInt64($"Balance:{this.owner}"));
        }

        [Fact]
        public void TransferFrom_Greater_Than_Owners_Balance_Returns_False()
        {
            ulong balance = 0; // Balance should be less than amount we are trying to send
            ulong amount = balance + 1;
            ulong allowance = amount + 1; // Allowance should be more than amount we are trying to send

            // Setup the Message.Sender address
            this.mockContractState.Setup(m => m.Message)
                .Returns(new Message(this.contract, this.sender, 0));

            var standardToken = new StandardToken(this.mockContractState.Object, 100_000, this.name, this.symbol, this.decimals);

            // Setup the balance of the owner in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt64($"Balance:{this.owner}")).Returns(balance);

            // Setup the allowance of the sender in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt64($"Allowance:{this.owner}:{this.sender}")).Returns(allowance);

            Assert.False(standardToken.TransferFrom(this.owner, this.destination, amount));

            // Verify we queried the sender's allowance
            this.mockPersistentState.Verify(s => s.GetUInt64($"Allowance:{this.owner}:{this.sender}"));

            // Verify we queried the owner's balance
            this.mockPersistentState.Verify(s => s.GetUInt64($"Balance:{this.owner}"));
        }

        [Fact]
        public void TransferFrom_To_Destination_With_Balance_Greater_Than_uint_MaxValue_Returns_False()
        {
            ulong destinationBalance = ulong.MaxValue; // Destination balance should be ulong.MaxValue
            ulong amount = 1;
            ulong allowance = amount + 1; // Allowance should be more than amount we are trying to send
            ulong ownerBalance = allowance + 1; // Owner balance should be more than allowance

            // Setup the Message.Sender address
            this.mockContractState.Setup(m => m.Message)
                .Returns(new Message(this.contract, this.sender, 0));

            var standardToken = new StandardToken(this.mockContractState.Object, 100_000, this.name, this.symbol, this.decimals);

            // Setup the balance of the owner in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt64($"Balance:{this.owner}")).Returns(ownerBalance);

            // Setup the balance of the destination in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt64($"Balance:{this.destination}")).Returns(destinationBalance);

            // Setup the allowance of the sender in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt64($"Allowance:{this.owner}:{this.sender}")).Returns(allowance);

            Assert.ThrowsAny<OverflowException>(() => standardToken.TransferFrom(this.owner, this.destination, amount));

            // Verify we queried the sender's allowance
            this.mockPersistentState.Verify(s => s.GetUInt64($"Allowance:{this.owner}:{this.sender}"));

            // Verify we queried the owner's balance
            this.mockPersistentState.Verify(s => s.GetUInt64($"Balance:{this.owner}"));
        }

        [Fact]
        public void TransferFrom_To_Destination_Success_Returns_True()
        {
            ulong destinationBalance = 100;
            ulong amount = 1;
            ulong allowance = amount + 1; // Allowance should be more than amount we are trying to send
            ulong ownerBalance = allowance + 1; // Owner balance should be more than allowance

            // Setup the Message.Sender address
            this.mockContractState.Setup(m => m.Message)
                .Returns(new Message(this.contract, this.sender, 0));

            var standardToken = new StandardToken(this.mockContractState.Object, 100_000, this.name, this.symbol, this.decimals);

            int callOrder = 1;

            // Setup the allowance of the sender in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt64($"Allowance:{this.owner}:{this.sender}")).Returns(allowance)
                .Callback(() => Assert.Equal(1, callOrder++));

            // Setup the balance of the owner in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt64($"Balance:{this.owner}")).Returns(ownerBalance)
                .Callback(() => Assert.Equal(2, callOrder++));

            // Setup the balance of the destination in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt64($"Balance:{this.destination}")).Returns(destinationBalance)
                .Callback(() => Assert.Equal(3, callOrder++));

            // Set the sender's new allowance
            this.mockPersistentState.Setup(s => s.SetUInt64($"Allowance:{this.owner}:{this.sender}", It.IsAny<uint>()))
                .Callback(() => Assert.Equal(4, callOrder++));

            // Set the owner's new balance
            this.mockPersistentState.Setup(s => s.SetUInt64($"Balance:{this.owner}", It.IsAny<uint>()))
                .Callback(() => Assert.Equal(5, callOrder++));

            // Setup the destination's balance. Important that this happens AFTER setting the owner's balance
            this.mockPersistentState.Setup(s => s.SetUInt64($"Balance:{this.destination}", It.IsAny<uint>()))
                .Callback(() => Assert.Equal(6, callOrder++));

            Assert.True(standardToken.TransferFrom(this.owner, this.destination, amount));

            // Verify we queried the sender's allowance
            this.mockPersistentState.Verify(s => s.GetUInt64($"Allowance:{this.owner}:{this.sender}"));

            // Verify we queried the owner's balance
            this.mockPersistentState.Verify(s => s.GetUInt64($"Balance:{this.owner}"));

            // Verify we queried the destination's balance
            this.mockPersistentState.Verify(s => s.GetUInt64($"Balance:{this.destination}"));

            // Verify we set the sender's allowance
            this.mockPersistentState.Verify(s => s.SetUInt64($"Allowance:{this.owner}:{this.sender}", allowance - amount));

            // Verify we set the owner's balance
            this.mockPersistentState.Verify(s => s.SetUInt64($"Balance:{this.owner}", ownerBalance - amount));

            // Verify we set the receiver's balance
            this.mockPersistentState.Verify(s => s.SetUInt64($"Balance:{this.destination}", destinationBalance + amount));

            this.mockContractLogger.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new StandardToken.TransferLog { From = this.owner, To = this.destination, Amount = amount }));
        }

        [Fact]
        public void TransferTo_Self()
        {
            ulong balance = 100;
            ulong amount = 27;

            Address subject = this.sender;

            this.mockContractState.Setup(m => m.Message)
                .Returns(new Message(this.contract, subject, 0));

            var standardToken = new StandardToken(this.mockContractState.Object, 100_000, this.name, this.symbol, this.decimals);

            // Setup the balance of the owner in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt64($"Balance:{subject}")).Returns(balance);

            // If a call is made to set the subject's balance to amount, make sure we update the state
            this.mockPersistentState.Setup(s => s.SetUInt64($"Balance:{subject}", balance - amount))
                .Callback(() =>
                    this.mockPersistentState.Setup(s => s.GetUInt64($"Balance:{subject}")).Returns(balance - amount));

            // Transfer all of the subject's tokens
            Assert.True(standardToken.TransferTo(subject, amount));

            // Verify we set the subject's balance to the difference between the amounts
            this.mockPersistentState.Verify(s => s.SetUInt64($"Balance:{subject}", balance - amount));

            // Verify we get the subject's balance
            this.mockPersistentState.Verify(s => s.GetUInt64($"Balance:{subject}"));

            // Verify we set the subject's balance back to the initial same amount
            this.mockPersistentState.Verify(s => s.SetUInt64($"Balance:{subject}", balance));

            this.mockContractLogger.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new StandardToken.TransferLog { From = subject, To = subject, Amount = amount }));
        }

        [Fact]
        public void TransferFrom_Self()
        {
            ulong balance = 100;
            ulong amount = 27;

            Address subject = this.sender;

            this.mockContractState.Setup(m => m.Message)
                .Returns(new Message(this.contract, subject, 0));

            var standardToken = new StandardToken(this.mockContractState.Object, 100_000, this.name, this.symbol, this.decimals);

            // Setup the balance of the owner in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt64($"Balance:{subject}")).Returns(balance);

            // Setup the allowance of the owner in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt64($"Allowance:{subject}:{subject}")).Returns(balance);

            // If a call is made to set the subject's balance to amount, make sure we update the state
            this.mockPersistentState.Setup(s => s.SetUInt64($"Balance:{subject}", balance - amount))
                .Callback(() =>
                    this.mockPersistentState.Setup(s => s.GetUInt64($"Balance:{subject}")).Returns(balance - amount));

            // If a call is made to set the subject's allowance to amount, make sure we update the state
            this.mockPersistentState.Setup(s => s.SetUInt64($"Allowance:{subject}:{subject}", balance - amount))
                .Callback(() =>
                    this.mockPersistentState.Setup(s => s.GetUInt64($"Allowance:{subject}:{subject}")).Returns(balance - amount));

            // Transfer all of the subject's tokens
            Assert.True(standardToken.TransferFrom(subject, subject, amount));

            // Verify we set the subject's balance to the difference between the amounts
            this.mockPersistentState.Verify(s => s.SetUInt64($"Balance:{subject}", balance - amount));

            // Verify we get the subject's balance
            this.mockPersistentState.Verify(s => s.GetUInt64($"Balance:{subject}"));

            // Verify we set the subject's balance back to the initial same amount
            this.mockPersistentState.Verify(s => s.SetUInt64($"Balance:{subject}", balance));

            // Verify we get the subject's allowance
            this.mockPersistentState.Verify(s => s.GetUInt64($"Allowance:{subject}:{subject}"));

            // Verify we set the subject's allowance to the difference between the amounts
            this.mockPersistentState.Verify(s => s.SetUInt64($"Allowance:{subject}:{subject}", balance - amount));

            this.mockContractLogger.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new StandardToken.TransferLog { From = subject, To = subject, Amount = amount }));
        }

        [Fact]
        public void Constructor_Sets_Name_Symbol_And_Decimals()
        {
            Address subject = this.sender;

            this.mockContractState.Setup(m => m.Message)
                .Returns(new Message(this.contract, subject, 0));

            var standardToken = new StandardToken(this.mockContractState.Object, 100_000, this.name, this.symbol, this.decimals);

            // Verify we set the name and the symbol
            this.mockPersistentState.Verify(s => s.SetString("Name", this.name));
            this.mockPersistentState.Verify(s => s.SetString("Symbol", this.symbol));
            this.mockPersistentState.Verify(s => s.SetUInt32("Decimals", this.decimals));
        }
    }
}
