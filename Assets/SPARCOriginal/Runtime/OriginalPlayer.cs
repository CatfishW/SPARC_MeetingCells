using System;
using System.Linq;
using Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

namespace Sparc.Original
{
    /// <summary>New input/movement driver for the original player body, rig, controller and cameras.</summary>
    public sealed class OriginalPlayer:MonoBehaviour
    {
        public OriginalRuntime runtime;
        public bool InputLocked,ContinuousLook=true;
        public Vector2 TouchMove,TouchLook;
        public float InteractionRange=2;
        public Animator animator;
        public GameObject firstPerson,thirdPerson,model;
        public float speed=4,jumpForce=0.66f;
        Rigidbody body;CapsuleCollider capsule;OriginalBehaviour source;Vector2 movement;Vector3 crowdVelocity;float pitch,pendingYaw;float lastJump=-10;
        bool modelRequested,cameraListener;
        Quaternion firstBase,thirdBase;Vector3 previous;
        public float DistanceTravelled {get;private set;}
        public bool Walking {get;private set;}
        float groundedAt=-10;
        public bool ThirdPerson=>thirdPerson&&thirdPerson.activeSelf;
        public bool CanControl=>runtime&&runtime.Started&&!runtime.ShuttingDown&&!runtime.HandoffStarted&&!InputLocked&&!runtime.CinematicActive&&!runtime.presentation.IsSpeaking&&!runtime.activities.IsOpen&&!runtime.activities.IsWorksheetOpen&&(!runtime.activityUi||!runtime.activityUi.IsTaskPanelOpen)&&!Typing();
        public void ReleaseTouchInput(){TouchMove=Vector2.zero;TouchLook=Vector2.zero;movement=Vector2.zero;pendingYaw=0;}
        public Vector3 CrowdVelocity=>crowdVelocity;
        public void ApplyCrowdImpulse(Vector3 impulse)
        {
            if(!CanControl||!body||body.isKinematic)return;impulse.y=0;
            crowdVelocity=Vector3.ClampMagnitude(crowdVelocity+impulse/Mathf.Max(.01f,body.mass),6f);
        }
#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern void SPARC_BrowserGameplay(int active);
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern int SPARC_BrowserPointer();
#endif
        public void Initialize(OriginalBehaviour configuration)
        {
            source=configuration;speed=source.Number("speedMovements",4);jumpForce=source.Number("jumpForce",0.66f);
            body=GetComponent<Rigidbody>();capsule=GetComponent<CapsuleCollider>();if(body){body.constraints|=RigidbodyConstraints.FreezeRotation;body.interpolation=RigidbodyInterpolation.Interpolate;}
            var transforms=GetComponentsInChildren<Transform>(true);
            firstPerson=transforms.FirstOrDefault(t=>t.name=="Main Player Camera")?.gameObject;
            thirdPerson=transforms.FirstOrDefault(t=>t.name=="TPCamera")?.gameObject;
            // Both native obstacle solvers otherwise treat our Default-layer capsule
            // as an occluder. Keep their world masks and authored rig unchanged.
            if(thirdPerson&&!string.IsNullOrEmpty(tag)&&!CompareTag("Untagged"))
            {
                var camera=thirdPerson.GetComponent<CinemachineVirtualCamera>();
                var follow=camera?camera.GetCinemachineComponent<Cinemachine3rdPersonFollow>():null;
                if(follow)follow.IgnoreTag=tag;
                var avoidance=thirdPerson.GetComponent<CinemachineCollider>();
                if(avoidance)avoidance.m_IgnoreTag=tag;
            }
            model=runtime.First("NPCLookAtManager")?.Object("PlayerModel")??transforms.FirstOrDefault(t=>t.name=="Player")?.gameObject;
            // The original player base activates this same model in Awake. Retain that startup
            // behavior; enabling only Animator leaves every original locomotion clip invisible.
            if(model)model.SetActive(true);
            if(firstPerson)firstPerson.SetActive(true);if(thirdPerson)thirdPerson.SetActive(false);
            // Do not discover the disabled source Animator through Animator.parameters.
            // The Editor exposes that cache before activation; the WebGL player may not.
            // Bind the actual original controller identity first, then enable its native rig.
            var candidates=(model?model.GetComponentsInChildren<Animator>(true):GetComponentsInChildren<Animator>(true));
            animator=candidates.FirstOrDefault(a=>a.runtimeAnimatorController&&a.runtimeAnimatorController.name=="Customized_Character_Anime")
                ??candidates.FirstOrDefault(a=>a.runtimeAnimatorController);
            if(!animator)runtime.Missing("original-player-animator-binding");
            if(animator){animator.enabled=true;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.applyRootMotion=false;}
            if(firstPerson)firstBase=firstPerson.transform.localRotation;if(thirdPerson)thirdBase=thirdPerson.transform.localRotation;previous=transform.position;
            if(!cameraListener){CinemachineCore.CameraUpdatedEvent.AddListener(CameraUpdated);cameraListener=true;}
            foreach(var b in FindObjectsByType<Button>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {if(b.name=="PerspectiveSwitchBtn")b.onClick.AddListener(TogglePerspective);else if(b.name=="ViewResetBtn")b.onClick.AddListener(ResetView);else if(b.name=="JumpBtn")b.onClick.AddListener(Jump);}
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLInput.captureAllKeyboardInput=false;WebGLInput.stickyCursorLock=false;
#endif
        }
        public void TogglePerspective(){if(!firstPerson||!thirdPerson||(runtime&&runtime.HandoffStarted))return;bool next=!thirdPerson.activeSelf;thirdPerson.SetActive(next);firstPerson.SetActive(!next);ShowModel(next);}
        public void ResetView(){if(runtime&&runtime.HandoffStarted)return;pitch=0;if(firstPerson)firstPerson.transform.localRotation=firstBase;if(thirdPerson)thirdPerson.transform.localRotation=thirdBase;}
        public void ShowModel(bool show)
        {
            modelRequested=show;
            int layer=LayerMask.NameToLayer("PlayerModel");if(layer<0)return;
            foreach(var c in FindObjectsByType<Camera>(FindObjectsInactive.Include,FindObjectsSortMode.None))if(show)c.cullingMask|=1<<layer;else c.cullingMask&=~(1<<layer);
            foreach(var brain in FindObjectsByType<CinemachineBrain>(FindObjectsSortMode.None))CameraUpdated(brain);
        }
        void CameraUpdated(CinemachineBrain brain)
        {
            if(!brain||brain.gameObject.scene!=gameObject.scene||!brain.OutputCamera)return;
            int layer=LayerMask.NameToLayer("PlayerModel");if(layer<0)return;
            var camera=brain.OutputCamera;
            // The native blend and world avoidance can put the output camera through
            // the avatar. Keep the rig active; hide only its render layer until clear.
            if(modelRequested&&(!runtime||!runtime.activityUi||!runtime.activityUi.IsTaskPanelOpen)&&!CameraNearBody(camera.transform.position))camera.cullingMask|=1<<layer;
            else camera.cullingMask&=~(1<<layer);
        }
        bool CameraNearBody(Vector3 position)
        {
            if(!capsule)return false;
            var scale=capsule.transform.lossyScale;scale=new Vector3(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z));
            int axis=capsule.direction;var localAxis=axis==0?Vector3.right:axis==1?Vector3.up:Vector3.forward;
            float radius=capsule.radius*(axis==0?Mathf.Max(scale.y,scale.z):axis==1?Mathf.Max(scale.x,scale.z):Mathf.Max(scale.x,scale.y));
            float halfSegment=Mathf.Max(0,capsule.height*scale[axis]*.5f-radius);
            var center=capsule.transform.TransformPoint(capsule.center);var direction=capsule.transform.TransformDirection(localAxis).normalized;
            var closest=center+direction*Mathf.Clamp(Vector3.Dot(position-center,direction),-halfSegment,halfSegment);
            return Vector3.Distance(position,closest)<=radius+.85f;
        }
        public void Jump()
        {
            if(!body||!CanControl||Time.time-lastJump<source.Number("jumpCooldown",0.9f))return;
            var collider=GetComponent<Collider>();float h=collider?collider.bounds.extents.y:0.5f;
            if(Time.time-groundedAt>0.12f&&!Physics.RaycastAll(transform.position+Vector3.up*0.02f,Vector3.down,h+0.2f,~0,QueryTriggerInteraction.Ignore).Any(hit=>!hit.transform.IsChildOf(transform)&&hit.normal.y>0.5f))return;
            lastJump=Time.time;groundedAt=-10;if(animator){animator.SetTrigger("Jump");animator.SetBool("Grounded",false);}body.AddForce(Vector3.up*jumpForce,ForceMode.Impulse);
        }
        bool Typing()
        {var selected=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;return selected&&(selected.GetComponent<TMP_InputField>()||selected.GetComponent<InputField>());}
        void Update()
        {
            if(!runtime)return;bool available=CanControl;
#if UNITY_WEBGL && !UNITY_EDITOR
            SPARC_BrowserGameplay(available?1:0);
#endif
            movement=available?TouchMove:Vector2.zero;
            var k=Keyboard.current;var m=Mouse.current;
            if(available&&k!=null)
            {
                movement+=new Vector2((k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0),(k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0));
                if(k.vKey.wasPressedThisFrame)TogglePerspective();if(k.spaceKey.wasPressedThisFrame)Jump();
            }
            if(available&&Gamepad.current!=null){movement+=Gamepad.current.leftStick.ReadValue();if(Gamepad.current.buttonSouth.wasPressedThisFrame)Jump();}
            bool look=m!=null&&m.rightButton.isPressed;
#if UNITY_WEBGL && !UNITY_EDITOR
            look|=SPARC_BrowserPointer()!=0;
#else
            look|=Cursor.lockState==CursorLockMode.Locked;
#endif
            if(available&&look&&m!=null)
            {
                var delta=m.delta.ReadValue()*source.Number("keyboardLookSpeed",1.15f)*0.12f;
                pendingYaw+=delta.x;pitch=Mathf.Clamp(pitch-delta.y,-70,75);
                if(firstPerson)firstPerson.transform.localRotation=firstBase*Quaternion.Euler(pitch,0,0);
                if(thirdPerson)thirdPerson.transform.localRotation=thirdBase*Quaternion.Euler(pitch,0,0);
            }
            if(available&&TouchLook.sqrMagnitude>0)
            {var delta=TouchLook;pendingYaw+=delta.x;pitch=Mathf.Clamp(pitch-delta.y,-70,75);if(firstPerson)firstPerson.transform.localRotation=firstBase*Quaternion.Euler(pitch,0,0);if(thirdPerson)thirdPerson.transform.localRotation=thirdBase*Quaternion.Euler(pitch,0,0);}
            TouchLook=Vector2.zero;
            movement=Vector2.ClampMagnitude(movement,1);
            float travelled=Vector3.Distance(previous,transform.position);if(travelled<2)DistanceTravelled+=travelled;previous=transform.position;
            Walking=available&&movement.sqrMagnitude>0.01f;
            if(animator){animator.SetBool("IsWalking",Walking);animator.SetFloat("Speed",Walking?movement.magnitude:0);animator.SetBool("Grounded",Time.time-groundedAt<0.12f);}
            if(Walking&&model){var direction=transform.right*movement.x+transform.forward*movement.y;if(direction.sqrMagnitude>0.001f)model.transform.rotation=Quaternion.LookRotation(direction);}
        }
        void FixedUpdate()
        {
            if(!body||!runtime||!runtime.Started)return;
            if(runtime.CinematicActive||runtime.HandoffStarted){pendingYaw=0;crowdVelocity=Vector3.zero;if(!body.isKinematic)body.linearVelocity=Vector3.zero;return;}
            bool available=CanControl;var facing=body.rotation;
            // Interpolation owns the rendered Transform. Accumulate every input delta
            // until physics can rotate its authoritative pose, so sparse touch events
            // and high render rates cannot overwrite or lose earlier turns.
            if(available&&Mathf.Abs(pendingYaw)>.00001f){facing=Quaternion.AngleAxis(pendingYaw,Vector3.up)*facing;body.MoveRotation(facing);}
            pendingYaw=0;
            var direction=available?facing*new Vector3(movement.x,0,movement.y):Vector3.zero;
            var push=available?crowdVelocity:Vector3.zero;
            if(!body.isKinematic)body.linearVelocity=new Vector3(direction.x*speed+push.x,body.linearVelocity.y,direction.z*speed+push.z);
            crowdVelocity*=Mathf.Exp(-4*Time.fixedDeltaTime);
        }
        void OnCollisionStay(Collision contact){if(contact.contacts.Any(c=>c.normal.y>0.5f))groundedAt=Time.time;}
        void OnApplicationFocus(bool focus){if(!focus)ReleaseTouchInput();}
        void OnApplicationPause(bool paused){if(paused)ReleaseTouchInput();}
        void OnDestroy(){if(cameraListener)CinemachineCore.CameraUpdatedEvent.RemoveListener(CameraUpdated);}
        void OnDisable()
        {
            ReleaseTouchInput();crowdVelocity=Vector3.zero;if(animator)animator.SetBool("IsWalking",false);
#if UNITY_WEBGL && !UNITY_EDITOR
            SPARC_BrowserGameplay(0);
#endif
        }
    }
}
