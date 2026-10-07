using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace SinmaiAlpha.Preview;
public sealed class PreviewSeekHandler:MonoBehaviour,IPointerDownHandler,IPointerUpHandler
{
 public Action<bool> Drag;
 public void OnPointerDown(PointerEventData e)=>Drag?.Invoke(true);
 public void OnPointerUp(PointerEventData e)=>Drag?.Invoke(false);
}
public sealed partial class PreviewControls:MonoBehaviour
{
 public PreviewSettings Values {get;private set;}=new PreviewSettings();
 public float Rate {get;private set;}=1;
 bool pending,stopped,dragging,ready,namespaced,loaded;
 float applyAt=-1,nextLoadingPoll,nextHostPoll;
 double loadingAnimationStart;
 int loadingStage;
 Transform loadingSpinner;
 IntPtr hostWindow;
 string requestPath,endpoint;
 AudioTimeProvider clock;
 PreviewAudio audio;
 StandardControlsAssets assets;
 GameObject ui;
 Slider progress;
 TMP_Text timeLabel;
 Image playIcon;
 readonly HttpClient http=new HttpClient(new HttpClientHandler{UseProxy=false});
 void Awake() {
  if(long.TryParse(Environment.GetEnvironmentVariable("SINMAI_ALPHA_PREVIEW_HOST"),out var hwnd))hostWindow=new IntPtr(hwnd);
  endpoint=Environment.GetEnvironmentVariable("SINMAI_ALPHA_PREVIEW_URL");
  requestPath=Path.Combine(Path.GetDirectoryName(Environment.GetEnvironmentVariable("SINMAI_ALPHA_PREVIEW_WAV")),"request.json");
  audio=GetComponent<PreviewAudio>();
  try{if(File.Exists(PreviewSettings.FilePath))Values=Newtonsoft.Json.JsonConvert.DeserializeObject<PreviewSettings>(File.ReadAllText(PreviewSettings.FilePath))??new PreviewSettings();}catch(Exception e){Debug.LogWarning(e.Message);}
  Values.Validate();
 }
 void Start() {
  try {
   assets=new StandardControlsAssets(Path.GetFullPath(Path.Combine(Application.dataPath,"..","..","UI")));
   ui=assets.Instantiate("main");ui.name="Chart browser controls";DontDestroyOnLoad(ui);
   foreach(var name in new[]{"OverlayMenu","OverlayWindow","RightPanel","Fps","Covers"})Find(name).SetActive(false);
   Find("Panel").SetActive(true);
   Find("LoadingText").SetActive(true);
   Find("LoadingText").GetComponent<TMP_Text>().text=PreviewLoading.Counter(0);
   loadingSpinner=Find("LoadingText/Image").transform;
   GameObject.Find("CanvasButtons")?.SetActive(false);
   if(EventSystem.current==null){var events=new GameObject("Preview EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));DontDestroyOnLoad(events);}
   Bind("TogglePlay",()=>{if(stopped){stopped=false;_=Send(0,0);}else _=Send(clock!=null&&clock.IsPaused?4:3,Current);});
   Bind("Stop",Stop);
   Bind("MenuButton",()=>Find("OverlayMenu").SetActive(!Find("OverlayMenu").activeSelf));
   Bind("FullScreenButton",ToggleFullscreen);
   if(hostWindow!=IntPtr.Zero)foreach(var toggle in UnityEngine.Object.FindObjectsByType<ToggleFullScreen>(FindObjectsSortMode.None))toggle.enabled=false;
   playIcon=Find("TogglePlay/Icon").GetComponent<Image>();
   var oldTime=Find("TimeText").GetComponent<Text>();
   oldTime.enabled=false;
   var digits=new GameObject("Progress digits",typeof(RectTransform),typeof(CanvasRenderer),typeof(TextMeshProUGUI));
   var digitsRect=(RectTransform)digits.transform;digitsRect.SetParent(oldTime.transform,false);
   digitsRect.anchorMin=Vector2.zero;digitsRect.anchorMax=Vector2.one;digitsRect.offsetMin=Vector2.zero;digitsRect.offsetMax=Vector2.zero;
   timeLabel=digits.GetComponent<TextMeshProUGUI>();timeLabel.font=assets.BrowserFont;timeLabel.color=oldTime.color;
   timeLabel.alignment=TextAlignmentOptions.MidlineLeft;timeLabel.enableAutoSizing=false;timeLabel.fontSize=3.1f;
   timeLabel.textWrappingMode=TextWrappingModes.NoWrap;timeLabel.overflowMode=TextOverflowModes.Overflow;timeLabel.raycastTarget=false;
   var track=Find("Panel/TargetFPSSlider");progress=track.GetComponent<Slider>()??track.AddComponent<Slider>();
   progress.fillRect=track.transform.Find("Fill Area/Fill") as RectTransform;progress.handleRect=track.transform.Find("Handle Slide Area/Handle") as RectTransform;progress.targetGraphic=progress.handleRect.GetComponent<Image>();progress.wholeNumbers=false;progress.minValue=0;progress.maxValue=Math.Max(1,audio.Length);
   track.AddComponent<PreviewSeekHandler>().Drag=down=>{dragging=down;if(!down){stopped=false;_=Seek(progress.value);}};
   var speed=Find("SpeedSelect").GetComponent<TMP_Dropdown>();speed.SetValueWithoutNotify(0);speed.onValueChanged.RemoveAllListeners();speed.onValueChanged.AddListener(i=>{Rate=new[]{1f,.75f,.5f,.25f}[Math.Min(3,i)];Apply();});
   const string content="OverlayMenu/SettingsBody/Viewport/Content/";
   Configure(content+"Offset/OffsetSlider",Values.AudioOffsetMs,v=>Values.AudioOffsetMs=v,content+"Offset/OffsetDisplay","0 MS",true);
   Configure(content+"NoteSpeed/NoteSpeedSlider",Values.NoteSpeed,v=>Values.NoteSpeed=v,content+"NoteSpeed/NoteSpeedDisplay","0.#",true,10);
   Configure(content+"TouchSpeed/TouchSpeedSlider",Values.TouchSpeed,v=>Values.TouchSpeed=v,content+"TouchSpeed/TouchSpeedDisplay","0.#",true,10);
   Configure(content+"BGCover/BGCoverSlider",Values.BackgroundDim,v=>Values.BackgroundDim=v,content+"BGCover/BGCoverDisplay","0.##",true,10);
   var combo=Find(content+"Toggles/Combo").GetComponent<Toggle>();combo.SetIsOnWithoutNotify(Values.Combo);combo.onValueChanged.RemoveAllListeners();combo.onValueChanged.AddListener(v=>{Values.Combo=v;Changed(true);});
   var displayRows=new[]{"Offset","NoteSpeed","TouchSpeed","BGCover","Toggles"};var displayOpen=false;
   foreach(var n in displayRows)Find(content+n).SetActive(false);
   Bind(content+"Group1",()=>{displayOpen=!displayOpen;foreach(var n in displayRows)Find(content+n).SetActive(displayOpen);});
   var parent=Find(content.TrimEnd('/')).transform;var rows=new GameObject[5];
   var names=new[]{"BGM","Answer","Judge","Slide","Break"};
   var values=new[]{Values.BgmDb,Values.AnswerDb,Values.JudgeDb,Values.SlideDb,Values.BreakDb};
   Action<float>[] setters={v=>Values.BgmDb=v,v=>Values.AnswerDb=v,v=>Values.JudgeDb=v,v=>Values.SlideDb=v,v=>Values.BreakDb=v};
   for(var i=0;i<rows.Length;i++) {
    var row=assets.Instantiate("volume");row.name=names[i]+"Volume";row.transform.SetParent(parent,false);rows[i]=row;
    row.transform.Find("Text").GetComponent<TMP_Text>().text=names[i];var slider=row.GetComponentInChildren<Slider>(true);var text=row.transform.Find("VolumeDisplay")?.GetComponent<TMP_Text>()??row.GetComponentsInChildren<TMP_Text>(true)[1];
    var setter=setters[i];slider.SetValueWithoutNotify(values[i]);text.text=values[i].ToString("0 dB",CultureInfo.InvariantCulture);slider.onValueChanged.RemoveAllListeners();slider.onValueChanged.AddListener(v=>{setter(v);text.text=v.ToString("0 dB",CultureInfo.InvariantCulture);Changed(false);});row.SetActive(false);
   }
   var volumeOpen=false;Bind(content+"Volume",()=>{volumeOpen=!volumeOpen;foreach(var row in rows)row.SetActive(volumeOpen);});
   // The native viewer uses global GameObject.Find("Background"). Scope the
   // imported UI objects so they cannot shadow the viewer's scene services.
   foreach(var child in ui.GetComponentsInChildren<Transform>(true))if(child!=ui.transform)child.name="Browser."+child.name;
   foreach(var selectable in ui.GetComponentsInChildren<Selectable>(true))selectable.interactable=true;
   namespaced=true;ui.SetActive(true);SetLoading(true);Canvas.ForceUpdateCanvases();
   foreach(var button in UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
    if(!button.transform.IsChildOf(ui.transform))button.gameObject.SetActive(false);
   GameObject.Find("TimeText")?.SetActive(false);GameObject.Find("TimeText (1)")?.SetActive(false);
#if PREVIEW_DIAGNOSTICS
   StartCoroutine(CheckUI());
#endif
   Debug.Log("[Sinmai-Alpha Preview] Original chart-browser UI loaded; buttons="+ui.GetComponentsInChildren<Button>(true).Length);
  }catch(Exception e){Debug.LogError("[Sinmai-Alpha Preview] UI failed: "+e);File.WriteAllText(Path.Combine(Path.GetDirectoryName(requestPath),"ui-error.txt"),e.ToString());}
 }
 [StructLayout(LayoutKind.Sequential)] struct HostRect {public int left,top,right,bottom;}
 [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr window,out HostRect rect);
 [DllImport("user32.dll")] static extern bool PostMessage(IntPtr window,uint message,IntPtr wparam,IntPtr lparam);
 void ToggleFullscreen(){if(hostWindow!=IntPtr.Zero)PostMessage(hostWindow,0x8001,IntPtr.Zero,IntPtr.Zero);else Screen.fullScreen=!Screen.fullScreen;}
 void SetLoading(bool value) {
  if(value)loadingAnimationStart=Time.realtimeSinceStartupAsDouble;
  Find("LoadingText").GetComponent<TMP_Text>().text=loaded?"Loading":PreviewLoading.Counter(loadingStage);
  Find("LoadingText").SetActive(value);
  foreach(var path in new[]{"TogglePlay","Stop","SpeedSelect","Panel/TargetFPSSlider"})Find(path).GetComponent<Selectable>().interactable=!value;
 }
 GameObject Find(string path)=>ui.transform.Find(namespaced?string.Join("/",path.Split('/').Select(p=>"Browser."+p)):path)?.gameObject??throw new InvalidDataException("Browser control missing: "+path);
 void Bind(string path,UnityEngine.Events.UnityAction callback){var button=Find(path).GetComponent<Button>();button.onClick.RemoveAllListeners();button.onClick.AddListener(callback);}
 void Configure(string path,float value,Action<float> setter,string label,string format,bool reload,float units=1) {
  var slider=Find(path).GetComponent<Slider>();var text=Find(label).GetComponent<TMP_Text>();slider.SetValueWithoutNotify(value*units);text.text=value.ToString(format,CultureInfo.InvariantCulture);slider.onValueChanged.RemoveAllListeners();slider.onValueChanged.AddListener(v=>{v/=units;setter(v);text.text=v.ToString(format,CultureInfo.InvariantCulture);Changed(reload);});
 }
 double Current=>stopped?0:Math.Max(0,clock?.AudioTime??0);
 void Update() {
  if(loadingSpinner!=null&&loadingSpinner.gameObject.activeInHierarchy)loadingSpinner.localRotation=Quaternion.Euler(0,0,-PreviewLoading.SpinnerAngle(Time.realtimeSinceStartupAsDouble-loadingAnimationStart));
  if(hostWindow!=IntPtr.Zero&&Input.GetKeyDown(KeyCode.Escape))ToggleFullscreen();
  if(hostWindow!=IntPtr.Zero&&Time.realtimeSinceStartup>=nextHostPoll){
   nextHostPoll=Time.realtimeSinceStartup+.2f;
   if(GetClientRect(hostWindow,out var rect)&&rect.right>0&&rect.bottom>0&&(Screen.width!=rect.right||Screen.height!=rect.bottom))Screen.SetResolution(rect.right,rect.bottom,false);
  }
  if(!loaded&&ui!=null&&Time.realtimeSinceStartup>=nextLoadingPoll){
   nextLoadingPoll=Time.realtimeSinceStartup+.1f;
   try{if(int.TryParse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(requestPath),"loading-stage.txt")),out var step)){loadingStage=step;Find("LoadingText").GetComponent<TMP_Text>().text=PreviewLoading.Counter(step);}}catch(IOException){}
  }
  if(clock==null){var obj=GameObject.Find("AudioTimeProvider");if(obj!=null)clock=obj.GetComponent<AudioTimeProvider>();}
  if(!ready&&clock!=null&&ui!=null){ready=true;File.WriteAllText(Path.Combine(Path.GetDirectoryName(requestPath),"player-ready.txt"),"ready");Debug.Log("[Sinmai-Alpha Preview] First player update");}
  if(!loaded&&ready&&File.Exists(Path.Combine(Path.GetDirectoryName(requestPath),"chart-ready.txt"))) {
   loaded=true;progress.maxValue=Math.Max(1,audio.Length);SetLoading(false);
   StartCoroutine(PublishRenderedFrame());
   Debug.Log("[Sinmai-Alpha Preview] Loaded paused at "+Current);
  }
  if(applyAt>=0&&Time.realtimeSinceStartup>=applyAt&&!pending&&loaded){applyAt=-1;Apply();}
  if(progress!=null&&!dragging)progress.SetValueWithoutNotify((float)Current);
  if(timeLabel!=null)timeLabel.text=TimeSpan.FromSeconds(Current).ToString(@"m\:ss\.ffff");
  if(playIcon!=null)playIcon.sprite=assets.Icon(!loaded||stopped||clock==null||clock.IsPaused?"play_fill":"pause");
 }
 System.Collections.IEnumerator PublishRenderedFrame() {
  Canvas.ForceUpdateCanvases();
  // The host overlay remains visible through resize and the first rendered chart frame.
  yield return new WaitForEndOfFrame();
  yield return new WaitForEndOfFrame();
  File.WriteAllText(Path.Combine(Path.GetDirectoryName(requestPath),"frame-ready.txt"),"rendered");
 }
 void Changed(bool reload){Values.Validate();Directory.CreateDirectory(Path.GetDirectoryName(PreviewSettings.FilePath));File.WriteAllText(PreviewSettings.FilePath,Newtonsoft.Json.JsonConvert.SerializeObject(Values));if(reload)applyAt=Time.realtimeSinceStartup+.35f;}
 async void Apply(){var paused=stopped||clock==null||clock.IsPaused;await Send(paused?8:0,Current);}
 async Task Seek(double time){var paused=clock!=null&&clock.IsPaused;await Send(paused?8:0,time);}
 async void Stop(){stopped=true;await Send(8,0);}
 public static string BuildRequest(string payload,int control,double time,PreviewSettings value,float rate,bool deferStart=false) {
  var json=Newtonsoft.Json.Linq.JObject.Parse(payload);json["control"]=control;json["deferPlaybackStart"]=deferStart;json["outerBackgroundCover"]=1;json["startTime"]=Math.Max(0,time);json["startAt"]=DateTime.Now.Ticks;json["noteSpeed"]=value.NoteSpeed;json["starSpeed"]=0;json["editorPlayMethod"]=0;json["touchSpeed"]=value.TouchSpeed;json["audioSpeed"]=rate;json["innerBackgroundCover"]=value.BackgroundDim;json["comboStatusType"]=value.Combo?1:0;json["showComboInfo"]=true;return json.ToString(Newtonsoft.Json.Formatting.None);
 }
 async Task Send(int control,double time,bool deferStart=false) {
  if(!loaded||pending||!File.Exists(requestPath))return;pending=true;SetLoading(true);
  try {audio?.ResetSounds();var payload=BuildRequest(File.ReadAllText(requestPath),control,time,Values,Rate,deferStart);using var response=await http.PostAsync(endpoint,new StringContent(payload,Encoding.UTF8,"application/json"));var result=await response.Content.ReadAsStringAsync();if(!response.IsSuccessStatusCode||!Regex.IsMatch(result,"\"ok\"\\s*:\\s*true"))throw new IOException(result);}
  catch(Exception e){Debug.LogError("[Sinmai-Alpha Preview] Control failed: "+e);}finally{pending=false;SetLoading(false);}
 }
 void OnDestroy(){http.Dispose();if(ui!=null)Destroy(ui);}
}
