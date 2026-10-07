using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Object=UnityEngine.Object;
namespace SinmaiAlpha.Preview;

// Loads the chart browser's exported UI hierarchy and assets. Layout, sprites,
// font glyphs and Selectable settings belong to the existing browser UI.
internal sealed class StandardControlsAssets
{
 readonly JObject package;
 readonly string folder;
 readonly Dictionary<string,Object> assets=new();
 public StandardControlsAssets(string path) {folder=path;package=JObject.Parse(File.ReadAllText(Path.Combine(path,"controls.json")));}
 static float F(JToken o,string k,float fallback=0)=>o?[k]?.Value<float>()??fallback;
 static Vector2 V2(JToken v)=>new Vector2(F(v,"x"),F(v,"y"));
 static Vector3 V3(JToken v)=>new Vector3(F(v,"x"),F(v,"y"),F(v,"z"));
 static Color C(JToken v)=>new Color(F(v,"r"),F(v,"g"),F(v,"b"),F(v,"a",1));
 public TMP_FontAsset BrowserFont=>Asset("aa3a52cbd4e4a6547afcecee3cfad05e") as TMP_FontAsset;
 public Sprite Icon(string name)=>Asset((string)package["icons"][name]) as Sprite;
 Object Asset(string guid)
 {
  if(string.IsNullOrEmpty(guid)||guid.StartsWith("000000"))return null;
  if(assets.TryGetValue(guid,out var cached))return cached;
  var desc=package["assets"][guid];if(desc==null)return null;
  assets[guid]=null;var kind=(string)desc["kind"];var d=desc["data"];
  Object value=null;
  switch(kind) {
   case "Texture2D": var tex=new Texture2D(2,2,TextureFormat.RGBA32,false);tex.name=(string)desc["name"];tex.LoadImage(File.ReadAllBytes(Path.Combine(folder,(string)desc["file"])));tex.wrapMode=TextureWrapMode.Clamp;value=tex;break;
   case "Sprite":
    var image=Ref(d["m_RD"]["texture"],null) as Texture2D;var r=d["m_RD"]["textureRect"]??d["m_Rect"];var b=d["m_Border"];
    // Vector-exported control icons retain a full 256px viewport in the
    // browser. Reusing only the tight shape bounds removes their padding.
    if(new[]{"play_fill","pause","stop"}.Contains((string)d["m_Name"])) {
     value=Sprite.Create(image,new Rect(0,0,image.width,image.height),new Vector2(.5f,.5f),F(d,"m_PixelsToUnits",100));
     value.name=(string)d["m_Name"];break;
    }
    value=Sprite.Create(image,new Rect(F(r,"x"),F(r,"y"),F(r,"width"),F(r,"height")),V2(d["m_Pivot"]),F(d,"m_PixelsToUnits",100),0,SpriteMeshType.FullRect,new Vector4(F(b,"x"),F(b,"y"),F(b,"z"),F(b,"w")));value.name=(string)d["m_Name"];break;
   case "Shader": value=Shader.Find((string)desc["name"]);break;
   case "Font": value=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");break;
   case "Material":
    var shader=Ref(d["m_Shader"],null) as Shader??Shader.Find("TextMeshPro/Distance Field")??Shader.Find("UI/Default");
    var mat=new Material(shader);mat.name=(string)d["m_Name"];value=mat;assets[guid]=mat;
    var props=d["m_SavedProperties"];
    foreach(var prop in ((JObject)props["m_Floats"]).Properties())if(mat.HasProperty(prop.Name))mat.SetFloat(prop.Name,prop.Value.Value<float>());
    foreach(var prop in ((JObject)props["m_Colors"]).Properties())if(mat.HasProperty(prop.Name))mat.SetColor(prop.Name,C(prop.Value));
    foreach(var prop in ((JObject)props["m_TexEnvs"]).Properties())if(mat.HasProperty(prop.Name)){mat.SetTexture(prop.Name,Ref(prop.Value["m_Texture"],null) as Texture);mat.SetTextureScale(prop.Name,V2(prop.Value["m_Scale"]));mat.SetTextureOffset(prop.Name,V2(prop.Value["m_Offset"]));}
    foreach(var keyword in d["m_ValidKeywords"]??new JArray())mat.EnableKeyword(keyword.Value<string>());break;
   case "TMP_FontAsset":
    var font=ScriptableObject.CreateInstance<TMP_FontAsset>();value=font;assets[guid]=font;
    JsonUtility.FromJsonOverwrite(Resolve(d,null).ToString(),font);BindReferences(font,d,null);font.material=Ref(d["material"],null) as Material;font.name=(string)d["m_Name"];font.atlasPopulationMode=AtlasPopulationMode.Static;font.ReadFontAssetDefinition();break;
  }
  return assets[guid]=value;
 }
 Object Ref(JToken p,Dictionary<string,Object> objects)
 {
  var id=(string)p?["fileID"];if(id==null||id=="0")return null;
  var guid=(string)p["guid"];
  if(guid!=null) {
   if(guid.StartsWith("000000")) {
    if(id=="12800000")return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    return null;
   }
   return Asset(guid);
  }
  return objects!=null&&objects.TryGetValue(id,out var o)?o:null;
 }
 JToken Resolve(JToken token,Dictionary<string,Object> objects)
 {
  if(token is JObject obj) {
   if(obj["fileID"]!=null)return new JObject{{"instanceID",unchecked((int)(Ref(obj,objects)?.GetEntityId().GetRawData()??0))}};
   var result=new JObject();foreach(var p in obj.Properties()) {
    if(p.Name=="m_Script"||p.Name=="m_GameObject"||p.Name=="m_CorrespondingSourceObject"||p.Name=="m_PrefabInstance"||p.Name=="m_PrefabAsset")continue;
    result[p.Name]=p.Name=="m_PersistentCalls"?new JObject{{"m_Calls",new JArray()}}:Resolve(p.Value,objects);
   }return result;
  }
  if(token is JArray arr)return new JArray(arr.Select(v=>Resolve(v,objects)));
  return token.DeepClone();
 }
 // Entity IDs changed in Unity 6.6. Restore object references directly after
 // ordinary serialized values, rather than depending on the old instanceID JSON.
 static FieldInfo Field(System.Type type,string name) {
  for(var t=type;t!=null;t=t.BaseType){var f=t.GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly);if(f!=null)return f;}return null;
 }
 void BindReferences(object owner,JToken values,Dictionary<string,Object> objects) {
  if(owner==null||!(values is JObject data))return;
  foreach(var prop in data.Properties()) {
   if(prop.Name=="m_GameObject"||prop.Name=="m_Script"||prop.Name=="m_CorrespondingSourceObject"||prop.Name=="m_PrefabInstance"||prop.Name=="m_PrefabAsset")continue;
   var field=Field(owner.GetType(),prop.Name);if(field==null)continue;
   if(prop.Value is JObject pointer&&pointer["fileID"]!=null) {
    var reference=Ref(pointer,objects);if(reference==null||field.FieldType.IsInstanceOfType(reference))field.SetValue(owner,reference);
   }else if(prop.Value is JArray arr&&field.GetValue(owner) is IList list) {
    for(var i=0;i<Math.Min(arr.Count,list.Count);i++)if(arr[i] is JObject ptr&&ptr["fileID"]!=null)list[i]=Ref(ptr,objects);else BindReferences(list[i],arr[i],objects);
   }else if(prop.Value is JObject) {
    var nested=field.GetValue(owner);BindReferences(nested,prop.Value,objects);if(nested!=null&&field.FieldType.IsValueType)field.SetValue(owner,nested);
   }
  }
 }
 public GameObject Instantiate(string name)
 {
  var records=(JArray)package[name];var objects=new Dictionary<string,Object>();
  foreach(var obj in records.Where(o=>(string)o["kind"]=="GameObject")) {
   var d=obj["data"];var go=new GameObject((string)d["m_Name"],typeof(RectTransform));go.layer=5;go.SetActive(false);objects[(string)obj["id"]]=go;
  }
  foreach(var obj in records.Where(o=>(string)o["kind"]!="GameObject")) {
   var d=obj["data"];var go=Ref(d["m_GameObject"],objects) as GameObject;if(go==null)continue;
   var kind=(string)obj["kind"];Object c=null;
   if(kind=="RectTransform"||kind=="Transform")c=go.transform;
   else {
    var type=kind.StartsWith("TMPro.")?typeof(TMP_Text).Assembly.GetType(kind):kind.StartsWith("UnityEngine.")?typeof(Button).Assembly.GetType(kind):AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("UnityEngine."+kind,false)).FirstOrDefault(t=>t!=null);
    if(type!=null)c=go.GetComponent(type)??go.AddComponent(type);
   }
   if(c!=null)objects[(string)obj["id"]]=c;
  }
  GameObject root=null;
  foreach(var obj in records.Where(o=>(string)o["kind"]=="RectTransform"||(string)o["kind"]=="Transform")) {
   var d=obj["data"];var t=(RectTransform)objects[(string)obj["id"]];var parent=Ref(d["m_Father"],objects) as Transform;
   t.SetParent(parent,false);if(parent==null)root=t.gameObject;
   t.localPosition=V3(d["m_LocalPosition"]);var q=d["m_LocalRotation"];t.localRotation=new Quaternion(F(q,"x"),F(q,"y"),F(q,"z"),F(q,"w",1));t.localScale=V3(d["m_LocalScale"]);
   if(d["m_AnchorMin"]!=null){t.anchorMin=V2(d["m_AnchorMin"]);t.anchorMax=V2(d["m_AnchorMax"]);t.pivot=V2(d["m_Pivot"]);t.sizeDelta=V2(d["m_SizeDelta"]);t.anchoredPosition=V2(d["m_AnchoredPosition"]);}
  }
  foreach(var obj in records)if(objects.TryGetValue((string)obj["id"],out var target)) {
   if(target is MonoBehaviour){JsonUtility.FromJsonOverwrite(Resolve(obj["data"],objects).ToString(),target);BindReferences(target,obj["data"],objects);}
   if(target is RectTransform rt) {var children=(JArray)obj["data"]["m_Children"];if(children!=null)for(int i=0;i<children.Count;i++)(Ref(children[i],objects) as Transform)?.SetSiblingIndex(i);}
  }
  foreach(var obj in records.Where(o=>(string)o["kind"]=="GameObject")) {var go=(GameObject)objects[(string)obj["id"]];if(go!=root)go.SetActive(obj["data"]["m_IsActive"].Value<int>()!=0);}
  if(root.TryGetComponent<Canvas>(out var canvas)){canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=10000;canvas.additionalShaderChannels=AdditionalCanvasShaderChannels.TexCoord1|AdditionalCanvasShaderChannels.Normal|AdditionalCanvasShaderChannels.Tangent;}
  return root;
 }
}
