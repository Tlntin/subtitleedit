using Nikse.SubtitleEdit.Features.Video.SpeechToText.GptTranscribe;
using Nikse.SubtitleEdit.Features.Video.SpeechToText.OpenAiCompatible;
using Nikse.SubtitleEdit.Logic.Config;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace UITests.Features.Video.SpeechToText.GptTranscribe;

public class GptConnectionTestServiceTests
{
    private static OpenAiCompatibleSettings Settings(OpenAiSttRequestProfile profile = OpenAiSttRequestProfile.GptTranscribe) => new()
    {
        EndpointUrl = "https://example.test/v1/audio/transcriptions?token=query-secret", ApiKey = "secret-test-key",
        Model = "gpt-transcribe", RequestProfile = profile, ExtraHeaders = "X-Test: header-secret", TimeoutSeconds = 30,
    };

    [Fact]
    public async Task SendsActualBuiltInWave_WithAuthenticationModelAndRequestProfile()
    {
        using var handler = new Handler(async (request, token) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("secret-test-key", request.Headers.Authorization!.Parameter);
            Assert.Equal("header-secret", request.Headers.GetValues("X-Test").Single());
            var parts = Assert.IsType<MultipartFormDataContent>(request.Content).ToArray();
            var file = parts.Single(p => p.Headers.ContentDisposition!.Name!.Trim('"') == "file");
            var bytes = await file.ReadAsByteArrayAsync(token);
            Assert.Equal("RIFF", Encoding.ASCII.GetString(bytes, 0, 4));
            Assert.InRange(bytes.Length, 32000, 160000);
            Assert.Equal("gpt-transcribe", await parts.Single(p => p.Headers.ContentDisposition!.Name!.Trim('"') == "model").ReadAsStringAsync(token));
            Assert.Contains(parts, p => p.Headers.ContentDisposition!.Name!.Trim('"') == "response_format");
            return Json("""{"text":"Hello, this is a connection test."}""");
        });
        using var http = new HttpClient(handler);
        var result = await new GptConnectionTestService(http).TestAsync(Settings(), TestContext.Current.CancellationToken);
        Assert.True(result.Success);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData(401)] [InlineData(403)] [InlineData(404)] [InlineData(429)] [InlineData(500)] [InlineData(400)]
    public async Task HttpFailures_AreSpecific_AndRedactAllConfiguredSecrets(int status)
    {
        using var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)
        {
            Content = new StringContent("""{"error":{"message":"Failure: secret-test-key header-secret query-secret"}}"""),
        }));
        using var http = new HttpClient(handler);
        var result = await new GptConnectionTestService(http).TestAsync(Settings(), TestContext.Current.CancellationToken);
        Assert.False(result.Success);
        Assert.Contains("HTTP " + status, result.Message);
        Assert.DoesNotContain("secret-test-key", result.Message);
        Assert.DoesNotContain("header-secret", result.Message);
        Assert.DoesNotContain("query-secret", result.Message);
        Assert.Equal(1, handler.Calls); // No silent retries / repeated test charges.
    }

    [Theory]
    [InlineData("<html>Proxy login</html>", false)]
    [InlineData("{}", false)]
    [InlineData("{\"text\":\"\"}", false)]
    [InlineData("{\"text\":\"hello\"}", true)]
    public async Task ReachableButInvalidTranscription_DoesNotPass(string json, bool nativeTimes)
    {
        using var handler = new Handler((_, _) => Task.FromResult(Json(json)));
        using var http = new HttpClient(handler);
        var result = await new GptConnectionTestService(http).TestAsync(
            Settings(nativeTimes ? OpenAiSttRequestProfile.GptDiarize : OpenAiSttRequestProfile.GptTranscribe), TestContext.Current.CancellationToken);
        Assert.False(result.Success);
        Assert.Equal(Se.Language.Video.AudioToText.GptConnectionInvalidResponse, result.Message);
    }

    [Fact]
    public async Task Diarize_RequiresNativeSegmentTimes()
    {
        using var handler = new Handler((_, _) => Task.FromResult(Json("""{"text":"Hello","segments":[{"start":0.1,"end":0.7,"text":"Hello"}]}""")));
        using var http = new HttpClient(handler);
        Assert.True((await new GptConnectionTestService(http).TestAsync(Settings(OpenAiSttRequestProfile.GptDiarize),
            TestContext.Current.CancellationToken)).Success);
    }

    [Fact]
    public async Task NetworkError_DoesNotExposeExceptionText()
    {
        using var handler = new Handler((_, _) => throw new HttpRequestException("secret-test-key query-secret header-secret"));
        using var http = new HttpClient(handler);
        var result = await new GptConnectionTestService(http).TestAsync(Settings(), TestContext.Current.CancellationToken);
        Assert.False(result.Success);
        Assert.DoesNotContain("secret", result.Message);
    }

    [Fact]
    public async Task Timeout_IsReportedWithoutSensitiveExceptionDetails()
    {
        using var handler = new Handler((_, _) => throw new OperationCanceledException("secret-test-key"));
        using var http = new HttpClient(handler);
        var result = await new GptConnectionTestService(http).TestAsync(Settings(), TestContext.Current.CancellationToken);
        Assert.Equal(Se.Language.Video.AudioToText.GptConnectionTimeout, result.Message);
    }

    [Fact]
    public async Task UserCancellation_StopsTheRequest()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new Handler(async (_, token) =>
        {
            entered.SetResult();
            await Task.Delay(Timeout.Infinite, token);
            return Json("{}");
        });
        using var http = new HttpClient(handler);
        var task = new GptConnectionTestService(http).TestAsync(Settings(), cts.Token);
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK)
    { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        internal int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        { Calls++; return send(request, token); }
    }
}
