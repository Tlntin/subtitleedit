namespace Nikse.SubtitleEdit.Logic.Config.Language;

public class LanguageAudioToText
{
    public string Title { get; set; }
    public string Transcribe { get; set; }
    public string TranslateToEnglish { get; set; }
    public string Transcribing { get; set; }
    public string TranscribingXOfY { get; set; }
    public string SkipExistingSubtitles { get; set; } = "Skip files with existing subtitles...";
    public string SkipEmbeddedSubtitles { get; set; } = "Skip files with embedded subtitle tracks";
    public string SkipExternalSubtitles { get; set; } = "Skip files with external subtitles";
    public string SkipExistingSubtitlesHint { get; set; } = "Checked when starting a batch. External subtitles must be beside the media with the same name, optionally followed by a language tag (movie.en.srt).";
    public string SkippedEmbeddedSubtitles { get; set; } = "Skipped: embedded subtitles";
    public string SkippedExternalSubtitles { get; set; } = "Skipped: external subtitles";
    public string SkippedFilesX { get; set; } = "Skipped files: {0}";
    public string CurrentFileRemainingX { get; set; } = "Current file remaining: {0}";
    public string BatchRemainingX { get; set; } = "Entire batch remaining: {0}";
    public string BatchRemainingCalculating { get; set; } = "Entire batch remaining: estimating...";
    public string BatchElapsedX { get; set; } = "Batch elapsed: {0}";
    public string GptVadTimingHint { get; set; } = "Transcribes continuous audio in context windows of about 24 seconds. VAD only suggests cut points; quiet speech and gaps are preserved. Uses an installed Qwen3 forced aligner for sentence times; otherwise retains chunk-level times.";
    public string GptNativeTimingHint { get; set; } = "Uses the model's native segment start/end times (diarized_json). Local VAD is not required for this model.";
    public string GptDetectingSpeech { get; set; } = "Analyzing audio boundaries; continuous audio is retained...";
    public string GptTranscribingParts { get; set; } = "Audio chunks completed: {0}/{1}";
    public string GptNoSpeech { get; set; } = "Audio contains only digital silence; no audio was uploaded.";
    public string GptContextChunks { get; set; } = "Prepared {0} continuous audio chunks (about 24 seconds each), covering the complete source timeline.";
    public string GptVadBoundaryFallback { get; set; } = "VAD boundary detection failed; using continuous fixed-size chunks.";
    public string GptAlignmentEnabled { get; set; } = "Using installed Qwen3 forced alignment for sentence timestamps.";
    public string GptAlignmentUnavailable { get; set; } = "Local alignment is off or no installed Qwen3 aligner was found. Keeping chunk-level times; sentence alignment is not applied.";
    public string GptAligningChunk { get; set; } = "Aligning sentence timestamps: chunk {0}/{1}...";
    public string GptAlignmentFailed { get; set; } = "Local alignment failed for chunk {0}; keeping its full text with chunk-level times.";
    public string GptEmptyChunk { get; set; } = "Audio {0:0.000}–{1:0.000} s: endpoint returned empty text. Check this interval.";
    public string GptUseLocalAlignment { get; set; } = "Use installed Qwen3 aligner for sentence times (slower)";
    public string GptCancelled { get; set; } = "Transcription cancelled.";
    public string GptCancelledPartial { get; set; } = "Cancelled; {0} recognized segments saved locally: {1}";
    public string GptPartialSaveFailed { get; set; } = "Cancelled; the partial subtitle could not be saved: {0}";
    public string GptTranscribingChunk { get; set; } = "Transcribing audio chunk {0}/{1} ({2:0.000}–{3:0.000} s)...";
    public string GptSilentChunk { get; set; } = "Audio {0:0.000}–{1:0.000} s contains only digital silence; skipped locally.";
    public string GptMissingTimings { get; set; } = "The transcription endpoint did not return valid segment start/end times. Select a GPT model with local VAD, or an endpoint supporting diarized_json.";
    public string GptConfigurationRequired { get; set; } = "Configure a valid transcription endpoint and select a supported GPT model.";
    public string GptApiKeyRequired { get; set; } = "Enter the API key for the selected transcription endpoint.";
    public string GptConcurrentRequests { get; set; } = "Concurrent transcription requests";
    public string GptMoreSettings { get; set; } = "More settings";
    public string GptConnectionTest { get; set; } = "Test connection";
    public string GptConnectionTestHint { get; set; } = "Sends about 4 seconds of built-in test speech to the selected model. This may incur a small API charge. Waits up to 30 seconds.";
    public string GptConnectionTesting { get; set; } = "Testing transcription endpoint, API key and model...";
    public string GptConnectionSuccess { get; set; } = "Connection test passed: {0}, {1} s. The transcription endpoint returned valid text.";
    public string GptConnectionFailed { get; set; } = "Connection test failed: {0}";
    public string GptConnectionCancelled { get; set; } = "Connection test cancelled.";
    public string GptConnectionTimeout { get; set; } = "Connection test timed out after 30 seconds. Check the network, proxy or service availability.";
    public string GptConnectionUnauthorized { get; set; } = "API key rejected. Check that the key belongs to this endpoint.";
    public string GptConnectionForbidden { get; set; } = "Access denied. Check permissions for this model.";
    public string GptConnectionNotFound { get; set; } = "Endpoint or model not found. Check the full transcription URL and model availability.";
    public string GptConnectionRateLimit { get; set; } = "Quota exhausted or requests rate-limited. Check the account balance and rate limits.";
    public string GptConnectionServerError { get; set; } = "The transcription service reported a server error. Try again later.";
    public string GptConnectionNetworkError { get; set; } = "Could not connect. Check the URL, network, proxy and TLS certificate.";
    public string GptConnectionRequestError { get; set; } = "The request was rejected or could not be sent. Check the endpoint, model and additional headers.";
    public string GptConnectionInvalidResponse { get; set; } = "The endpoint responded, but did not return valid transcription text or the required native segment times. Check API compatibility.";
    public string BatchSavingSubtitle { get; set; } = "Saving subtitle...";
    public string BatchSaveFailedTitle { get; set; } = "Unable to save subtitle";
    public string BatchSaveFailedMessage { get; set; } = "Transcription finished, but the subtitle could not be saved to:\n{0}\n\n{1}\n\nFor an SMB share, reconnect and check write access, then retry. Choosing another folder also changes the output folder for the remaining files.";
    public string BatchSaveRecoveryMessage { get; set; } = "A local recovery copy of the completed subtitle is saved at:\n{0}\n\nRetrying or changing folders saves this result without transcribing it again.";
    public string BatchSaveRecoveryFailedMessage { get; set; } = "The local recovery copy could not be saved: {0}\n\nThe result and engine temporary files are still retained in this session. Retry or choose a writable folder before closing.";
    public string BatchSaveRetry { get; set; } = "Retry saving";
    public string BatchSaveChooseFolder { get; set; } = "Choose output folder...";
    public string BatchSaveStop { get; set; } = "Stop batch";
    public string BatchSaveStopped { get; set; } = "Stopped: subtitle not saved to destination";
    public string SubtitleFilesGenerated { get; set; }
    public string SubtitleFilesGeneratedX { get; set; }
    public string SubtitleFilesGeneratedXOfY { get; set; }
    public string LinesTranscribedXOfYFailedZ { get; set; }
    public string LinesTranscribedXOfYCancelled { get; set; }
    public string NoLinesTranscribed { get; set; }
    public string ApplyTranscribedLines { get; set; }
    public string RetryFailedLines { get; set; }
    public string IsolateSpeech { get; set; }
    public string IsolateSpeechHint { get; set; }
    public string IsolatingSpeech { get; set; }
    public string IsolateSpeechFailed { get; set; }
    public string DetectSpeakers { get; set; }
    public string DetectSpeakersHint { get; set; }
    public string DetectSpeakersNeedsNewerCrispAsr { get; set; }
    public string XNeedsNewerCrispAsr { get; set; }
    public string Vad { get; set; }
    public string VadHint { get; set; }
    public string InputLanguage { get; set; }
    public string AdvancedWhisperSettings { get; set; }
    public string DownloadingSpeechToTextEngine { get; set; }
    public string UnpackingSpeechToTextEngine { get; set; }
    public string EnableVad { get; set; }
    public string WhisperXxlStandard { get; set; }
    public string WhisperXxlStandardAsia { get; set; }
    public string WhisperXxlSentence { get; set; }
    public string WhisperXxlSingleWords { get; set; }
    public string WhisperXxlHighlightWord { get; set; }
    public string SelectModel { get; set; }
    public string AddCustomModelDotDotDot { get; set; }
    public string CustomModelHelp { get; set; }
    public string CustomModelHelpGguf { get; set; }
    public string ViewToolsLogFile { get; set; }
    public string UpdateXTitle { get; set; }
    public string UpdateXMessage { get; set; }
    public string? DownloadingSpeechToTextModel { get; set; }
    public string WhisperPostProcessingTitle { get; set; }
    public string AdjustTimings { get; set; }
    public string MergeShortLines { get; set; }
    public string BreakSplitLongLines { get; set; }
    public string FixShortDuration { get; set; }
    public string FixCasing { get; set; }
    public string AddPeriods { get; set; }
    public string ChangeUnderlineToColor { get; set; }
    public string RemoveNonSpeechLines { get; set; }
    public string RemoveNonSpeechLinesHint { get; set; }
    public string RemoveRepeatedLines { get; set; }
    public string RemoveRepeatedLinesHint { get; set; }
    public string ShowQualityReport { get; set; }
    public string QualityReportTitle { get; set; }
    public string QualityReportNoIssues { get; set; }
    public string QualityReportSummaryX { get; set; }
    public string QualityReportLinesChecked { get; set; }
    public string QualityReportIssuesFound { get; set; }
    public string QualityReportLinesRemoved { get; set; }
    public string QualityReportTooShortHint { get; set; }
    public string QualityReportTooLongHint { get; set; }
    public string QualityReportOverlapHint { get; set; }
    public string QualityReportNonSpeech { get; set; }
    public string QualityReportNonSpeechHint { get; set; }
    public string QualityReportRepeated { get; set; }
    public string QualityReportRepeatedHint { get; set; }
    public string QualityReportRemoved { get; set; }
    public string QualityReportAll { get; set; }
    public string QualityReportIssue { get; set; }
    public string QualityReportTip { get; set; }
    public string QualityReportDoNotShowAgain { get; set; }

