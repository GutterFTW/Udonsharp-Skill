// Test adapters only. This file is excluded from Unity import and the shipped world.
using System;
namespace UnityEngine {
 public class Object { public string name;public static void DestroyImmediate(Object value) {} }
 public class GameObject : Object { public bool activeSelf=true; public GameObject(string name="") {} public void SetActive(bool active) {activeSelf=active;} public T AddComponent<T>() where T:Component,new() {return new T {gameObject=this};} public object GetComponent(Type t) {return null;} }
 public class Component:Object {public GameObject gameObject;}
 public class MonoBehaviour:Component {}
 public class HeaderAttribute:Attribute {public HeaderAttribute(string s){}}
 public class TooltipAttribute:Attribute {public TooltipAttribute(string s){}}
 public class DefaultExecutionOrderAttribute:Attribute {public DefaultExecutionOrderAttribute(int i){}}
 public struct Color {public static Color white=>new Color(1,1,1);public float r,g,b,a; public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;} }
 public static class Mathf {public static int RoundToInt(float n)=>(int)Math.Round(n);public static int Clamp(int n,int min,int max)=>Math.Clamp(n,min,max);public static int Min(int a,int b)=>Math.Min(a,b);public static int Max(int a,int b)=>Math.Max(a,b);}
 public static class Time {public static int frameCount=100;public static float realtimeSinceStartup;}
 public static class Debug {public static void Log(object s){} public static void LogWarning(object s){} public static void LogError(object s){} }
 public static class ColorUtility {public static string ToHtmlStringRGB(Color c)=>"FFFFFF";}
}
namespace UdonSharp {
 public enum BehaviourSyncMode{Manual}
 public class UdonBehaviourSyncModeAttribute:Attribute {public UdonBehaviourSyncModeAttribute(BehaviourSyncMode m){}}
 public class UdonSyncedAttribute:Attribute {}
 public class UdonSharpBehaviour:UnityEngine.MonoBehaviour {
  public int SerializationRequests;
  public void RequestSerialization(){SerializationRequests++;}
  public void SendCustomEventDelayedFrames(string name,int delay){}
  public virtual void OnPlayerJoined(VRC.SDKBase.VRCPlayerApi p){}
  public virtual void OnPlayerLeft(VRC.SDKBase.VRCPlayerApi p){}
  public virtual void OnDeserialization(){}
  public virtual void OnOwnershipTransferred(VRC.SDKBase.VRCPlayerApi p){}
  public virtual void OnStringLoadSuccess(VRC.SDK3.StringLoading.IVRCStringDownload r){}
  public virtual void OnStringLoadError(VRC.SDK3.StringLoading.IVRCStringDownload r){}
  public virtual void OnPostSerialization(VRC.Udon.Common.SerializationResult r){}
 }
}
namespace VRC.SDKBase {
 public class VRCPlayerApi {public string displayName;public bool isLocal,isMaster;public bool Valid;public int playerId=>_GetPlayerId(this);public static Func<VRCPlayerApi,int> _GetPlayerId; public void AddToList(){Valid=true;}public void RemoveFromList(){Valid=false;} }
 public static class Utilities {public static bool IsValid(object o)=>o!=null && (o is not VRCPlayerApi p || p.Valid);}
 public static class Networking {
  public static Func<VRCPlayerApi> _LocalPlayer,_GetInstanceOwner;
  public static Func<bool> _IsInstanceOwner,_IsMaster;
  public static Func<VRCPlayerApi,UnityEngine.GameObject,bool> _IsOwner;
  public static Action<VRCPlayerApi,UnityEngine.GameObject> _SetOwner;
  public static VRCPlayerApi LocalPlayer=>_LocalPlayer?.Invoke();public static VRCPlayerApi InstanceOwner=>_GetInstanceOwner?.Invoke();
  public static bool IsInstanceOwner=>_IsInstanceOwner?.Invoke()??false;public static bool IsClogged;
  public static bool IsOwner(UnityEngine.GameObject go)=>_IsOwner?.Invoke(LocalPlayer,go)??false;
  public static void SetOwner(VRCPlayerApi p,UnityEngine.GameObject go)=>_SetOwner?.Invoke(p,go);
 }
 public class VRCUrl {private string value;public VRCUrl(string value){this.value=value;}public string Get()=>value;}
}
namespace VRC.SDK3.StringLoading {
 public interface IVRCStringDownload {string Result{get;}VRC.SDKBase.VRCUrl Url{get;}string Error{get;}int ErrorCode{get;} }
 public static class VRCStringDownloader {public static void LoadUrl(VRC.SDKBase.VRCUrl u,VRC.Udon.Common.Interfaces.IUdonEventReceiver r){} }
}
namespace VRC.Udon {public class UdonBehaviour:VRC.Udon.Common.Interfaces.IUdonEventReceiver {} }
namespace VRC.Udon.Common.Interfaces {public interface IUdonEventReceiver {} }
namespace VRC.Udon.Common {public struct SerializationResult {public bool success;public int byteCount;public SerializationResult(bool success,int byteCount){this.success=success;this.byteCount=byteCount;}} }
public class VipWhitelistUI {
 public VipWhitelistManager manager;public void UpdateRowForName(string n,bool authed){}public void SetDjSystemEnabled(bool e){}public void OnPlayerJoined(VRC.SDKBase.VRCPlayerApi p){}public void OnPlayerLeft(VRC.SDKBase.VRCPlayerApi p){}public void RebuildPlayerList(){}public void UpdateRowTogglesFromAuth(){}
}
namespace NUnit.Framework {
 public class TestAttribute:Attribute {}
 public class SetUpAttribute:Attribute {}
 public class TearDownAttribute:Attribute {}
 [AttributeUsage(AttributeTargets.Method, AllowMultiple=true)]
 public class TestCaseAttribute:Attribute { public object[] Arguments; public TestCaseAttribute(params object[] arguments){Arguments=arguments;} }
 public static class Assert {public static void IsTrue(bool value,string message=null){if(!value)throw new Exception(message??"Expected true");}public static void IsFalse(bool value,string message=null){if(value)throw new Exception(message??"Expected false");} public static void AreEqual(object expected,object actual,string message=null){if(!Equals(expected,actual))throw new Exception(message??$"Expected '{expected}', got '{actual}'");} }
}
