using System;
using System.Collections.Generic;
using System.Linq;

namespace Nikse.SubtitleEdit.Features.Video.SpeechToText;

// A run-wide estimate, independent of the current engine's per-file progress bar.
// Completed files provide a fallback while the next file is extracting audio or silent.
internal sealed class SpeechToTextBatchProgress
{
    private readonly double[] _durations;
    private int _index;
    private double _completedMediaSeconds;
    private double _completedWallSeconds;
    private DateTime _itemStarted;
    private double _lastFraction;
    private double? _remainingAtSample;
    private DateTime _sampleTime;

    internal SpeechToTextBatchProgress(IEnumerable<double> durations, DateTime started)
    {
        _durations = durations.Select(d => double.IsFinite(d) ? Math.Max(0, d) : 0).ToArray();
        _itemStarted = started;
    }

    internal void CompleteItem(bool succeeded, DateTime now)
    {
        lock (_durations)
        {
            if (_index >= _durations.Length) return;
            if (succeeded && _durations[_index] > 0)
            {
                _completedMediaSeconds += _durations[_index];
                _completedWallSeconds += Math.Max(0.001, (now - _itemStarted).TotalSeconds);
            }

            _index++;
            _itemStarted = now;
            _lastFraction = 0;
            _remainingAtSample = null;
        }
    }

    internal TimeSpan? EstimateRemaining(double currentFraction, DateTime now)
    {
        lock (_durations)
        {
            if (_index >= _durations.Length) return TimeSpan.Zero;
            var elapsed = Math.Max(0, (now - _itemStarted).TotalSeconds);
            var fraction = double.IsFinite(currentFraction) ? Math.Clamp(currentFraction, 0, 1) : 0;
            if (fraction > _lastFraction && elapsed > 0)
            {
                var processed = _completedMediaSeconds + _durations[_index] * fraction;
                var wall = _completedWallSeconds + elapsed;
                if (processed > 0)
                {
                    var remainingMedia = _durations.Skip(_index).Sum() - _durations[_index] * fraction;
                    _remainingAtSample = remainingMedia * wall / processed;
                    _sampleTime = now;
                    _lastFraction = fraction;
                }
            }
            else if (_remainingAtSample == null && _completedMediaSeconds > 0)
            {
                _remainingAtSample = _durations.Skip(_index).Sum() * _completedWallSeconds / _completedMediaSeconds - elapsed;
                _sampleTime = now;
            }

            if (_remainingAtSample == null) return null;
            var remaining = Math.Max(0, _remainingAtSample.Value - (now - _sampleTime).TotalSeconds);
            return TimeSpan.FromSeconds(Math.Min(remaining, TimeSpan.MaxValue.TotalSeconds - 1));
        }
    }

    internal static string FormatDuration(TimeSpan duration) =>
        $"{(long)Math.Max(0, duration.TotalHours):00}:{Math.Max(0, duration.Minutes):00}:{Math.Max(0, duration.Seconds):00}";
}
