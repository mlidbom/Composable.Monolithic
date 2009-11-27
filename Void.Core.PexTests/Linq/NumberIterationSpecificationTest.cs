// <copyright file="NumberIterationSpecificationTest.cs" company="Microsoft">Copyright � Microsoft 2009</copyright>
using System;
using Microsoft.Pex.Framework;
using Microsoft.Pex.Framework.Validation;
using NUnit.Framework;
using Void.Linq;

namespace Void.Linq
{
    /// <summary>This class contains parameterized unit tests for IterationSpecification</summary>
    [PexClass(typeof(Number.IterationSpecification))]
    [PexAllowedExceptionFromTypeUnderTest(typeof(InvalidOperationException))]
    [PexAllowedExceptionFromTypeUnderTest(typeof(ArgumentException), AcceptExceptionSubtypes = true)]
    [TestFixture]
    public partial class NumberIterationSpecificationTest
    {
    }
}
