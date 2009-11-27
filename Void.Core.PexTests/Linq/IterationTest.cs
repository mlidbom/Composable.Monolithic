// <copyright file="IterationTest.cs" company="Microsoft">Copyright � Microsoft 2009</copyright>
using System;
using System.Collections.Generic;
using Microsoft.Pex.Framework;
using Microsoft.Pex.Framework.Validation;
using NUnit.Framework;
using Void.Linq;

namespace Void.Linq
{
    /// <summary>This class contains parameterized unit tests for Iteration</summary>
    [PexClass(typeof(Iteration))]
    [PexAllowedExceptionFromTypeUnderTest(typeof(InvalidOperationException))]
    [PexAllowedExceptionFromTypeUnderTest(typeof(ArgumentException), AcceptExceptionSubtypes = true)]
    [TestFixture]
    public partial class IterationTest
    {
        /// <summary>Test stub for ForEach(IEnumerable`1&lt;!!0&gt;, Func`2&lt;!!0,!!1&gt;)</summary>
        [PexGenericArguments(typeof(int), typeof(int))]
        [PexMethod]
        public void ForEach<TSource,TReturn>(IEnumerable<TSource> source, Func<TSource, TReturn> action)
        {
            Iteration.ForEach<TSource, TReturn>(source, action);
            // TODO: add assertions to method IterationTest.ForEach(IEnumerable`1<!!0>, Func`2<!!0,!!1>)
        }

        /// <summary>Test stub for ForEach(IEnumerable`1&lt;!!0&gt;, Action`1&lt;!!0&gt;)</summary>
        [PexGenericArguments(typeof(int))]
        [PexMethod]
        public void ForEach01<T>(IEnumerable<T> source, Action<T> action)
        {
            Iteration.ForEach<T>(source, action);
            // TODO: add assertions to method IterationTest.ForEach01(IEnumerable`1<!!0>, Action`1<!!0>)
        }

        /// <summary>Test stub for ForEach(IEnumerable`1&lt;!!0&gt;, Action`2&lt;!!0,Int32&gt;)</summary>
        [PexGenericArguments(typeof(int))]
        [PexMethod]
        public void ForEach02<T>(IEnumerable<T> source, Action<T, int> action)
        {
            Iteration.ForEach<T>(source, action);
            // TODO: add assertions to method IterationTest.ForEach02(IEnumerable`1<!!0>, Action`2<!!0,Int32>)
        }
    }
}
