using Nikse.SubtitleEdit.Features.Video.SpeechToText.GptTranscribe;
using Nikse.SubtitleEdit.Features.Video.SpeechToText.OpenAiCompatible;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace UITests.Features.Video.SpeechToText.GptTranscribe;

public class GptTranscribeTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("se-gpt-tests-");
    public void Dispose() => _root.Delete(true);
    private string AudioFile()
    {
        var path = Path.Combine(_root.FullName, "input.wav");
        File.WriteAllBytes(path, new byte[44]);
        return path;
    }

    private static OpenAiCompatibleSettings Settings(string model = "gpt-transcribe") => new()
    {
        EndpointUrl = "https://example.test/v1/audio/transcriptions", ApiKey = "test-key",
        Model = model, RequestProfile = GptTranscribeModels.Find(model)!.Profile, TimeoutSeconds = 30,
    };

    private static HttpResponseMessage Json(string value) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(value, Encoding.UTF8, "application/json"),
    };

    [Fact]
    public void ProcessingSpeed_ShowsActualDecimalMultiplier()
    {
        var message = string.Format(System.Globalization.CultureInfo.InvariantCulture,
            new Nikse.SubtitleEdit.Logic.Config.Language.LanguageAudioToText().GptProcessingSpeed, 42.4, 180, 180 / 42.4);
        Assert.Contains("4.2x", message);
    }

    private static async Task<Dictionary<string, List<string>>> Fields(HttpRequestMessage request)
    {
        var form = Assert.IsType<MultipartFormDataContent>(request.Content);
        var result = new Dictionary<string, List<string>>();
        foreach (var part in form)
        {
            var key = part.Headers.ContentDisposition!.Name!.Trim('"');
            if (!result.TryGetValue(key, out var values)) result[key] = values = [];
            if (key != "file") values.Add(await part.ReadAsStringAsync());
        }
        return result;
    }

    [Theory]
    [InlineData("gpt-transcribe")]
    [InlineData("gpt-4o-transcribe")]
    [InlineData("gpt-4o-mini-transcribe")]
    [InlineData("gpt-4o-mini-transcribe-2025-12-15")]
    public async Task TextModels_RequestJsonWithoutUnsupportedTimestampOptions(string model)
    {
        var settings = Settings(model);
        settings.Language = "en,zh";
        settings.Prompt = "Vocabulary";
        using var handler = new Handler(async (request, _) =>
        {
            var fields = await Fields(request);
            Assert.Equal([model], fields["model"]);
            Assert.Equal(["json"], fields["response_format"]);
            Assert.False(fields.ContainsKey("timestamp_granularities[]"));
            Assert.False(fields.ContainsKey("chunking_strategy"));
            Assert.False(fields.ContainsKey("stream"));
            Assert.Equal(["Vocabulary"], fields["prompt"]);
            if (model == "gpt-transcribe")
            {
                Assert.Equal(["en", "zh"], fields["languages[]"]);
                Assert.False(fields.ContainsKey("language"));
            }
            else Assert.Equal(["en"], fields["language"]);
            return Json("""{"text":"Hello", "languages":[{"code":"en"}]}""");
        });
        using var http = new HttpClient(handler);
        var result = await new OpenAiSttService(http, settings).TranscribeAsync(AudioFile(), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("Hello", result.Text);
        Assert.Equal("en", result.Language);
    }

    [Fact]
    public async Task Diarize_UsesNativeTimesAndStringIds_WithoutLocalVadOrUnsupportedFields()
    {
        var settings = Settings("gpt-4o-transcribe-diarize");
        settings.Prompt = "Must not send";
        settings.Temperature = 0.5;
        var audio = new FakeAudio([]);
        using var handler = new Handler(async (request, _) =>
        {
            var fields = await Fields(request);
            Assert.Equal(["diarized_json"], fields["response_format"]);
            Assert.Equal(["auto"], fields["chunking_strategy"]);
            Assert.False(fields.ContainsKey("prompt"));
            Assert.False(fields.ContainsKey("temperature"));
            Assert.False(fields.ContainsKey("timestamp_granularities[]"));
            return Json("""{"text":"Hello","segments":[{"id":"seg_001","speaker":"A","start":2.5,"end":4.2,"text":"Hello"}]}""");
        });
        using var http = new HttpClient(handler);
        var service = Service(settings, 2, http, audio);
        var result = await service.TranscribeAsync(AudioFile(), cancellationToken: TestContext.Current.CancellationToken);
        var segment = Assert.Single(result.Segments!);
        Assert.Equal(2.5, segment.Start);
        Assert.Equal(4.2, segment.End);
        Assert.Null(audio.PcmPath);
    }

    [Fact]
    public async Task ContextChunks_CoverCompleteTimeline_WhenResponsesFinishOutOfOrder()
    {
        var audio = new FakeAudio([new(2, 3.5), new(7.2, 8.8)], duration: 60);
        using var handler = new Handler(async (request, token) =>
        {
            var file = Assert.IsType<MultipartFormDataContent>(request.Content).Single(p => p.Headers.ContentDisposition!.Name!.Trim('"') == "file");
            var bytes = await file.ReadAsByteArrayAsync(token);
            Assert.Equal("RIFF", Encoding.ASCII.GetString(bytes, 0, 4));
            Assert.Equal("audio/wav", file.Headers.ContentType!.MediaType);
            Assert.Equal(16000, BitConverter.ToInt32(bytes, 24));
            var first = file.Headers.ContentDisposition!.FileName!.Contains("clip-0");
            await Task.Delay(first ? 100 : 1, token);
            return Json(first ? """{"text":"First sentence. Another sentence."}""" : """{"text":"Second."}""");
        });
        using var http = new HttpClient(handler);
        var seen = new List<OpenAiCompatibleSegment>();
        var result = await Service(Settings(), 2, http, audio).TranscribeAsync(AudioFile(),
            segmentProgress: new InlineProgress<OpenAiCompatibleSegment>(seen.Add), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(3, result.Segments!.Count);
        Assert.Equal(3, seen.Count);
        Assert.Equal(0, seen[0].Start);
        Assert.Equal(24, seen[0].End);
        Assert.Equal("First sentence. Another sentence.", seen[0].Text); // no proportional sentence timestamps
        Assert.Equal(24, seen[1].Start);
        Assert.Equal(48, seen[1].End);
        Assert.Equal(60, seen[2].End);
        Assert.False(Directory.Exists(Path.GetDirectoryName(audio.PcmPath)!));
    }

    [Fact]
    public async Task ActualSilero_OnSilence_ProducesNoRequests()
    {
        var audio = new FakeAudio([], useRealVad: true);
        using var handler = new Handler((_, _) => throw new InvalidOperationException("Silent audio must not be uploaded"));
        using var http = new HttpClient(handler);
        var result = await Service(Settings(), 2, http, audio).TranscribeAsync(AudioFile(),
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Empty(result.Segments!);
        Assert.Equal(string.Empty, result.Text);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task RateLimit_RetriesBoundedly_AndPreservesClipTiming()
    {
        var attempt = 0;
        using var handler = new Handler((_, _) =>
        {
            if (Interlocked.Increment(ref attempt) > 1) return Task.FromResult(Json("""{"text":"Recovered"}"""));
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StringContent("retry") };
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMilliseconds(10));
            return Task.FromResult(response);
        });
        using var http = new HttpClient(handler);
        var result = await Service(Settings(), 1, http, new FakeAudio([new(1, 2)])).TranscribeAsync(AudioFile(),
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(2, handler.Calls);
        Assert.Equal(0, Assert.Single(result.Segments!).Start);
    }

    [Fact]
    public async Task AuthenticationFailure_IsNotRetried_AndCleansUpAudio()
    {
        var audio = new FakeAudio([new(1, 2)]);
        using var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("unauthorized"),
        }));
        using var http = new HttpClient(handler);
        await Assert.ThrowsAsync<HttpRequestException>(() => Service(Settings(), 1, http, audio)
            .TranscribeAsync(AudioFile(), cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(1, handler.Calls);
        Assert.False(Directory.Exists(Path.GetDirectoryName(audio.PcmPath)!));
    }

    [Fact]
    public async Task Cancellation_StopsRequestAndRemovesTemporaryAudio()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var audio = new FakeAudio([new(1, 2)]);
        using var handler = new Handler(async (_, token) =>
        {
            entered.SetResult();
            await Task.Delay(Timeout.Infinite, token);
            return Json("{}");
        });
        using var http = new HttpClient(handler);
        var task = Service(Settings(), 1, http, audio).TranscribeAsync(AudioFile(), cancellationToken: cts.Token);
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.False(Directory.Exists(Path.GetDirectoryName(audio.PcmPath)!));
    }

    [Theory]
    [InlineData("""{"text":"Hello"}""")]
    [InlineData("""{"text":"Hello","segments":[{"start":0,"end":0,"text":"Hello"}]}""")]
    [InlineData("""{"text":"Hello","segments":[{"start":3,"end":1,"text":"Hello"}]}""")]
    public void DiarizeWithoutValidTimes_FailsInsteadOfInventingSubtitleTimes(string json) =>
        Assert.Throws<InvalidDataException>(() => GptTranscribeResponseParser.Parse(json, true));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SpeechNotDetectedOrVadFailed_StillUploadsAudibleAudio(bool vadFailed)
    {
        using var handler = new Handler((_, _) => Task.FromResult(Json("""{"text":"Quiet speech retained"}""")));
        using var http = new HttpClient(handler);
        var result = await Service(Settings(), 2, http, new FakeAudio([], failVad: vadFailed))
            .TranscribeAsync(AudioFile(), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(1, handler.Calls);
        Assert.Equal("Quiet speech retained", Assert.Single(result.Segments!).Text);
        Assert.Equal(0, result.Segments![0].Start);
        Assert.Equal(10, result.Segments[0].End);
    }

    [Fact]
    public async Task LocalAlignment_ProvidesMeasuredSentenceTimesWithinTheLargerContextClip()
    {
        using var handler = new Handler((_, _) => Task.FromResult(Json("""{"text":"First. Second."}""")));
        using var http = new HttpClient(handler);
        var aligner = new FakeAligner(false);
        var result = await new GptTranscribeService(Settings(), 1, http, new FakeAudio([]), aligner)
            .TranscribeAsync(AudioFile(), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("First. Second.", aligner.Text);
        Assert.Equal(2, result.Segments!.Count);
        Assert.Equal(2.1, result.Segments[0].Start);
        Assert.Equal(7.8, result.Segments[1].End);
        Assert.False(result.HasChunkLevelTimings);
    }

    [Fact]
    public async Task AlignmentFailure_PreservesAllRecognizedText()
    {
        using var handler = new Handler((_, _) => Task.FromResult(Json("""{"text":"First. Second."}""")));
        using var http = new HttpClient(handler);
        var result = await new GptTranscribeService(Settings(), 1, http, new FakeAudio([]), new FakeAligner(true))
            .TranscribeAsync(AudioFile(), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("First. Second.", Assert.Single(result.Segments!).Text);
        Assert.Equal(10, result.Segments![0].End);
        Assert.True(result.HasChunkLevelTimings);
    }

    [Fact]
    public async Task Cancel_StopsAllConcurrentRequestsBeforeAnyMoreChunksStart()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var count = 0;
        var bothEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new Handler(async (_, token) =>
        {
            if (Interlocked.Increment(ref count) == 2) bothEntered.SetResult();
            await Task.Delay(Timeout.Infinite, token);
            return Json("{}");
        });
        using var http = new HttpClient(handler);
        var audio = new FakeAudio([], duration: 120);
        var task = Service(Settings(), 2, http, audio).TranscribeAsync(AudioFile(), cancellationToken: cts.Token);
        await bothEntered.Task.WaitAsync(TestContext.Current.CancellationToken);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.Equal(2, handler.Calls);
        Assert.False(Directory.Exists(Path.GetDirectoryName(audio.PcmPath)!));
    }

    [Fact]
    public async Task Pipeline_StartsMoreRequestsWhileEarlierHttpAndAlignmentAreBlocked_WithBoundedLookAhead()
    {
        var token = TestContext.Current.CancellationToken;
        var releaseFirstRequest = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseAlignment = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thirdRequest = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var active = 0;
        var maximum = 0;
        using var handler = new Handler(async (request, ct) =>
        {
            var file = Assert.IsType<MultipartFormDataContent>(request.Content)
                .Single(p => p.Headers.ContentDisposition!.Name!.Trim('"') == "file");
            var index = int.Parse(Path.GetFileNameWithoutExtension(file.Headers.ContentDisposition!.FileName!.Trim('"'))[5..]);
            var current = Interlocked.Increment(ref active);
            int previous;
            do { previous = maximum; } while (current > previous && Interlocked.CompareExchange(ref maximum, current, previous) != previous);
            try
            {
                if (index == 0) await releaseFirstRequest.Task.WaitAsync(ct);
                if (index == 2) thirdRequest.TrySetResult();
                return Json($$"""{"text":"Chunk {{index}}."}""");
            }
            finally { Interlocked.Decrement(ref active); }
        });
        using var http = new HttpClient(handler);
        var audio = new FakeAudio([], duration: 144);
        var seen = new List<OpenAiCompatibleSegment>();
        var task = new GptTranscribeService(Settings(), 2, http, audio, new BlockingAligner(releaseAlignment.Task))
            .TranscribeAsync(AudioFile(), segmentProgress: new InlineProgress<OpenAiCompatibleSegment>(seen.Add), cancellationToken: token);
        try
        {
            // This can only happen before releasing either earlier stage if HTTP has
            // its own slots rather than a barrier around each pair of complete chunks.
            await thirdRequest.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
            Assert.InRange(handler.Calls, 3, 4);
            Assert.InRange(maximum, 1, 2);
            Assert.Empty(seen);
        }
        finally
        {
            releaseFirstRequest.TrySetResult();
            releaseAlignment.TrySetResult();
        }
        var result = await task;
        Assert.Equal(6, result.Segments!.Count);
        Assert.Equal(Enumerable.Range(0, 6).Select(i => $"Chunk {i}."), seen.Select(s => s.Text));
        Assert.Equal(Enumerable.Range(0, 6).Select(i => i * 24.0), seen.Select(s => s.Start));
        Assert.False(Directory.Exists(Path.GetDirectoryName(audio.PcmPath)!));
    }

    [Fact]
    public async Task Pipeline_HttpFailureCancelsOtherWorkers_PreservesOriginalError_AndAwaitsCleanup()
    {
        var secondEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondExited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var count = 0;
        using var handler = new Handler(async (_, token) =>
        {
            if (Interlocked.Increment(ref count) == 1)
            {
                await secondEntered.Task.WaitAsync(token);
                return new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("unauthorized") };
            }
            secondEntered.TrySetResult();
            try { await Task.Delay(Timeout.Infinite, token); }
            finally { secondExited.TrySetResult(); }
            return Json("{}");
        });
        using var http = new HttpClient(handler);
        var audio = new FakeAudio([], duration: 144);
        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => Service(Settings(), 2, http, audio)
            .TranscribeAsync(AudioFile(), cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
        Assert.True(secondExited.Task.IsCompletedSuccessfully);
        Assert.False(Directory.Exists(Path.GetDirectoryName(audio.PcmPath)!));
    }

    private sealed class BlockingAligner(Task release) : IGptSubtitleAligner
    {
        public async Task<IReadOnlyList<OpenAiCompatibleSegment>> AlignAsync(string clip, string text, double duration, CancellationToken token)
        {
            await release.WaitAsync(token);
            return [new() { Start = 0, End = duration, Text = text }];
        }
    }

    private sealed class FakeAligner(bool fail) : IGptSubtitleAligner
    {
        internal string? Text;
        public Task<IReadOnlyList<OpenAiCompatibleSegment>> AlignAsync(string clip, string text, double duration, CancellationToken token)
        {
            Text = text;
            if (fail) throw new InvalidDataException("test alignment failure");
            return Task.FromResult<IReadOnlyList<OpenAiCompatibleSegment>>([
                new() { Text = "First.", Start = 2.1, End = 3.4 },
                new() { Text = "Second.", Start = 6.2, End = 7.8 }]);
        }
    }

    private static GptTranscribeService Service(OpenAiCompatibleSettings settings, int concurrency,
        HttpClient http, IGptAudioProcessor audio) => new(settings, concurrency, http, audio, useLocalAlignment: false);

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Interlocked.Increment(ref Calls);
            return send(request, token);
        }
    }

    private sealed class FakeAudio(IReadOnlyList<SpeechInterval> intervals, bool useRealVad = false, int duration = 10, bool failVad = false) : IGptAudioProcessor
    {
        public string? PcmPath;
        public Task DecodeAsync(string audioFile, string pcmFile, CancellationToken token)
        {
            PcmPath = pcmFile;
            var bytes = new byte[32000 * duration];
            if (!useRealVad) Array.Fill(bytes, (byte)1);
            File.WriteAllBytes(pcmFile, bytes);
            return Task.CompletedTask;
        }
        public IReadOnlyList<SpeechInterval> Detect(string pcmFile, CancellationToken token) => failVad
            ? throw new InvalidOperationException("test VAD failure")
            : useRealVad ? SileroVadSpeechDetector.Detect(pcmFile, token) : intervals;
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
