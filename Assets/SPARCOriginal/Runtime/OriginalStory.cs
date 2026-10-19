using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Sparc.Original
{
    /// <summary>Executes the serialized original graph, without inventing chapters, dialogue or transitions.</summary>
    public sealed class OriginalStory:MonoBehaviour
    {
        public OriginalRuntime runtime;
        readonly Dictionary<string,OriginalBehaviour> blocks=new(StringComparer.Ordinal);
        readonly HashSet<string> running=new(StringComparer.Ordinal);
        public readonly List<string> Trace=new();
        public readonly List<string> CompletedBlocks=new();
        public string CurrentBlock {get;private set;}
        public bool Talking=>runtime && runtime.presentation && runtime.presentation.IsSpeaking;
        public IEnumerable<OriginalBehaviour> Blocks=>blocks.Values;
        public void Initialize(IEnumerable<OriginalBehaviour> all)
        {blocks.Clear();foreach(var b in all.Where(b=>b.sourceType=="Block")){string name=b.Text("blockName");if(!string.IsNullOrEmpty(name))blocks[name]=b;}}
        public bool IsRunning(string name)=>name!=null&&running.Contains(name);
        public bool Execute(string name)
        {
            if(!runtime.Started || string.IsNullOrWhiteSpace(name))return false;
            if(!blocks.TryGetValue(name,out var block)){runtime.Missing("block:"+name);return false;}
            if(!running.Add(name))return false;
            StartCoroutine(Run(block,name,0));return true;
        }
        IEnumerator Run(OriginalBehaviour block,string name,int depth)
        {
            if(depth>32){runtime.Missing("graph-recursion:"+name);running.Remove(name);yield break;}
            CurrentBlock=name;
            try
            {
                foreach(var reference in block.Data["commandList"] as JArray??new JArray())
                {
                    if(!runtime || !runtime.Started)yield break;
                    var command=block.Resolve(reference) as OriginalBehaviour;
                    if(!command){runtime.Missing("command-reference:"+name+":"+OriginalBehaviour.Key(reference));yield break;}
                    Trace.Add(name+"/"+command.sourceId+"/"+command.sourceType);
                    switch(command.sourceType)
                    {
                        case "Say": yield return runtime.presentation.Say(command);break;
                        case "Wait":
                            float seconds=(float?)(command.Data["_duration"]?["floatVal"])??command.Number("durationOLD");
                            if(seconds>0)yield return new WaitForSeconds(seconds);break;
                        case "SetActive":
                            var target=OriginalBehaviour.AsGameObject(command.Resolve(command.Data["_targetGameObject"]?["gameObjectVal"]));
                            if(target){bool state=((int?)command.Data["activeState"]?["booleanVal"]??0)!=0;runtime.SetActive(target,state);}else runtime.Missing("SetActive:"+command.sourceId);
                            break;
                        case "CallMethod":
                            if((long?)command.Data["targetObject"]?["fileID"]==0||string.IsNullOrEmpty(command.Text("methodName"))){runtime.EventTrace.Add("source-empty-method:"+command.sourceId);break;}
                            runtime.CallObject(command.Object("targetObject"),command.Text("methodName"),null,command.Number("delay"));break;
                        case "Call":
                            var callee=command.Ref("targetBlock") as OriginalBehaviour;
                            if(!callee)
                            {
                                if((long?)command.Data["targetBlock"]?["fileID"]==0){runtime.EventTrace.Add("source-empty-call:"+command.sourceId);if((int?)command.Data["callMode"]==0)yield break;break;}
                                runtime.Missing("Call:"+command.sourceId);break;
                            }
                            string next=callee.Text("blockName");int mode=(int?)command.Data["callMode"]??0;
                            // Original Fungus enum: Stop=0, Continue=1, WaitUntilFinished=2, StopThenCall=3.
                            if(mode==0||mode==3){running.Remove(name);Execute(next);yield break;}
                            if(mode==1){Execute(next);break;}
                            if(running.Contains(next)){runtime.Missing("graph-cycle:"+name+"->"+next);break;}
                            running.Add(next);yield return Run(callee,next,depth+1);CurrentBlock=name;
                            break;
                        case "InvokeMethod":runtime.InvokeSerialized(command);break;
                        default:runtime.Missing("opcode:"+command.sourceType+":"+command.sourceId);yield break;
                    }
                }
                CompletedBlocks.Add(name);
            }
            finally{running.Remove(name);if(CurrentBlock==name)CurrentBlock=null;}
        }
        public void StopGraph(){StopAllCoroutines();running.Clear();CurrentBlock=null;if(runtime.presentation)runtime.presentation.CancelSpeech();}
    }
}
