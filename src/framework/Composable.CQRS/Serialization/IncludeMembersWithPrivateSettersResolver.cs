﻿using System.Reflection;
using Composable.SystemCE;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Composable.Serialization
{
    class IncludeMembersWithPrivateSettersResolver : DefaultContractResolver, IStaticInstancePropertySingleton
    {
        public static readonly IncludeMembersWithPrivateSettersResolver Instance = new IncludeMembersWithPrivateSettersResolver();
        protected IncludeMembersWithPrivateSettersResolver()
        {
        }

        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var prop = base.CreateProperty(member, memberSerialization);

            if(!prop.Writable)
            {
                var property = member as PropertyInfo;
                if(property != null)
                {
                    var hasPrivateSetter = property.GetSetMethod(true) != null;
                    prop.Writable = hasPrivateSetter;
                }
            }

            return prop;
        }
    }
}