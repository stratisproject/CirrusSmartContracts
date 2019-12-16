using Moq;
using Stratis.SmartContracts;
using System;
using Xunit;

namespace WheelGame.Tests
{
  
  public class WheelGameTests
  {
    private class TransferResult : ITransferResult
    {
      public TransferResult(bool success, object returnValue = null)
      {
        ReturnValue = returnValue;
        Success = success;
      }

      public object ReturnValue { get; set; }
      public bool Success { get; set; }
    }

    private readonly Mock<ISmartContractState> mockContractState;
    private readonly Mock<IPersistentState> mockPersistentState;
    private readonly Mock<IContractLogger> mockContractLogger;
    private readonly Mock<IInternalTransactionExecutor> mockInternalExecutor;
    private readonly Address contract = new Address(1, 1, 1, 1, 1);
    private readonly Address owner = new Address(2, 2, 2, 2, 2);
    private readonly ulong bid = 10000000;
    private readonly byte blockDelay = 7;
    private readonly ulong startBlockNumber = 100;
    private Address[] Participants =
    {
      new Address(0, 0, 0, 0, 1),
      new Address(0, 0, 0, 0, 2),
      new Address(0, 0, 0, 0, 3)
    };

    public WheelGameTests()
    {
      mockContractLogger = new Mock<IContractLogger>();
      mockPersistentState = new Mock<IPersistentState>();
      mockContractState = new Mock<ISmartContractState>();
      mockInternalExecutor = new Mock<IInternalTransactionExecutor>();
      mockContractState.Setup(x => x.InternalTransactionExecutor).Returns(mockInternalExecutor.Object);
      this.mockContractState.Setup(s => s.PersistentState).Returns(this.mockPersistentState.Object);
      this.mockContractState.Setup(s => s.ContractLogger).Returns(this.mockContractLogger.Object);
      //this.mockContractState.Setup(s => s.InternalTransactionExecutor);
    }

    private Wheel NewWheel(ulong bid, byte timeoutBlock, ulong currentBlock)
    {
      this.mockContractState.Setup(b => b.Block.Number).Returns(currentBlock);
      var res = new Wheel(mockContractState.Object, bid, timeoutBlock);

      this.mockPersistentState.Setup(p => p.GetUInt64(nameof(Wheel.Bid))).Returns(bid);
      this.mockPersistentState.Setup(p => p.GetChar(nameof(Wheel.BlockDelay))).Returns((char)blockDelay);
      return res;
    }

    [Fact]
    public void Constructor_Parameters_Properly()
    {
      this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));

      ulong currentBlock = 100;

      var game = NewWheel(bid, blockDelay, currentBlock);

