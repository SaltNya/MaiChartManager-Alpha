#if PREVIEW_DIAGNOSTICS
using System;
using System.IO;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
namespace SinmaiAlpha.Preview;
public sealed partial class PreviewControls
{
 System.Collections.IEnumerator CheckUI()
 {
  var output=Environment.GetEnvironmentVariable("SINMAI_ALPHA_PREVIEW_DIAGNOSTICS");if(string.IsNullOrEmpty(output))yield break;
  // Compare the shared evaluator with Unity's evaluator of the exported browser clip.
  var referenceCurve=new AnimationCurve(new Keyframe(0,0,0,0),new Keyframe(1,-360,0,0));
  for(var sample=0;sample<256;sample++) {
   var seconds=sample/64.0;var phase=(float)(seconds-Math.Floor(seconds));
   if(Math.Abs(referenceCurve.Evaluate(phase)+PreviewLoading.SpinnerAngle(seconds))>.001f)
    throw new Exception("Spinner differs from the original browser animation at "+seconds);
  }
  File.WriteAllText(Path.Combine(output,"spinner-curve-check.txt"),"PASS 256 samples / 4 cycles match the original browser animation evaluated by Unity; clockwise, 1 second, zero endpoint tangents.");
  yield return null;
  ScreenCapture.CaptureScreenshot(Path.Combine(output,"alpha-loading.png"));
  while(!loaded)yield return null;
  var pausedTime=clock.AudioTime;
  yield return new WaitForSecondsRealtime(2);
  if(!clock.IsPaused||clock.PlaybackStarted||Math.Abs(clock.AudioTime-pausedTime)>.001f)throw new Exception("Initial preview did not stay paused");
  if(GetComponent<AudioSource>().isPlaying)throw new Exception("Initial preview played audio");
  if(timeLabel.font.name!="Minimoon SDF")throw new Exception("Progress font differs from browser");
  timeLabel.ForceMeshUpdate();
  if(timeLabel.renderedHeight*ui.GetComponent<Canvas>().scaleFactor>30||timeLabel.textInfo.lineCount!=1)throw new Exception("Progress digits are too large or wrap");
  Debug.Log("UI CHECK initial paused, no audio, Minimoon font; time="+clock.AudioTime);
  foreach(var control in ui.GetComponentsInChildren<UnityEngine.UI.Selectable>(true))if(!control.interactable)throw new Exception("Browser control disabled: "+control.name);
  var button=Find("TogglePlay").GetComponent<Button>();
  var point=UnityEngine.RectTransformUtility.WorldToScreenPoint(null,button.transform.position);
  var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
  UnityEngine.EventSystems.EventSystem.current.RaycastAll(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=point},hits);
  if(hits.Count==0||hits[0].gameObject.GetComponentInParent<Button>()!=button)throw new Exception("Play button is not pointer-accessible");
  Debug.Log("UI CHECK actual button raycast and interactable state passed");
  ScreenCapture.CaptureScreenshot(Path.Combine(output,"alpha-controls.png"));yield return new WaitForSecondsRealtime(1);
  Find("MenuButton").GetComponent<Button>().onClick.Invoke();Find("OverlayMenu/SettingsBody/Viewport/Content/Group1").GetComponent<Button>().onClick.Invoke();
  yield return new WaitForSecondsRealtime(1);ScreenCapture.CaptureScreenshot(Path.Combine(output,"alpha-display.png"));yield return new WaitForSecondsRealtime(1);
  Find("OverlayMenu/SettingsBody/Viewport/Content/Group1").GetComponent<Button>().onClick.Invoke();Find("OverlayMenu/SettingsBody/Viewport/Content/Volume").GetComponent<Button>().onClick.Invoke();
  yield return new WaitForSecondsRealtime(1);ScreenCapture.CaptureScreenshot(Path.Combine(output,"alpha-volume.png"));yield return new WaitForSecondsRealtime(1);
  Find("MenuButton").GetComponent<Button>().onClick.Invoke();
  Find("TogglePlay").GetComponent<Button>().onClick.Invoke();while(pending)yield return null;
  if(clock.IsPaused)throw new Exception("Manual first play failed");
  Find("TogglePlay").GetComponent<Button>().onClick.Invoke();while(pending)yield return null;
  Debug.Log("UI CHECK pause="+clock.IsPaused);if(!clock.IsPaused)throw new Exception("Pause did not pause");
  Find("TogglePlay").GetComponent<Button>().onClick.Invoke();while(pending)yield return null;
  Debug.Log("UI CHECK resume="+!clock.IsPaused);if(clock.IsPaused)throw new Exception("Resume did not resume");
  Find("Stop").GetComponent<Button>().onClick.Invoke();while(pending)yield return null;yield return null;
  Debug.Log("UI CHECK stop time="+clock.AudioTime+" paused="+clock.IsPaused);
  var seek=progress.GetComponent<PreviewSeekHandler>();seek.Drag(true);progress.value=10;seek.Drag(false);while(pending)yield return null;
  Debug.Log("UI CHECK seek time="+clock.AudioTime);
  var note=Find("OverlayMenu/SettingsBody/Viewport/Content/NoteSpeed/NoteSpeedSlider").GetComponent<Slider>();note.value=90;yield return new WaitForSecondsRealtime(1);while(pending)yield return null;
  Debug.Log("UI CHECK note speed="+Values.NoteSpeed+" paused="+clock.IsPaused);if(Values.NoteSpeed!=9)throw new Exception("Browser slider binding failed");
  note.value=70;yield return new WaitForSecondsRealtime(1);while(pending)yield return null;
  var rate=Find("SpeedSelect").GetComponent<TMPro.TMP_Dropdown>();rate.value=1;while(pending)yield return null;
  Debug.Log("UI CHECK rate="+Rate+" clock="+clock.CurrentSpeed);rate.value=0;while(pending)yield return null;
  Find("TogglePlay").GetComponent<Button>().onClick.Invoke();while(pending)yield return null;
  if(hostWindow!=IntPtr.Zero) {
   var originalWidth=Screen.width;var originalHeight=Screen.height;
   Find("FullScreenButton").GetComponent<Button>().onClick.Invoke();yield return new WaitForSecondsRealtime(.7f);
   if(Screen.width==originalWidth&&Screen.height==originalHeight)throw new Exception("Host fullscreen did not resize player");
   Find("FullScreenButton").GetComponent<Button>().onClick.Invoke();yield return new WaitForSecondsRealtime(.7f);
   if(Screen.width!=originalWidth||Screen.height!=originalHeight)throw new Exception("Host fullscreen did not restore player dimensions");
   Debug.Log("UI CHECK embedded fullscreen and restored dimensions "+Screen.width+"x"+Screen.height);
  }
  File.WriteAllText(Path.Combine(output,"alpha-ui-checks.txt"),"PASS original browser buttons: menu/display/volume, pause/resume/stop, seek, note-speed setting, playback rate");
 }
}
#endif
