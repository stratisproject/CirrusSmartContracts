using Stratis.SmartContracts.CLR.Compilation;
using Xunit;

namespace ICOContract.Regression.Tests
{
    public class ICOContractTests
    {
        [Fact]
        public void Investment_Test()
        {
            // Compile the contract we want to deploy
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("ICOContract.cs");
            Assert.True(compilationResult.Success);
            using (var chain = new TestChain().Initialize())
            {
                // Get an address we can use for deploying
                var deployerAddress = chain.PreloadedAddresses[0];
                var totalSupply = 100ul;
                var rate = 2ul;

                var parameters = new object[] { totalSupply, "Gluon", "Glu", 1000ul /*duration*/, rate };

                // Create and send transaction to mempool with parameters
                var createResult = chain.SendCreateContractTransaction(deployerAddress, compilationResult.Compilation, 0, parameters);

                // Mine a block which will contain our sent transaction
                chain.MineBlocks(1);

                // Check the receipt to see that contract deployment was successful
                var receipt = chain.GetReceipt(createResult.TransactionId);
                Assert.Equal(deployerAddress, receipt.From);

                // Check that the code is indeed saved on-chain
                var savedCode = chain.GetCode(createResult.NewContractAddress);
                Assert.NotNull(savedCode);

                // Use another identity to invest
                var investorAddress = chain.PreloadedAddresses[1];

                // Send a call to the invest method
                var callResult = chain.SendCallContractTransaction(investorAddress, "Invest", createResult.NewContractAddress, 5ul);
                chain.MineBlocks(1);

                receipt = chain.GetReceipt(callResult.TransactionId);

                // Call a method locally to check the state is as expected
                var localCallResult = chain.CallContractMethodLocally(investorAddress, "TokenBalance", createResult.NewContractAddress, 0);
                Assert.Equal(90ul, (ulong)localCallResult.Return);
            }
        }
    }
}
