namespace SinmaiAlpha.Preview;
// Shared by the window and player; each stage begins with actual work, not a delay.
public static class PreviewLoading
{
 public const int Total=7;
 // Ordinary browser: Image.controller -> New Animation.anim (9565121a933521349a26cbd7c11068ab).
 // Loop 1s, Z 0 -> -360, both tangents 0, unweighted cubic Hermite.
 // Return screen-clockwise degrees; Unity's Z axis has the opposite sign.
 public static float SpinnerAngle(double elapsedSeconds) {
  var t=elapsedSeconds-System.Math.Floor(elapsedSeconds);
  return (float)(360*t*t*(3-2*t));
 }
 public static string Label(int stage)=>stage switch {
  0=>"Opening preview", 1=>"Starting player", 2=>"Reading chart",
  3=>"Preparing media", 4=>"Parsing notes and effects", 5=>"Loading audio",
  6=>"Building chart scene", 7=>"Drawing first frame", _=>"Loading"
 };
 public static string Counter(int stage)=>$"Loading ({System.Math.Max(0,System.Math.Min(Total,stage))}/{Total})";
}
