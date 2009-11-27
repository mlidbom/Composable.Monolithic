// <auto-generated>
// This file contains automatically generated unit tests.
// Do NOT modify this file manually.
// 
// When Pex is invoked again,
// it might remove or update any previously generated unit tests.
// 
// If the contents of this file becomes outdated, e.g. if it does not
// compile anymore, you may delete this file and invoke Pex again.
// </auto-generated>
using System;
using Microsoft.Pex.Framework.Generated;
using Microsoft.Pex.Framework;
using System.Collections.Generic;
using NUnit.Framework;
using Microsoft.Pex.Engine.Exceptions;

namespace Void.Hierarchies
{
    public partial class HierarchyExtensionsTest
    {
[Test]
[PexGeneratedBy(typeof(HierarchyExtensionsTest))]
public void Unwrap02()
{
    IEnumerable<int> iEnumerable;
    IAutoHierarchy<int>[] iAutoHierarchys = new IAutoHierarchy<int>[0];
    iEnumerable = this.Unwrap<int>((IEnumerable<IAutoHierarchy<int>>)iAutoHierarchys);
    PexAssert.IsNotNull((object)iEnumerable);
}
[Test]
[PexGeneratedBy(typeof(HierarchyExtensionsTest))]
[PexRaisedContractException(PexExceptionState.Expected)]
public void Unwrap04()
{
    try
    {
      if (!PexContract.HasRequiredRuntimeContracts(typeof(HierarchyExtensions), (PexRuntimeContractsFlags)4223))
        PexAssert.Inconclusive("assembly Void.Core is not instrumented with runtime contracts");
      IEnumerable<int> iEnumerable;
      iEnumerable = this.Unwrap<int>((IEnumerable<IAutoHierarchy<int>>)null);
      throw new AssertFailedException();
    }
    catch(Exception ex)
    {
      if (!PexContract.IsContractException(ex))
        throw ex;
    }
}
    }
}