      // Verify that PersistentState was called with the total supply
      this.mockPersistentState.Verify(s => s.SetChar(nameof(Wheel.BlockDelay), (char)blockDelay));
      this.mockPersistentState.Verify(s => s.SetUInt64(nameof(Wheel.Bid), bid));
      this.mockPersistentState.Verify(s => s.SetBool(nameof(Wheel.IsGameStarted), false));
    }

    [Fact]
    public void Can_Make_Bid_Game_Is_Not_Started()
    {

      this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, Participants[0], bid));

      var game = NewWheel(bid, blockDelay, startBlockNumber);

      game.StakeBid();

      this.mockPersistentState.Verify(s => s.SetAddress(nameof(Wheel.LastBidOwner), Participants[0]));
      this.mockPersistentState.Verify(s => s.SetUInt32(nameof(Wheel.RoundCounter), 1));
      this.mockPersistentState.Verify(s => s.SetUInt64(nameof(Wheel.Staked), bid));
      this.mockPersistentState.Verify(s => s.SetBool(nameof(Wheel.IsGameStarted), true));
      this.mockPersistentState.Verify(s => s.SetUInt64(nameof(Wheel.TimeoutBlock), startBlockNumber + blockDelay));
      this.mockContractLogger.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new Wheel.NewBidLog { BlockNumber = startBlockNumber, Owner = Participants[0] }));
    }

    [Fact]
    public void Can_Make_Bid_Game_Is_Started()
    {
      var game = NewWheel(bid, blockDelay, startBlockNumber);

      //setup ppersistant state, as there was a participant before
      this.mockPersistentState.Setup(s => s.GetAddress(nameof(Wheel.LastBidOwner))).Returns(Participants[0]);
      this.mockPersistentState.Setup(s => s.GetUInt32(nameof(Wheel.RoundCounter))).Returns(1);
      this.mockPersistentState.Setup(s => s.GetUInt64(nameof(Wheel.Staked))).Returns(bid);
      this.mockPersistentState.Setup(s => s.GetBool(nameof(Wheel.IsGameStarted))).Returns(true);
      this.mockPersistentState.Setup(s => s.GetUInt64(nameof(Wheel.TimeoutBlock))).Returns(startBlockNumber + blockDelay);

      this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, Participants[1], bid));
      game.StakeBid();

      this.mockPersistentState.Verify(s => s.SetAddress(nameof(Wheel.LastBidOwner), Participants[1]));
      this.mockPersistentState.Verify(s => s.SetUInt32(nameof(Wheel.RoundCounter), 2));
      this.mockPersistentState.Verify(s => s.SetUInt64(nameof(Wheel.Staked), bid << 1));
      this.mockContractLogger.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new Wheel.NewBidLog { BlockNumber = startBlockNumber, Owner = Participants[1] }));
    }

    [Fact]
    public void Make_Bid_Game_Timeout()
    {
      var game = NewWheel(bid, blockDelay, startBlockNumber);

      //previous participants who will become a winner
      this.mockPersistentState.Setup(s => s.GetAddress(nameof(Wheel.LastBidOwner))).Returns(Participants[0]);
      this.mockPersistentState.Setup(s => s.GetUInt32(nameof(Wheel.RoundCounter))).Returns(5);
      this.mockPersistentState.Setup(s => s.GetUInt64(nameof(Wheel.Staked))).Returns(bid << 2);
      this.mockPersistentState.Setup(s => s.GetBool(nameof(Wheel.IsGameStarted))).Returns(true);
      this.mockPersistentState.Setup(s => s.GetUInt64(nameof(Wheel.TimeoutBlock))).Returns(startBlockNumber + blockDelay);
      //isTimeout will return true
      this.mockContractState.Setup(s => s.Block.Number).Returns(startBlockNumber + blockDelay);

      this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, Participants[1], bid));

      //it should finish the previous game, set the winner and start a new one
      game.StakeBid();

      this.mockPersistentState.Verify(s => s.SetAddress(nameof(Wheel.LastBidOwner), Participants[1]));
      this.mockPersistentState.Verify(s => s.SetUInt32(nameof(Wheel.RoundCounter), 1));
      this.mockPersistentState.Verify(s => s.SetUInt64(nameof(Wheel.Staked), bid));
      this.mockPersistentState.Verify(s => s.SetBool(nameof(Wheel.IsGameStarted), true));
      this.mockPersistentState.Verify(s => s.SetUInt64(nameof(Wheel.TimeoutBlock), startBlockNumber + blockDelay + blockDelay));
      this.mockContractLogger.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new Wheel.NewBidLog { BlockNumber = startBlockNumber + blockDelay, Owner = Participants[1] }));
      this.mockPersistentState.Verify(s => s.SetUInt64($"balance:{Participants[0]}", bid << 2));
      this.mockContractLogger.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new Wheel.WinnerLog { Winner = Participants[0], Amount = bid << 2 }));
    }

    [Fact]
    public void Withdraw_Game_Timeout()
    {
      var game = NewWheel(bid, blockDelay, startBlockNumber);

      //previous participants who will become a winner
      this.mockPersistentState.Setup(s => s.GetAddress(nameof(Wheel.LastBidOwner))).Returns(Participants[0]);
      this.mockPersistentState.Setup(s => s.GetUInt32(nameof(Wheel.RoundCounter))).Returns(5);
      this.mockPersistentState.Setup(s => s.GetUInt64(nameof(Wheel.Staked))).Returns(bid << 2);
      this.mockPersistentState.Setup(s => s.GetBool(nameof(Wheel.IsGameStarted))).Returns(true);
      this.mockPersistentState.Setup(s => s.GetUInt64(nameof(Wheel.TimeoutBlock))).Returns(startBlockNumber + blockDelay);
      //isTimeout will return true
      this.mockContractState.Setup(s => s.Block.Number).Returns(startBlockNumber + blockDelay);

      //try to withdraw by different account
      //this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, Participants[1], 0));
      //Assert.Throws(new Exception(), () => game.Withdraw());
      // Mock contract call
      mockInternalExecutor.Setup(s =>
          s.Transfer(
              It.IsAny<ISmartContractState>(),
              It.IsAny<Address>(),
              It.IsAny<ulong>()))
          .Returns(new TransferResult(true));

      this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, Participants[0], 0));
      game.Withdraw();

      this.mockPersistentState.Verify(s => s.SetAddress(nameof(Wheel.LastBidOwner), Address.Zero));
      this.mockPersistentState.Verify(s => s.SetUInt32(nameof(Wheel.RoundCounter), 0));
      this.mockPersistentState.Verify(s => s.SetUInt64(nameof(Wheel.Staked), 0));
      this.mockPersistentState.Verify(s => s.SetBool(nameof(Wheel.IsGameStarted), false));
      this.mockPersistentState.Verify(s => s.SetUInt64(nameof(Wheel.TimeoutBlock), startBlockNumber + blockDelay - 1));
      this.mockContractLogger.Verify(l => l.Log(It.IsAny<ISmartContractState>(), new Wheel.WinnerLog { Winner = Participants[0], Amount = bid << 2 }));
    }

    [Fact]
    public void Withdraw_Game_Is_Started()
    {
      var game = NewWheel(bid, blockDelay, startBlockNumber);

      //previous participants who will become a winner
      this.mockPersistentState.Setup(s => s.GetAddress(nameof(Wheel.LastBidOwner))).Returns(Participants[0]);
      this.mockPersistentState.Setup(s => s.GetUInt32(nameof(Wheel.RoundCounter))).Returns(5);
      this.mockPersistentState.Setup(s => s.GetUInt64(nameof(Wheel.Staked))).Returns(bid << 2);
      this.mockPersistentState.Setup(s => s.GetBool(nameof(Wheel.IsGameStarted))).Returns(true);
      this.mockPersistentState.Setup(s => s.GetUInt64(nameof(Wheel.TimeoutBlock))).Returns(startBlockNumber + blockDelay);
      this.mockPersistentState.Setup(s => s.GetUInt64($"balance:{Participants[1]}")).Returns(bid * 10);

      //is not timeout yet
      this.mockContractState.Setup(s => s.Block.Number).Returns(startBlockNumber + blockDelay - 1);

      mockInternalExecutor.Setup(s =>
          s.Transfer(
              It.IsAny<ISmartContractState>(),
              It.IsAny<Address>(),
              It.IsAny<ulong>()))
          .Returns(new TransferResult(true));

      this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, Participants[1], 0));
      game.Withdraw();

      this.mockPersistentState.Verify(s => s.SetUInt64($"balance:{Participants[1]}", 0));
    }
  }
}