    public string EngineSettings { get; set; }
    public string EngineSettingsSubtitle { get; set; }
    public string BackendAndUpdateStatus { get; set; }
    public string AddLanguageCodeToFileName { get; set; }
    public string AddLanguageCodeToFileNameHint { get; set; }
    public string KeepPartialTranscriptionQuestion { get; set; }
    public string KeepXLinesQuestion { get; set; }
    public string EngineCouldNotStart { get; set; }
    public string SharedLibraryXMissing { get; set; }
    public string SharedLibraryBundledRedownload { get; set; }
    public string SharedLibraryInstallIt { get; set; }
    public string IncompleteModel { get; set; }
    public string IncompleteModelMessage { get; set; }
    public string UnknownArgumentX { get; set; }
    public string UnknownArgumentMessage { get; set; }
    public string CudaFailed { get; set; }
    public string CudaOutOfMemory { get; set; }
    public string CuBlasFailed { get; set; }
    public string CuBlasNotSupportedCause { get; set; }
    public string TryAnotherModelEngineOrCpu { get; set; }
    public string ComputeTypeAlreadySet { get; set; }
    public string AddingXFixesThisAddNow { get; set; }
    public string CudaNotAvailable { get; set; }
    public string WhisperXCpuOnly { get; set; }
    public string EngineBuiltWithoutCuda { get; set; }
    public string RemoveGpuSettingsOrRunOnCpu { get; set; }
    public string RemoveGpuSettingsQuestionX { get; set; }
    public string NoResultFile { get; set; }
    public string NoResultFileMessage { get; set; }
    public string NoTranscriptionResult { get; set; }
    public string CrispAsrNoSubtitles { get; set; }
    public string CouldNotGenerateAudioFileX { get; set; }
    public string NoVideoFile { get; set; }
    public string NoVideoFileFound { get; set; }
    public string UnableToStartX { get; set; }
    public string UnableToStartXY { get; set; }
    public string ProgramFileXNotFoundYRedownload { get; set; }
    public string XRequiresEngineSelectVersion { get; set; }
    public string XRequiresCrispAsrSelectVersion { get; set; }
    public string XRequiresForcedAlignerUseY { get; set; }
    public string UpdatedModelXAvailable { get; set; }
    public string XSelectedNotInstalledUseY { get; set; }
    public string EnglishModelContinue { get; set; }
    public string NoAudioTrackFound { get; set; }
    public string NoAudioTrackFoundInX { get; set; }

