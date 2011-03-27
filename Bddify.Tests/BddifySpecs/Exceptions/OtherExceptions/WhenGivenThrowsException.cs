using System;
using Bddify.Core;
using NSubstitute;
using NUnit.Framework;

namespace Bddify.Tests.BddifySpecs.Exceptions.OtherExceptions
{
    public class WhenGivenThrowsException : OtherExceptionBase
    {
        [SetUp]
        public void SetupContext()
        {
            Assert.Throws<Exception>(() => Sut.Execute(givenShouldThrow:true));
        }

        [Test]
        public void GivenShouldBeReportedAsFailed()
        {
            Assert.That(Sut.GivenStep.Result, Is.EqualTo(StepExecutionResult.Failed));
        }

        [Test]
        public void WhenShouldNotBeExecuted()
        {
            Assert.That(Sut.WhenStep.Result, Is.EqualTo(StepExecutionResult.NotExecuted));
        }

        [Test]
        public void ThenShouldNotBeExecuted()
        {
            Assert.That(Sut.ThenStep.Result, Is.EqualTo(StepExecutionResult.NotExecuted));
        }

    }
}