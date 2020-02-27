using Moq;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts;
using Xunit;
using static JsonConfig;

namespace StratisSwap.Tests
{
    public class JsonConfigTests
    {
        private readonly Mock<ISmartContractState> MockContractState;
        private readonly Mock<IPersistentState> MockPersistentState;
        private readonly Mock<IContractLogger> MockContractLogger;
        private readonly Mock<IInternalTransactionExecutor> MockInternalExecutor;
        private readonly Address AdminOne;
        private readonly Address AdminTwo;
        private readonly Address ContributorOne;
        private readonly Address Unauthorized;
        private readonly Address ContractAddress;
        private const string DefaultConfig = "{\"json\": \"Test\"}";
        private const string UpdatedConfig = "{\"json\": \"Updated\"}";

        public JsonConfigTests()
        {
            MockContractLogger = new Mock<IContractLogger>();
            MockPersistentState = new Mock<IPersistentState>();
            MockContractState = new Mock<ISmartContractState>();
            MockInternalExecutor = new Mock<IInternalTransactionExecutor>();
            MockContractState.Setup(x => x.PersistentState).Returns(MockPersistentState.Object);
            MockContractState.Setup(x => x.ContractLogger).Returns(MockContractLogger.Object);
            MockContractState.Setup(x => x.InternalTransactionExecutor).Returns(MockInternalExecutor.Object);
            AdminOne = "0x0000000000000000000000000000000000000001".HexToAddress();
            AdminTwo = "0x0000000000000000000000000000000000000002".HexToAddress();
            ContributorOne = "0x0000000000000000000000000000000000000003".HexToAddress();
            Unauthorized = "0x0000000000000000000000000000000000000005".HexToAddress();
            ContractAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        }

        private JsonConfig createNewConfigContract()
        {
            MockContractState.Setup(x => x.Message).Returns(new Message(ContractAddress, AdminOne, 0));
            MockContractState.Setup(x => x.Block.Number).Returns(12345);
            MockPersistentState.Setup(x => x.GetBool($"Admin:{AdminOne}")).Returns(true);

            return new JsonConfig(MockContractState.Object, DefaultConfig);
        }

