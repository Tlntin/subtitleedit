using System;
using System.Threading;

namespace Nikse.SubtitleEdit.Features.Video.SpeechToText;

internal sealed class SpeechToTextOnlineCancellation
{
    private readonly object _gate = new();
    private CancellationTokenSource? _active;
    internal CancellationTokenSource Begin()
    {
        lock (_gate) return _active = new CancellationTokenSource();
    }
    internal void Complete(CancellationTokenSource owner)
    {
        lock (_gate)
        {
            if (ReferenceEquals(_active, owner)) _active = null;
            owner.Dispose();
        }
    }
    internal void Cancel()
    {
        lock (_gate) _active?.Cancel();
    }
}

// Progress<T> posts asynchronously and can still be pending when the response is saved.
// Collect transcript data synchronously; dispatch only its visual presentation to the UI.
internal sealed class SpeechToTextSynchronousProgress<T>(Action<T> report) : IProgress<T>
{
    public void Report(T value) => report(value);
}
