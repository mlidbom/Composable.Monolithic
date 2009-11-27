// <copyright file="ZippingTest.cs" company="Microsoft">Copyright � Microsoft 2009</copyright>
using System;
using System.Collections.Generic;
using Microsoft.Pex.Framework;
using Microsoft.Pex.Framework.Validation;
using NUnit.Framework;
using Void.Linq;

namespace Void.Linq
{
    /// <summary>This class contains parameterized unit tests for Zipping</summary>
    [PexClass(typeof(Zipping))]
    [PexAllowedExceptionFromTypeUnderTest(typeof(InvalidOperationException))]
    [PexAllowedExceptionFromTypeUnderTest(typeof(ArgumentException), AcceptExceptionSubtypes = true)]
    [TestFixture]
    public partial class ZippingTest
    {
        /// <summary>Test stub for Zip(IEnumerable`1&lt;!!0&gt;, IEnumerable`1&lt;!!1&gt;, Func`3&lt;!!0,!!1,!!2&gt;)</summary>
        [PexGenericArguments(typeof(int), typeof(int), typeof(int))]
        [PexMethod]
        public IEnumerable<TResult> Zip<TFirst,TSecond,TResult>(
            IEnumerable<TFirst> first,
            IEnumerable<TSecond> second,
            Func<TFirst, TSecond, TResult> selector
        )
        {
            IEnumerable<TResult> result
               = Zipping.Zip<TFirst, TSecond, TResult>(first, second, selector);
            return result;
            // TODO: add assertions to method ZippingTest.Zip(IEnumerable`1<!!0>, IEnumerable`1<!!1>, Func`3<!!0,!!1,!!2>)
        }

        /// <summary>Test stub for Zip(IEnumerable`1&lt;!!0&gt;, IEnumerable`1&lt;!!1&gt;)</summary>
        [PexGenericArguments(typeof(int), typeof(int))]
        [PexMethod]
        public IEnumerable<Zipping.Pair<TFirst, TSecond>> Zip01<TFirst,TSecond>(IEnumerable<TFirst> first, IEnumerable<TSecond> second)
        {
            IEnumerable<Zipping.Pair<TFirst, TSecond>> result
               = Zipping.Zip<TFirst, TSecond>(first, second);
            return result;
            // TODO: add assertions to method ZippingTest.Zip01(IEnumerable`1<!!0>, IEnumerable`1<!!1>)
        }
    }
}
