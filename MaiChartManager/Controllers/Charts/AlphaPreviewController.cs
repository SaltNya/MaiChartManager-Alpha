using MaiChartManager.Services;
using Microsoft.AspNetCore.Mvc;
namespace MaiChartManager.Controllers.Charts;

[ApiController]
[Route("MaiChartManagerServlet/[action]Api")]
public class AlphaPreviewController(StaticSettings settings, AlphaPreviewService preview) : ControllerBase
{
    [HttpPost("/MaiChartManagerServlet/[action]Api/{assetDir}/{id:int}/{level:int}")]
    public async Task<ActionResult<AlphaPreviewSession>> StartAlphaPreview(string assetDir,int id,int level,[FromBody] AlphaPreviewOptions options,[FromQuery] string? side=null)
    {
        var music=settings.GetMusic(id,assetDir); if(music==null)return NotFound();
        try { return await preview.Start(music,level,side,options,HttpContext.RequestAborted); }
        catch(Exception e) when(e is ArgumentException or IOException or InvalidOperationException or TimeoutException) { return BadRequest(e.Message); }
    }
    [HttpPost("/MaiChartManagerServlet/[action]Api/{session}")]
    public Task<AlphaPreviewSession> StopAlphaPreview(string session)=>preview.Stop(session);
    [HttpGet("/MaiChartManagerServlet/[action]Api/{session}")]
    public AlphaPreviewSession GetAlphaPreviewState(string session)=>preview.State(session);
}