    public LanguageAudioToText()
    {
        KeepPartialTranscriptionQuestion = "Keep partial transcription?";
        KeepXLinesQuestion = "Do you want to keep {0} lines?";
        EngineCouldNotStart = "Speech to text engine could not start";
        SharedLibraryXMissing = "The speech to text engine could not start - the shared library \"{0}\" is missing.";
        SharedLibraryBundledRedownload = "This library is part of the engine download, so the installed engine is incomplete.\n\nRe-download the engine (Download button next to the engine) and try again.";
        SharedLibraryInstallIt = "Install it with your package manager and try again.";
        IncompleteModel = "Incomplete model";
        IncompleteModelMessage = "The model is incomplete. Please download the full model.";
        UnknownArgumentX = "Unknown argument: {0}";
        UnknownArgumentMessage = "Unknown argument. Please check the advanced settings.";
        CudaFailed = "CUDA failed";
        CudaOutOfMemory = "Whisper ran out of CUDA memory - try a smaller model or run on CPU.";
        CuBlasFailed = "cuBLAS failed";
        CuBlasNotSupportedCause = "The GPU could not run the model with the compute type the engine picked - cuBLAS returned CUBLAS_STATUS_NOT_SUPPORTED, and no text was transcribed.";
        TryAnotherModelEngineOrCpu = "Try another model, another engine, or run on CPU.";
        ComputeTypeAlreadySet = "The parameters already set \"--compute_type\" - try another value, such as \"float16\", \"float32\", or \"bfloat16\" - the \"int8\" types fail this way on many newer GPUs.";
        AddingXFixesThisAddNow = "Adding \"{0}\" to the parameters normally fixes this.\n\nAdd it now?";
        CudaNotAvailable = "CUDA not available";
        WhisperXCpuOnly = "This WhisperX build runs on the CPU only - it cannot use \"--device cuda\", so no text was transcribed.";
        EngineBuiltWithoutCuda = "This engine was built without CUDA support, so it cannot run on the GPU and no text was transcribed.";
        RemoveGpuSettingsOrRunOnCpu = "Remove any GPU settings from the parameters, or run on CPU.";
        RemoveGpuSettingsQuestionX = "Remove the GPU settings from the parameters so it runs on the CPU?\n\nNew parameters: {0}";
        NoResultFile = "No result file";
        NoResultFileMessage = "No result file was generated by Whisper, but some text was captured from standard output.";
        NoTranscriptionResult = "No transcription result";
        CrispAsrNoSubtitles = "Crisp ASR finished without generating subtitles. Please check the tools log for engine output.";
        CouldNotGenerateAudioFileX = "Could not generate the audio file (ffmpeg exit code {0}).\nPlease check the tools log for the ffmpeg output.";
        NoVideoFile = "No video file";
        NoVideoFileFound = "No video file found!";
        UnableToStartX = "Unable to start {0}";
        UnableToStartXY = "Unable to start {0}: {1}";
        ProgramFileXNotFoundYRedownload = "The {0} program file was not found:\n{1}\n\nIt may have been deleted or quarantined by antivirus software - if so, please restore it or add an exclusion for the folder.\n\nDo you want to re-download \"{0}\"?";
        XRequiresEngineSelectVersion = "\"{0}\" requires downloading the engine.\n\nSelect a version to download:";
        XRequiresCrispAsrSelectVersion = "\"{0}\" requires downloading the CrispASR engine.\n\nSelect a version to download:";
        XRequiresForcedAlignerUseY = "\"{0}\" requires a forced aligner to create timestamps.\nDownload and use {1}?";
        UpdatedModelXAvailable = "An updated {0} is available (re-converted for Crisp ASR v0.8.36).\nDownload and use it?";
        XSelectedNotInstalledUseY = "\"{0}\" is selected but not installed.\nDownload and use {1}?";
        EnglishModelContinue = "English model should only be used with English language.\nContinue anyway?";
        NoAudioTrackFound = "No audio track found";
        NoAudioTrackFoundInX = "No audio track was found in {0}";
        Title = "Speech to text";
        Transcribe = "Transcribe";
        TranslateToEnglish = "Translate to English";
        Transcribing = "Transcribing...";
        TranscribingXOfY = "Transcribing {0} of {1}...";
        SubtitleFilesGenerated = "Subtitle files generated";
        SubtitleFilesGeneratedX = "Subtitle files generated: {0}";
        SubtitleFilesGeneratedXOfY = "Subtitle files generated: {0} of {1}";
        LinesTranscribedXOfYFailedZ = "{0} of {1} lines were transcribed - {2} failed or had no speech.\n\nFailed lines keep their current text. Apply the transcribed lines, or retry the failed ones (you can change engine or settings first)?";
        LinesTranscribedXOfYCancelled = "Transcription was cancelled after {0} of {1} lines.\n\nApply the transcribed lines? The other lines keep their current text.";
        NoLinesTranscribed = "None of the {0} lines could be transcribed.\nPlease check the tools log for details.";
        ApplyTranscribedLines = "Apply transcribed";
        RetryFailedLines = "Retry failed";
        IsolateSpeech = "Isolate speech (slow)";
        IsolateSpeechHint = "Removes music and sound effects before transcribing. Gives fewer missed lines and better timing on audio with loud music, but takes about as long as the audio itself with a GPU - and many times longer without one.";
        IsolatingSpeech = "Isolating speech...";
        IsolateSpeechFailed = "Could not isolate the speech - transcribing the original audio instead.";
        DetectSpeakers = "Detect speakers";
        DetectSpeakersHint = "Starts each line with the speaker, like \"(speaker 0)\", using NVIDIA's Sortformer diarization (up to 8 speakers). Works best with backends that split lines where the speaker changes, like Parakeet and Cohere.";
        DetectSpeakersNeedsNewerCrispAsr = "\"Detect speakers\" needs Crisp ASR v{0} or newer, but v{1} is installed.\n\nUpdate Crisp ASR (the gear button next to the engine) or turn off \"Detect speakers\".";
        XNeedsNewerCrispAsr = "{0} needs Crisp ASR v{1} or newer, but v{2} is installed.\n\nDownload and install the update now?";
        Vad = "VAD";
        VadHint = "Voice activity detection: only the speech is transcribed, cut where the speaker pauses.\nAuto: Silero, and only for the engines that need it (Cohere, Mega, Index-Echo, Granite).\nSilero, FireRedVAD, Whisper-VAD, WebRTC: always on, for every Crisp ASR engine. FireRedVAD is the most accurate, Whisper-VAD gives the fewest, cleanest cuts but is the slowest.\nIndex-Echo always uses Silero. --vad, --vad-model or --chunk-seconds in the advanced settings take precedence.";
        InputLanguage = "Input language";
        AdvancedWhisperSettings = "Advanced speech-to-text parameters";
        DownloadingSpeechToTextEngine = "Downloading speech-to-text engine";
        UnpackingSpeechToTextEngine = "Unpacking speech-to-text engine";
        EnableVad = "Enable VAD";
        WhisperXxlStandard = "Standard";
        WhisperXxlStandardAsia = "Standard Asia";
        WhisperXxlSentence = "Sentence-level";
        WhisperXxlSingleWords = "Single words";
        WhisperXxlHighlightWord = "Highlight word";
        SelectModel = "Select model";
        AddCustomModelDotDotDot = "Add custom model...";
        CustomModelHelp = "You can use your own model: pick the model file (whisper.cpp ggml '.bin') or model folder (faster-whisper folder with a 'model.bin' inside). It is copied to the models folder and added to the list above.";
        CustomModelHelpGguf = "You can use your own model: pick a qwen3-asr.cpp '.gguf' file (one file holding both the audio encoder and the language model, e.g. from OpenVoiceOS or FlippyDora on Hugging Face). It is copied to the models folder and added to the list above. GGUF files made for llama.cpp, transcribe.cpp or CapsWriter use a different layout and cannot be used.";
        ViewToolsLogFile = "View tools log file";
        UpdateXTitle = "Update {0}?";
        UpdateXMessage = "A newer version of {0} is available.{1}{1}Download and install the update now?";
        DownloadingSpeechToTextModel = "Downloading speech-to-text model";
        WhisperPostProcessingTitle = "Whisper post-processing";
        AdjustTimings = "Adjust timings";
        MergeShortLines = "Merge short lines";
        BreakSplitLongLines = "Break/split long lines";
        FixShortDuration = "Fix short duration";
        FixCasing = "Fix casing";
        AddPeriods = "Add periods";
        ChangeUnderlineToColor = "Change underline to color";
        RemoveNonSpeechLines = "Remove non-speech lines";
        RemoveNonSpeechLinesHint = "Drop lines that only describe sound, like \"[Music]\" or \"(waves crashing)\"";
        RemoveRepeatedLines = "Remove repeated lines";
        RemoveRepeatedLinesHint = "Drop lines that repeat the previous line word for word (engine loops)";
        ShowQualityReport = "Show quality report after transcription";
        QualityReportTitle = "Transcription quality report";
        QualityReportNoIssues = "No issues found - the transcription looks good.";
        QualityReportSummaryX = "{0} issue(s) found in {1} line(s)";
        QualityReportLinesChecked = "Lines checked";
        QualityReportIssuesFound = "Issues found";
        QualityReportLinesRemoved = "Lines removed";
        QualityReportTooShortHint = "Shorter than the minimum display time, or reading speed above the maximum";
        QualityReportTooLongHint = "Longer than the maximum display time, or very few words over a long time (often a hallucination)";
        QualityReportOverlapHint = "Ends after the next line starts";
        QualityReportNonSpeech = "Non-speech";
        QualityReportNonSpeechHint = "Only a sound or music description, like \"[Music]\"";
        QualityReportRepeated = "Repeated";
        QualityReportRepeatedHint = "Same text as the previous line (engine loop)";
        QualityReportRemoved = "Removed";
        QualityReportAll = "All";
        QualityReportIssue = "Issue";
        QualityReportTip = "Lines are listed with their numbers in the new subtitle. Use \"Fix common errors\" or the post-processing settings to fix more automatically, or try another model/engine if many lines are affected.";
        QualityReportDoNotShowAgain = "Do not show this report again";

        EngineSettings = "Speech-to-text engine settings";
        EngineSettingsSubtitle = "Speech-to-text engine";
        BackendAndUpdateStatus = "Backend and update status";
        AddLanguageCodeToFileName = "Add language code to file name";
        AddLanguageCodeToFileNameHint = "Name generated subtitles like \"video.en.srt\" instead of \"video.srt\"";
    }
}
