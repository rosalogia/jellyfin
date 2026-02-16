using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Clips;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Emby.Server.Implementations.ScheduledTasks.Tasks;

/// <summary>
/// Deletes expired clip files.
/// </summary>
public class DeleteExpiredClipsTask : IScheduledTask, IConfigurableScheduledTask
{
    private readonly ILogger<DeleteExpiredClipsTask> _logger;
    private readonly IClipManager _clipManager;
    private readonly ILocalizationManager _localization;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteExpiredClipsTask"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{DeleteExpiredClipsTask}"/> interface.</param>
    /// <param name="clipManager">Instance of the <see cref="IClipManager"/> interface.</param>
    /// <param name="localization">Instance of the <see cref="ILocalizationManager"/> interface.</param>
    public DeleteExpiredClipsTask(
        ILogger<DeleteExpiredClipsTask> logger,
        IClipManager clipManager,
        ILocalizationManager localization)
    {
        _logger = logger;
        _clipManager = clipManager;
        _localization = localization;
    }

    /// <inheritdoc />
    public string Name => "Delete expired clips";

    /// <inheritdoc />
    public string Description => "Deletes clip files that are older than 30 days.";

    /// <inheritdoc />
    public string Category => _localization.GetLocalizedString("TasksMaintenanceCategory");

    /// <inheritdoc />
    public string Key => "DeleteExpiredClips";

    /// <inheritdoc />
    public bool IsHidden => false;

    /// <inheritdoc />
    public bool IsEnabled => true;

    /// <inheritdoc />
    public bool IsLogged => true;

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        yield return new TaskTriggerInfo
        {
            Type = TaskTriggerInfoType.IntervalTrigger,
            IntervalTicks = TimeSpan.FromHours(24).Ticks
        };
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        progress.Report(0);

        await _clipManager.DeleteExpiredClipsAsync(cancellationToken).ConfigureAwait(false);

        progress.Report(100);
    }
}
