using System;
using Moq;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR;
using Xunit;
using static Arena;

namespace HashBattleTest
{
    public class ArenaTest
    {
        private readonly IPersistentState state;
        private readonly Mock<ISmartContractState> mockContractState;
        private readonly Mock<IPersistentState> mockPersistentState;
        private readonly Mock<IContractLogger> mockContractLogger;
        private readonly Mock<IInternalTransactionExecutor> mTransactionExecutor;
        private Address contract;
        private Address ownerAddress;
        private Address playerAddress1;
        private Address playerAddress2;
        private Address playerAddress3;
        private Address playerAddress4;

        public ArenaTest()
        {
            this.state = new InMemoryState();
            this.mockPersistentState = new Mock<IPersistentState>();
            this.mockContractState = new Mock<ISmartContractState>();
            this.mockContractLogger = new Mock<IContractLogger>();
            this.mTransactionExecutor = new Mock<IInternalTransactionExecutor>();
            this.mockContractState.Setup(s => s.PersistentState).Returns(this.state);
            this.mockContractState.Setup(s => s.ContractLogger).Returns(this.mockContractLogger.Object);
            this.mockContractState.Setup(s => s.InternalTransactionExecutor).Returns(this.mTransactionExecutor.Object);
            this.contract = "0x0000000000000000000000000000000000000001".HexToAddress();
            this.ownerAddress = "0x0000000000000000000000000000000000000002".HexToAddress();
            this.playerAddress1 = "0x0000000000000000000000000000000000000003".HexToAddress();
            this.playerAddress2 = "0x0000000000000000000000000000000000000004".HexToAddress();
            this.playerAddress3 = "0x0000000000000000000000000000000000000005".HexToAddress();
            this.playerAddress4 = "0x0000000000000000000000000000000000000006".HexToAddress();
        }


        [Fact]
        public void TestBattle()
        {
            Arena arena = StartBattleTest();
            Player1EnterGameTest(arena);
            Player2EnterGameTest(arena);
            Player3EnterGameTest(arena);
            Player4EnterGameTest(arena);
            Player1EndGameTest(arena);
            Player2EndGameTest(arena);
            Player3EndGameTest(arena);
            Player4EndGameTest(arena);
            GetGameWinnerTest(arena);
        }

        private Arena StartBattleTest()
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.ownerAddress, 0));
            Arena arena = new Arena(this.mockContractState.Object);
            ulong battleId = arena.StartBattle(1);

            this.mockContractLogger.Verify(m => m.Log(this.mockContractState.Object, new BattleEventLog { Event = "Start", BattleId = battleId, Address = this.ownerAddress }));
            return arena;
        }

        private void Player1EnterGameTest(Arena arena)
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.playerAddress1, 1));
            arena.EnterBattle(1);

            this.mockContractLogger.Verify(m => m.Log(this.mockContractState.Object, new BattleEventLog { Event = "Enter", BattleId = 1, Address = this.playerAddress1 }));
        }

        private void Player2EnterGameTest(Arena arena)
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.playerAddress2, 1));
            arena.EnterBattle(1);

            this.mockContractLogger.Verify(m => m.Log(this.mockContractState.Object, new BattleEventLog { Event = "Enter", BattleId = 1, Address = this.playerAddress2 }));
        }

        private void Player3EnterGameTest(Arena arena)
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.playerAddress3, 1));
            arena.EnterBattle(1);

            this.mockContractLogger.Verify(m => m.Log(this.mockContractState.Object, new BattleEventLog { Event = "Enter", BattleId = 1, Address = this.playerAddress3 }));
        }

        private void Player4EnterGameTest(Arena arena)
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.playerAddress4, 1));
            arena.EnterBattle(1);

            this.mockContractLogger.Verify(m => m.Log(this.mockContractState.Object, new BattleEventLog { Event = "Enter", BattleId = 1, Address = this.playerAddress4 }));
        }

        private void Player1EndGameTest(Arena arena)
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.ownerAddress, 0));
            arena.EndBattle(this.playerAddress1, 1, 10);

            this.mockContractLogger.Verify(m => m.Log(this.mockContractState.Object, new BattleEventLog { Event = "End", BattleId = 1, Address = this.ownerAddress }));
        }

        private void Player2EndGameTest(Arena arena)
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.ownerAddress, 0));
            arena.EndBattle(this.playerAddress2, 1, 20);

            this.mockContractLogger.Verify(m => m.Log(this.mockContractState.Object, new BattleEventLog { Event = "End", BattleId = 1, Address = this.ownerAddress }));
        }

        private void Player3EndGameTest(Arena arena)
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.ownerAddress, 0));
            arena.EndBattle(this.playerAddress3, 1, 30);

            this.mockContractLogger.Verify(m => m.Log(this.mockContractState.Object, new BattleEventLog { Event = "End", BattleId = 1, Address = this.ownerAddress }));
        }

        private void Player4EndGameTest(Arena arena)
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.ownerAddress, 0));
            arena.EndBattle(this.playerAddress4, 1, 40);

            this.mockContractLogger.Verify(m => m.Log(this.mockContractState.Object, new BattleEventLog { Event = "End", BattleId = 1, Address = this.ownerAddress }));
        }

        private void GetGameWinnerTest(Arena arena)
        {
            this.mockContractState.Setup(m => m.Message).Returns(new Message(this.contract, this.ownerAddress, 0));
            Address winner = arena.GetWinner(1);

            Assert.Equal(this.playerAddress4, winner);
        }
    }
}
