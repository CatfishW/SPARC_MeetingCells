using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
namespace Sparc.Original
{
    /// <summary>Retains original reflection-probe objects and cubemap resolutions but spreads their work.
    /// Rendering all 23 probes and particle shadow passes during scene startup exhausted WebAssembly memory.</summary>
    public sealed class OriginalReflectionScheduler:MonoBehaviour
    {
        public ReflectionProbe[] probes;
        IEnumerator Start()
        {
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)yield break;
            while(!OriginalRuntime.Instance||!OriginalRuntime.Instance.Started)yield return null;
            yield return new WaitForSeconds(1);
            var camera=Camera.main;var position=camera?camera.transform.position:Vector3.zero;
            foreach(var probe in probes.Where(p=>p).OrderBy(p=>Vector3.SqrMagnitude(p.transform.position-position)))
            {
                if(!probe.isActiveAndEnabled)continue;
                int job=probe.RenderProbe();float deadline=Time.realtimeSinceStartup+15;
                while(probe&&!probe.IsFinishedRendering(job)&&Time.realtimeSinceStartup<deadline)yield return null;
                yield return new WaitForSeconds(0.3f);
            }
        }
    }
}
