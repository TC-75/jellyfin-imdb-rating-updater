using System.Collections.Generic;
using Jellyfin.Plugin.ImdbRatings.ScheduledTasks;
using MediaBrowser.Common.Api;
using MediaBrowser.Common.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ImdbRatings.Api;

[ApiController]
[Route("Plugins/ImdbRatings/RunHistory")]
[Authorize(Policy = Policies.RequiresElevation)]
public sealed class RunHistoryController : ControllerBase
{
    private readonly RefreshRunHistory _history;

    public RunHistoryController(IApplicationPaths applicationPaths, ILogger<RunHistoryController> logger)
    {
        _history = new RefreshRunHistory(applicationPaths.DataPath, logger);
    }

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public ActionResult<IReadOnlyList<RefreshRun>> GetRunHistory()
        => Ok(_history.Read());
}
