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
using NUnit.Framework;
using Microsoft.Pex.Engine.Exceptions;

namespace Void
{
    public partial class EnumExtensionsTest
    {
[Test]
[PexGeneratedBy(typeof(EnumExtensionsTest))]
[PexRaisedContractException(PexExceptionState.Expected)]
public void HasFlag02()
{
    try
    {
      if (!PexContract.HasRequiredRuntimeContracts(typeof(EnumExtensions), (PexRuntimeContractsFlags)4223))
        PexAssert.Inconclusive("assembly Void.Core is not instrumented with runtime contracts");
      bool b;
      b = this.HasFlag((Enum)null, (Enum)null);
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
