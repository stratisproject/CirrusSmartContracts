﻿using Stratis.SmartContracts;
using System;
using System.Collections.Generic;
using System.Text;

namespace DividendTokenContract.Tests
{
    public class TransferResult : ITransferResult
    {
        public object ReturnValue { get; private set; }

        public bool Success { get; private set; }

        public static TransferResult Failed() => new TransferResult { Success = false };


        public static TransferResult Succeed(object returnValue) => new TransferResult { Success = true, ReturnValue = returnValue };
    }
}
