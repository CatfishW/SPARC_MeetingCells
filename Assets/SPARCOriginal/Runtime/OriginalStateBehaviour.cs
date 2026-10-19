using System;
using UnityEngine;
namespace Sparc.Original
{
    public sealed class OriginalStateBehaviour:StateMachineBehaviour
    {
        public string sourceType,sourceId,sourceScope;
        [TextArea] public string configJson;
        public OriginalReference[] sourceReferences=Array.Empty<OriginalReference>();
    }
}
