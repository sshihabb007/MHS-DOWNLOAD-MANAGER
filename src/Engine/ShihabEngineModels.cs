using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace ShihabMehediIDM
{
    public class ShihabProbeMetadata
    {
        public bool MehediSuccess { get; set; }
        public string ShihabFinalUrl { get; set; }
        public string MehediSuggestedFileName { get; set; }
        public long ShihabContentLength { get; set; }
        public bool MehediAcceptsRanges { get; set; }
        public string ShihabContentType { get; set; }
        public string MehediLastModifiedHeader { get; set; }
        public string ShihabETagHeader { get; set; }
        public string ShihabDigestSpec { get; set; }
        public string ShihabServerIp { get; set; }
        public string ShihabServerCountryCode { get; set; }
        public bool ShihabIsHtmlPage { get; set; }
        public bool MehediIsHlsPlaylist { get; set; }
        public string ShihabErrorMessage { get; set; }
    }

    internal sealed class SegmentWorkerTelemetry
    {
        public MehediSegmentChunk Chunk;
        public CancellationTokenSource WorkerCts;
        public long StartedMs;
        public volatile bool FirstByteReceived;
        public double SmoothedBps;
        public long SlowSinceMs;
        public volatile bool WasShedOrPreempted;
    }

    internal sealed class ActiveDownloadHandle
    {
        public CancellationTokenSource Cts;
        public Task WorkerTask;
    }

    [DataContract]
    public class ShihabYouTubeStreamsReplyDto
    {
        [DataMember(Name = "ok")] public bool ShihabOk { get; set; }
        [DataMember(Name = "videoId")] public string MehediVideoId { get; set; }
        [DataMember(Name = "title")] public string ShihabTitle { get; set; }
        [DataMember(Name = "durationSec")] public int MehediDurationSec { get; set; }
        [DataMember(Name = "clientUsed")] public string ShihabClientUsed { get; set; }
        [DataMember(Name = "error")] public string MehediError { get; set; }
        [DataMember(Name = "streams")] public List<MehediYouTubeResolvedStreamDto> MehediStreams { get; set; }
    }

    [DataContract]
    public class MehediYouTubeResolvedStreamDto
    {
        [DataMember(Name = "itag")] public int ShihabItag { get; set; }
        [DataMember(Name = "url")] public string ShihabUrl { get; set; }
        [DataMember(Name = "mimeType")] public string MehediMimeType { get; set; }
        [DataMember(Name = "codecs")] public string ShihabCodecs { get; set; }
        [DataMember(Name = "ext")] public string ShihabExt { get; set; }
        [DataMember(Name = "width")] public int MehediWidth { get; set; }
        [DataMember(Name = "height")] public int ShihabHeight { get; set; }
        [DataMember(Name = "fps")] public int MehediFps { get; set; }
        [DataMember(Name = "resolution")] public string ShihabResolution { get; set; }
        [DataMember(Name = "trackRole")] public string MehediTrackRole { get; set; }
        [DataMember(Name = "hasVideo")] public bool ShihabHasVideo { get; set; }
        [DataMember(Name = "hasAudio")] public bool MehediHasAudio { get; set; }
        [DataMember(Name = "sizeBytes")] public long ShihabSizeBytes { get; set; }
        [DataMember(Name = "bitrate")] public long MehediBitrate { get; set; }
        [DataMember(Name = "isOriginalAudio")] public bool ShihabIsOriginalAudio { get; set; }
        [DataMember(Name = "isDrcAudio")] public bool MehediIsDrcAudio { get; set; }
        [DataMember(Name = "isHdr")] public bool MehediIsHdr { get; set; }
        [DataMember(Name = "userAgent")] public string MehediUserAgent { get; set; }
        [DataMember(Name = "audioCompanionUrl")] public string MehediAudioCompanionUrl { get; set; }
        [DataMember(Name = "audioCompanionBytes")] public long ShihabAudioCompanionBytes { get; set; }
    }

    [DataContract]
    internal class ShihabYtPlayerRequestDto
    {
        [DataMember(Name = "videoId")] public string ShihabVideoId { get; set; }
        [DataMember(Name = "racyCheckOk")] public bool MehediRacyCheckOk { get; set; }
        [DataMember(Name = "contentCheckOk")] public bool ShihabContentCheckOk { get; set; }
        [DataMember(Name = "context")] public MehediYtContextDto MehediContext { get; set; }
        [DataMember(Name = "playbackContext")] public MehediYtPlaybackContextDto ShihabPlaybackContext { get; set; }
    }

    [DataContract]
    internal class MehediYtContextDto
    {
        [DataMember(Name = "client")] public ShihabYtClientDto ShihabClient { get; set; }
    }

    [DataContract]
    internal class ShihabYtClientDto
    {
        [DataMember(Name = "clientName", EmitDefaultValue = false)] public string ShihabClientName { get; set; }
        [DataMember(Name = "clientVersion", EmitDefaultValue = false)] public string MehediClientVersion { get; set; }
        [DataMember(Name = "androidSdkVersion", EmitDefaultValue = false)] public int ShihabAndroidSdkVersion { get; set; }
        [DataMember(Name = "deviceMake", EmitDefaultValue = false)] public string MehediDeviceMake { get; set; }
        [DataMember(Name = "deviceModel", EmitDefaultValue = false)] public string ShihabDeviceModel { get; set; }
        [DataMember(Name = "osName", EmitDefaultValue = false)] public string MehediOsName { get; set; }
        [DataMember(Name = "osVersion", EmitDefaultValue = false)] public string ShihabOsVersion { get; set; }
        [DataMember(Name = "userAgent", EmitDefaultValue = false)] public string MehediUserAgent { get; set; }
        [DataMember(Name = "hl", EmitDefaultValue = false)] public string ShihabHl { get; set; }
        [DataMember(Name = "gl", EmitDefaultValue = false)] public string MehediGl { get; set; }
        [DataMember(Name = "utcOffsetMinutes")] public int ShihabUtcOffsetMinutes { get; set; }
        [DataMember(Name = "visitorData", EmitDefaultValue = false)] public string MehediVisitorData { get; set; }
    }

    [DataContract]
    internal class MehediYtPlaybackContextDto
    {
        [DataMember(Name = "contentPlaybackContext")] public ShihabYtContentPlaybackContextDto MehediContentPlaybackContext { get; set; }
    }

    [DataContract]
    internal class ShihabYtContentPlaybackContextDto
    {
        [DataMember(Name = "html5Preference")] public string ShihabHtml5Preference { get; set; }
    }

    [DataContract]
    internal class MehediYtPlayerResponseDto
    {
        [DataMember(Name = "playabilityStatus")] public ShihabYtPlayabilityStatusDto ShihabPlayabilityStatus { get; set; }
        [DataMember(Name = "videoDetails")] public MehediYtVideoDetailsDto MehediVideoDetails { get; set; }
        [DataMember(Name = "streamingData")] public ShihabYtStreamingDataDto ShihabStreamingData { get; set; }
    }

    [DataContract]
    internal class ShihabYtPlayabilityStatusDto
    {
        [DataMember(Name = "status")] public string ShihabStatus { get; set; }
        [DataMember(Name = "reason")] public string MehediReason { get; set; }
    }

    [DataContract]
    internal class MehediYtVideoDetailsDto
    {
        [DataMember(Name = "videoId")] public string ShihabVideoId { get; set; }
        [DataMember(Name = "title")] public string MehediTitle { get; set; }
        [DataMember(Name = "lengthSeconds")] public string ShihabLengthSeconds { get; set; }
    }

    [DataContract]
    internal class ShihabYtStreamingDataDto
    {
        [DataMember(Name = "formats")] public List<MehediYtFormatDto> ShihabFormats { get; set; }
        [DataMember(Name = "adaptiveFormats")] public List<MehediYtFormatDto> MehediAdaptiveFormats { get; set; }
    }

    [DataContract]
    internal class MehediYtFormatDto
    {
        [DataMember(Name = "itag")] public int ShihabItag { get; set; }
        [DataMember(Name = "url")] public string ShihabUrl { get; set; }
        [DataMember(Name = "signatureCipher")] public string MehediSignatureCipher { get; set; }
        [DataMember(Name = "cipher")] public string ShihabCipher { get; set; }
        [DataMember(Name = "mimeType")] public string MehediMimeType { get; set; }
        [DataMember(Name = "bitrate")] public long ShihabBitrate { get; set; }
        [DataMember(Name = "averageBitrate")] public long MehediAverageBitrate { get; set; }
        [DataMember(Name = "width")] public int ShihabWidth { get; set; }
        [DataMember(Name = "height")] public int MehediHeight { get; set; }
        [DataMember(Name = "fps")] public int ShihabFps { get; set; }
        [DataMember(Name = "qualityLabel")] public string MehediQualityLabel { get; set; }
        [DataMember(Name = "contentLength")] public string ShihabContentLength { get; set; }
        [DataMember(Name = "approxDurationMs")] public string MehediApproxDurationMs { get; set; }
        [DataMember(Name = "audioChannels")] public int ShihabAudioChannels { get; set; }
        [DataMember(Name = "audioSampleRate")] public string MehediAudioSampleRate { get; set; }
        [DataMember(Name = "drmFamilies")] public List<string> ShihabDrmFamilies { get; set; }
        [DataMember(Name = "audioTrack")] public ShihabYtAudioTrackDto MehediAudioTrack { get; set; }
        [DataMember(Name = "colorInfo")] public MehediYtColorInfoDto ShihabColorInfo { get; set; }
    }

    [DataContract]
    internal class ShihabYtAudioTrackDto
    {
        [DataMember(Name = "displayName")] public string ShihabDisplayName { get; set; }
        [DataMember(Name = "id")] public string MehediId { get; set; }
        [DataMember(Name = "audioIsDefault")] public bool ShihabAudioIsDefault { get; set; }
    }

    [DataContract]
    internal class MehediYtColorInfoDto
    {
        [DataMember(Name = "transferCharacteristics")] public string ShihabTransferCharacteristics { get; set; }
    }

    internal sealed class ShihabCachedYtEntry
    {
        public DateTime MehediCachedAtUtc;
        public ShihabYouTubeStreamsReplyDto ShihabReply;
    }
}

