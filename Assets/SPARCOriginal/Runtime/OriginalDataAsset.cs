using System;
using UnityEngine;
namespace Sparc.Original
{
    public sealed class OriginalDataAsset:ScriptableObject
    {
        public string sourceType,sourceId,sourceScope;
        [TextArea] public string configJson;
        public OriginalReference[] sourceReferences=Array.Empty<OriginalReference>();
    }
}
