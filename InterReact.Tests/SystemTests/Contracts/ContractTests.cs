﻿using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using InterReact.Messages;
using InterReact.Extensions;
using InterReact.Tests.Utility;
using Xunit;
using Xunit.Abstractions;

namespace InterReact.Tests.SystemTests.Contracts
{
    public class ContractTests : BaseSystemTest
    {
        public ContractTests(SystemTestFixture fixture, ITestOutputHelper output) : base(fixture, output) { }

        [Fact]
        public async Task TestRequestContractData()
        {
            var task = Client
                .Response
                .OfType<ContractDataEnd>()
                .FirstAsync().ToTask();

            Client.Request.RequestContractData(Id, Stock1);

            await task.Timeout();
        }
    }
}

