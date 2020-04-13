using NBitcoin;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR.Compilation;
using Stratis.SmartContracts.CLR.Local;
using Stratis.SmartContracts.CLR.Serialization;
using Stratis.SmartContracts.Networks;
using Xunit;

namespace ICOContract.Integration.Tests
{
    public partial class ICOContractTests
    {
        [Fact]
        public void Deployment_Test()
        {
            // Compile the contract we want to deploy
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("ICOContract.cs");
            Assert.True(compilationResult.Success);
            using (var chain = new TestChain().Initialize())
            {
                var owner = chain.PreloadedAddresses[0];
                var totalSupply = 100ul;
                var serializer = new Serializer(new ContractPrimitiveSerializer(new SmartContractsPoARegTest()));

                var periods = new SalePeriodInput[] { new SalePeriodInput { Multiplier = 2, DurationBlocks = 1 } };
                var parameters = new object[] { totalSupply, "Gluon", "Glu", serializer.Serialize(periods) };

                var createResult = chain.SendCreateContractTransaction(owner, compilationResult.Compilation, 0, parameters);

                // Mine a block which will contain our sent transaction
                chain.MineBlocks(1);

                // Check the receipt to see that contract deployment was successful
                var receipt = chain.GetReceipt(createResult.TransactionId);
                Assert.Equal(owner, receipt.From);

                // Check that the code is indeed saved on-chain
                var savedCode = chain.GetCode(createResult.NewContractAddress);
                Assert.NotNull(savedCode);
            }
        }

        //[Fact]
        //public void Investment_Test()
        //{
        //    // Compile the contract we want to deploy
        //    ContractCompilationResult compilationResult = ContractCompiler.CompileFile("ICOContract.cs");
        //    Assert.True(compilationResult.Success);
        //    using (var chain = new TestChain().Initialize())
        //    {
        //        // Get an address we can use for deploying
        //        var deployerAddress = chain.PreloadedAddresses[0];
        //        var totalSupply = 100ul;
        //        var rate = 2ul;

        //        var parameters = new object[] { totalSupply, "Gluon", "Glu", 1000ul /*duration*/, rate };

        //        // Create and send transaction to mempool with parameters
        //        var createResult = chain.SendCreateContractTransaction(deployerAddress, compilationResult.Compilation, 0, parameters);

        //        // Mine a block which will contain our sent transaction
        //        chain.MineBlocks(1);

        //        // Check the receipt to see that contract deployment was successful
        //        var receipt = chain.GetReceipt(createResult.TransactionId);
        //        Assert.Equal(deployerAddress, receipt.From);

        //        // Check that the code is indeed saved on-chain
        //        var savedCode = chain.GetCode(createResult.NewContractAddress);
        //        Assert.NotNull(savedCode);

        //        // Use another identity to invest
        //        var investorAddress = chain.PreloadedAddresses[1];

        //        // Send a call to the invest method
        //        var callResult = chain.SendCallContractTransaction(investorAddress, "Invest", createResult.NewContractAddress, 5ul);
        //        chain.MineBlocks(1);

        //        receipt = chain.GetReceipt(callResult.TransactionId);

        //        // Call a method locally to check the state is as expected
        //        var localCallResult = chain.CallContractMethodLocally(investorAddress, "TokenBalance", createResult.NewContractAddress, 0);
        //        Assert.Equal(90ul, (ulong)localCallResult.Return);
        //    }
        //}

        [Fact]
        public void OverSold_Investment_Test()
        {
            // Compile the contract we want to deploy
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("ICOContract.cs");
            Assert.True(compilationResult.Success);
            using (var chain = new TestChain().Initialize())
            {
                // Get an address we can use for deploying
                var owner = chain.PreloadedAddresses[0];
                var totalSupply = 100ul;
                var serializer = new Serializer(new ContractPrimitiveSerializer(new SmartContractsPoARegTest()));

                var periods = new SalePeriodInput[] { new SalePeriodInput { Multiplier = 2, DurationBlocks = 1 } };
                var parameters = new object[] { totalSupply, "Gluon", "Glu", serializer.Serialize(periods) };

                var createResult = chain.SendCreateContractTransaction(owner, compilationResult.Compilation, 0, parameters);

                chain.MineBlocks(1);

                var investor = chain.PreloadedAddresses[1];

                var currentBalance = chain.GetBalance(investor);
                chain.SendCallContractTransaction(investor, "Invest", createResult.NewContractAddress, 60ul);
                chain.MineBlocks(1);

                var localCallResult = chain.CallContractMethodLocally(owner, "TokenBalance", createResult.NewContractAddress, 0);

                Assert.Equal(0ul, (ulong)localCallResult.Return); // All tokens are sold

                var contractBalance = chain.GetBalance(createResult.NewContractAddress);

                Assert.Equal(Money.Coins(50), contractBalance); // 50 allowed and 10 refunded

                var investorTokenBalance = (ulong)chain.CallContractMethodLocally(owner, "GetBalance", createResult.NewContractAddress, 0, new object[] { investor }).Return;

                // Verify investor's token balance
                Assert.Equal(totalSupply, investorTokenBalance);

                var refundAmount = currentBalance - chain.GetBalance(investor);

                Assert.Equal(Money.Coins(50), Money.Satoshis(refundAmount)); // 60 invested, 50 spend and 10 refunded
            }
        }
    }
}
