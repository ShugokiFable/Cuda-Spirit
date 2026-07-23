using CudaSpirit.Core.Models;

namespace CudaSpirit.Core.Services.Live;

/// <summary>
/// Supplies the current <see cref="PlayerState"/> to the rest of the app.
///
/// This is deliberately an interface so the *source* of truth is pluggable, while the app and the
/// AI context pipeline stay identical. The shipped implementation is
/// <see cref="ManualLiveStateProvider"/>, which draws from what the user has entered plus data from
/// ToS-safe APIs (market values on owned gear, etc.).
///
/// It intentionally does NOT read the game's process memory or sniff its network traffic. Those
/// techniques violate Black Desert's terms and get accounts banned; keeping the boundary at this
/// interface means the rest of the codebase never assumes an unsafe source exists.
/// </summary>
public interface ILiveStateProvider
{
    /// <summary>The most recent snapshot. Never null; returns an empty state before first capture.</summary>
    PlayerState Current { get; }

    /// <summary>Raised whenever <see cref="Current"/> is replaced.</summary>
    event EventHandler<PlayerState>? StateChanged;

    /// <summary>Replace the current snapshot (e.g. after the user edits stats or a vision parse).</summary>
    void Publish(PlayerState state);
}
