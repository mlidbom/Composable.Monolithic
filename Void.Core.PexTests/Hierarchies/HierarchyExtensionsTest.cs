// <copyright file="HierarchyExtensionsTest.cs" company="Microsoft">Copyright � Microsoft 2009</copyright>
using System;
using System.Collections.Generic;
using Microsoft.Pex.Framework;
using Microsoft.Pex.Framework.Validation;
using NUnit.Framework;
using Void.Hierarchies;

namespace Void.Hierarchies
{
    /// <summary>This class contains parameterized unit tests for HierarchyExtensions</summary>
    [PexClass(typeof(HierarchyExtensions))]
    [PexAllowedExceptionFromTypeUnderTest(typeof(InvalidOperationException))]
    [PexAllowedExceptionFromTypeUnderTest(typeof(ArgumentException), AcceptExceptionSubtypes = true)]
    [TestFixture]
    public partial class HierarchyExtensionsTest
    {
        /// <summary>Test stub for AsHierarchy(!!0, Func`2&lt;!!0,IEnumerable`1&lt;!!0&gt;&gt;)</summary>
        [PexGenericArguments(typeof(int))]
        [PexMethod]
        public IAutoHierarchy<T> AsHierarchy<T>(T me, Func<T, IEnumerable<T>> childGetter)
        {
            IAutoHierarchy<T> result = HierarchyExtensions.AsHierarchy<T>(me, childGetter);
            return result;
            // TODO: add assertions to method HierarchyExtensionsTest.AsHierarchy(!!0, Func`2<!!0,IEnumerable`1<!!0>>)
        }

        /// <summary>Test stub for Flatten(!!0)</summary>
        [PexMethod]
        public IEnumerable<T> Flatten<T>(T root)
            where T : IHierarchy<T>
        {
            IEnumerable<T> result = HierarchyExtensions.Flatten<T>(root);
            return result;
            // TODO: add assertions to method HierarchyExtensionsTest.Flatten(!!0)
        }

        /// <summary>Test stub for Unwrap(IEnumerable`1&lt;IAutoHierarchy`1&lt;!!0&gt;&gt;)</summary>
        [PexGenericArguments(typeof(int))]
        [PexMethod]
        public IEnumerable<T> Unwrap<T>(IEnumerable<IAutoHierarchy<T>> root)
        {
            IEnumerable<T> result = HierarchyExtensions.Unwrap<T>(root);
            return result;
            // TODO: add assertions to method HierarchyExtensionsTest.Unwrap(IEnumerable`1<IAutoHierarchy`1<!!0>>)
        }
    }
}