        [Fact]
        public void Creates_New_Contract()
        {
            var contract = createNewConfigContract();

            MockPersistentState.Verify(x => x.SetBool($"Admin:{AdminOne}", true));
            Assert.True(contract.IsAdmin(AdminOne));

            MockContractLogger.Verify(x => x.Log(It.IsAny<ISmartContractState>(), It.IsAny<RoleLog>()), Times.Once);
            MockContractLogger.Verify(x => x.Log(It.IsAny<ISmartContractState>(), It.IsAny<ConfigLog>()), Times.Once);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void UpdateAdmin_Success(bool approval)
        {
            var contract = createNewConfigContract();
            var newAdminAddress = AdminTwo;

            MockContractState.Setup(x => x.Message).Returns(new Message(ContractAddress, AdminOne, 0));

            contract.UpdateAdmin(newAdminAddress, approval);

            MockPersistentState.Verify(x => x.SetBool($"Admin:{AdminTwo}", approval), Times.Once);

            var expectedRoleLog = new RoleLog
            {
                Blame = contract.Message.Sender,
                UpdatedAddress = newAdminAddress,
                UpdatedValue = approval,
                Action = nameof(JsonConfig.UpdateAdmin),
                Block = contract.Block.Number
            };

            MockContractLogger.Verify(x => x.Log(It.IsAny<ISmartContractState>(), expectedRoleLog), Times.Once);
        }

        [Fact]
        public void UpdateAdmin_Failure_Sender_IsNot_Admin()
        {
            var newAdminAddress = AdminTwo;
            var approval = true;
            var contract = createNewConfigContract();

            MockContractState.Setup(x => x.Message).Returns(new Message(ContractAddress, Unauthorized, 0));

            Assert.ThrowsAny<SmartContractAssertException>(() => contract.UpdateAdmin(newAdminAddress, approval));

            MockPersistentState.Verify(x => x.SetBool($"Admin:{newAdminAddress}", approval), Times.Never);

            var expectedRoleLog = new RoleLog
            {
                Blame = Unauthorized,
                UpdatedAddress = newAdminAddress,
                Action = nameof(contract.UpdateAdmin),
                UpdatedValue = approval,
                Block = contract.Block.Number
            };

            MockContractLogger.Verify(x => x.Log(It.IsAny<ISmartContractState>(), expectedRoleLog), Times.Never);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void UpdateContributor_Success(bool approval)
        {
            var contract = createNewConfigContract();
            var newContributorAddress = ContributorOne;

            MockContractState.Setup(x => x.Message).Returns(new Message(ContractAddress, AdminOne, 0));

            contract.UpdateContributor(newContributorAddress, approval);

            MockPersistentState.Verify(x => x.SetBool($"Contributor:{newContributorAddress}", approval), Times.Once);

            var expectedRoleLog = new RoleLog
            {
                Blame = contract.Message.Sender,
                UpdatedAddress = newContributorAddress,
                UpdatedValue = approval,
                Action = nameof(JsonConfig.UpdateContributor),
                Block = contract.Block.Number
            };

            MockContractLogger.Verify(x => x.Log(It.IsAny<ISmartContractState>(), expectedRoleLog), Times.Once);
        }

        [Fact]
        public void UpdateContributor_Failure_Sender_IsNot_Admin()
        {
            var newContributorAddress = ContributorOne;
            var approval = true;
            var contract = createNewConfigContract();

            MockContractState.Setup(x => x.Message).Returns(new Message(ContractAddress, Unauthorized, 0));

            Assert.ThrowsAny<SmartContractAssertException>(() => contract.UpdateContributor(newContributorAddress, approval));

            MockPersistentState.Verify(x => x.SetBool($"Contributor:{newContributorAddress}", approval), Times.Never);

            var expectedRoleLog = new RoleLog
            {
                Blame = Unauthorized,
                UpdatedAddress = newContributorAddress,
                Action = nameof(contract.UpdateContributor),
                UpdatedValue = approval,
                Block = contract.Block.Number
            };

            MockContractLogger.Verify(x => x.Log(It.IsAny<ISmartContractState>(), expectedRoleLog), Times.Never);
        }

        [Fact]
        public void UpdateConfig_Success_From_Admin()
        {
            var contract = createNewConfigContract();
            var sender = AdminOne;

            MockContractState.Setup(x => x.Message).Returns(new Message(ContractAddress, sender, 0));

            contract.UpdateConfig(UpdatedConfig);

            MockPersistentState.Verify(x => x.GetBool($"Admin:{sender}"), Times.Once);

            var expectedConfigLog = new ConfigLog
            {
                Blame = sender,
                Config = UpdatedConfig,
                Block = contract.Block.Number
            };

            MockContractLogger.Verify(x => x.Log(It.IsAny<ISmartContractState>(), expectedConfigLog), Times.Once);
        }

        [Fact]
        public void UpdateConfig_Success_From_Contributor()
        {
            var contract = createNewConfigContract();
            var sender = ContributorOne;

            MockPersistentState.Setup(x => x.GetBool($"Contributor:{sender}")).Returns(true);

            MockContractState.Setup(x => x.Message).Returns(new Message(ContractAddress, sender, 0));

            contract.UpdateConfig(UpdatedConfig);

            MockPersistentState.Verify(x => x.GetBool($"Contributor:{sender}"), Times.Once);

            var expectedConfigLog = new ConfigLog
            {
                Blame = sender,
                Config = UpdatedConfig,
                Block = contract.Block.Number
            };

            MockContractLogger.Verify(x => x.Log(It.IsAny<ISmartContractState>(), expectedConfigLog), Times.Once);
        }

        [Fact]
        public void UpdateConfig_Failure_Not_Admin_Or_Contributor()
        {
            var contract = createNewConfigContract();
            var sender = Unauthorized;

            MockContractState.Setup(x => x.Message).Returns(new Message(ContractAddress, sender, 0));

            Assert.ThrowsAny<SmartContractAssertException>(() => contract.UpdateConfig(UpdatedConfig));

            MockPersistentState.Verify(x => x.GetBool($"Contributor:{sender}"), Times.Once);

            MockPersistentState.Verify(x => x.GetBool($"Admin:{sender}"), Times.Once);

            var expectedConfigLog = new ConfigLog
            {
                Blame = sender,
                Config = UpdatedConfig,
                Block = contract.Block.Number
            };

            MockContractLogger.Verify(x => x.Log(It.IsAny<ISmartContractState>(), expectedConfigLog), Times.Never);
        }
    }
}
