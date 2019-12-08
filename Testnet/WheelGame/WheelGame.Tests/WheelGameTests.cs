using Moq;
using Stratis.SmartContracts;
using System;
using Xunit;

namespace WheelGame.Tests
{
  public class WheelGameTests
  {
    private readonly Mock<ISmartContractState> mockContractState;
    private readonly Mock<IPersistentState> mockPersistentState;
    private readonly Mock<IContractLogger> mockContractLogger;
    private Address owner;
    private Address sender;
    private Address contract;
    private Address spender;
    private Address destination;

    public WheelGameTests()
    {
      //var network = new SmartContractPosTest();
      this.mockContractLogger = new Mock<IContractLogger>();
      this.mockPersistentState = new Mock<IPersistentState>();
      this.mockContractState = new Mock<ISmartContractState>();
      this.mockContractState.Setup(s => s.PersistentState).Returns(this.mockPersistentState.Object);
      this.mockContractState.Setup(s => s.ContractLogger).Returns(this.mockContractLogger.Object);
    }

    [Fact]
    public void Constructor_Parameters_Properly()
    {
      this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.owner, 0));

      ulong bid = 200;
      byte blockDelay = 10;

      var game = new Wheel(mockContractState.Object, bid, blockDelay);

      // Verify that PersistentState was called with the total supply
      this.mockPersistentState.Verify(s => s.SetChar(nameof(Wheel.BlockDelay), (char)blockDelay));
      this.mockPersistentState.Verify(s => s.SetUInt64(nameof(Wheel.Bid), bid));
      this.mockPersistentState.Verify(s => s.SetBool(nameof(Wheel.IsGameStarted), true));
    }
  }
}
