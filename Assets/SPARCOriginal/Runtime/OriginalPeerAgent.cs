using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

namespace Sparc.Original
{
    /// <summary>Reimplements the original free-form peer-agent contract. No local guessed grading.
    /// Provider secrets stay in the same-origin server adapter, never in serialized player data.</summary>
    public sealed class OriginalPeerAgent:MonoBehaviour
    {
        public OriginalRuntime runtime;
        OriginalBehaviour source;UnityWebRequest request;string session;int serial,failures,answers;
        public bool Pending {get;private set;}
        public string Outcome {get;private set;}="";
        public string ServiceState {get;private set;}="idle";
        public int Answers=>answers;public int Failures=>failures;
        void Start()
        {
            source=runtime.First("LLM_Manager_Unified");
            var input=source?source.Component<TMP_InputField>("UserInputField"):null;var panel=input?input.GetComponentInParent<Canvas>(true):null;
            if(panel)foreach(var button in panel.GetComponentsInChildren<UnityEngine.UI.Button>(true).Where(b=>b.name=="Send"))button.onClick.AddListener(Submit);
        }
        OriginalBehaviour Source=>source?source:source=runtime.First("LLM_Manager_Unified");
        void Visible(string field,bool active){var target=Source?Source.Object(field):null;if(target)target.SetActive(active);}
        public bool Handles(OriginalBehaviour sender,string method)
        {
            if(sender.sourceType=="SendUserInput2LLAMA3"&&method=="UpdateUserInput")return true; // Source compatibility receiver never sends the same input twice.
            if(sender.sourceType!="LLM_Manager_Unified"||method!="UpdateAndSendUserInput")return false;
            Submit();return true;
        }
        public void Submit()
        {
            if(Pending||!Source||!runtime.Started||runtime.ShuttingDown||Outcome.Length>0)return;
            var input=Source.Component<TMP_InputField>("UserInputField");string text=input?input.text:"";
            if(string.IsNullOrWhiteSpace(text)||text.Length>2000)return;
            if(answers>5){Display("Sorry, my brain is fried.","A");return;}
            input.text="";Pending=true;ServiceState="waiting";Visible("WaitForResponse",true);Visible("AnswerFieldParent",false);Visible("ContinueButton",false);Visible("CompleteAButton",false);Visible("CompleteBButton",false);
            StartCoroutine(Send(++serial,text));
        }
        IEnumerator Send(int ticket,string text)
        {
            string url;
#if UNITY_WEBGL && !UNITY_EDITOR
            if(!Uri.TryCreate(Application.absoluteURL,UriKind.Absolute,out var page)||page.Scheme is not ("http" or "https")){ApplyFailure(ticket);yield break;}
            url=new Uri(page,"/api/sparc/agent").AbsoluteUri;
#else
            url="http://127.0.0.1:8092/api/sparc/agent";
#endif
            var content=new JObject{["text"]=text,["session"]=session??""};
            using(var operation=new UnityWebRequest(url,"POST"))
            {
                request=operation;operation.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(content.ToString(Formatting.None)));operation.downloadHandler=new DownloadHandlerBuffer();operation.SetRequestHeader("Content-Type","application/json");operation.timeout=Mathf.Clamp((int)Source.Number("responseTimeout",45),1,60);
#if !UNITY_WEBGL || UNITY_EDITOR
                operation.SetRequestHeader("Origin","http://127.0.0.1:8092");
#endif
                yield return operation.SendWebRequest();
                if(ticket!=serial||runtime.ShuttingDown)yield break;
                if(operation.result!=UnityWebRequest.Result.Success){ApplyFailure(ticket);request=null;yield break;}
                JObject response=null;try{if(operation.downloadHandler.data.Length<=8192)response=JObject.Parse(operation.downloadHandler.text);}catch(JsonException){}
                if(response==null||(bool?)response["ok"]!=true||response["text"]?.Type!=JTokenType.String||((string)response["text"]).Length>4096)ApplyFailure(ticket);
                else{session=(string)response["session"];ApplyResponse(ticket,(string)response["text"]);}
                request=null;
            }
        }
        public static string Choice(string text)
        {
            string normalized=" "+Regex.Replace((text??"").ToLowerInvariant(),"[^\\p{L}\\p{Nd}]"," ")+" ";
            foreach(string choice in new[]{"b","a"})
            {
                var signals=new[]{" choose "+choice+" "," option "+choice+" "," answer "+choice+" "," chose "+choice+" "," pick "+choice+" "," selected "+choice+" "," "+choice+" is correct "," "+choice+" is right "," "+choice+" correct "," "+choice+" right "};
                if(signals.Any(normalized.Contains))return choice.ToUpperInvariant();
            }
            return "";
        }
        void Display(string text,string outcome)
        {
            Pending=false;Outcome=outcome;Visible("WaitForResponse",false);Visible("AnswerFieldParent",true);
            var label=Source.Component<TMP_Text>("AnswerField");if(label){label.richText=false;label.text=text;}
            Visible("CompleteAButton",outcome=="A");Visible("CompleteBButton",outcome=="B");Visible("ContinueButton",outcome.Length==0);
        }
        void ApplyResponse(int ticket,string response)
        {if(ticket!=serial||!Pending)return;answers++;failures=Mathf.Max(0,failures-1);ServiceState="response";Display(response,Choice(response));}
        void ApplyFailure(int ticket)
        {
            if(ticket!=serial||!Pending)return;failures++;ServiceState="unavailable";
            if(failures>=(int)Source.Number("maxFail2respond",3)||answers>5)Display("Sorry, my brain is fried.","A");
            else Display("[Error] Feedback service unavailable. Please try again.","");
        }
        void OnDestroy(){serial++;if(request!=null)request.Abort();Pending=false;}
    }
}
