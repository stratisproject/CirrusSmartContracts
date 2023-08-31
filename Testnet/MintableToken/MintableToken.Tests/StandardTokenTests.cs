﻿using System;
using Moq;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR;
using Xunit;

namespace MintableTokenTests
{
    /// <summary>
    /// These tests are reproduced from the original StandardToken project to ensure that no breaking changes have been made to the contract interface.
    /// </summary>
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
        private byte decimals;

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

            UInt256 totalSupply = 100_000;
            var standardToken = new MintableToken(this.mockContractState.Object, totalSupply, this.name, this.symbol, "CIRRUS", "Address");

            // Verify that PersistentState was called with the total supply
            this.mockPersistentState.Verify(s => s.SetUInt256(nameof(MintableToken.TotalSupply), totalSupply));
        }

        [Fact]
        public void Constructor_Assigns_TotalSupply_To_Owner()
        {
            UInt256 totalSupply = 100_000;
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));

            var standardToken = new MintableToken(this.mockContractState.Object, totalSupply, this.name, this.symbol, "CIRRUS", "Address");

            // Verify that PersistentState was called with the total supply
            this.mockPersistentState.Verify(s => s.SetUInt256($"Balance:{this.owner}", totalSupply));
        }

        [Fact]
        public void GetBalance_Returns_Correct_Balance()
        {
            UInt256 balance = 100;

            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));

            var standardToken = new MintableToken(this.mockContractState.Object, 100_000, this.name, this.symbol, "CIRRUS", "Address");

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

            var standardToken = new MintableToken(this.mockContractState.Object, 100_000, this.name, this.symbol, "CIRRUS", "Address");

            standardToken.Approve(this.spender, 0, approval);

            this.mockPersistentState.Verify(s => s.SetUInt256($"Allowance:{this.owner}:{this.spender}", approval));

            this.mockContractLogger.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new MintableToken.ApprovalLog { Owner = this.owner, Spender = this.spender, Amount = approval, OldAmount = 0 }));
        }

        [Fact]
        public void Approve_Sets_Approval_Correctly_When_NonZero()
        {
            UInt256 approval = 1000;
            UInt256 newApproval = 2000;

            // Setup the Message.Sender address
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));

            var standardToken = new MintableToken(this.mockContractState.Object, 100_000, this.name, this.symbol, "CIRRUS", "Address");

            // Set up an existing allowance
            this.mockPersistentState.Setup(s => s.GetUInt256($"Allowance:{this.owner}:{this.spender}")).Returns(approval);

            standardToken.Approve(this.spender, approval, newApproval);

            this.mockPersistentState.Verify(s => s.SetUInt256($"Allowance:{this.owner}:{this.spender}", newApproval));

            this.mockContractLogger.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new MintableToken.ApprovalLog { Owner = this.owner, Spender = this.spender, Amount = newApproval, OldAmount = approval }));
        }

        [Fact]
        public void Approve_Does_Not_Set_Approval_If_Different()
        {
            ulong approval = 1000;
            ulong newApproval = 2000;

            // Setup the Message.Sender address
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));

            var standardToken = new MintableToken(this.mockContractState.Object, 100_000, this.name, this.symbol, "CIRRUS", "Address");

            // Set up an existing allowance
            this.mockPersistentState.Setup(s => s.GetUInt64($"Allowance:{this.owner}:{this.spender}")).Returns(approval);

            // Attempt to set the new approval for a different earlier approval
            var differentApproval = approval + 1;

            Assert.False((bool)standardToken.Approve(this.spender, differentApproval, newApproval));

            this.mockPersistentState.Verify(s => s.SetUInt64($"Allowance:{this.owner}:{this.spender}", It.IsAny<ulong>()), Times.Never);
        }

        [Fact]
        public void Allowance_Returns_Correct_Allowance()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.sender, 0));

            var standardToken = new MintableToken(this.mockContractState.Object, 100_000, this.name, this.symbol, "CIRRUS", "Address");

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

            var standardToken = new MintableToken(this.mockContractState.Object, 100_000, this.name, this.symbol, "CIRRUS", "Address");

            Assert.True((bool)standardToken.TransferTo(this.destination, amount));
            this.mockContractLogger.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new MintableToken.TransferLog { From = this.sender, To = this.destination, Amount = amount }));
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

            var standardToken = new MintableToken(this.mockContractState.Object, 100_000, this.name, this.symbol, "CIRRUS", "Address");

            // Setup the balance of the sender's address in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt256($"Balance:{this.sender}")).Returns(balance);

            // Setup the balance of the recipient's address in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt256($"Balance:{this.destination}")).Returns(destinationBalance);

            Assert.True((bool)standardToken.TransferTo(this.destination, amount));

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

            var standardToken = new MintableToken(this.mockContractState.Object, 100_000, this.name, this.symbol, "CIRRUS", "Address");

            // Setup the balance of the address in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt256($"Balance:{this.sender}")).Returns(balance);

            Assert.False((bool)standardToken.TransferTo(this.destination, amount));

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

            var standardToken = new MintableToken(this.mockContractState.Object, 100_000, this.name, this.symbol, "CIRRUS", "Address");

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

            var standardToken = new MintableToken(this.mockContractState.Object, 100_000, this.name, this.symbol, "CIRRUS", "Address");

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

            Assert.True((bool)standardToken.TransferTo(this.destination, amount));

            // Verify we queried the sender's balance
            this.mockPersistentState.Verify(s => s.GetUInt256($"Balance:{this.sender}"));

            // Verify we queried the destination's balance
            this.mockPersistentState.Verify(s => s.GetUInt256($"Balance:{this.destination}"));

            // Verify we set the sender's balance
            this.mockPersistentState.Verify(s => s.SetUInt256($"Balance:{this.sender}", senderBalance - amount));

            // Verify we set the receiver's balance
            this.mockPersistentState.Verify(s => s.SetUInt256($"Balance:{this.destination}", destinationBalance + amount));

            this.mockContractLogger.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new MintableToken.TransferLog { From = this.sender, To = this.destination, Amount = amount }));
        }

        [Fact]
        public void TransferFrom_0_Returns_True()
        {
            ulong amount = 0;

            // Setup the Message.Sender address
            this.mockContractState.Setup(m => m.Message)
                .Returns(new Message(this.contract, this.sender, 0));

            var standardToken = new MintableToken(this.mockContractState.Object, 100_000, this.name, this.symbol, "CIRRUS", "Address");

            Assert.True((bool)standardToken.TransferFrom(this.owner, this.destination, amount));

            this.mockContractLogger.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new MintableToken.TransferLog { From = this.owner, To = this.destination, Amount = amount }));
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

            var standardToken = new MintableToken(this.mockContractState.Object, 100_000, this.name, this.symbol, "CIRRUS", "Address");

            // Setup the balance of the owner in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt256($"Balance:{this.owner}")).Returns(balance);

            // Setup the balance of the address in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt256($"Allowance:{this.owner}:{this.sender}")).Returns(allowance);

            Assert.True((bool)standardToken.TransferFrom(this.owner, this.destination, amount));

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

            var standardToken = new MintableToken(this.mockContractState.Object, 100_000, this.name, this.symbol, "CIRRUS", "Address");

            // Setup the balance of the owner in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt256($"Balance:{this.owner}")).Returns(balance);

            // Setup the balance of the address in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt256($"Allowance:{this.owner}:{this.sender}")).Returns(allowance);

            Assert.False((bool)standardToken.TransferFrom(this.owner, this.destination, amount));

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

            var standardToken = new MintableToken(this.mockContractState.Object, 100_000, this.name, this.symbol, "CIRRUS", "Address");

            // Setup the balance of the owner in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt256($"Balance:{this.owner}")).Returns(balance);

            // Setup the allowance of the sender in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt256($"Allowance:{this.owner}:{this.sender}")).Returns(allowance);

            Assert.False((bool)standardToken.TransferFrom(this.owner, this.destination, amount));

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

            var standardToken = new MintableToken(this.mockContractState.Object, 100_000, this.name, this.symbol, "CIRRUS", "Address");

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

            var standardToken = new MintableToken(this.mockContractState.Object, 100_000, this.name, this.symbol, "CIRRUS", "Address");

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

            Assert.True((bool)standardToken.TransferFrom(this.owner, this.destination, amount));

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

            this.mockContractLogger.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new MintableToken.TransferLog { From = this.owner, To = this.destination, Amount = amount }));
        }

        [Fact]
        public void TransferTo_Self()
        {
            UInt256 balance = 100;
            UInt256 amount = 27;

            Address subject = this.sender;

            this.mockContractState.Setup(m => m.Message)
                .Returns(new Message(this.contract, subject, 0));

            var standardToken = new MintableToken(this.mockContractState.Object, 100_000, this.name, this.symbol, "CIRRUS", "Address");

            // Setup the balance of the owner in persistent state
            this.mockPersistentState.Setup(s => s.GetUInt256($"Balance:{subject}")).Returns(balance);

            // If a call is made to set the subject's balance to amount, make sure we update the state
            this.mockPersistentState.Setup(s => s.SetUInt256($"Balance:{subject}", balance - amount))
                .Callback(() =>
                    this.mockPersistentState.Setup(s => s.GetUInt256($"Balance:{subject}")).Returns(balance - amount));

            // Transfer all of the subject's tokens
            Assert.True((bool)standardToken.TransferTo(subject, amount));

            // Verify we set the subject's balance to the difference between the amounts
            this.mockPersistentState.Verify(s => s.SetUInt256($"Balance:{subject}", balance - amount));

            // Verify we get the subject's balance
            this.mockPersistentState.Verify(s => s.GetUInt256($"Balance:{subject}"));

            // Verify we set the subject's balance back to the initial same amount
            this.mockPersistentState.Verify(s => s.SetUInt256($"Balance:{subject}", balance));

            this.mockContractLogger.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new MintableToken.TransferLog { From = subject, To = subject, Amount = amount }));
        }

        [Fact]
        public void TransferFrom_Self()
        {
            UInt256 balance = 100;
            UInt256 amount = 27;

            Address subject = this.sender;

            this.mockContractState.Setup(m => m.Message)
                .Returns(new Message(this.contract, subject, 0));

            var standardToken = new MintableToken(this.mockContractState.Object, 100_000, this.name, this.symbol, "CIRRUS", "Address");

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
            Assert.True((bool)standardToken.TransferFrom(subject, subject, amount));

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

            this.mockContractLogger.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new MintableToken.TransferLog { From = subject, To = subject, Amount = amount }));
        }

        [Fact]
        public void Constructor_Sets_Name_And_Symbol_And_Decimals()
        {
            Address subject = this.sender;

            this.mockContractState.Setup(m => m.Message)
                .Returns(new Message(this.contract, subject, 0));

            var standardToken = new MintableToken(this.mockContractState.Object, 100_000, this.name, this.symbol, "CIRRUS", "Address");

            // Verify we set the name and the symbol
            this.mockPersistentState.Verify(s => s.SetString(nameof(standardToken.Name), this.name));
            this.mockPersistentState.Verify(s => s.SetString(nameof(standardToken.Symbol), this.symbol));
            this.mockPersistentState.Verify(s => s.SetBytes(nameof(standardToken.Decimals), new[] { this.decimals }));

            // Verify name property returns name.
            this.mockPersistentState.Setup<string>(s => s.GetString(nameof(standardToken.Name))).Returns(this.name);
            Assert.Equal(this.name, (string)standardToken.Name);

            // Verify symbol property returns symbol.
            this.mockPersistentState.Setup<string>(s => s.GetString(nameof(standardToken.Symbol))).Returns(this.symbol);
            Assert.Equal(this.symbol, (string)standardToken.Symbol);

            // Verify decimals property returns decimals.
            this.mockPersistentState.Setup<byte[]>(s => s.GetBytes(nameof(standardToken.Decimals))).Returns(new[] { this.decimals });
            Assert.Equal(this.decimals, standardToken.Decimals);
        }
    }
}
