using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Scripting;

namespace Sparc.Original
{
    [Serializable] public sealed class OriginalReference { public string key; public UnityEngine.Object target; }
    /// <summary>New implementation boundary retaining the original component identity and serialized data.</summary>
    [Preserve] public sealed class OriginalBehaviour : MonoBehaviour
    {
        public string sourceType, sourceId, sourceScope;
        [TextArea] public string configJson;
        public OriginalReference[] sourceReferences=Array.Empty<OriginalReference>();
        JObject config;Dictionary<string,UnityEngine.Object> references;
        public JObject Data => config ??= JObject.Parse(string.IsNullOrEmpty(configJson)?"{}":configJson);
        public string Text(string key,string fallback="")=>Data[key]?.Type==JTokenType.Null?fallback:(string)Data[key]??fallback;
        public float Number(string key,float fallback=0)=>Data[key]?.Type is JTokenType.Integer or JTokenType.Float?(float)Data[key]:fallback;
        public bool Flag(string key,bool fallback=false)=>Data[key]==null?fallback:Data[key].Type==JTokenType.Boolean?(bool)Data[key]:Data[key].Type==JTokenType.Integer?(int)Data[key]!=0:fallback;
        public UnityEngine.Object Resolve(JToken value)
        {
            if(value is not JObject obj || (string)obj["fileID"]=="0")return null;
            references ??= BuildReferences();return references.TryGetValue(Key(obj),out var result)?result:null;
        }
        Dictionary<string,UnityEngine.Object> BuildReferences(){var map=new Dictionary<string,UnityEngine.Object>();foreach(var r in sourceReferences??Array.Empty<OriginalReference>())map[r.key]=r.target;return map;}
        public static string Key(JToken obj)=>((string)obj?["guid"]??"")+":"+((string)obj?["fileID"]??"0");
        public UnityEngine.Object Ref(string name)=>Resolve(Data[name]);
        public GameObject Object(string key)=>AsGameObject(Ref(key));
        public static GameObject AsGameObject(UnityEngine.Object value)=>value is GameObject go?go:value is Component c?c.gameObject:null;
        public T Component<T>(string key) where T:Component{var v=Ref(key);return v is T t?t:AsGameObject(v)?.GetComponent<T>();}
        void OnEnable(){if(OriginalRuntime.Instance)OriginalRuntime.Instance.Activated(this);}
        void OnDisable(){if(OriginalRuntime.Instance)OriginalRuntime.Instance.Deactivated(this);}
        void OnDestroy(){if(OriginalRuntime.Instance)OriginalRuntime.Instance.Destroyed(this);}
        void OnTriggerEnter(Collider other){if(OriginalRuntime.Instance)OriginalRuntime.Instance.Trigger(this,other,true);}
        void OnTriggerExit(Collider other){if(OriginalRuntime.Instance)OriginalRuntime.Instance.Trigger(this,other,false);}
        public void Dispatch(string method,object argument=null){if(OriginalRuntime.Instance)OriginalRuntime.Instance.Call(this,method,argument);}
        public void ExecuteBlock()=>Dispatch("ExecuteBlock");
        public void ExecuteBlock(string value)=>Dispatch("ExecuteBlock",value);
        public void ChangeExecuteBlock(string value)=>Dispatch("ChangeExecuteBlock",value);
        public void StartTalk()=>Dispatch("StartTalk");
        public void StartObserve()=>Dispatch("StartObserve");
        public void OnInteractionEnd()=>Dispatch("OnInteractionEnd");
        public void OnEitherButtonClicked()=>Dispatch("OnEitherButtonClicked");
        public void SuppressInteractionUi()=>Dispatch("SuppressInteractionUi",true);
        public void SuppressInteractionUi(bool value)=>Dispatch("SuppressInteractionUi",value);
        public void ReleaseInteractionUi()=>Dispatch("ReleaseInteractionUi",true);
        public void ReleaseInteractionUi(bool value)=>Dispatch("ReleaseInteractionUi",value);
        public void EnableDrawing()=>Dispatch("EnableDrawing");
        public void DisableDrawing()=>Dispatch("DisableDrawing");
        public void Allow2Talk2Player()=>Dispatch("Allow2Talk2Player");
        public void DontAllow2Talk2Player()=>Dispatch("DontAllow2Talk2Player");
        public void DontAllow2Talk2Player(float value)=>Dispatch("DontAllow2Talk2Player",value);
        public void AddPlayerModelToCullingMask()=>Dispatch("AddPlayerModelToCullingMask");
        public void CompleteQuestion()=>Dispatch("CompleteQuestion");
        public void ContinueAfterWin()=>Dispatch("ContinueAfterWin");
        public void ContinueAfterLoss()=>Dispatch("ContinueAfterLoss");
        public void OnExitButton()=>Dispatch("OnExitButton");
        public void NextPage()=>Dispatch("NextPage");
        public void PrevPage()=>Dispatch("PrevPage");
        public void Submit()=>Dispatch("Submit");
        public void DoJump()=>Dispatch("DoJump");
        public void BeginDrag()=>Dispatch("BeginDrag");
        public void EndDrag()=>Dispatch("EndDrag");
        public void OnMove(Vector2 value)=>Dispatch("OnMove",value);
        public void OnValueChanged(bool value)=>Dispatch("OnValueChanged",value);
        public void OnValueChanged(float value)=>Dispatch("OnValueChanged",value);
        public void OnCancelButton()=>Dispatch("OnCancelButton");
        public void OnConfirmButton()=>Dispatch("OnConfirmButton");
        public void OnReturnButtonClicked()=>Dispatch("OnReturnButtonClicked");
        public void SendClicked()=>Dispatch("SendClicked");
        public void UpdateAndSendUserInput()=>Dispatch("UpdateAndSendUserInput");
        public void UpdateUserInput(string value)=>Dispatch("UpdateUserInput",value);
        public void UpdateUserInput()=>Dispatch("UpdateUserInput");
        public bool ContinuousRightController{set=>Dispatch("set_ContinuousRightController",value);}
        public void PlayPunchSound(){if(sourceType=="WBCAnimation")Dispatch("PlayPunchSound");}
        public void PlayEffect_Pos1()=>Dispatch("PlayEffect_Pos1");
        public void PlayEffect_Pos2()=>Dispatch("PlayEffect_Pos2");
        public void playPunchEffect(){if(sourceType=="WBCAnimation")Dispatch("playPunchEffect");}
        public void WBC_Punch()=>Dispatch("WBC_Punch");
        public void WBC_Wave()=>Dispatch("WBC_Wave");
        public void OnPlayerLeave()=>Dispatch("OnPlayerLeave");
    }
}
