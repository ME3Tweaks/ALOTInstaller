using System;
using System.Collections.Generic;

namespace ALOTInstallerCore.Helpers
{
    public class CaseInsensitiveDictionary<V> : Dictionary<string, V>
    {
        public CaseInsensitiveDictionary() : base(StringComparer.OrdinalIgnoreCase)
        {
        }
    }
}
