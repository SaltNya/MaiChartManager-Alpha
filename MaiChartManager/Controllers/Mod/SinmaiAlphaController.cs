using MaiChartManager.Services;
using Microsoft.AspNetCore.Mvc;

namespace MaiChartManager.Controllers.Mod;

public record SinmaiAlphaPreferences(bool SuppressNotice);
public record AlphaDifficultySelection(string Theme);
public record AlphaTapInHoldSelection(bool? AllowTapInHold);

[ApiController]
[Route("MaiChartManagerServlet/[action]Api")]
public class SinmaiAlphaController : ControllerBase
{
    public record AlphaDifficultyOption(string Id,string Name,string Color);
    [HttpGet]
    public IEnumerable<AlphaDifficultyOption> GetAlphaDifficultyThemes()=>SinmaiAlphaIntegration.Themes().Select(t=>new AlphaDifficultyOption(t.Id,t.Name,t.Color));
    [HttpGet("/MaiChartManagerServlet/[action]Api/{assetDir}/{id:int}/{level:int}")]
    public ActionResult<AlphaTapInHoldSelection> GetAlphaTapInHold([FromServices] StaticSettings settings, string assetDir, int id, int level)
    {
        var music = settings.GetMusic(id, assetDir);
        if (music == null) return NotFound();
        if (level is < 0 or > 4 || music.Id >= 100000) return BadRequest(AlphaText.Get("AlphaNativeUtage"));
        try { return new AlphaTapInHoldSelection(SinmaiAlphaIntegration.GetTapInHold(SinmaiAlphaIntegration.ChartPath(music, level), level)); }
        catch (ArgumentException error) { return BadRequest(error.Message); }
    }
    [HttpPut("/MaiChartManagerServlet/[action]Api/{assetDir}/{id:int}/{level:int}")]
    public IActionResult SetAlphaTapInHold([FromServices] StaticSettings settings, string assetDir, int id, int level, [FromBody] AlphaTapInHoldSelection selection)
    {
        if (!SinmaiAlphaIntegration.Status().Installed) return BadRequest(AlphaText.Get("AlphaNotInstalled"));
        var music = settings.GetMusic(id, assetDir);
        if (music == null) return NotFound();
        if (level is < 0 or > 4 || music.Id >= 100000) return BadRequest(AlphaText.Get("AlphaNativeUtage"));
        try { SinmaiAlphaIntegration.SetTapInHold(SinmaiAlphaIntegration.ChartPath(music, level), level, selection.AllowTapInHold); return NoContent(); }
        catch (ArgumentException error) { return BadRequest(error.Message); }
    }
    [HttpGet] public SinmaiAlphaStatus GetSinmaiAlphaStatus() => SinmaiAlphaIntegration.Status();
    [HttpPut] public void SetSinmaiAlphaPreferences([FromBody] SinmaiAlphaPreferences preferences)
    {
        StaticSettings.Config.SuppressSinmaiAlphaNotice = preferences.SuppressNotice;
        StaticSettings.Config.Save();
    }
    [HttpGet("/MaiChartManagerServlet/[action]Api/{assetDir}/{id:int}/{level:int}")]
    public ActionResult<AlphaDifficultySelection> GetAlphaDifficulty([FromServices] StaticSettings settings, string assetDir, int id, int level)
    {
        var music = settings.GetMusic(id, assetDir);
        if (music == null) return NotFound();
        if (level is < 0 or > 4 || music.Id >= 100000) return BadRequest(AlphaText.Get("AlphaUtageThemeUnsupported"));
        try { return new AlphaDifficultySelection(SinmaiAlphaIntegration.GetTheme(SinmaiAlphaIntegration.ChartPath(music, level), level)); }
        catch (ArgumentException e) { return BadRequest(e.Message); }
    }
    [HttpPut("/MaiChartManagerServlet/[action]Api/{assetDir}/{id:int}/{level:int}")]
    public IActionResult SetAlphaDifficulty([FromServices] StaticSettings settings, string assetDir, int id, int level, [FromBody] AlphaDifficultySelection selection)
    {
        var music = settings.GetMusic(id, assetDir);
        if (music == null) return NotFound();
        if (level is < 0 or > 4 || music.Id >= 100000) return BadRequest(AlphaText.Get("AlphaUtageThemeUnsupported"));
        try { SinmaiAlphaIntegration.SetTheme(SinmaiAlphaIntegration.ChartPath(music, level), selection.Theme, level); return NoContent(); }
        catch (ArgumentException e) { return BadRequest(e.Message); }
    }
}
