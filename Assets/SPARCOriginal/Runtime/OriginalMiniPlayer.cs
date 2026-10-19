using UnityEngine;
using UnityEngine.InputSystem;
namespace Sparc.Original
{
    /// <summary>Drives the original Rigidbody2D/canvas player with the authored speed and axis limit.</summary>
    public sealed class OriginalMiniPlayer:MonoBehaviour
    {
        public OriginalBehaviour source;public OriginalMiniGame game;
        Rigidbody2D body;RectTransform rect;
        void Awake(){body=GetComponent<Rigidbody2D>();rect=transform as RectTransform;}
        void FixedUpdate()
        {
            if(!source||!game||game.Finished||game.Transitioning){if(body)body.linearVelocity=Vector2.zero;return;}
            var key=Keyboard.current;Vector2 move=Vector2.zero;
            if(key!=null)move=new Vector2((key.dKey.isPressed||key.rightArrowKey.isPressed?1:0)-(key.aKey.isPressed||key.leftArrowKey.isPressed?1:0),(key.wKey.isPressed||key.upArrowKey.isPressed?1:0)-(key.sKey.isPressed||key.downArrowKey.isPressed?1:0));
            if(Gamepad.current!=null)move+=Gamepad.current.leftStick.ReadValue();if(!source.Flag("allowVerticalMovement"))move.y=0;
            move=Vector2.ClampMagnitude(move,1);float scale=Mathf.Max(0.001f,transform.parent.lossyScale.x);Vector2 speed=move*source.Number("_speed",300)*scale;
            if(body)body.linearVelocity=speed;else transform.position+=(Vector3)speed*Time.fixedDeltaTime;
            if(rect&&game.Area){var bounds=game.Area.rect;Vector2 p=game.Area.InverseTransformPoint(rect.position);var half=rect.rect.size*0.5f;p.x=Mathf.Clamp(p.x,bounds.xMin+half.x,bounds.xMax-half.x);if(source.Flag("allowVerticalMovement"))p.y=Mathf.Clamp(p.y,bounds.yMin+half.y,bounds.yMax-half.y);var position=game.Area.TransformPoint(p);rect.position=new Vector3(position.x,position.y,rect.position.z);}
        }
    }
}
