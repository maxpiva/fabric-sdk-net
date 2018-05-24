using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperledger.Fabric.Tests.Helper
{
    public class ExpectedExceptionWithMessageAttribute : ExpectedExceptionBaseAttribute
    {
        public ExpectedExceptionWithMessageAttribute(Type exceptionType, string expectedMessage)

        {
            ExceptionType = exceptionType;
            ExpectedMessage = expectedMessage;
        }

        public Type ExceptionType { get; set; }

        public string ExpectedMessage { get; set; }


        protected override void Verify(Exception e)
        {
            if (e.GetType() != ExceptionType)
            {
                Assert.Fail($"ExpectedExceptionWithMessage failed. Expected exception type: <{ExceptionType.FullName}>. Actual exception type: <{e.GetType().FullName}>. Exception message: <{e.Message}>");
            }

            if (ExpectedMessage != e.Message)
            {
                Assert.Fail($"ExpectedExceptionWithMessage failed. Expected message to contain: <{ExpectedMessage}>. Actual message: <{e.Message}>. Exception type: <{e.GetType().FullName}>");
            }
        }
    }
}