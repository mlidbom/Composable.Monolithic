﻿using System.Globalization;

namespace Composable.SystemCE
{
    static class IntCE
    {
        internal static int ParseInvariant(string intAsString) => int.Parse(intAsString, CultureInfo.InvariantCulture);
        internal static string ToStringInvariant(this int @this) => @this.ToString(CultureInfo.InvariantCulture);
    }
}
