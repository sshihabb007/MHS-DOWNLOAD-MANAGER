"use strict";

const SHIHAB_BRIDGE_BASE_URL = "http://127.0.0.1:45779";
const MEHEDI_NATIVE_HOST_NAME = "com.shihab.mehedi.idm";
const SHIHAB_DEFAULT_EXTENSIONS = "3GP 7Z AAC ACE AIF APK ARJ ASF AVI BIN BZ2 EXE GZ GZIP IMG ISO LZH M4A M4V MKV MOV MP3 MP4 MPA MPE MPEG MPG MSI MSU OGG OGV PDF PLJ PPS PPT PPTX QT R0* R1* RA RAR RM RMVB SEA SIT SITX TAR TIF TIFF WAV WEBM WMA WMV XZ Z ZIP";

// Complete YouTube itag lookup for plain unencrypted HTTP streams
const SHIHAB_YOUTUBE_ITAG_MAP = {
  18: { mehediRes: "360p", shihabRole: "progressive", mehediExt: "mp4" },
  22: { mehediRes: "720p", shihabRole: "progressive", mehediExt: "mp4" },
  37: { mehediRes: "1080p", shihabRole: "progressive", mehediExt: "mp4" },
  59: { mehediRes: "480p", shihabRole: "progressive", mehediExt: "mp4" },
  78: { mehediRes: "480p", shihabRole: "progressive", mehediExt: "mp4" },
  133: { mehediRes: "240p", shihabRole: "video_only", mehediExt: "mp4" },
  134: { mehediRes: "360p", shihabRole: "video_only", mehediExt: "mp4" },
  135: { mehediRes: "480p", shihabRole: "video_only", mehediExt: "mp4" },
  136: { mehediRes: "720p", shihabRole: "video_only", mehediExt: "mp4" },
  137: { mehediRes: "1080p", shihabRole: "video_only", mehediExt: "mp4" },
  138: { mehediRes: "2160p", shihabRole: "video_only", mehediExt: "mp4" },
  160: { mehediRes: "144p", shihabRole: "video_only", mehediExt: "mp4" },
  264: { mehediRes: "1440p", shihabRole: "video_only", mehediExt: "mp4" },
  266: { mehediRes: "2160p", shihabRole: "video_only", mehediExt: "mp4" },
  298: { mehediRes: "720p", shihabRole: "video_only", mehediExt: "mp4" },
  299: { mehediRes: "1080p", shihabRole: "video_only", mehediExt: "mp4" },
  242: { mehediRes: "240p", shihabRole: "video_only", mehediExt: "webm" },
  243: { mehediRes: "360p", shihabRole: "video_only", mehediExt: "webm" },
  244: { mehediRes: "480p", shihabRole: "video_only", mehediExt: "webm" },
  247: { mehediRes: "720p", shihabRole: "video_only", mehediExt: "webm" },
  248: { mehediRes: "1080p", shihabRole: "video_only", mehediExt: "webm" },
  271: { mehediRes: "1440p", shihabRole: "video_only", mehediExt: "webm" },
  278: { mehediRes: "144p", shihabRole: "video_only", mehediExt: "webm" },
  302: { mehediRes: "720p", shihabRole: "video_only", mehediExt: "webm" },
  303: { mehediRes: "1080p", shihabRole: "video_only", mehediExt: "webm" },
  308: { mehediRes: "1440p", shihabRole: "video_only", mehediExt: "webm" },
  313: { mehediRes: "2160p", shihabRole: "video_only", mehediExt: "webm" },
  315: { mehediRes: "2160p", shihabRole: "video_only", mehediExt: "webm" },
  394: { mehediRes: "144p", shihabRole: "video_only", mehediExt: "mp4" },
  395: { mehediRes: "240p", shihabRole: "video_only", mehediExt: "mp4" },
  396: { mehediRes: "360p", shihabRole: "video_only", mehediExt: "mp4" },
  397: { mehediRes: "480p", shihabRole: "video_only", mehediExt: "mp4" },
  398: { mehediRes: "720p", shihabRole: "video_only", mehediExt: "mp4" },
  399: { mehediRes: "1080p", shihabRole: "video_only", mehediExt: "mp4" },
  400: { mehediRes: "1440p", shihabRole: "video_only", mehediExt: "mp4" },
  401: { mehediRes: "2160p", shihabRole: "video_only", mehediExt: "mp4" },
  139: { mehediRes: "48kbps", shihabRole: "audio", mehediExt: "m4a" },
  140: { mehediRes: "128kbps", shihabRole: "audio", mehediExt: "m4a" },
  141: { mehediRes: "256kbps", shihabRole: "audio", mehediExt: "m4a" },
  249: { mehediRes: "50kbps", shihabRole: "audio", mehediExt: "webm" },
  250: { mehediRes: "70kbps", shihabRole: "audio", mehediExt: "webm" },
  251: { mehediRes: "160kbps", shihabRole: "audio", mehediExt: "webm" },
  599: { mehediRes: "32kbps", shihabRole: "audio", mehediExt: "m4a" },
  600: { mehediRes: "35kbps", shihabRole: "audio", mehediExt: "webm" }
};

const shihabRuntimeState = {
  mehediDesktopOnline: false,
  shihabLastConfigSyncMs: 0,
  shihabIntegrationEnabled: true,
  mehediShowVideoPanel: true,
  shihabPanelMiniMode: false,
  mehediPanelSkipAutoStart: false,
  shihabPreventKey: "Alt",
  mehediForceKey: "Ins",
  shihabMinVideoSizeKb: 250,
  shihabCaptureOpenedFiles: false,
  mehediCaptureMinSizeKb: 0,
  mehediCaptureExtensionsSet: new Set(),
  shihabCaptureWildcardRegexes: [],
  shihabExcludedPatterns: ["localhost", "127.0.0.1", "*.local"],
  mehediExcludedAddressPatterns: [],
  mehediAltBypassUntilMsByTab: new Map(),
  shihabForceCaptureUntilMsByTab: new Map(),
  mehediDetectedStreamsByTab: new Map(),
  shihabRequestHeadersByUrl: new Map(),
  mehediResponseMetaByUrl: new Map(),
  mehediContainerProbeCache: new Map(),
  shihabExpandedHlsUrls: new Set(),
  mehediActiveRelayJobIds: new Set(),
  shihabHandledDownloadIds: new Set(),
  mehediInterceptingDownloadIds: new Set(),
  shihabInFlightYtResolves: new Map(),
  shihabWorkerBootMs: Date.now(),
  mehediBrowserStartupUntilMs: 0,
  mehediHasTrustedClickInSession: false,
  mehediLastTrustedGestureGlobalMs: 0,
  mehediTrustedGestureUntilByTab: new Map(),
  mehediTrustedGestureByHost: new Map(),
  mehediTrustedClickedUrls: new Map(),
  mehediUserCreatedNewTabs: new Map(),
  mehediRequestSourceTabByUrl: new Map(),
  shihabPostBodyByUrl: new Map(),
  shihabLocalExcludedSites: new Set()
};

function shihabIsPoisonedYouTubeWebUrl(mehediRawUrl) {
  if (!mehediRawUrl || typeof mehediRawUrl !== "string") return false;
  try {
    const shihabU = new URL(mehediRawUrl);
    const mehediH = (shihabU.hostname || "").toLowerCase();
    if (!mehediH.endsWith(".googlevideo.com") && mehediH !== "googlevideo.com") return false;
    const shihabClient = (shihabU.searchParams.get("c") || "").toUpperCase();
    if (
      shihabClient === "WEB" ||
      shihabClient === "MWEB" ||
      shihabClient === "WEB_REMIX" ||
      shihabClient === "WEB_CREATOR" ||
      shihabClient === "TVHTML5"
    ) {
      return true;
    }
    if (shihabU.searchParams.has("siu") || shihabU.searchParams.has("sabr") || shihabU.searchParams.has("ump")) {
      return true;
    }
  } catch (_) {}
  return false;
}

function mehediExtractYouTubeVideoIdFromPageUrl(shihabPageUrl) {
  if (!shihabPageUrl || typeof shihabPageUrl !== "string") return "";
  try {
    const mehediU = new URL(shihabPageUrl);
    const shihabH = (mehediU.hostname || "").toLowerCase();
    if (shihabH === "youtu.be" || shihabH.endsWith(".youtu.be")) {
      const mehediSeg = (mehediU.pathname || "").replace(/^\/+/, "").split("/")[0] || "";
      return /^[A-Za-z0-9_-]{11}$/.test(mehediSeg) ? mehediSeg : "";
    }
    if (
      shihabH === "youtube.com" ||
      shihabH.endsWith(".youtube.com") ||
      shihabH === "youtube-nocookie.com" ||
      shihabH.endsWith(".youtube-nocookie.com")
    ) {
      const mehediV = mehediU.searchParams.get("v") || "";
      if (/^[A-Za-z0-9_-]{11}$/.test(mehediV)) return mehediV;
      const shihabM = (mehediU.pathname || "").match(/^\/(?:shorts|embed|live|v)\/([A-Za-z0-9_-]{11})(?:[/?#]|$)/i);
      if (shihabM && shihabM[1]) return shihabM[1];
    }
  } catch (_) {}
  return "";
}

function shihabApplyExtensionSpec(mehediRawString) {
  const shihabExactSet = new Set();
  const mehediWildcards = [];
  const shihabParts = String(mehediRawString || SHIHAB_DEFAULT_EXTENSIONS).toLowerCase().split(/[\s,;]+/);
  for (const mehediTok of shihabParts) {
    const shihabClean = mehediTok.replace(/^\.+/, "").trim();
    if (!shihabClean) continue;
    if (shihabClean.includes("*") || shihabClean.includes("?")) {
      const mehediEsc = shihabClean.replace(/[.+^${}()|[\]\\]/g, "\\$&").replace(/\*/g, ".*").replace(/\?/g, ".");
      mehediWildcards.push(new RegExp("^" + mehediEsc + "$", "i"));
    } else {
      shihabExactSet.add(shihabClean);
    }
  }
  shihabRuntimeState.mehediCaptureExtensionsSet = shihabExactSet;
  shihabRuntimeState.shihabCaptureWildcardRegexes = mehediWildcards;
}
shihabApplyExtensionSpec(SHIHAB_DEFAULT_EXTENSIONS);

function mehediMatchesConfiguredExtension(shihabExtCandidate) {
  if (!shihabExtCandidate) return false;
  const mehediClean = String(shihabExtCandidate).toLowerCase().replace(/^\.+/, "").trim();
  if (!mehediClean) return false;
  if (shihabRuntimeState.mehediCaptureExtensionsSet.has(mehediClean)) {
    return true;
  }
  for (const shihabRx of shihabRuntimeState.shihabCaptureWildcardRegexes) {
    if (shihabRx.test(mehediClean)) {
      return true;
    }
  }
  return false;
}

function shihabExtractContentDispositionFileName(mehediRawDisp) {
  if (!mehediRawDisp) return "";
  const shihabStarMatch = String(mehediRawDisp).match(/filename\*\s*=\s*(?:UTF-8''|utf-8'')?([^;\r\n]+)/i);
  if (shihabStarMatch && shihabStarMatch[1]) {
    try {
      const mehediDecoded = decodeURIComponent(shihabStarMatch[1].trim().replace(/^["']|["']$/g, ""));
      if (mehediDecoded) return mehediDecoded;
    } catch (_) {}
  }
  const mehediQuotedMatch = String(mehediRawDisp).match(/filename\s*=\s*"([^"]+)"/i);
  if (mehediQuotedMatch && mehediQuotedMatch[1]) {
    return mehediQuotedMatch[1].trim();
  }
  const shihabPlainMatch = String(mehediRawDisp).match(/filename\s*=\s*([^;\r\n]+)/i);
  if (shihabPlainMatch && shihabPlainMatch[1]) {
    return shihabPlainMatch[1].trim().replace(/^["']|["']$/g, "");
  }
  return "";
}

function mehediInferExtensionFromAttachmentMime(shihabMimeRaw, mehediHasAttachmentHeader) {
  const shihabMime = String(shihabMimeRaw || "").split(";")[0].trim().toLowerCase();
  if (!shihabMime) return "";
  const mehediMimeExtTable = {
    "video/x-matroska": "mkv",
    "video/matroska": "mkv",
    "video/mp4": "mp4",
    "video/webm": "webm",
    "video/quicktime": "mov",
    "video/x-msvideo": "avi",
    "video/x-ms-wmv": "wmv",
    "video/x-flv": "flv",
    "audio/mpeg": "mp3",
    "audio/mp4": "m4a",
    "audio/x-m4a": "m4a",
    "audio/wav": "wav",
    "audio/flac": "flac",
    "audio/ogg": "ogg",
    "application/zip": "zip",
    "application/x-zip-compressed": "zip",
    "application/x-rar-compressed": "rar",
    "application/vnd.rar": "rar",
    "application/x-7z-compressed": "7z",
    "application/gzip": "gz",
    "application/x-gzip": "gz",
    "application/x-bzip2": "bz2",
    "application/x-xz": "xz",
    "application/x-tar": "tar",
    "application/x-iso9660-image": "iso",
    "application/vnd.android.package-archive": "apk",
    "application/x-msdownload": "exe",
    "application/x-msi": "msi"
  };
  if (mehediMimeExtTable[shihabMime]) {
    return mehediMimeExtTable[shihabMime];
  }
  if (mehediHasAttachmentHeader && shihabMime === "application/pdf") {
    return "pdf";
  }
  return "";
}

function mehediWildcardMatchHost(shihabHostname, mehediPatternsList) {
  if (!shihabHostname || !Array.isArray(mehediPatternsList)) {
    return false;
  }
  const shihabLowerHost = shihabHostname.toLowerCase();
  for (const mehediRawPat of mehediPatternsList) {
    const shihabPat = String(mehediRawPat || "").trim().toLowerCase();
    if (!shihabPat) continue;
    const mehediEscaped = shihabPat.replace(/[.+^${}()|[\]\\]/g, "\\$&").replace(/\*/g, ".*").replace(/\?/g, ".");
    const shihabRegex = new RegExp("^" + mehediEscaped + "$", "i");
    if (shihabRegex.test(shihabLowerHost)) {
      return true;
    }
  }
  return false;
}

function shihabHeightRatingFromDimensions(mehediWidth, shihabHeight) {
  const mehediW = Number(mehediWidth || 0);
  const shihabH = Number(shihabHeight || 0);
  if (mehediW <= 0 && shihabH <= 0) return "";
  const mehediShortEdge = (mehediW > 0 && shihabH > 0) ? Math.min(mehediW, shihabH) : Math.max(mehediW, shihabH);
  if (mehediShortEdge >= 2000) return "2160p";
  if (mehediShortEdge >= 1350) return "1440p";
  if (mehediShortEdge >= 1000) return "1080p";
  if (mehediShortEdge >= 680) return "720p";
  if (mehediShortEdge >= 510) return "540p";
  if (mehediShortEdge >= 440) return "480p";
  if (mehediShortEdge >= 320) return "360p";
  if (mehediShortEdge >= 210) return "240p";
  return "144p";
}

function mehediNumericHeightRank(shihabResLabel) {
  const mehediMatch = String(shihabResLabel || "").match(/(\d{3,4})p/i);
  return mehediMatch ? parseInt(mehediMatch[1], 10) : 0;
}

function shihabExtractExtensionFromUrlOrName(mehediUrl, shihabFilename) {
  if (shihabFilename) {
    const mehediDot = shihabFilename.lastIndexOf(".");
    if (mehediDot > 0 && mehediDot < shihabFilename.length - 1) {
      return shihabFilename.slice(mehediDot + 1).toLowerCase();
    }
  }
  try {
    const shihabParsed = new URL(mehediUrl);
    const mehediItag = parseInt(shihabParsed.searchParams.get("itag") || "0", 10);
    if (mehediItag && SHIHAB_YOUTUBE_ITAG_MAP[mehediItag]) {
      return SHIHAB_YOUTUBE_ITAG_MAP[mehediItag].mehediExt;
    }
    const mehediQueryMime = (shihabParsed.searchParams.get("mime") || "").toLowerCase();
    if (mehediQueryMime.includes("mp4")) return mehediQueryMime.startsWith("audio/") ? "m4a" : "mp4";
    if (mehediQueryMime.includes("webm")) return "webm";
    if (mehediQueryMime.includes("mpegurl")) return "m3u8";

    const mehediLeaf = decodeURIComponent(shihabParsed.pathname.split("/").pop() || "");
    const shihabDot = mehediLeaf.lastIndexOf(".");
    if (shihabDot > 0 && shihabDot < mehediLeaf.length - 1) {
      return mehediLeaf.slice(shihabDot + 1).toLowerCase();
    }
  } catch (shihabErr) {
    // Ignore malformed URL
  }
  return "";
}

function shihabIsGibberishOrCdnFileName(mehediRawName) {
  if (!mehediRawName) return true;
  const shihabBase = String(mehediRawName).split(/[\\/]/).pop().replace(/\.[a-z0-9]{2,5}$/i, "").trim();
  if (!shihabBase || shihabBase.length < 2) return true;

  const mehediGenericSet = new Set([
    "video", "videoplayback", "playback", "media", "master", "index", "playlist",
    "chunklist", "manifest", "stream", "download", "file", "watch", "reel", "reels",
    "audio", "clip", "mhs video", "mhs_video", "instagram", "instagram video",
    "facebook", "facebook video", "youtube", "youtube video"
  ]);
  if (mehediGenericSet.has(shihabBase.toLowerCase())) return true;

  if (/^(AQ[A-Za-z0-9_-]{8,}|An[A-Za-z0-9_-]{8,}|[0-9]{6,}_[0-9]{6,}[0-9_a-z]*)$/i.test(shihabBase)) {
    return true;
  }
  if (!/\s/.test(shihabBase)) {
    if (/^[0-9a-f-]{16,}$/i.test(shihabBase)) return true;
    if (shihabBase.length >= 22 && /^[A-Za-z0-9_-]+$/.test(shihabBase)) {
      const mehediTransitions = (shihabBase.match(/[a-z][A-Z]|[A-Z][a-z][0-9]|[0-9][A-Za-z]/g) || []).length;
      if (mehediTransitions >= 3) return true;
    }
  }
  return false;
}

function mehediSanitizeVideoTitleForFile(shihabRawTitle, mehediFallbackUrl) {
  let shihabClean = String(shihabRawTitle || "")
    .replace(/^\(\d+\+?\)\s*/, "")
    .replace(/^[▶►•·\s]+/, "")
    .replace(/\s*(?:[|\-·•—]\s*(?:Facebook|Watch|Reels|YouTube|Instagram|TikTok|X|Twitter|Reddit|Vimeo|Dailymotion|Twitch))+\s*$/gi, "")
    .replace(/\s*(?:Hide translation|See translation|See original|Translated from [A-Za-z ]+)\s*$/gi, "")
    .replace(/\s*(?:\.\.\.|…)?\s*(?:more|See more|See less|আরও দেখুন|কম দেখুন)\s*$/gi, "")
    .replace(/[\\/:*?"<>|\x00-\x1f]+/g, " ")
    .replace(/\s+/g, " ")
    .replace(/[. ]+$/, "")
    .trim();

  const mehediNoiseOnly = /^(facebook|reels?|watch|youtube|instagram|home|notifications|video|untitled|mhs video|mhs_video)$/i;
  if (!shihabClean || mehediNoiseOnly.test(shihabClean)) {
    shihabClean = "";
  }

  if (!shihabClean && mehediFallbackUrl) {
    try {
      const shihabPage = new URL(mehediFallbackUrl);
      const mehediReelMatch = shihabPage.pathname.match(/\/(reel|reels|p|videos|shorts|watch)\/([A-Za-z0-9_-]+)/i);
      const shihabHostShort = shihabPage.hostname.replace(/^www\./i, "").split(".")[0];
      const mehediSiteLabel = shihabHostShort ? (shihabHostShort.charAt(0).toUpperCase() + shihabHostShort.slice(1)) : "Video";
      if (mehediReelMatch) {
        shihabClean = `${mehediSiteLabel} ${mehediReelMatch[1]} ${mehediReelMatch[2]}`;
      } else if (shihabPage.searchParams.get("v")) {
        shihabClean = `${mehediSiteLabel} Video ${shihabPage.searchParams.get("v")}`;
      } else {
        shihabClean = `${mehediSiteLabel} Video`;
      }
    } catch (mehediErr) {
      shihabClean = "Video";
    }
  }

  if (shihabClean.length > 105) {
    shihabClean = shihabClean.slice(0, 105).trim();
  }
  return shihabClean || "Video";
}

function shihabNormalizeMediaStreamUrl(mehediRawUrl) {
  try {
    const shihabUrlObj = new URL(mehediRawUrl);
    let mehediWasRangeSliced = false;

    for (const mehediParam of ["bytestart", "byteend", "alr"]) {
      if (shihabUrlObj.searchParams.has(mehediParam)) {
        shihabUrlObj.searchParams.delete(mehediParam);
        if (mehediParam !== "alr") mehediWasRangeSliced = true;
      }
    }

    if (shihabUrlObj.searchParams.has("range")) {
      const mehediSignedParams = (shihabUrlObj.searchParams.get("sparams") || "").split(",");
      const shihabRangeSigned = mehediSignedParams.includes("range");
      if (!shihabRangeSigned) {
        shihabUrlObj.searchParams.delete("range");
        shihabUrlObj.searchParams.delete("rn");
        shihabUrlObj.searchParams.delete("rbuf");
        mehediWasRangeSliced = true;
      }
    }

    return {
      shihabCleanUrl: shihabUrlObj.toString(),
      mehediWasSliced: mehediWasRangeSliced
    };
  } catch (shihabUrlErr) {
    return {
      shihabCleanUrl: mehediRawUrl,
      mehediWasSliced: false
    };
  }
}

// Decode Instagram / Facebook efg=<base64_json> parameter to extract asset ID, audio vs video role, and resolution
function mehediInspectUrlQueryMetadata(shihabCleanUrl) {
  const mehediResult = {
    shihabClenBytes: -1,
    mehediTrackRole: "",
    shihabResolution: "",
    mehediGroupKey: "",
    shihabAltIds: [],
    shihabExtOverride: "",
    mehediIsGoogleVideo: false
  };

  try {
    const shihabParsed = new URL(shihabCleanUrl);
    const mehediHost = shihabParsed.hostname.toLowerCase();

    if (mehediHost.endsWith(".googlevideo.com") || shihabParsed.pathname.includes("/videoplayback")) {
      mehediResult.mehediIsGoogleVideo = true;
      const shihabClen = parseInt(shihabParsed.searchParams.get("clen") || "-1", 10);
      if (shihabClen > 0) {
        mehediResult.shihabClenBytes = shihabClen;
      }
      const mehediItag = parseInt(shihabParsed.searchParams.get("itag") || "0", 10);
      if (mehediItag && SHIHAB_YOUTUBE_ITAG_MAP[mehediItag]) {
        const shihabSpec = SHIHAB_YOUTUBE_ITAG_MAP[mehediItag];
        mehediResult.shihabResolution = shihabSpec.mehediRes;
        mehediResult.mehediTrackRole = shihabSpec.shihabRole;
        mehediResult.shihabExtOverride = shihabSpec.mehediExt;
      } else {
        const mehediMime = (shihabParsed.searchParams.get("mime") || "").toLowerCase();
        if (mehediMime.startsWith("audio/")) {
          mehediResult.mehediTrackRole = "audio";
          mehediResult.shihabExtOverride = mehediMime.includes("mp4") ? "m4a" : "webm";
        } else if (mehediMime.startsWith("video/")) {
          mehediResult.mehediTrackRole = "video_only";
          mehediResult.shihabExtOverride = mehediMime.includes("webm") ? "webm" : "mp4";
        }
      }
      const shihabDocId = shihabParsed.searchParams.get("docid") || "";
      if (shihabDocId) {
        mehediResult.mehediGroupKey = `yt:${shihabDocId}`;
        mehediResult.shihabAltIds.push(shihabDocId, `yt:${shihabDocId}`);
      }
    }

    const mehediEfgParam = shihabParsed.searchParams.get("efg");
    if (mehediEfgParam) {
      const shihabB64 = decodeURIComponent(mehediEfgParam).replace(/-/g, "+").replace(/_/g, "/");
      const mehediPadded = shihabB64 + "=".repeat((4 - (shihabB64.length % 4)) % 4);
      const shihabDecodedStr = atob(mehediPadded);
      const mehediEfgObj = JSON.parse(shihabDecodedStr);
      if (mehediEfgObj && typeof mehediEfgObj === "object") {
        const shihabXpvId = mehediEfgObj.xpv_asset_id ? String(mehediEfgObj.xpv_asset_id) : "";
        const mehediVidId = mehediEfgObj.video_id ? String(mehediEfgObj.video_id) : "";
        const shihabAstId = mehediEfgObj.asset_id ? String(mehediEfgObj.asset_id) : "";
        if (shihabXpvId) {
          mehediResult.mehediGroupKey = `asset:${shihabXpvId}`;
          mehediResult.shihabAltIds.push(shihabXpvId, `asset:${shihabXpvId}`);
        }
        if (mehediVidId) {
          if (!mehediResult.mehediGroupKey) {
            mehediResult.mehediGroupKey = `vid:${mehediVidId}`;
          }
          mehediResult.shihabAltIds.push(mehediVidId, `vid:${mehediVidId}`);
        }
        if (shihabAstId) {
          if (!mehediResult.mehediGroupKey) {
            mehediResult.mehediGroupKey = `asset:${shihabAstId}`;
          }
          mehediResult.shihabAltIds.push(shihabAstId, `asset:${shihabAstId}`);
        }
        const mehediTag = String(mehediEfgObj.vencode_tag || mehediEfgObj.encode_tag || "").toLowerCase();
        if (mehediTag) {
          if (mehediTag.includes("audio") || mehediTag.includes("heaac") || mehediTag.includes("aac") || mehediTag.endsWith("_abr")) {
            mehediResult.mehediTrackRole = "audio";
            mehediResult.shihabExtOverride = "m4a";
          } else if (mehediTag.includes("progressive") || mehediTag.startsWith("svr_")) {
            mehediResult.mehediTrackRole = "progressive";
          } else if (mehediTag.includes("dash") || mehediTag.includes("vp9") || mehediTag.includes("av1") || mehediTag.includes("h264") || /_q\d0/.test(mehediTag)) {
            mehediResult.mehediTrackRole = "video_only";
          }

          const shihabResInTag = mehediTag.match(/(?:^|[_.-])(2160p|1440p|1080p|720p|540p|480p|360p|240p|144p)(?:$|[_.-])/i);
          if (shihabResInTag) {
            mehediResult.shihabResolution = shihabResInTag[1].toLowerCase();
          } else {
            const mehediDimsInTag = mehediTag.match(/(\d{3,4})x(\d{3,4})/);
            if (mehediDimsInTag) {
              mehediResult.shihabResolution = shihabHeightRatingFromDimensions(
                parseInt(mehediDimsInTag[1], 10),
                parseInt(mehediDimsInTag[2], 10)
              );
            }
          }
        }
      }
    }

    if (!mehediResult.shihabResolution) {
      const shihabPathDims = shihabParsed.pathname.match(/\/(\d{3,4})x(\d{3,4})\//);
      if (shihabPathDims) {
        mehediResult.shihabResolution = shihabHeightRatingFromDimensions(
          parseInt(shihabPathDims[1], 10),
          parseInt(shihabPathDims[2], 10)
        );
      } else {
        const mehediPathP = shihabParsed.pathname.match(/(?:^|[^0-9])(2160p|1440p|1080p|720p|540p|480p|360p|240p|144p)(?:[^0-9]|$)/i);
        if (mehediPathP) {
          mehediResult.shihabResolution = mehediPathP[1].toLowerCase();
        }
      }
    }
  } catch (mehediInspectErr) {
    // Ignore decode errors
  }

  return mehediResult;
}

// Parse ISO-BMFF (MP4/M4A/fMP4) or WebM header bytes (first 32 KB) to determine exact resolution & video vs audio tracks!
function shihabParseBinaryContainerHeader(mehediBytes) {
  const shihabOut = {
    shihabHasVideo: false,
    mehediHasAudio: false,
    shihabIsDrm: false,
    mehediWidth: 0,
    shihabHeight: 0
  };
  if (!mehediBytes || mehediBytes.length < 16) return shihabOut;

  // 1) WebM / Matroska EBML magic: 1A 45 DF A3
  if (mehediBytes[0] === 0x1a && mehediBytes[1] === 0x45 && mehediBytes[2] === 0xdf && mehediBytes[3] === 0xa3) {
    for (let mehediI = 4; mehediI < mehediBytes.length - 6; mehediI++) {
      // TrackType element (0x83, size 0x81, val 1=video, 2=audio)
      if (mehediBytes[mehediI] === 0x83 && mehediBytes[mehediI + 1] === 0x81) {
        if (mehediBytes[mehediI + 2] === 1) shihabOut.shihabHasVideo = true;
        else if (mehediBytes[mehediI + 2] === 2) shihabOut.mehediHasAudio = true;
      }
      // PixelWidth (0xB0)
      if (mehediBytes[mehediI] === 0xb0 && (mehediBytes[mehediI + 1] === 0x82 || mehediBytes[mehediI + 1] === 0x81)) {
        const shihabLen = mehediBytes[mehediI + 1] & 0x7f;
        const mehediW = shihabLen === 2 ? ((mehediBytes[mehediI + 2] << 8) | mehediBytes[mehediI + 3]) : mehediBytes[mehediI + 2];
        if (mehediW >= 64 && mehediW <= 8192 && shihabOut.mehediWidth === 0) shihabOut.mehediWidth = mehediW;
      }
      // PixelHeight (0xBA)
      if (mehediBytes[mehediI] === 0xba && (mehediBytes[mehediI + 1] === 0x82 || mehediBytes[mehediI + 1] === 0x81)) {
        const shihabLen = mehediBytes[mehediI + 1] & 0x7f;
        const mehediH = shihabLen === 2 ? ((mehediBytes[mehediI + 2] << 8) | mehediBytes[mehediI + 3]) : mehediBytes[mehediI + 2];
        if (mehediH >= 64 && mehediH <= 8192 && shihabOut.shihabHeight === 0) shihabOut.shihabHeight = mehediH;
      }
      // ContentEncryption (0x6D80)
      if (mehediBytes[mehediI] === 0x6d && mehediBytes[mehediI + 1] === 0x80) {
        shihabOut.shihabIsDrm = true;
      }
    }
    return shihabOut;
  }

  // 2) ISO-BMFF (MP4 / M4A / MOV / fMP4) recursive box walker
  const mehediView = new DataView(mehediBytes.buffer, mehediBytes.byteOffset, mehediBytes.byteLength);
  const shihabContainerTypes = new Set(["moov", "trak", "mdia", "minf", "stbl", "edts", "dinf", "mvex"]);

  function mehediWalkBoxes(shihabStart, mehediLimit) {
    let shihabPos = shihabStart;
    while (shihabPos + 8 <= mehediLimit && shihabPos + 8 <= mehediBytes.length) {
      let mehediBoxSize = mehediView.getUint32(shihabPos, false);
      const shihabType = String.fromCharCode(
        mehediBytes[shihabPos + 4],
        mehediBytes[shihabPos + 5],
        mehediBytes[shihabPos + 6],
        mehediBytes[shihabPos + 7]
      );
      let mehediHdrLen = 8;
      if (mehediBoxSize === 1) {
        if (shihabPos + 16 > mehediBytes.length) break;
        const shihabHi = mehediView.getUint32(shihabPos + 8, false);
        const mehediLo = mehediView.getUint32(shihabPos + 12, false);
        mehediBoxSize = (shihabHi * 4294967296) + mehediLo;
        mehediHdrLen = 16;
      } else if (mehediBoxSize === 0) {
        mehediBoxSize = mehediLimit - shihabPos;
      }
      if (mehediBoxSize < mehediHdrLen) break;

      const shihabBoxEnd = Math.min(mehediLimit, Math.min(mehediBytes.length, shihabPos + mehediBoxSize));

      if (shihabType === "pssh" || shihabType === "sinf" || shihabType === "encv" || shihabType === "enca") {
        shihabOut.shihabIsDrm = true;
      } else if (shihabType === "hdlr" && shihabPos + mehediHdrLen + 12 <= mehediBytes.length) {
        const mehediSub = String.fromCharCode(
          mehediBytes[shihabPos + mehediHdrLen + 8],
          mehediBytes[shihabPos + mehediHdrLen + 9],
          mehediBytes[shihabPos + mehediHdrLen + 10],
          mehediBytes[shihabPos + mehediHdrLen + 11]
        );
        if (mehediSub === "vide") shihabOut.shihabHasVideo = true;
        else if (mehediSub === "soun") shihabOut.mehediHasAudio = true;
      } else if (shihabType === "tkhd" && shihabPos + mehediHdrLen + 84 <= mehediBytes.length) {
        const shihabVer = mehediBytes[shihabPos + mehediHdrLen];
        const mehediWidthOffset = shihabPos + mehediHdrLen + (shihabVer === 1 ? 88 : 76);
        if (mehediWidthOffset + 8 <= mehediBytes.length) {
          const shihabFixedW = mehediView.getUint32(mehediWidthOffset, false) >>> 16;
          const mehediFixedH = mehediView.getUint32(mehediWidthOffset + 4, false) >>> 16;
          if (shihabFixedW > 0 && mehediFixedH > 0) {
            shihabOut.mehediWidth = shihabFixedW;
            shihabOut.shihabHeight = mehediFixedH;
          }
        }
      } else if (shihabType === "stsd" && shihabPos + mehediHdrLen + 16 <= mehediBytes.length) {
        const mehediEntryFourCc = String.fromCharCode(
          mehediBytes[shihabPos + mehediHdrLen + 12],
          mehediBytes[shihabPos + mehediHdrLen + 13],
          mehediBytes[shihabPos + mehediHdrLen + 14],
          mehediBytes[shihabPos + mehediHdrLen + 15]
        );
        if (mehediEntryFourCc === "encv" || mehediEntryFourCc === "enca") {
          shihabOut.shihabIsDrm = true;
        }
      } else if (shihabContainerTypes.has(shihabType)) {
        mehediWalkBoxes(shihabPos + mehediHdrLen, shihabBoxEnd);
      }

      if (shihabPos + mehediBoxSize > mehediBytes.length) {
        break;
      }
      shihabPos += mehediBoxSize;
    }
  }

  mehediWalkBoxes(0, mehediBytes.length);
  return shihabOut;
}

// Probe first 32 KB of a media stream to resolve exact Content-Range total size, resolution, and audio/video tracks
async function mehediProbeStreamBinaryHeaderOnce(shihabStreamRecord, mehediTabId) {
  const shihabUrl = shihabStreamRecord.shihabStreamUrl;
  if (!shihabUrl || shihabStreamRecord.shihabExt === "m3u8") return;

  if (shihabRuntimeState.mehediContainerProbeCache.has(shihabUrl)) {
    const mehediCached = shihabRuntimeState.mehediContainerProbeCache.get(shihabUrl);
    if (mehediCached) {
      shihabApplyProbeResultToStream(shihabStreamRecord, mehediCached);
    }
    return;
  }

  // Reserve slot so parallel calls don't duplicate network requests
  shihabRuntimeState.mehediContainerProbeCache.set(shihabUrl, null);
  if (shihabRuntimeState.mehediContainerProbeCache.size > 300) {
    const shihabOldest = shihabRuntimeState.mehediContainerProbeCache.keys().next().value;
    shihabRuntimeState.mehediContainerProbeCache.delete(shihabOldest);
  }

  try {
    const mehediIsGv = shihabStreamRecord.mehediIsGoogleVideo;
    const shihabProbeUrl = mehediIsGv
      ? (shihabUrl + (shihabUrl.includes("?") ? "&" : "?") + "range=0-32767")
      : shihabUrl;
    const mehediController = new AbortController();
    const shihabTimeout = setTimeout(() => mehediController.abort(), 5000);
    const mehediFetchHeaders = mehediIsGv ? {} : { Range: "bytes=0-32767" };

    const shihabResp = await fetch(shihabProbeUrl, {
      method: "GET",
      headers: mehediFetchHeaders,
      credentials: shihabStreamRecord.mehediOmitCookies ? "omit" : "include",
      signal: mehediController.signal
    });

    if (!shihabResp.ok && shihabResp.status !== 206) {
      clearTimeout(shihabTimeout);
      return;
    }

    let mehediResolvedTotal = -1;
    const shihabContentRange = shihabResp.headers.get("content-range") || "";
    if (shihabContentRange) {
      const mehediSlash = shihabContentRange.lastIndexOf("/");
      if (mehediSlash > 0) {
        const shihabParsedTotal = parseInt(shihabContentRange.slice(mehediSlash + 1).trim(), 10);
        if (shihabParsedTotal > 0) mehediResolvedTotal = shihabParsedTotal;
      }
    } else if (shihabResp.status === 200 && !mehediIsGv) {
      const mehediCl = parseInt(shihabResp.headers.get("content-length") || "-1", 10);
      if (mehediCl > 0) mehediResolvedTotal = mehediCl;
    }

    const shihabChunks = [];
    let mehediAccum = 0;
    if (shihabResp.body && typeof shihabResp.body.getReader === "function") {
      const shihabReader = shihabResp.body.getReader();
      while (mehediAccum < 32768) {
        const { done: mehediDone, value: shihabVal } = await shihabReader.read();
        if (mehediDone || !shihabVal) break;
        shihabChunks.push(shihabVal);
        mehediAccum += shihabVal.byteLength;
      }
      try { await shihabReader.cancel(); } catch (_) {}
    } else {
      const mehediAb = await shihabResp.arrayBuffer();
      shihabChunks.push(new Uint8Array(mehediAb, 0, Math.min(32768, mehediAb.byteLength)));
      mehediAccum = shihabChunks[0].byteLength;
    }
    clearTimeout(shihabTimeout);

    const mehediMerged = new Uint8Array(mehediAccum);
    let shihabCursor = 0;
    for (const mehediPart of shihabChunks) {
      mehediMerged.set(mehediPart, shihabCursor);
      shihabCursor += mehediPart.byteLength;
    }

    const shihabParsedHdr = shihabParseBinaryContainerHeader(mehediMerged);
    shihabParsedHdr.mehediTotalBytes = mehediResolvedTotal;
    shihabRuntimeState.mehediContainerProbeCache.set(shihabUrl, shihabParsedHdr);

    shihabApplyProbeResultToStream(shihabStreamRecord, shihabParsedHdr);
    shihabPushUpdatedStreamsToTab(mehediTabId);
  } catch (mehediProbeErr) {
    // Ignore CORS / network probe failures; URL/manifest metadata still applies
  }
}

function shihabApplyProbeResultToStream(mehediRecord, shihabProbe) {
  if (!mehediRecord || !shihabProbe) return;
  if (shihabProbe.shihabIsDrm) {
    mehediRecord.shihabIsDrm = true;
    return;
  }
  if (shihabProbe.mehediTotalBytes > 0 && (mehediRecord.shihabSizeBytes <= 0 || !mehediRecord.shihabExactSizeKnown)) {
    mehediRecord.shihabSizeBytes = shihabProbe.mehediTotalBytes;
    mehediRecord.shihabExactSizeKnown = true;
  }
  if (shihabProbe.mehediWidth > 0 && shihabProbe.shihabHeight > 0) {
    mehediRecord.shihabWidth = shihabProbe.mehediWidth;
    mehediRecord.mehediHeight = shihabProbe.shihabHeight;
    if (!mehediRecord.shihabResolution) {
      mehediRecord.shihabResolution = shihabHeightRatingFromDimensions(shihabProbe.mehediWidth, shihabProbe.shihabHeight);
    }
  }
  if (shihabProbe.shihabHasVideo && shihabProbe.mehediHasAudio) {
    mehediRecord.mehediTrackRole = "progressive";
  } else if (shihabProbe.shihabHasVideo && !shihabProbe.mehediHasAudio) {
    mehediRecord.mehediTrackRole = "video_only";
  } else if (!shihabProbe.shihabHasVideo && shihabProbe.mehediHasAudio) {
    mehediRecord.mehediTrackRole = "audio";
    if (mehediRecord.shihabExt === "mp4") {
      mehediRecord.shihabExt = "m4a";
    }
  }
}

// Expand HLS master playlists (#EXT-X-STREAM-INF) into explicit 1080p / 720p / 480p / Audio entries!
async function shihabExpandHlsMasterPlaylistOnce(mehediMasterUrl, shihabTabId) {
  if (!mehediMasterUrl || shihabRuntimeState.shihabExpandedHlsUrls.has(mehediMasterUrl)) {
    return;
  }
  shihabRuntimeState.shihabExpandedHlsUrls.add(mehediMasterUrl);

  try {
    const mehediResp = await fetch(mehediMasterUrl, { credentials: "include" });
    if (!mehediResp.ok) return;
    const shihabText = await mehediResp.text();
    if (!shihabText || !shihabText.includes("#EXT-X-STREAM-INF")) return;
    if (/#EXT-X-KEY:(?!.*METHOD=NONE)/i.test(shihabText)) return;

    const mehediLines = shihabText.split(/\r?\n/).map((shihabL) => shihabL.trim()).filter(Boolean);
    const shihabAudioGroups = new Map();

    for (const mehediLine of mehediLines) {
      if (mehediLine.startsWith("#EXT-X-MEDIA:") && /TYPE=AUDIO/i.test(mehediLine)) {
        const shihabGrp = (mehediLine.match(/GROUP-ID="([^"]+)"/i) || [])[1];
        const mehediUri = (mehediLine.match(/URI="([^"]+)"/i) || [])[1];
        if (shihabGrp && mehediUri) {
          try {
            const shihabAbsAud = new URL(mehediUri, mehediMasterUrl).toString();
            if (!shihabAudioGroups.has(shihabGrp) || /DEFAULT=YES/i.test(mehediLine)) {
              shihabAudioGroups.set(shihabGrp, shihabAbsAud);
            }
          } catch (_) {}
        }
      }
    }

    let mehediAddedAny = false;
    for (let shihabI = 0; shihabI < mehediLines.length - 1; shihabI++) {
      const mehediInf = mehediLines[shihabI];
      if (!mehediInf.startsWith("#EXT-X-STREAM-INF")) continue;
      const shihabNextUri = mehediLines[shihabI + 1];
      if (!shihabNextUri || shihabNextUri.startsWith("#")) continue;

      let mehediVariantUrl = "";
      try {
        mehediVariantUrl = new URL(shihabNextUri, mehediMasterUrl).toString();
      } catch (_) {
        continue;
      }

      const shihabResAttr = mehediInf.match(/RESOLUTION=(\d+)x(\d+)/i);
      const mehediW = shihabResAttr ? parseInt(shihabResAttr[1], 10) : 0;
      const shihabH = shihabResAttr ? parseInt(shihabResAttr[2], 10) : 0;
      const mehediBwMatch = mehediInf.match(/BANDWIDTH=(\d+)/i);
      const shihabBw = mehediBwMatch ? parseInt(mehediBwMatch[1], 10) : 0;
      const mehediAudGrp = (mehediInf.match(/AUDIO="([^"]+)"/i) || [])[1] || "";
      const shihabPairedAudUrl = mehediAudGrp ? (shihabAudioGroups.get(mehediAudGrp) || "") : "";

      shihabUpsertTabStreamRecord(shihabTabId, {
        shihabStreamUrl: mehediVariantUrl,
        mehediMimeType: "application/vnd.apple.mpegurl",
        shihabExt: "m3u8",
        shihabSizeBytes: -1,
        mehediTrackRole: shihabPairedAudUrl ? "video_only" : "progressive",
        shihabWidth: mehediW,
        mehediHeight: shihabH,
        shihabResolution: shihabHeightRatingFromDimensions(mehediW, shihabH),
        mehediBandwidth: shihabBw,
        shihabGroupKey: `hls:${mehediMasterUrl.split("?")[0]}`,
        mehediAudioCompanionUrl: shihabPairedAudUrl
      });
      mehediAddedAny = true;
    }

    if (mehediAddedAny) {
      shihabPushUpdatedStreamsToTab(shihabTabId);
    }
  } catch (_) {}
}

function shihabUpsertTabStreamRecord(mehediTabId, shihabIncoming) {
  if (typeof mehediTabId !== "number" || mehediTabId < 0 || !shihabIncoming || !shihabIncoming.shihabStreamUrl) {
    return null;
  }
  if (shihabIsPoisonedYouTubeWebUrl(shihabIncoming.shihabStreamUrl)) {
    return null;
  }

  let mehediTabStreams = shihabRuntimeState.mehediDetectedStreamsByTab.get(mehediTabId);
  if (!mehediTabStreams) {
    mehediTabStreams = [];
    shihabRuntimeState.mehediDetectedStreamsByTab.set(mehediTabId, mehediTabStreams);
  }

  const shihabUrlMeta = mehediInspectUrlQueryMetadata(shihabIncoming.shihabStreamUrl);
  const mehediHdrInfo = shihabRuntimeState.shihabRequestHeadersByUrl.get(shihabIncoming.shihabStreamUrl) || null;

  const shihabEffectiveRole =
    shihabIncoming.mehediTrackRole ||
    shihabUrlMeta.mehediTrackRole ||
    (String(shihabIncoming.mehediMimeType || "").startsWith("audio/") ? "audio" : "progressive");

  const mehediEffectiveRes =
    shihabIncoming.shihabResolution ||
    shihabUrlMeta.shihabResolution ||
    shihabHeightRatingFromDimensions(shihabIncoming.shihabWidth, shihabIncoming.mehediHeight);

  const shihabEffectiveExt =
    shihabUrlMeta.shihabExtOverride ||
    shihabIncoming.shihabExt ||
    shihabExtractExtensionFromUrlOrName(shihabIncoming.shihabStreamUrl, "") ||
    (shihabEffectiveRole === "audio" ? "m4a" : "mp4");

  const mehediEffectiveSize =
    (shihabIncoming.shihabSizeBytes && shihabIncoming.shihabSizeBytes > 0)
      ? shihabIncoming.shihabSizeBytes
      : (shihabUrlMeta.shihabClenBytes > 0 ? shihabUrlMeta.shihabClenBytes : -1);

  let shihabEffectiveGroup = shihabIncoming.shihabFromManifest
    ? (shihabIncoming.shihabGroupKey || shihabUrlMeta.mehediGroupKey || "")
    : (shihabUrlMeta.mehediGroupKey || shihabIncoming.shihabGroupKey || "");

  const mehediCombinedAltIds = Array.from(new Set([
    ...(Array.isArray(shihabIncoming.mehediAltIds) ? shihabIncoming.mehediAltIds.map(String) : []),
    ...(Array.isArray(shihabUrlMeta.shihabAltIds) ? shihabUrlMeta.shihabAltIds.map(String) : []),
    ...(shihabIncoming.mehediShortcode ? [String(shihabIncoming.mehediShortcode)] : [])
  ].filter(Boolean)));

  // If a live webRequest audio/video slice belongs to an already-registered MPD manifest group via altIds, unify into that MPD's group
  if (!shihabIncoming.shihabFromManifest && shihabEffectiveGroup) {
    const mehediMatchingManifestGroups = new Set();
    for (const shihabRec of mehediTabStreams) {
      if (shihabRec.shihabFromManifest && shihabRec.shihabGroupKey && Array.isArray(shihabRec.mehediAltIds)) {
        if (shihabRec.mehediAltIds.includes(shihabEffectiveGroup)) {
          mehediMatchingManifestGroups.add(shihabRec.shihabGroupKey);
        }
      }
    }
    if (mehediMatchingManifestGroups.size === 1) {
      shihabEffectiveGroup = mehediMatchingManifestGroups.values().next().value;
    }
  }

  // Inherit or propagate shihabTitleHint strictly within the exact same groupKey
  let mehediEffectiveTitleHint = shihabIncoming.shihabTitleHint || "";
  if (shihabEffectiveGroup) {
    if (mehediEffectiveTitleHint) {
      for (const shihabPeer of mehediTabStreams) {
        if (shihabPeer.shihabGroupKey === shihabEffectiveGroup && !shihabPeer.shihabTitleHint) {
          shihabPeer.shihabTitleHint = mehediEffectiveTitleHint;
        }
      }
    } else {
      const shihabTitledPeer = mehediTabStreams.find(
        (mehediP) => mehediP.shihabGroupKey === shihabEffectiveGroup && Boolean(mehediP.shihabTitleHint)
      );
      if (shihabTitledPeer) {
        mehediEffectiveTitleHint = shihabTitledPeer.shihabTitleHint;
      }
    }
  }

  const mehediExisting = mehediTabStreams.find((shihabRec) => shihabRec.shihabStreamUrl === shihabIncoming.shihabStreamUrl);
  if (mehediExisting) {
    if (mehediEffectiveSize > mehediExisting.shihabSizeBytes) {
      mehediExisting.shihabSizeBytes = mehediEffectiveSize;
    }
    if (mehediEffectiveRes && !mehediExisting.shihabResolution) {
      mehediExisting.shihabResolution = mehediEffectiveRes;
    }
    if (shihabIncoming.shihabWidth > 0 && !mehediExisting.shihabWidth) {
      mehediExisting.shihabWidth = shihabIncoming.shihabWidth;
    }
    if (shihabIncoming.mehediHeight > 0 && !mehediExisting.mehediHeight) {
      mehediExisting.mehediHeight = shihabIncoming.mehediHeight;
    }
    if (shihabIncoming.mehediDurationSec > 0 && !mehediExisting.mehediDurationSec) {
      mehediExisting.mehediDurationSec = shihabIncoming.mehediDurationSec;
    }
    if (shihabIncoming.mehediTrackRole && mehediExisting.mehediTrackRole === "progressive") {
      mehediExisting.mehediTrackRole = shihabIncoming.mehediTrackRole;
    }
    if (shihabEffectiveGroup && (!mehediExisting.shihabGroupKey || shihabIncoming.shihabFromManifest)) {
      mehediExisting.shihabGroupKey = shihabEffectiveGroup;
    }
    if (shihabIncoming.shihabFromManifest) {
      mehediExisting.shihabFromManifest = true;
    }
    if (mehediCombinedAltIds.length > 0) {
      const shihabMergedAlt = new Set([...(mehediExisting.mehediAltIds || []), ...mehediCombinedAltIds]);
      mehediExisting.mehediAltIds = Array.from(shihabMergedAlt);
    }
    if (shihabIncoming.mehediShortcode && !mehediExisting.mehediShortcode) {
      mehediExisting.mehediShortcode = shihabIncoming.mehediShortcode;
    }
    if (mehediEffectiveTitleHint && !mehediExisting.shihabTitleHint) {
      mehediExisting.shihabTitleHint = mehediEffectiveTitleHint;
    }
    if (shihabIncoming.mehediAudioCompanionUrl && !mehediExisting.mehediAudioCompanionUrl) {
      mehediExisting.mehediAudioCompanionUrl = shihabIncoming.mehediAudioCompanionUrl;
    }
    if (Number(shihabIncoming.shihabAudioCompanionBytes || -1) > 0) {
      mehediExisting.shihabAudioCompanionBytes = Number(shihabIncoming.shihabAudioCompanionBytes);
    }
    if (shihabIncoming.mehediCustomUserAgent) {
      mehediExisting.mehediCustomUserAgent = shihabIncoming.mehediCustomUserAgent;
    }
    if (shihabIncoming.mehediCapturedReferer) {
      mehediExisting.mehediCapturedReferer = shihabIncoming.mehediCapturedReferer;
    }
    if (typeof shihabIncoming.shihabIsOriginalAudio === "boolean") {
      mehediExisting.shihabIsOriginalAudio = shihabIncoming.shihabIsOriginalAudio;
    }
    if (typeof shihabIncoming.mehediIsHdr === "boolean") {
      mehediExisting.mehediIsHdr = shihabIncoming.mehediIsHdr;
    }
    if (mehediHdrInfo) {
      mehediExisting.shihabOrigin = mehediHdrInfo.shihabOrigin || mehediExisting.shihabOrigin;
      mehediExisting.mehediCapturedReferer = mehediHdrInfo.mehediReferer || mehediExisting.mehediCapturedReferer;
      mehediExisting.mehediOmitCookies = !mehediHdrInfo.shihabHadCookie;
    }
    return mehediExisting;
  }

  const mehediHasCustomMobileUa = Boolean(shihabIncoming.mehediCustomUserAgent);
  const shihabDefaultGvOrigin = (shihabUrlMeta.mehediIsGoogleVideo && !mehediHasCustomMobileUa)
    ? "https://www.youtube.com"
    : "";
  const mehediDefaultGvReferer = "";

  const shihabNewRecord = {
    shihabStreamUrl: shihabIncoming.shihabStreamUrl,
    mehediMimeType: shihabIncoming.mehediMimeType || "",
    shihabCodecs: shihabIncoming.shihabCodecs || "",
    shihabExt: shihabEffectiveExt,
    shihabSizeBytes: mehediEffectiveSize,
    shihabExactSizeKnown: Boolean(shihabIncoming.shihabExactSizeKnown || shihabUrlMeta.shihabClenBytes > 0),
    mehediTrackRole: shihabEffectiveRole,
    shihabWidth: shihabIncoming.shihabWidth || 0,
    mehediHeight: shihabIncoming.mehediHeight || 0,
    mehediDurationSec: Number(shihabIncoming.mehediDurationSec || 0),
    shihabResolution: mehediEffectiveRes || "",
    mehediBandwidth: shihabIncoming.mehediBandwidth || 0,
    shihabGroupKey: shihabEffectiveGroup,
    shihabFromManifest: Boolean(shihabIncoming.shihabFromManifest),
    mehediShortcode: shihabIncoming.mehediShortcode || "",
    mehediAltIds: mehediCombinedAltIds,
    shihabTitleHint: mehediEffectiveTitleHint,
    mehediAudioCompanionUrl: shihabIncoming.mehediAudioCompanionUrl || "",
    shihabAudioCompanionBytes: Number(shihabIncoming.shihabAudioCompanionBytes || -1),
    mehediCustomUserAgent: shihabIncoming.mehediCustomUserAgent || "",
    shihabIsOriginalAudio: Boolean(shihabIncoming.shihabIsOriginalAudio),
    mehediIsHdr: Boolean(shihabIncoming.mehediIsHdr),
    shihabSkipBinaryProbe: Boolean(shihabIncoming.shihabSkipBinaryProbe),
    mehediIsGoogleVideo: shihabUrlMeta.mehediIsGoogleVideo,
    shihabOrigin: shihabIncoming.shihabOrigin !== undefined
      ? shihabIncoming.shihabOrigin
      : ((mehediHdrInfo && mehediHdrInfo.shihabOrigin) || shihabDefaultGvOrigin),
    mehediCapturedReferer: shihabIncoming.mehediCapturedReferer ||
      (mehediHdrInfo && mehediHdrInfo.mehediReferer) ||
      mehediDefaultGvReferer,
    mehediOmitCookies: typeof shihabIncoming.mehediOmitCookies === "boolean"
      ? shihabIncoming.mehediOmitCookies
      : (mehediHdrInfo ? !mehediHdrInfo.shihabHadCookie : shihabUrlMeta.mehediIsGoogleVideo),
    shihabIsDrm: false,
    shihabDetectedAt: Date.now()
  };

  mehediTabStreams.push(shihabNewRecord);
  if (mehediTabStreams.length > 120) {
    mehediTabStreams.splice(0, mehediTabStreams.length - 120);
  }

  // Trigger lightweight binary header probe when resolution, audio/video role, or exact size is missing
  if (!shihabNewRecord.shihabSkipBinaryProbe &&
      shihabNewRecord.shihabExt !== "m3u8" &&
      (!shihabNewRecord.shihabResolution || shihabNewRecord.shihabSizeBytes <= 0 || !shihabUrlMeta.mehediTrackRole)) {
    mehediProbeStreamBinaryHeaderOnce(shihabNewRecord, mehediTabId);
  }
  if (shihabNewRecord.shihabExt === "m3u8" && !shihabNewRecord.shihabResolution) {
    shihabExpandHlsMasterPlaylistOnce(shihabNewRecord.shihabStreamUrl, mehediTabId);
  }

  return shihabNewRecord;
}

async function mehediFetchLiveTabCaptionAndTitle(shihabTabId) {
  let mehediCaption = "";
  let shihabTabUrl = "";
  let mehediTabTitle = "";

  if (typeof shihabTabId === "number" && shihabTabId >= 0) {
    try {
      const shihabTabObj = await chrome.tabs.get(shihabTabId);
      if (shihabTabObj) {
        shihabTabUrl = shihabTabObj.url || "";
        mehediTabTitle = shihabTabObj.title || "";
      }
    } catch (_) {}

    try {
      const shihabDomReply = await chrome.tabs.sendMessage(shihabTabId, { shihabCmd: "mehedi_get_video_caption" });
      if (shihabDomReply && shihabDomReply.shihabVideoTitle) {
        mehediCaption = shihabDomReply.shihabVideoTitle;
      }
      if (!shihabTabUrl && shihabDomReply && shihabDomReply.mehediPageUrl) {
        shihabTabUrl = shihabDomReply.mehediPageUrl;
      }
    } catch (_) {}
  }

  const shihabBestStem = mehediSanitizeVideoTitleForFile(mehediCaption || mehediTabTitle, shihabTabUrl);
  return {
    shihabBestStem,
    mehediHasRealCaption: Boolean(mehediCaption && mehediCaption.trim().length > 1),
    shihabTabUrl,
    mehediTabTitle
  };
}

// Pair DASH Video-only tracks with their matching Audio track, attach exact resolutions, VIDEO vs AUDIO tags, and sizes
async function mehediEnrichStreamDisplayNamesForTab(shihabTabId) {
  const mehediRawStreams = shihabRuntimeState.mehediDetectedStreamsByTab.get(shihabTabId) || [];
  if (mehediRawStreams.length === 0) return [];

  const shihabMeta = await mehediFetchLiveTabCaptionAndTitle(shihabTabId);
  const mehediValidStreams = mehediRawStreams.filter((shihabRec) => !shihabRec.shihabIsDrm);

  // Index best audio tracks per groupKey (and tab fallback)
  const shihabAudioTracks = mehediValidStreams.filter((mehediRec) => mehediRec.mehediTrackRole === "audio");

  function mehediSelectBestCompanionAudio(shihabVideoRec) {
    const mehediSameGroupAudios = shihabVideoRec.shihabGroupKey
      ? shihabAudioTracks.filter((shihabA) => shihabA.shihabGroupKey === shihabVideoRec.shihabGroupKey)
      : shihabAudioTracks.filter((shihabA) => Math.abs(shihabA.shihabDetectedAt - shihabVideoRec.shihabDetectedAt) < 30000);

    const shihabPool = mehediSameGroupAudios.length > 0
      ? mehediSameGroupAudios
      : (!shihabVideoRec.shihabGroupKey ? shihabAudioTracks : []);
    if (shihabPool.length === 0) return null;

    const mehediVidExt = String(shihabVideoRec.shihabExt || "mp4").toLowerCase();
    const shihabSortedAudios = shihabPool.slice().sort((mehediA, shihabB) => {
      const shihabAOrig = mehediA.shihabIsOriginalAudio ? 1 : 0;
      const mehediBOrig = shihabB.shihabIsOriginalAudio ? 1 : 0;
      if (shihabAOrig !== mehediBOrig) return mehediBOrig - shihabAOrig;

      const mehediACompat = mehediVidExt === "webm"
        ? (mehediA.shihabExt === "webm" ? 1 : 0)
        : (mehediA.shihabExt === "m4a" || mehediA.shihabExt === "mp4" ? 1 : 0);
      const shihabBCompat = mehediVidExt === "webm"
        ? (shihabB.shihabExt === "webm" ? 1 : 0)
        : (shihabB.shihabExt === "m4a" || shihabB.shihabExt === "mp4" ? 1 : 0);
      if (mehediACompat !== shihabBCompat) return shihabBCompat - mehediACompat;
      return (shihabB.mehediBandwidth || shihabB.shihabSizeBytes || 0) - (mehediA.mehediBandwidth || mehediA.shihabSizeBytes || 0);
    });
    return shihabSortedAudios[0];
  }

  const shihabDistinctGroups = new Set(
    mehediValidStreams.map((shihabR) => shihabR.shihabGroupKey).filter(Boolean)
  );
  const mehediHasMultipleGroups = shihabDistinctGroups.size > 1;

  const mehediPrepared = [];
  for (const shihabEntry of mehediValidStreams) {
    // Hide raw master .m3u8 once its resolution variants have been expanded
    if (shihabEntry.shihabExt === "m3u8" && !shihabEntry.shihabResolution &&
        shihabRuntimeState.shihabExpandedHlsUrls.has(shihabEntry.shihabStreamUrl)) {
      const mehediHasExpandedVariants = mehediValidStreams.some(
        (shihabOther) => shihabOther !== shihabEntry && shihabOther.shihabExt === "m3u8" && shihabOther.shihabResolution
      );
      if (mehediHasExpandedVariants) continue;
    }

    const mehediIsAudioOnly = shihabEntry.mehediTrackRole === "audio";
    let shihabCompanionUrl = shihabEntry.mehediAudioCompanionUrl || "";
    let mehediCompanionBytes = shihabEntry.shihabAudioCompanionBytes > 0 ? shihabEntry.shihabAudioCompanionBytes : -1;

    if (shihabEntry.mehediTrackRole === "video_only") {
      if (!shihabCompanionUrl) {
        const mehediBestAudio = mehediSelectBestCompanionAudio(shihabEntry);
        if (mehediBestAudio) {
          shihabCompanionUrl = mehediBestAudio.shihabStreamUrl;
          mehediCompanionBytes = mehediBestAudio.shihabSizeBytes > 0 ? mehediBestAudio.shihabSizeBytes : -1;
        }
      }
    }

    const shihabDisplayBytes = (!mehediIsAudioOnly && shihabEntry.shihabSizeBytes > 0)
      ? (shihabEntry.shihabSizeBytes + (mehediCompanionBytes > 0 ? mehediCompanionBytes : 0))
      : shihabEntry.shihabSizeBytes;

    const mehediFmtUpper = (shihabEntry.shihabExt === "m3u8" ? "MP4" : (shihabEntry.shihabExt || "mp4")).toUpperCase();
    const shihabMediaKind = mehediIsAudioOnly ? "AUDIO" : "VIDEO";
    const mehediQualText = mehediIsAudioOnly
      ? (shihabEntry.shihabResolution || "Audio")
      : (shihabEntry.shihabResolution || (shihabEntry.shihabExt === "m3u8" ? "HLS" : "HD"));
    const shihabBadgeText = `${mehediFmtUpper} ${mehediQualText}`;

    const mehediRawUrlLeaf = decodeURIComponent((shihabEntry.shihabStreamUrl.split("?")[0].split("/").pop() || ""));
    let shihabCleanStem = "";
    if (!shihabIsGibberishOrCdnFileName(mehediRawUrlLeaf) && mehediRawUrlLeaf.includes(".")) {
      shihabCleanStem = mehediRawUrlLeaf.replace(/\.[a-z0-9]{2,5}$/i, "");
    } else if (shihabEntry.shihabTitleHint) {
      shihabCleanStem = mehediSanitizeVideoTitleForFile(shihabEntry.shihabTitleHint, shihabMeta.shihabTabUrl);
    } else if (!mehediHasMultipleGroups) {
      shihabCleanStem = shihabMeta.shihabBestStem;
    } else {
      const mehediShortGrp = String(shihabEntry.mehediShortcode || shihabEntry.shihabGroupKey || "").replace(/^[a-z]+:/i, "").slice(-8);
      shihabCleanStem = mehediShortGrp
        ? `${mehediSanitizeVideoTitleForFile("", shihabMeta.shihabTabUrl)} ${mehediShortGrp}`
        : mehediSanitizeVideoTitleForFile("", shihabMeta.shihabTabUrl);
    }

    const mehediTargetExt = shihabEntry.shihabExt === "m3u8"
      ? ".mp4"
      : ("." + (shihabEntry.shihabExt || (mehediIsAudioOnly ? "m4a" : "mp4")));
    const shihabQualTagForFile = mehediIsAudioOnly ? "Audio" : (shihabEntry.shihabResolution || "");
    const mehediResolvedName = shihabQualTagForFile
      ? `${shihabCleanStem} [${shihabQualTagForFile}]${mehediTargetExt}`
      : `${shihabCleanStem}${mehediTargetExt}`;

    mehediPrepared.push({
      shihabStreamUrl: shihabEntry.shihabStreamUrl,
      mehediMimeType: shihabEntry.mehediMimeType,
      shihabExt: shihabEntry.shihabExt,
      shihabSizeBytes: shihabEntry.shihabSizeBytes,
      mehediDisplaySizeBytes: shihabDisplayBytes,
      mehediMediaKind: shihabMediaKind,
      mehediTrackRole: shihabEntry.mehediTrackRole,
      shihabWidth: shihabEntry.shihabWidth || 0,
      mehediHeight: shihabEntry.mehediHeight || 0,
      mehediDurationSec: shihabEntry.mehediDurationSec || 0,
      shihabResolution: shihabEntry.shihabResolution || "",
      shihabQualityLabel: shihabQualTagForFile,
      mehediBadge: shihabBadgeText,
      shihabDisplayName: shihabCleanStem,
      shihabTitleHint: shihabEntry.shihabTitleHint || "",
      mehediResolvedFileName: mehediResolvedName,
      shihabGroupKey: shihabEntry.shihabGroupKey || "",
      mehediShortcode: shihabEntry.mehediShortcode || "",
      mehediAltIds: Array.isArray(shihabEntry.mehediAltIds) ? shihabEntry.mehediAltIds : [],
      mehediAudioCompanionUrl: shihabCompanionUrl,
      shihabAudioCompanionBytes: mehediCompanionBytes,
      shihabOrigin: shihabEntry.shihabOrigin || "",
      mehediCapturedReferer: shihabEntry.mehediCapturedReferer || "",
      mehediOmitCookies: Boolean(shihabEntry.mehediOmitCookies),
      mehediCustomUserAgent: shihabEntry.mehediCustomUserAgent || "",
      shihabIsOriginalAudio: Boolean(shihabEntry.shihabIsOriginalAudio),
      mehediIsHdr: Boolean(shihabEntry.mehediIsHdr),
      shihabIsAudioOnly: mehediIsAudioOnly,
      mehediBandwidth: shihabEntry.mehediBandwidth || 0,
      shihabCodecs: shihabEntry.shihabCodecs || "",
      shihabDetectedAt: shihabEntry.shihabDetectedAt
    });
  }

  // Deduplicate same (groupKey + mediaKind + resolution + format) keeping SDR / progressive / MP4 avc1 / largest size first
  mehediPrepared.sort((shihabA, mehediB) => {
    if (shihabA.shihabIsAudioOnly !== mehediB.shihabIsAudioOnly) {
      return shihabA.shihabIsAudioOnly ? 1 : -1;
    }
    if (shihabA.shihabIsAudioOnly && shihabA.shihabIsOriginalAudio !== mehediB.shihabIsOriginalAudio) {
      return shihabA.shihabIsOriginalAudio ? -1 : 1;
    }
    const shihabRankA = mehediNumericHeightRank(shihabA.shihabResolution);
    const mehediRankB = mehediNumericHeightRank(mehediB.shihabResolution);
    if (shihabRankA !== mehediRankB) {
      return mehediRankB - shihabRankA;
    }
    if (shihabA.mehediIsHdr !== mehediB.mehediIsHdr) {
      return shihabA.mehediIsHdr ? 1 : -1;
    }
    const shihabProgA = (shihabA.mehediTrackRole === "progressive" || shihabA.mehediAudioCompanionUrl) ? 1 : 0;
    const mehediProgB = (mehediB.mehediTrackRole === "progressive" || mehediB.mehediAudioCompanionUrl) ? 1 : 0;
    if (shihabProgA !== mehediProgB) {
      return mehediProgB - shihabProgA;
    }
    const shihabMp4A = shihabA.shihabExt === "mp4" || shihabA.shihabExt === "m4a" ? 1 : 0;
    const mehediMp4B = mehediB.shihabExt === "mp4" || mehediB.shihabExt === "m4a" ? 1 : 0;
    if (shihabMp4A !== mehediMp4B) {
      return mehediMp4B - shihabMp4A;
    }
    const shihabAvcA = String(shihabA.shihabCodecs || "").includes("avc1") ? 1 : 0;
    const mehediAvcB = String(mehediB.shihabCodecs || "").includes("avc1") ? 1 : 0;
    if (shihabAvcA !== mehediAvcB) {
      return mehediAvcB - shihabAvcA;
    }
    return (mehediB.mehediDisplaySizeBytes || mehediB.mehediBandwidth || 0) -
           (shihabA.mehediDisplaySizeBytes || shihabA.mehediBandwidth || 0);
  });

  const shihabDeduped = [];
  const mehediSeenVariantKeys = new Set();
  for (const shihabItem of mehediPrepared) {
    // Skip video_only streams that have NO audio companion WHEN a progressive or audio-paired stream exists in the same group
    if (shihabItem.mehediTrackRole === "video_only" && !shihabItem.mehediAudioCompanionUrl) {
      const mehediHasPlayableWithSound = mehediPrepared.some(
        (shihabCand) =>
          !shihabCand.shihabIsAudioOnly &&
          shihabCand.shihabGroupKey === shihabItem.shihabGroupKey &&
          (shihabCand.mehediTrackRole === "progressive" || Boolean(shihabCand.mehediAudioCompanionUrl))
      );
      if (mehediHasPlayableWithSound) {
        continue;
      }
    }

    const mehediDedupKey = shihabItem.shihabIsAudioOnly
      ? `${shihabItem.shihabGroupKey || shihabItem.shihabStreamUrl}|AUDIO`
      : (shihabItem.shihabResolution
          ? `${shihabItem.shihabGroupKey}|VIDEO|${shihabItem.shihabResolution}`
          : `${shihabItem.shihabStreamUrl}`);
    if (mehediSeenVariantKeys.has(mehediDedupKey)) {
      continue;
    }
    mehediSeenVariantKeys.add(mehediDedupKey);
    shihabDeduped.push(shihabItem);
  }

  return shihabDeduped;
}

function shihabPushUpdatedStreamsToTab(mehediTabId) {
  if (typeof mehediTabId !== "number" || mehediTabId < 0) return;
  mehediUpdateToolbarBadgeForTab(mehediTabId);
  mehediEnrichStreamDisplayNamesForTab(mehediTabId).then((shihabEnriched) => {
    chrome.tabs.sendMessage(mehediTabId, {
      shihabCmd: "mehedi_streams_updated",
      shihabShowPanel: shihabRuntimeState.mehediShowVideoPanel !== false,
      shihabPanelMiniMode: Boolean(shihabRuntimeState.shihabPanelMiniMode),
      mehediPreventKey: shihabRuntimeState.shihabPreventKey || "Alt",
      shihabForceKey: shihabRuntimeState.mehediForceKey || "Ins",
      mehediStreams: shihabEnriched
    }).catch(() => {});
  }).catch(() => {});
}

async function mehediCallDesktopBridge(shihabEndpoint, mehediMethod, shihabPayloadObj) {
  try {
    const mehediController = new AbortController();
    const shihabTimer = setTimeout(() => mehediController.abort(), 1800);
    const mehediFetchOpts = {
      method: mehediMethod || "GET",
      headers: { "Content-Type": "application/json", "X-Shihab-Client": "MV3-Extension" },
      signal: mehediController.signal
    };
    if (shihabPayloadObj && mehediMethod === "POST") {
      mehediFetchOpts.body = JSON.stringify(shihabPayloadObj);
    }
    const shihabResp = await fetch(SHIHAB_BRIDGE_BASE_URL + shihabEndpoint, mehediFetchOpts);
    clearTimeout(shihabTimer);
    if (shihabResp.ok) {
      return await shihabResp.json();
    }
  } catch (shihabHttpErr) {
    if (shihabEndpoint === "/shihab/relay-jobs") {
      return null;
    }
    try {
      const mehediNativeReq = shihabEndpoint === "/shihab/capture"
        ? { action: "capture", capture: shihabPayloadObj }
        : (shihabEndpoint === "/shihab/capture-batch"
          ? { action: "capture-batch", batch: shihabPayloadObj }
          : { action: "ping" });
      const shihabNativeReply = await chrome.runtime.sendNativeMessage(MEHEDI_NATIVE_HOST_NAME, mehediNativeReq);
      if (shihabNativeReply && shihabNativeReply.ok) {
        return shihabNativeReply;
      }
    } catch (_) {}
  }
  return null;
}

const SHIHAB_CAPTURE_OPENED_DNR_RULE_ID = 45779;

async function shihabSyncDeclarativeCaptureOpenedFilesRule() {
  if (!chrome.declarativeNetRequest || typeof chrome.declarativeNetRequest.updateDynamicRules !== "function") {
    return;
  }
  try {
    const mehediShouldEnable = Boolean(
      shihabRuntimeState.mehediDesktopOnline &&
      shihabRuntimeState.shihabIntegrationEnabled &&
      shihabRuntimeState.shihabCaptureOpenedFiles
    );
    const shihabAddRules = mehediShouldEnable
      ? [{
          id: SHIHAB_CAPTURE_OPENED_DNR_RULE_ID,
          priority: 1,
          action: {
            type: "modifyHeaders",
            responseHeaders: [
              { header: "Content-Disposition", operation: "set", value: "attachment" }
            ]
          },
          condition: {
            regexFilter: "^https?://[^?#]+\\.(pdf|mp3|m4a|wav|ogg|flac|mp4|mkv|webm|mov|avi)(\\?.*)?$",
            resourceTypes: ["main_frame"]
          }
        }]
      : [];
    await chrome.declarativeNetRequest.updateDynamicRules({
      removeRuleIds: [SHIHAB_CAPTURE_OPENED_DNR_RULE_ID],
      addRules: shihabAddRules
    });
  } catch (_) {}
}

async function shihabSyncDesktopConfiguration(mehediAllowRecentCache) {
  if (mehediAllowRecentCache && shihabRuntimeState.mehediDesktopOnline && (Date.now() - shihabRuntimeState.shihabLastConfigSyncMs) < 2500) {
    return { ok: true, integrationEnabled: shihabRuntimeState.shihabIntegrationEnabled };
  }
  const mehediReply = await mehediCallDesktopBridge("/shihab/ping", "GET", null);
  if (mehediReply && mehediReply.ok) {
    shihabRuntimeState.mehediDesktopOnline = true;
    shihabRuntimeState.shihabLastConfigSyncMs = Date.now();
    shihabRuntimeState.shihabIntegrationEnabled = mehediReply.integrationEnabled !== false;
    shihabRuntimeState.mehediShowVideoPanel = mehediReply.showVideoPanel !== false;
    shihabRuntimeState.shihabPanelMiniMode = Boolean(mehediReply.panelMiniMode);
    shihabRuntimeState.mehediPanelSkipAutoStart = Boolean(mehediReply.panelSkipAutoStart);
    shihabRuntimeState.shihabPreventKey = mehediReply.preventKey || "Alt";
    shihabRuntimeState.mehediForceKey = mehediReply.forceKey || "Ins";
    shihabRuntimeState.shihabMinVideoSizeKb = Number(mehediReply.minVideoSizeKb || 250);
    shihabRuntimeState.shihabCaptureOpenedFiles = Boolean(mehediReply.captureOpenedFiles);
    shihabRuntimeState.mehediCaptureMinSizeKb = Number(mehediReply.captureMinSizeKb || 0);
    shihabApplyExtensionSpec(mehediReply.autoCaptureExtensions);
    const shihabMergedExcluded = new Set(String(mehediReply.excludedSites || "").split(/\s+/).filter(Boolean));
    for (const mehediLocalSite of shihabRuntimeState.shihabLocalExcludedSites) {
      if (mehediLocalSite) shihabMergedExcluded.add(mehediLocalSite);
    }
    shihabRuntimeState.shihabExcludedPatterns = Array.from(shihabMergedExcluded);
    shihabRuntimeState.mehediExcludedAddressPatterns = String(mehediReply.excludedAddresses || "").split(/\s+/).filter(Boolean);
    chrome.storage.local.set({ mehediShowVideoPanelPref: shihabRuntimeState.mehediShowVideoPanel }).catch(() => {});
  } else {
    shihabRuntimeState.mehediDesktopOnline = false;
  }
  await shihabSyncDeclarativeCaptureOpenedFilesRule();
  mehediUpdateToolbarBadgeForTab(null);
  return mehediReply;
}

async function shihabInjectContentScriptIntoExistingTabs() {
  try {
    const mehediTabs = await chrome.tabs.query({ url: ["http://*/*", "https://*/*"] });
    for (const shihabTab of mehediTabs) {
      if (shihabTab && typeof shihabTab.id === "number" && shihabTab.id >= 0) {
        let mehediAlreadyAlive = false;
        try {
          const shihabPing = await chrome.tabs.sendMessage(
            shihabTab.id,
            { shihabCmd: "shihab_ping_content_alive" },
            { frameId: 0 }
          );
          mehediAlreadyAlive = Boolean(shihabPing && shihabPing.ok);
        } catch (_) {}
        if (mehediAlreadyAlive) {
          continue;
        }
        chrome.scripting.executeScript({
          target: { tabId: shihabTab.id, allFrames: true },
          world: "MAIN",
          files: ["mehedi_page_hook.js"]
        }).catch(() => {});
        chrome.scripting.executeScript({
          target: { tabId: shihabTab.id, allFrames: true },
          files: ["mehedi_content.js"]
        }).catch(() => {});
      }
    }
  } catch (_) {}
}

async function mehediBuildCookieHeaderForUrl(shihabUrl) {
  try {
    const mehediCookies = await chrome.cookies.getAll({ url: shihabUrl });
    if (!mehediCookies || mehediCookies.length === 0) {
      return "";
    }
    return mehediCookies.map((shihabCookie) => `${shihabCookie.name}=${shihabCookie.value}`).join("; ");
  } catch (_) {
    return "";
  }
}

async function shihabDispatchSingleUrlToDesktop(mehediCaptureParams) {
  const shihabUrl = mehediCaptureParams.shihabUrl || "";
  const mehediIsMagnet = /^magnet:\?/i.test(shihabUrl);
  const shihabIsHttp = /^https?:\/\//i.test(shihabUrl);
  const mehediIsFtp = /^ftps?:\/\//i.test(shihabUrl);
  if (!shihabIsHttp && !mehediIsFtp && !mehediIsMagnet) {
    return false;
  }

  const mehediHdrInfo = shihabIsHttp ? (shihabRuntimeState.shihabRequestHeadersByUrl.get(shihabUrl) || null) : null;
  const shihabUrlMeta = shihabIsHttp
    ? mehediInspectUrlQueryMetadata(shihabUrl)
    : { mehediIsGoogleVideo: false, shihabExtOverride: "", shihabClenBytes: -1, shihabResolution: "" };

  let mehediResolvedFileName = mehediCaptureParams.shihabFileName || "";
  if (!mehediIsMagnet && (!mehediResolvedFileName || shihabIsGibberishOrCdnFileName(mehediResolvedFileName))) {
    if (mehediCaptureParams.mehediTabTitle) {
      const shihabExt = shihabUrlMeta.shihabExtOverride || shihabExtractExtensionFromUrlOrName(shihabUrl, mehediResolvedFileName) || "mp4";
      const mehediCleanStem = mehediSanitizeVideoTitleForFile(mehediCaptureParams.mehediTabTitle, mehediCaptureParams.mehediReferrer || "");
      mehediResolvedFileName = `${mehediCleanStem}.${shihabExt}`;
    }
  }

  const shihabOmitCookies = !shihabIsHttp
    ? true
    : (typeof mehediCaptureParams.mehediOmitCookies === "boolean"
      ? mehediCaptureParams.mehediOmitCookies
      : (mehediHdrInfo ? !mehediHdrInfo.shihabHadCookie : shihabUrlMeta.mehediIsGoogleVideo));

  const mehediHasMobileUa = Boolean(mehediCaptureParams.mehediUserAgent);
  const mehediOrigin = mehediHasMobileUa
    ? ""
    : (mehediCaptureParams.shihabOrigin ||
       (mehediHdrInfo && mehediHdrInfo.shihabOrigin) ||
       (shihabUrlMeta.mehediIsGoogleVideo ? "https://www.youtube.com" : ""));

  const shihabEffectiveReferrer =
    mehediCaptureParams.mehediReferrer ||
    (mehediHdrInfo && mehediHdrInfo.mehediReferer) ||
    (shihabUrlMeta.mehediIsGoogleVideo ? "https://www.youtube.com/" : "");

  const mehediCookieHeader = shihabOmitCookies ? "" : await mehediBuildCookieHeaderForUrl(shihabUrl);
  const shihabKnownSize = Number(mehediCaptureParams.shihabFileSize || -1) > 0
    ? Number(mehediCaptureParams.shihabFileSize)
    : (shihabUrlMeta.shihabClenBytes > 0 ? shihabUrlMeta.shihabClenBytes : -1);

  let mehediResolvedPostBody = String(mehediCaptureParams.shihabPostBody || "");
  if (!mehediResolvedPostBody && shihabIsHttp) {
    const shihabPostRec = shihabRuntimeState.shihabPostBodyByUrl.get(shihabUrl);
    if (shihabPostRec && (Date.now() - shihabPostRec.mehediSeenAtMs) < 60000) {
      mehediResolvedPostBody = shihabPostRec.shihabBody || "";
    }
  }

  const shihabPayload = {
    url: shihabUrl,
    referrer: shihabEffectiveReferrer,
    cookies: mehediCookieHeader,
    userAgent: mehediCaptureParams.mehediUserAgent || navigator.userAgent,
    fileName: mehediResolvedFileName,
    mimeType: mehediCaptureParams.mehediMimeType || "",
    fileSize: shihabKnownSize,
    tabTitle: mehediCaptureParams.mehediTabTitle || "",
    silentMode: Boolean(mehediCaptureParams.shihabSilentMode),
    origin: mehediOrigin,
    omitCookies: shihabOmitCookies,
    audioUrl: mehediCaptureParams.mehediAudioCompanionUrl || "",
    audioSize: Number(mehediCaptureParams.shihabAudioCompanionBytes || -1),
    tabId: typeof mehediCaptureParams.shihabTabId === "number" ? mehediCaptureParams.shihabTabId : -1,
    qualityLabel: mehediCaptureParams.shihabQualityLabel || shihabUrlMeta.shihabResolution || "",
    incognito: Boolean(mehediCaptureParams.shihabIncognito),
    postBody: mehediResolvedPostBody
  };

  const mehediAck = await mehediCallDesktopBridge("/shihab/capture", "POST", shihabPayload);
  if (mehediAck && mehediAck.ok) {
    shihabRuntimeState.mehediDesktopOnline = true;
    mehediUpdateToolbarBadgeForTab(null);
    shihabKickRelayPollerSoon();
    return true;
  }
  return false;
}

// Browser-Tab / Service-Worker Fetch Relay for session-bound CDN streams (eliminates 403 Forbidden!)
async function mehediExecuteRelayJobInServiceWorker(shihabJob) {
  const mehediJobId = shihabJob.jobId;
  const shihabRawUrl = shihabJob.url || "";
  const mehediOmit = Boolean(shihabJob.omitCookies);
  let shihabTotalBytes = Number(shihabJob.totalBytes || -1);
  const mehediIsGv = shihabRawUrl.includes("googlevideo.com") || shihabRawUrl.includes("/videoplayback");

  const SHIHAB_RELAY_SLICE = 2 * 1024 * 1024;
  let mehediOffset = 0;

  while (true) {
    if (shihabTotalBytes > 0 && mehediOffset >= shihabTotalBytes) {
      break;
    }
    const shihabEnd = shihabTotalBytes > 0
      ? Math.min(shihabTotalBytes - 1, mehediOffset + SHIHAB_RELAY_SLICE - 1)
      : (mehediOffset + SHIHAB_RELAY_SLICE - 1);

    const mehediSliceUrl = mehediIsGv
      ? (shihabRawUrl + (shihabRawUrl.includes("?") ? "&" : "?") + `range=${mehediOffset}-${shihabEnd}`)
      : shihabRawUrl;

    const shihabHeaders = {};
    if (!mehediIsGv) {
      shihabHeaders["Range"] = `bytes=${mehediOffset}-${shihabEnd}`;
    }

    const mehediResp = await fetch(mehediSliceUrl, {
      method: "GET",
      headers: shihabHeaders,
      credentials: mehediOmit ? "omit" : "include"
    });

    if (!mehediResp.ok && mehediResp.status !== 206) {
      throw new Error(`HTTP ${mehediResp.status}`);
    }

    if (shihabTotalBytes <= 0) {
      const shihabCr = mehediResp.headers.get("content-range") || "";
      const mehediSlash = shihabCr.lastIndexOf("/");
      if (mehediSlash > 0) {
        const shihabParsedTot = parseInt(shihabCr.slice(mehediSlash + 1).trim(), 10);
        if (shihabParsedTot > 0) shihabTotalBytes = shihabParsedTot;
      } else if (mehediResp.status === 200 && !mehediIsGv) {
        const mehediCl = parseInt(mehediResp.headers.get("content-length") || "-1", 10);
        if (mehediCl > 0) shihabTotalBytes = mehediCl;
      }
    }

    const shihabBuf = await mehediResp.arrayBuffer();
    if (shihabBuf.byteLength === 0) {
      break;
    }

    const mehediPostResp = await fetch(
      `${SHIHAB_BRIDGE_BASE_URL}/shihab/relay-chunk?jobId=${encodeURIComponent(mehediJobId)}&offset=${mehediOffset}&total=${shihabTotalBytes}`,
      {
        method: "POST",
        headers: { "Content-Type": "application/octet-stream" },
        body: shihabBuf
      }
    );
    if (mehediPostResp.status === 410) {
      // Cancelled by user in mdm.exe
      return;
    }
    if (!mehediPostResp.ok) {
      throw new Error("Bridge chunk write failed");
    }

    mehediOffset += shihabBuf.byteLength;
    if (!mehediIsGv && mehediResp.status === 200) {
      break;
    }
    if (shihabTotalBytes <= 0 && shihabBuf.byteLength < SHIHAB_RELAY_SLICE) {
      break;
    }
  }

  await fetch(
    `${SHIHAB_BRIDGE_BASE_URL}/shihab/relay-done?jobId=${encodeURIComponent(mehediJobId)}&ok=1`,
    { method: "POST", body: "{}" }
  );
}

async function shihabHandleSingleRelayJob(mehediJob) {
  if (!mehediJob || !mehediJob.jobId || shihabRuntimeState.mehediActiveRelayJobIds.has(mehediJob.jobId)) {
    return;
  }
  shihabRuntimeState.mehediActiveRelayJobIds.add(mehediJob.jobId);

  try {
    let shihabHandledInTab = false;
    if (typeof mehediJob.tabId === "number" && mehediJob.tabId >= 0) {
      try {
      const mehediTabAck = await chrome.tabs.sendMessage(
        mehediJob.tabId,
        {
          shihabCmd: "mehedi_exec_tab_relay_job",
          mehediJob: mehediJob
        },
        { frameId: 0 }
      );
        if (mehediTabAck && mehediTabAck.ok) {
          shihabHandledInTab = true;
        }
      } catch (_) {}
    }

    if (!shihabHandledInTab) {
      await mehediExecuteRelayJobInServiceWorker(mehediJob);
    }
  } catch (shihabRelayErr) {
    try {
      await fetch(
        `${SHIHAB_BRIDGE_BASE_URL}/shihab/relay-done?jobId=${encodeURIComponent(mehediJob.jobId)}&ok=0&error=${encodeURIComponent(shihabRelayErr.message || "Relay failed")}`,
        { method: "POST", body: "{}" }
      );
    } catch (_) {}
  } finally {
    shihabRuntimeState.mehediActiveRelayJobIds.delete(mehediJob.jobId);
  }
}

let mehediRelayPollBusy = false;
async function shihabPollDesktopRelayJobs() {
  if (mehediRelayPollBusy) return;
  mehediRelayPollBusy = true;
  try {
    const shihabReply = await mehediCallDesktopBridge("/shihab/relay-jobs", "GET", null);
    if (shihabReply && shihabReply.ok && Array.isArray(shihabReply.jobs)) {
      shihabRuntimeState.mehediDesktopOnline = true;
      for (const mehediJob of shihabReply.jobs) {
        shihabHandleSingleRelayJob(mehediJob);
      }
    }
  } catch (_) {
  } finally {
    mehediRelayPollBusy = false;
  }
}

function shihabKickRelayPollerSoon() {
  setTimeout(shihabPollDesktopRelayJobs, 150);
  setTimeout(shihabPollDesktopRelayJobs, 600);
  setTimeout(shihabPollDesktopRelayJobs, 1500);
}

setInterval(shihabPollDesktopRelayJobs, 900);

function mehediInstallContextMenus() {
  chrome.contextMenus.removeAll(() => {
    chrome.contextMenus.create({
      id: "shihab_download_single_link",
      title: "Download with MHS Download Manager",
      contexts: ["link", "video", "audio", "image"]
    });
    chrome.contextMenus.create({
      id: "mehedi_download_all_links",
      title: "Download all links with MHS Download Manager",
      contexts: ["page", "frame"]
    });
    chrome.contextMenus.create({
      id: "shihab_download_selected_links",
      title: "Download selected links with MHS Download Manager",
      contexts: ["selection"]
    });
    chrome.contextMenus.create({
      id: "shihab_exclude_current_site",
      title: "Never capture downloads from this site",
      contexts: ["page", "frame"]
    });
  });
}

let shihabInitialDownloadsSeedPromise = null;

function shihabPruneExpiredGestureMaps(mehediNowMs) {
  for (const [shihabTabK, mehediExp] of shihabRuntimeState.mehediTrustedGestureUntilByTab.entries()) {
    if (mehediExp <= mehediNowMs) shihabRuntimeState.mehediTrustedGestureUntilByTab.delete(shihabTabK);
  }
  for (const [shihabHostK, mehediExp] of shihabRuntimeState.mehediTrustedGestureByHost.entries()) {
    if (mehediExp <= mehediNowMs) shihabRuntimeState.mehediTrustedGestureByHost.delete(shihabHostK);
  }
  for (const [shihabUrlK, mehediExp] of shihabRuntimeState.mehediTrustedClickedUrls.entries()) {
    if (mehediExp <= mehediNowMs) shihabRuntimeState.mehediTrustedClickedUrls.delete(shihabUrlK);
  }
  for (const [shihabNewTabK, mehediCreated] of shihabRuntimeState.mehediUserCreatedNewTabs.entries()) {
    if (mehediNowMs - mehediCreated > 45000) shihabRuntimeState.mehediUserCreatedNewTabs.delete(shihabNewTabK);
  }
}

function shihabPersistSessionDownloadState() {
  if (!chrome.storage || !chrome.storage.session) return;
  const mehediNow = Date.now();
  shihabPruneExpiredGestureMaps(mehediNow);
  chrome.storage.session.set({
    shihabSessionBootDone: true,
    mehediBrowserStartupUntilMs: shihabRuntimeState.mehediBrowserStartupUntilMs,
    mehediHasTrustedClickInSession: shihabRuntimeState.mehediHasTrustedClickInSession,
    shihabHandledIds: Array.from(shihabRuntimeState.shihabHandledDownloadIds).slice(-500),
    mehediTabGestures: Array.from(shihabRuntimeState.mehediTrustedGestureUntilByTab.entries()),
    mehediHostGestures: Array.from(shihabRuntimeState.mehediTrustedGestureByHost.entries()),
    mehediClickedUrls: Array.from(shihabRuntimeState.mehediTrustedClickedUrls.entries())
  }).catch(() => {});
}

function shihabMarkDownloadIdHandled(mehediDlId) {
  if (typeof mehediDlId !== "number") return;
  shihabRuntimeState.shihabHandledDownloadIds.add(mehediDlId);
  while (shihabRuntimeState.shihabHandledDownloadIds.size > 500) {
    const shihabOldest = shihabRuntimeState.shihabHandledDownloadIds.values().next().value;
    shihabRuntimeState.shihabHandledDownloadIds.delete(shihabOldest);
  }
  shihabPersistSessionDownloadState();
}

function mehediRecordTrustedUserGesture(shihabTabId, mehediPageUrl, shihabClickedHref) {
  const mehediNow = Date.now();
  shihabRuntimeState.mehediHasTrustedClickInSession = true;
  shihabRuntimeState.mehediLastTrustedGestureGlobalMs = mehediNow;
  shihabRuntimeState.mehediBrowserStartupUntilMs = 0;

  if (typeof shihabTabId === "number" && shihabTabId >= 0) {
    shihabRuntimeState.mehediTrustedGestureUntilByTab.set(shihabTabId, mehediNow + 15000);
  }
  for (const shihabCandidateUrl of [mehediPageUrl, shihabClickedHref]) {
    if (!shihabCandidateUrl || typeof shihabCandidateUrl !== "string") continue;
    try {
      const mehediHost = new URL(shihabCandidateUrl).hostname.toLowerCase();
      if (mehediHost) {
        shihabRuntimeState.mehediTrustedGestureByHost.set(mehediHost, mehediNow + 15000);
      }
    } catch (_) {}
  }
  if (shihabClickedHref && (shihabClickedHref.startsWith("http://") || shihabClickedHref.startsWith("https://"))) {
    shihabRuntimeState.mehediTrustedClickedUrls.set(shihabClickedHref, mehediNow + 30000);
  }
  shihabPersistSessionDownloadState();
}

async function mehediInitializeDownloadSessionGuard(shihabForceFreshBrowserBoot) {
  const mehediNow = Date.now();
  let shihabIsFreshBrowserLaunch = Boolean(shihabForceFreshBrowserBoot);

  if (chrome.storage && chrome.storage.session) {
    try {
      const mehediSess = await chrome.storage.session.get([
        "shihabSessionBootDone",
        "mehediBrowserStartupUntilMs",
        "mehediHasTrustedClickInSession",
        "shihabHandledIds",
        "mehediTabGestures",
        "mehediHostGestures",
        "mehediClickedUrls"
      ]);
      if (!shihabForceFreshBrowserBoot && mehediSess && mehediSess.shihabSessionBootDone) {
        shihabRuntimeState.mehediBrowserStartupUntilMs = Number(mehediSess.mehediBrowserStartupUntilMs || 0);
        shihabRuntimeState.mehediHasTrustedClickInSession = Boolean(mehediSess.mehediHasTrustedClickInSession);
        if (Array.isArray(mehediSess.shihabHandledIds)) {
          for (const shihabOldId of mehediSess.shihabHandledIds) {
            if (typeof shihabOldId === "number") shihabRuntimeState.shihabHandledDownloadIds.add(shihabOldId);
          }
        }
        if (Array.isArray(mehediSess.mehediTabGestures)) {
          for (const [mehediTId, shihabExp] of mehediSess.mehediTabGestures) {
            if (shihabExp > mehediNow) shihabRuntimeState.mehediTrustedGestureUntilByTab.set(mehediTId, shihabExp);
          }
        }
        if (Array.isArray(mehediSess.mehediHostGestures)) {
          for (const [mehediHost, shihabExp] of mehediSess.mehediHostGestures) {
            if (shihabExp > mehediNow) shihabRuntimeState.mehediTrustedGestureByHost.set(mehediHost, shihabExp);
          }
        }
        if (Array.isArray(mehediSess.mehediClickedUrls)) {
          for (const [mehediU, shihabExp] of mehediSess.mehediClickedUrls) {
            if (shihabExp > mehediNow) shihabRuntimeState.mehediTrustedClickedUrls.set(mehediU, shihabExp);
          }
        }
      } else {
        shihabIsFreshBrowserLaunch = true;
      }
    } catch (_) {
      shihabIsFreshBrowserLaunch = true;
    }
  } else if (shihabForceFreshBrowserBoot) {
    shihabIsFreshBrowserLaunch = true;
  }

  if (shihabIsFreshBrowserLaunch) {
    shihabRuntimeState.mehediBrowserStartupUntilMs = Date.now() + 10000;
    shihabRuntimeState.mehediHasTrustedClickInSession = false;
    shihabRuntimeState.mehediTrustedGestureUntilByTab.clear();
    shihabRuntimeState.mehediTrustedGestureByHost.clear();
    shihabRuntimeState.mehediTrustedClickedUrls.clear();
    shihabRuntimeState.mehediUserCreatedNewTabs.clear();
  }

  try {
    const mehediExistingDownloads = await new Promise((shihabResolve) => {
      try {
        chrome.downloads.search({}, (mehediList) => shihabResolve(Array.isArray(mehediList) ? mehediList : []));
      } catch (_) {
        shihabResolve([]);
      }
    });
    const shihabScanNow = Date.now();
    for (const mehediItem of mehediExistingDownloads) {
      if (!mehediItem || typeof mehediItem.id !== "number") continue;
      if (shihabIsFreshBrowserLaunch) {
        shihabRuntimeState.shihabHandledDownloadIds.add(mehediItem.id);
        continue;
      }
      const shihabItemStartMs = mehediItem.startTime ? Date.parse(mehediItem.startTime) : NaN;
      const mehediIsHistoricalOrInactive =
        mehediItem.state !== "in_progress" ||
        Boolean(mehediItem.paused) ||
        Boolean(mehediItem.error) ||
        (!Number.isNaN(shihabItemStartMs) && (shihabScanNow - shihabItemStartMs) > 6000);
      if (mehediIsHistoricalOrInactive) {
        shihabRuntimeState.shihabHandledDownloadIds.add(mehediItem.id);
      }
    }
  } catch (_) {}

  shihabPersistSessionDownloadState();
}

shihabInitialDownloadsSeedPromise = mehediInitializeDownloadSessionGuard(false);

chrome.storage.local.get(["mehediShowVideoPanelPref", "shihabLocalExcludedSites"]).then((shihabStored) => {
  if (shihabStored && typeof shihabStored.mehediShowVideoPanelPref === "boolean") {
    shihabRuntimeState.mehediShowVideoPanel = shihabStored.mehediShowVideoPanelPref;
  }
  if (shihabStored && Array.isArray(shihabStored.shihabLocalExcludedSites)) {
    for (const mehediHost of shihabStored.shihabLocalExcludedSites) {
      if (mehediHost && typeof mehediHost === "string") {
        shihabRuntimeState.shihabLocalExcludedSites.add(mehediHost);
        if (!shihabRuntimeState.shihabExcludedPatterns.includes(mehediHost)) {
          shihabRuntimeState.shihabExcludedPatterns.push(mehediHost);
        }
      }
    }
  }
}).catch(() => {});

shihabSyncDesktopConfiguration();

chrome.runtime.onInstalled.addListener(() => {
  shihabInitialDownloadsSeedPromise = mehediInitializeDownloadSessionGuard(false);
  mehediInstallContextMenus();
  shihabSyncDesktopConfiguration();
  shihabInjectContentScriptIntoExistingTabs();
});

chrome.runtime.onStartup.addListener(() => {
  shihabInitialDownloadsSeedPromise = mehediInitializeDownloadSessionGuard(true);
  mehediInstallContextMenus();
  shihabSyncDesktopConfiguration();
});

chrome.tabs.onCreated.addListener((shihabNewTab) => {
  if (!shihabNewTab || typeof shihabNewTab.id !== "number" || shihabNewTab.id < 0) return;
  const mehediNow = Date.now();
  shihabPruneExpiredGestureMaps(mehediNow);

  const shihabOpenerId = typeof shihabNewTab.openerTabId === "number" ? shihabNewTab.openerTabId : -1;
  const mehediOpenerGestureUntil = shihabOpenerId >= 0
    ? (shihabRuntimeState.mehediTrustedGestureUntilByTab.get(shihabOpenerId) || 0)
    : 0;

  if (
    mehediOpenerGestureUntil > mehediNow ||
    (shihabRuntimeState.mehediHasTrustedClickInSession && (mehediNow - shihabRuntimeState.mehediLastTrustedGestureGlobalMs) < 3500)
  ) {
    // Popup / target="_blank" download tab spawned directly from a user click
    shihabRuntimeState.mehediTrustedGestureUntilByTab.set(shihabNewTab.id, mehediNow + 15000);
    shihabPersistSessionDownloadState();
    return;
  }

  if (mehediNow > shihabRuntimeState.mehediBrowserStartupUntilMs) {
    const shihabInitialUrl = String(shihabNewTab.pendingUrl || shihabNewTab.url || "").toLowerCase();
    if (
      !shihabInitialUrl ||
      shihabInitialUrl === "about:blank" ||
      shihabInitialUrl.includes("://newtab")
    ) {
      shihabRuntimeState.mehediUserCreatedNewTabs.set(shihabNewTab.id, mehediNow);
    }
  }
});

chrome.contextMenus.onClicked.addListener(async (shihabInfo, mehediTab) => {
  const shihabTabUrl = (mehediTab && mehediTab.url) ? mehediTab.url : (shihabInfo.pageUrl || "");
  const mehediTabTitle = (mehediTab && mehediTab.title) ? mehediTab.title : "";

  if (shihabInfo.menuItemId === "shihab_exclude_current_site") {
    try {
      const mehediHost = new URL(shihabTabUrl).hostname.toLowerCase();
      if (mehediHost) {
        shihabRuntimeState.shihabLocalExcludedSites.add(mehediHost);
        if (!shihabRuntimeState.shihabExcludedPatterns.includes(mehediHost)) {
          shihabRuntimeState.shihabExcludedPatterns.push(mehediHost);
        }
        await chrome.storage.local.set({
          shihabLocalExcludedSites: Array.from(shihabRuntimeState.shihabLocalExcludedSites)
        });
      }
    } catch (_) {}
    return;
  }

  if (shihabInfo.menuItemId === "shihab_download_single_link") {
    const shihabTargetUrl = shihabInfo.linkUrl || shihabInfo.srcUrl || "";
    if (shihabTargetUrl) {
      await shihabDispatchSingleUrlToDesktop({
        shihabUrl: shihabTargetUrl,
        mehediReferrer: shihabTabUrl,
        mehediTabTitle: mehediTabTitle,
        shihabTabId: mehediTab ? mehediTab.id : -1,
        shihabIncognito: Boolean(mehediTab && mehediTab.incognito)
      });
    }
    return;
  }

  if (mehediTab && mehediTab.id >= 0 &&
      (shihabInfo.menuItemId === "mehedi_download_all_links" || shihabInfo.menuItemId === "shihab_download_selected_links")) {
    const mehediSelectionOnly = shihabInfo.menuItemId === "shihab_download_selected_links";
    try {
      const shihabResponse = await chrome.tabs.sendMessage(mehediTab.id, {
        shihabCmd: "mehedi_collect_page_links",
        mehediSelectionOnly: mehediSelectionOnly
      });
      if (shihabResponse && Array.isArray(shihabResponse.shihabLinks) && shihabResponse.shihabLinks.length > 0) {
        const mehediCookies = await mehediBuildCookieHeaderForUrl(shihabTabUrl);
        await mehediCallDesktopBridge("/shihab/capture-batch", "POST", {
          pageUrl: shihabTabUrl,
          pageTitle: mehediTabTitle,
          cookies: mehediCookies,
          userAgent: navigator.userAgent,
          links: shihabResponse.shihabLinks
        });
      }
    } catch (_) {}
  }
});

// Intercept Browser Downloads across ALL stages (onCreated, onDeterminingFilename, onChanged)
// so token/redirect URLs (e.g. https://new.direct-dl.lol/d/... -> Content-Disposition: Mirzapur.mkv) are ALWAYS captured automatically!
function shihabWildcardMatchAddress(mehediFullUrl, shihabPatternsList) {
  if (!mehediFullUrl || !Array.isArray(shihabPatternsList) || shihabPatternsList.length === 0) {
    return false;
  }
  for (const mehediRawPat of shihabPatternsList) {
    const shihabPat = String(mehediRawPat || "").trim();
    if (!shihabPat) continue;
    const mehediEsc = shihabPat.replace(/[.+^${}()|[\]\\]/g, "\\$&").replace(/\*/g, ".*").replace(/\?/g, ".");
    if (new RegExp("^" + mehediEsc + "$", "i").test(mehediFullUrl)) {
      return true;
    }
  }
  return false;
}

async function mehediCheckExplicitUserIntentOnce(mehediDownloadItem, shihabFinalUrl, mehediForceActive) {
  if (mehediForceActive) return true;

  const shihabNow = Date.now();
  shihabPruneExpiredGestureMaps(shihabNow);

  const mehediInitialUrl = mehediDownloadItem.url || "";
  if (
    (mehediInitialUrl && (shihabRuntimeState.mehediTrustedClickedUrls.get(mehediInitialUrl) || 0) > shihabNow) ||
    (shihabFinalUrl && (shihabRuntimeState.mehediTrustedClickedUrls.get(shihabFinalUrl) || 0) > shihabNow)
  ) {
    return true;
  }

  const shihabSourceRec =
    (mehediInitialUrl && shihabRuntimeState.mehediRequestSourceTabByUrl.get(mehediInitialUrl)) ||
    (shihabFinalUrl && shihabRuntimeState.mehediRequestSourceTabByUrl.get(shihabFinalUrl)) ||
    null;
  const mehediKnownTabId =
    shihabSourceRec && (shihabNow - shihabSourceRec.mehediSeenAtMs) < 60000
      ? shihabSourceRec.shihabTabId
      : -1;

  if (mehediKnownTabId >= 0) {
    if ((shihabRuntimeState.mehediTrustedGestureUntilByTab.get(mehediKnownTabId) || 0) > shihabNow) {
      return true;
    }
    if (
      shihabNow > shihabRuntimeState.mehediBrowserStartupUntilMs &&
      !mehediDownloadItem.referrer &&
      shihabRuntimeState.mehediUserCreatedNewTabs.has(mehediKnownTabId)
    ) {
      return true;
    }
    // Request originated from a known tab where the user has NOT clicked (e.g. session-restored or auto-redirect tab)
    return false;
  }

  // Fallback for browser-detached downloads (tabId === -1)
  if (shihabNow <= shihabRuntimeState.mehediBrowserStartupUntilMs && !shihabRuntimeState.mehediHasTrustedClickInSession) {
    return false;
  }

  if (mehediDownloadItem.referrer) {
    try {
      const shihabRefHost = new URL(mehediDownloadItem.referrer).hostname.toLowerCase();
      if (shihabRefHost && (shihabRuntimeState.mehediTrustedGestureByHost.get(shihabRefHost) || 0) > shihabNow) {
        return true;
      }
    } catch (_) {}
    return false;
  }

  try {
    const mehediActiveTabs = await chrome.tabs.query({ active: true, currentWindow: true });
    const shihabActiveTab = mehediActiveTabs && mehediActiveTabs[0] ? mehediActiveTabs[0] : null;
    if (shihabActiveTab && typeof shihabActiveTab.id === "number") {
      if ((shihabRuntimeState.mehediTrustedGestureUntilByTab.get(shihabActiveTab.id) || 0) > shihabNow) {
        return true;
      }
      const mehediTabUrl = String(shihabActiveTab.pendingUrl || shihabActiveTab.url || "").toLowerCase();
      if (
        shihabNow > shihabRuntimeState.mehediBrowserStartupUntilMs &&
        shihabRuntimeState.mehediUserCreatedNewTabs.has(shihabActiveTab.id) &&
        (shihabActiveTab.pendingUrl === shihabFinalUrl ||
          shihabActiveTab.pendingUrl === mehediInitialUrl ||
          mehediTabUrl === "about:blank" ||
          mehediTabUrl.includes("://newtab"))
      ) {
        return true;
      }
    }
  } catch (_) {}

  return false;
}

async function mehediVerifyDownloadUserIntentWithGrace(mehediDownloadItem, shihabFinalUrl, mehediForceActive) {
  if (await mehediCheckExplicitUserIntentOnce(mehediDownloadItem, shihabFinalUrl, mehediForceActive)) {
    return true;
  }
  const shihabNow = Date.now();
  const mehediStartMs = mehediDownloadItem.startTime ? Date.parse(mehediDownloadItem.startTime) : NaN;
  // Allow a brief 220ms IPC window when NOT in browser-startup restore in case a waking tab's click message is in flight
  if (
    shihabNow > shihabRuntimeState.mehediBrowserStartupUntilMs &&
    (Number.isNaN(mehediStartMs) || (shihabNow - mehediStartMs) < 3000)
  ) {
    await new Promise((shihabResolve) => setTimeout(shihabResolve, 220));
    return mehediCheckExplicitUserIntentOnce(mehediDownloadItem, shihabFinalUrl, mehediForceActive);
  }
  return false;
}

async function shihabTryInterceptBrowserDownload(mehediDownloadItem) {
  if (!mehediDownloadItem || typeof mehediDownloadItem.id !== "number") {
    return false;
  }
  const shihabDlId = mehediDownloadItem.id;
  if (shihabRuntimeState.shihabHandledDownloadIds.has(shihabDlId) ||
      shihabRuntimeState.mehediInterceptingDownloadIds.has(shihabDlId)) {
    return false;
  }

  // Never intercept historical, completed, interrupted, paused, or old downloads replayed on browser startup
  if (mehediDownloadItem.state && mehediDownloadItem.state !== "in_progress") {
    shihabMarkDownloadIdHandled(shihabDlId);
    return false;
  }
  if (mehediDownloadItem.paused || mehediDownloadItem.error) {
    shihabMarkDownloadIdHandled(shihabDlId);
    return false;
  }
  const mehediStartMs = mehediDownloadItem.startTime ? Date.parse(mehediDownloadItem.startTime) : NaN;
  if (!Number.isNaN(mehediStartMs) && (Date.now() - mehediStartMs) > 15000) {
    shihabMarkDownloadIdHandled(shihabDlId);
    return false;
  }

  if (shihabInitialDownloadsSeedPromise) {
    await shihabInitialDownloadsSeedPromise;
    if (shihabRuntimeState.shihabHandledDownloadIds.has(shihabDlId) ||
        shihabRuntimeState.mehediInterceptingDownloadIds.has(shihabDlId)) {
      return false;
    }
  }

  const shihabFinalUrl = mehediDownloadItem.finalUrl || mehediDownloadItem.url || "";
  const mehediIsMagnetDl = /^magnet:\?/i.test(shihabFinalUrl);
  if (!/^https?:\/\//i.test(shihabFinalUrl) && !/^ftps?:\/\//i.test(shihabFinalUrl) && !mehediIsMagnetDl) {
    return false;
  }
  if (mehediDownloadItem.byExtensionId && mehediDownloadItem.byExtensionId === chrome.runtime.id) {
    shihabMarkDownloadIdHandled(shihabDlId);
    return false;
  }

  const mehediHeaderMeta =
    shihabRuntimeState.mehediResponseMetaByUrl.get(shihabFinalUrl) ||
    shihabRuntimeState.mehediResponseMetaByUrl.get(mehediDownloadItem.url || "") ||
    null;

  const shihabLeafFilename = mehediDownloadItem.filename
    ? String(mehediDownloadItem.filename).split(/[\\/]/).pop()
    : ((mehediHeaderMeta && mehediHeaderMeta.shihabHeaderFileName) || "");

  const mehediEffectiveMime = mehediDownloadItem.mime || (mehediHeaderMeta && mehediHeaderMeta.mehediContentType) || "";
  const shihabEffectiveTotalBytes = mehediDownloadItem.totalBytes > 0
    ? mehediDownloadItem.totalBytes
    : ((mehediHeaderMeta && mehediHeaderMeta.shihabContentLength > 0) ? mehediHeaderMeta.shihabContentLength : -1);

  let shihabHostname = "magnet";
  if (!mehediIsMagnetDl) {
    try {
      shihabHostname = new URL(shihabFinalUrl).hostname;
    } catch (_) {
      return false;
    }
  }

  for (const [, shihabBypassUntil] of shihabRuntimeState.mehediAltBypassUntilMsByTab.entries()) {
    if (Date.now() < shihabBypassUntil) {
      shihabMarkDownloadIdHandled(shihabDlId);
      return false;
    }
  }

  let mehediForceActive = false;
  for (const [, shihabForceUntil] of shihabRuntimeState.shihabForceCaptureUntilMsByTab.entries()) {
    if (Date.now() < shihabForceUntil) {
      mehediForceActive = true;
      break;
    }
  }

  let shihabExt = mehediIsMagnetDl ? "torrent" : shihabExtractExtensionFromUrlOrName(shihabFinalUrl, shihabLeafFilename);
  if (!shihabExt) {
    if (/application\/x-bittorrent/i.test(mehediEffectiveMime)) {
      shihabExt = "torrent";
    } else {
      shihabExt = mehediInferExtensionFromAttachmentMime(
        mehediEffectiveMime,
        Boolean(mehediHeaderMeta && mehediHeaderMeta.shihabIsAttachment)
      );
    }
  }

  // If extension is still unknown on early onCreated (waiting for HTTP headers on token URL), do NOT mark handled yet —
  // onDeterminingFilename / onChanged will fire milliseconds later with the real filename & MIME!
  if (!mehediForceActive && !shihabExt) {
    return false;
  }

  shihabRuntimeState.mehediInterceptingDownloadIds.add(shihabDlId);
  try {
    // Require explicit user click / download gesture — block all startup session-restore & unsolicited auto-downloads!
    const shihabUserInitiated = await mehediVerifyDownloadUserIntentWithGrace(
      mehediDownloadItem,
      shihabFinalUrl,
      mehediForceActive
    );
    if (!shihabUserInitiated) {
      shihabMarkDownloadIdHandled(shihabDlId);
      return false;
    }

    const mehediPing = await shihabSyncDesktopConfiguration(true);
    if (!mehediPing || !mehediPing.ok || !shihabRuntimeState.shihabIntegrationEnabled) {
      return false;
    }

    if (!mehediForceActive) {
      if (!mehediIsMagnetDl && mehediWildcardMatchHost(shihabHostname, shihabRuntimeState.shihabExcludedPatterns)) {
        shihabMarkDownloadIdHandled(shihabDlId);
        return false;
      }
      if (shihabWildcardMatchAddress(shihabFinalUrl, shihabRuntimeState.mehediExcludedAddressPatterns)) {
        shihabMarkDownloadIdHandled(shihabDlId);
        return false;
      }
      if (shihabExt !== "torrent" && !mehediMatchesConfiguredExtension(shihabExt)) {
        return false;
      }
      if (
        shihabRuntimeState.mehediCaptureMinSizeKb > 0 &&
        shihabEffectiveTotalBytes > 0 &&
        shihabEffectiveTotalBytes < (shihabRuntimeState.mehediCaptureMinSizeKb * 1024)
      ) {
        shihabMarkDownloadIdHandled(shihabDlId);
        return false;
      }
    }

    shihabMarkDownloadIdHandled(shihabDlId);

    let mehediResolvedFile = shihabLeafFilename;
    if (!mehediResolvedFile && shihabExt && !mehediIsMagnetDl) {
      const shihabUrlLeaf = shihabExtractExtensionFromUrlOrName(shihabFinalUrl, "")
        ? decodeURIComponent(new URL(shihabFinalUrl).pathname.split("/").pop() || "")
        : "";
      mehediResolvedFile = shihabUrlLeaf || `${shihabHostname}_download.${shihabExt}`;
    }

    const mehediTransferred = await shihabDispatchSingleUrlToDesktop({
      shihabUrl: shihabFinalUrl,
      mehediReferrer: mehediDownloadItem.referrer || "",
      shihabFileName: mehediResolvedFile,
      mehediMimeType: mehediEffectiveMime,
      shihabFileSize: shihabEffectiveTotalBytes,
      shihabIncognito: Boolean(mehediDownloadItem.incognito)
    });

    if (mehediTransferred) {
      try {
        await chrome.downloads.cancel(shihabDlId);
      } catch (_) {}
      try {
        await chrome.downloads.erase({ id: shihabDlId });
      } catch (_) {}

      try {
        const mehediTabs = await chrome.tabs.query({ active: true, currentWindow: true });
        if (mehediTabs && mehediTabs[0] && (mehediTabs[0].pendingUrl === shihabFinalUrl || mehediTabs[0].url === "about:blank")) {
          const shihabCapturedPage = chrome.runtime.getURL("shihab_captured.html") + "?host=" + encodeURIComponent(shihabHostname);
          await chrome.tabs.update(mehediTabs[0].id, { url: shihabCapturedPage });
        }
      } catch (_) {}
      return true;
    } else {
      shihabRuntimeState.shihabHandledDownloadIds.delete(shihabDlId);
    }
  } finally {
    shihabRuntimeState.mehediInterceptingDownloadIds.delete(shihabDlId);
  }
  return false;
}

chrome.downloads.onCreated.addListener((mehediDownloadItem) => {
  shihabTryInterceptBrowserDownload(mehediDownloadItem).catch(() => {});
});

if (chrome.downloads && chrome.downloads.onDeterminingFilename) {
  chrome.downloads.onDeterminingFilename.addListener((mehediDownloadItem, shihabSuggest) => {
    // Allow Chrome's filename determination pipeline to proceed uninterrupted while we cancel & hand off if matched
    try {
      shihabSuggest();
    } catch (_) {}
    shihabTryInterceptBrowserDownload(mehediDownloadItem).catch(() => {});
  });
}

chrome.downloads.onChanged.addListener((shihabDelta) => {
  if (!shihabDelta || typeof shihabDelta.id !== "number") return;
  if (shihabDelta.state && shihabDelta.state.current && shihabDelta.state.current !== "in_progress") {
    shihabMarkDownloadIdHandled(shihabDelta.id);
    return;
  }
  if (!shihabDelta.filename && !shihabDelta.mime && !shihabDelta.finalUrl) return;
  if (shihabRuntimeState.shihabHandledDownloadIds.has(shihabDelta.id)) return;
  chrome.downloads.search({ id: shihabDelta.id }, (mehediItems) => {
    if (Array.isArray(mehediItems) && mehediItems[0]) {
      shihabTryInterceptBrowserDownload(mehediItems[0]).catch(() => {});
    }
  });
});

if (chrome.alarms) {
  try {
    chrome.alarms.create("shihab_bridge_keepalive", { periodInMinutes: 0.5 });
    chrome.alarms.onAlarm.addListener((mehediAlarm) => {
      if (mehediAlarm && mehediAlarm.name === "shihab_bridge_keepalive") {
        shihabSyncDesktopConfiguration(false).catch(() => {});
      }
    });
  } catch (_) {}
}

// Capture outgoing POST form bodies (bounded <= 64 KB) for form-POST download endpoints
try {
  chrome.webRequest.onBeforeRequest.addListener(
    (shihabReq) => {
      if (!shihabReq || shihabReq.method !== "POST" || !shihabReq.url || !shihabReq.url.startsWith("http") || !shihabReq.requestBody) {
        return;
      }
      let mehediEncodedBody = "";
      if (shihabReq.requestBody.formData && typeof shihabReq.requestBody.formData === "object") {
        const shihabParams = new URLSearchParams();
        for (const [mehediKey, shihabVals] of Object.entries(shihabReq.requestBody.formData)) {
          if (Array.isArray(shihabVals)) {
            for (const mehediVal of shihabVals) {
              if (typeof mehediVal === "string") shihabParams.append(mehediKey, mehediVal);
            }
          }
        }
        mehediEncodedBody = shihabParams.toString();
      }
      if (mehediEncodedBody && mehediEncodedBody.length <= 65536) {
        shihabRuntimeState.shihabPostBodyByUrl.set(shihabReq.url, {
          shihabBody: mehediEncodedBody,
          mehediSeenAtMs: Date.now()
        });
        if (shihabRuntimeState.shihabPostBodyByUrl.size > 200) {
          const shihabFirstPost = shihabRuntimeState.shihabPostBodyByUrl.keys().next().value;
          shihabRuntimeState.shihabPostBodyByUrl.delete(shihabFirstPost);
        }
      }
    },
    { urls: ["<all_urls>"] },
    ["requestBody"]
  );
} catch (_) {}

// Capture exact Origin / Referer / Cookie usage on outgoing media requests
chrome.webRequest.onBeforeSendHeaders.addListener(
  (mehediReqDetails) => {
    if (!mehediReqDetails || mehediReqDetails.tabId < 0 || !mehediReqDetails.url.startsWith("http")) {
      return;
    }
    shihabRuntimeState.mehediRequestSourceTabByUrl.set(mehediReqDetails.url, {
      shihabTabId: mehediReqDetails.tabId,
      mehediSeenAtMs: Date.now()
    });
    if (shihabRuntimeState.mehediRequestSourceTabByUrl.size > 500) {
      const mehediOldestReqUrl = shihabRuntimeState.mehediRequestSourceTabByUrl.keys().next().value;
      shihabRuntimeState.mehediRequestSourceTabByUrl.delete(mehediOldestReqUrl);
    }
    const shihabNorm = shihabNormalizeMediaStreamUrl(mehediReqDetails.url).shihabCleanUrl;
    let mehediOrigin = "";
    let shihabReferer = "";
    let mehediHadCookie = false;

    if (Array.isArray(mehediReqDetails.requestHeaders)) {
      for (const shihabHdr of mehediReqDetails.requestHeaders) {
        const mehediName = (shihabHdr.name || "").toLowerCase();
        if (mehediName === "origin") mehediOrigin = shihabHdr.value || "";
        else if (mehediName === "referer") shihabReferer = shihabHdr.value || "";
        else if (mehediName === "cookie" && shihabHdr.value) mehediHadCookie = true;
      }
    }

    shihabRuntimeState.shihabRequestHeadersByUrl.set(shihabNorm, {
      shihabOrigin: mehediOrigin,
      mehediReferer: shihabReferer,
      shihabHadCookie: mehediHadCookie
    });
    if (shihabRuntimeState.shihabRequestHeadersByUrl.size > 400) {
      const shihabFirst = shihabRuntimeState.shihabRequestHeadersByUrl.keys().next().value;
      shihabRuntimeState.shihabRequestHeadersByUrl.delete(shihabFirst);
    }
  },
  { urls: ["<all_urls>"] },
  ["requestHeaders", "extraHeaders"]
);

// Sniff unencrypted HTTP/HTTPS media streams per tab
chrome.webRequest.onHeadersReceived.addListener(
  (shihabDetails) => {
    if (!shihabDetails || !shihabDetails.url || !shihabDetails.url.startsWith("http")) {
      return;
    }

    let mehediContentType = "";
    let shihabContentLength = -1;
    let mehediTotalRangeBytes = -1;
    let shihabDispHeader = "";

    if (Array.isArray(shihabDetails.responseHeaders)) {
      for (const mehediHdr of shihabDetails.responseHeaders) {
        const shihabHName = (mehediHdr.name || "").toLowerCase();
        const mehediHVal = mehediHdr.value || "";
        if (shihabHName === "content-type") {
          mehediContentType = mehediHVal.toLowerCase();
        } else if (shihabHName === "content-length") {
          shihabContentLength = parseInt(mehediHVal || "-1", 10);
        } else if (shihabHName === "content-disposition") {
          shihabDispHeader = mehediHVal;
        } else if (shihabHName === "content-range") {
          const shihabRangeSlash = mehediHVal.lastIndexOf("/");
          if (shihabRangeSlash > 0) {
            const mehediTotalParsed = parseInt(mehediHVal.slice(shihabRangeSlash + 1).trim(), 10);
            if (!Number.isNaN(mehediTotalParsed) && mehediTotalParsed > 0) {
              mehediTotalRangeBytes = mehediTotalParsed;
            }
          }
        }
      }
    }

    // Cache Content-Disposition filenames & attachment MIME types even when tabId === -1 (browser download stream)
    const mehediHeaderFileName = shihabExtractContentDispositionFileName(shihabDispHeader);
    const shihabIsAttachment = /attachment/i.test(shihabDispHeader);
    if (mehediHeaderFileName || shihabIsAttachment || mehediContentType.includes("matroska") || mehediContentType.includes("octet-stream")) {
      shihabRuntimeState.mehediResponseMetaByUrl.set(shihabDetails.url, {
        shihabHeaderFileName: mehediHeaderFileName,
        mehediContentType: mehediContentType,
        shihabContentLength: shihabContentLength,
        shihabIsAttachment: shihabIsAttachment
      });
      if (shihabRuntimeState.mehediResponseMetaByUrl.size > 300) {
        const mehediFirstUrl = shihabRuntimeState.mehediResponseMetaByUrl.keys().next().value;
        shihabRuntimeState.mehediResponseMetaByUrl.delete(mehediFirstUrl);
      }
    }

    if (shihabDetails.tabId < 0) {
      return;
    }
    if (shihabDetails.method && shihabDetails.method.toUpperCase() !== "GET") {
      return;
    }

    if (shihabIsPoisonedYouTubeWebUrl(shihabDetails.url)) {
      return;
    }
    let shihabQueryMime = "";
    try {
      const mehediParsedUrl = new URL(shihabDetails.url);
      shihabQueryMime = (mehediParsedUrl.searchParams.get("mime") || "").toLowerCase();
      if (mehediParsedUrl.searchParams.get("ump") === "1" || mehediParsedUrl.searchParams.get("sabr") === "1") {
        return;
      }
    } catch (_) {}

    const mehediEffectiveMime = (mehediContentType.startsWith("video/") || mehediContentType.startsWith("audio/"))
      ? mehediContentType
      : shihabQueryMime;

    const mehediNormalized = shihabNormalizeMediaStreamUrl(shihabDetails.url);
    const shihabEffectiveUrl = mehediNormalized.shihabCleanUrl;
    const shihabUrlMeta = mehediInspectUrlQueryMetadata(shihabEffectiveUrl);
    const mehediExt = shihabUrlMeta.shihabExtOverride || shihabExtractExtensionFromUrlOrName(shihabEffectiveUrl, "");

    const shihabIsHls =
      mehediExt === "m3u8" ||
      mehediContentType.includes("mpegurl") ||
      mehediContentType.includes("vnd.apple.mpegurl");

    const mehediIsDirectMedia =
      ["mp4", "mkv", "webm", "mov", "m4v", "flv", "avi", "3gp", "mp3", "m4a", "aac", "flac", "wav", "ogg", "opus"].includes(mehediExt) ||
      mehediEffectiveMime.startsWith("video/") ||
      mehediEffectiveMime.startsWith("audio/");

    if (mehediContentType.includes("mp2t") || mehediContentType.includes("vnd.yt-ump") || mehediExt === "m4s" || mehediExt === "ts") {
      return;
    }
    if (!shihabIsHls && !mehediIsDirectMedia) {
      return;
    }

    const shihabKnownFullSize = mehediTotalRangeBytes > 0
      ? mehediTotalRangeBytes
      : (shihabUrlMeta.shihabClenBytes > 0 ? shihabUrlMeta.shihabClenBytes : shihabContentLength);

    const mehediIsAudioCandidate =
      shihabUrlMeta.mehediTrackRole === "audio" ||
      (mehediEffectiveMime || mehediContentType).startsWith("audio/") ||
      ["mp3", "m4a", "aac", "ogg", "opus", "wav"].includes(mehediExt);

    // Allow companion DASH audio tracks >= 40 KB while still dropping tiny 6 KB UI click/notification sounds!
    const shihabMinThresholdBytes = mehediIsAudioCandidate
      ? (40 * 1024)
      : Math.max(80 * 1024, shihabRuntimeState.shihabMinVideoSizeKb * 1024);

    const shihabIsUnmeasuredSlice = mehediNormalized.mehediWasSliced && mehediTotalRangeBytes <= 0 && shihabUrlMeta.shihabClenBytes <= 0;
    if (!shihabIsHls && !shihabIsUnmeasuredSlice && shihabKnownFullSize > 0 && shihabKnownFullSize < shihabMinThresholdBytes) {
      return;
    }

    shihabUpsertTabStreamRecord(shihabDetails.tabId, {
      shihabStreamUrl: shihabEffectiveUrl,
      mehediMimeType: mehediEffectiveMime || mehediContentType,
      shihabExt: mehediExt || (shihabIsHls ? "m3u8" : "mp4"),
      shihabSizeBytes: shihabIsUnmeasuredSlice ? -1 : shihabKnownFullSize,
      shihabExactSizeKnown: mehediTotalRangeBytes > 0 || shihabUrlMeta.shihabClenBytes > 0,
      mehediTrackRole: shihabUrlMeta.mehediTrackRole || (mehediIsAudioCandidate ? "audio" : ""),
      shihabResolution: shihabUrlMeta.shihabResolution || "",
      shihabGroupKey: shihabUrlMeta.mehediGroupKey || ""
    });

    shihabPushUpdatedStreamsToTab(shihabDetails.tabId);
  },
  { urls: ["<all_urls>"] },
  ["responseHeaders", "extraHeaders"]
);

chrome.tabs.onRemoved.addListener((shihabTabId) => {
  shihabRuntimeState.mehediDetectedStreamsByTab.delete(shihabTabId);
  shihabRuntimeState.mehediAltBypassUntilMsByTab.delete(shihabTabId);
  shihabRuntimeState.shihabForceCaptureUntilMsByTab.delete(shihabTabId);
  shihabRuntimeState.mehediTrustedGestureUntilByTab.delete(shihabTabId);
  shihabRuntimeState.mehediUserCreatedNewTabs.delete(shihabTabId);
});

function mehediUpdateToolbarBadgeForTab(shihabSpecificTabId) {
  if (!shihabRuntimeState.mehediDesktopOnline) {
    chrome.action.setBadgeText({ text: "OFF" });
    chrome.action.setBadgeBackgroundColor({ color: "#ef4444" });
  } else if (!shihabRuntimeState.shihabIntegrationEnabled) {
    chrome.action.setBadgeText({ text: "PAUS" });
    chrome.action.setBadgeBackgroundColor({ color: "#f59e0b" });
  } else {
    chrome.action.setBadgeText({ text: "" });
  }
  if (typeof shihabSpecificTabId === "number" && shihabSpecificTabId >= 0) {
    const mehediStreams = shihabRuntimeState.mehediDetectedStreamsByTab.get(shihabSpecificTabId) || [];
    if (mehediStreams.length > 0) {
      chrome.action.setBadgeText({ tabId: shihabSpecificTabId, text: String(Math.min(99, mehediStreams.length)) });
      chrome.action.setBadgeBackgroundColor({ tabId: shihabSpecificTabId, color: "#2563eb" });
    }
  }
}

async function shihabResolveYouTubeStreamsViaDesktopForTab(
  mehediTabId,
  shihabVideoId,
  mehediTitleHint,
  shihabVisitorData,
  mehediForce
) {
  if (typeof mehediTabId !== "number" || mehediTabId < 0 || !shihabVideoId || !/^[A-Za-z0-9_-]{11}$/.test(shihabVideoId)) {
    return { ok: false, shihabUsePageFallback: false };
  }

  const shihabInFlightKey = `${mehediTabId}:${shihabVideoId}`;
  if (!mehediForce && shihabRuntimeState.shihabInFlightYtResolves.has(shihabInFlightKey)) {
    return shihabRuntimeState.shihabInFlightYtResolves.get(shihabInFlightKey);
  }

  const mehediWorkPromise = (async () => {
    try {
      // Purge any stale poisoned c=WEB googlevideo entries on this tab
      const shihabCurList = shihabRuntimeState.mehediDetectedStreamsByTab.get(mehediTabId);
      if (Array.isArray(shihabCurList) && shihabCurList.length > 0) {
        const mehediCleaned = shihabCurList.filter((shihabRec) => !shihabIsPoisonedYouTubeWebUrl(shihabRec.shihabStreamUrl));
        if (mehediCleaned.length !== shihabCurList.length) {
          shihabRuntimeState.mehediDetectedStreamsByTab.set(mehediTabId, mehediCleaned);
        }
      }

      const shihabYtQuery =
        `/shihab/yt-streams?videoId=${encodeURIComponent(shihabVideoId)}` +
        (shihabVisitorData ? `&visitorData=${encodeURIComponent(shihabVisitorData)}` : "") +
        (mehediForce ? "&force=1" : "");

      let mehediBridgeRes = await mehediCallDesktopBridge(shihabYtQuery, "GET", null);
      if (!mehediBridgeRes) {
        // Fallback to Native Messaging stdio if mdm.exe wasn't open yet
        try {
          mehediBridgeRes = await chrome.runtime.sendNativeMessage(MEHEDI_NATIVE_HOST_NAME, {
            action: "yt-streams",
            videoId: shihabVideoId,
            visitorData: shihabVisitorData || ""
          });
        } catch (_) {}
      }

      if (!mehediBridgeRes || !mehediBridgeRes.ok || !Array.isArray(mehediBridgeRes.streams) || mehediBridgeRes.streams.length === 0) {
        return { ok: false, shihabUsePageFallback: true };
      }

      shihabRuntimeState.mehediDesktopOnline = true;
      const shihabFinalTitle = String(mehediBridgeRes.title || mehediTitleHint || "");
      const mehediDurationSec = Number(mehediBridgeRes.durationSec || 0);
      const shihabYtGrp = `yt:${shihabVideoId}`;
      const mehediWatchRef = `https://www.youtube.com/watch?v=${shihabVideoId}`;

      for (const shihabSt of mehediBridgeRes.streams) {
        if (!shihabSt || !shihabSt.url) continue;
        shihabUpsertTabStreamRecord(mehediTabId, {
          shihabStreamUrl: shihabSt.url,
          mehediMimeType: shihabSt.mimeType || "",
          shihabCodecs: shihabSt.codecs || "",
          shihabExt: shihabSt.ext || (shihabSt.trackRole === "audio" ? "m4a" : "mp4"),
          shihabSizeBytes: Number(shihabSt.sizeBytes || -1),
          shihabExactSizeKnown: Number(shihabSt.sizeBytes || -1) > 0,
          mehediTrackRole: shihabSt.trackRole || (shihabSt.hasVideo ? "video_only" : "audio"),
          shihabWidth: Number(shihabSt.width || 0),
          mehediHeight: Number(shihabSt.height || 0),
          mehediDurationSec: mehediDurationSec,
          shihabResolution: shihabSt.resolution || "",
          mehediBandwidth: Number(shihabSt.bitrate || 0),
          shihabGroupKey: shihabYtGrp,
          shihabFromManifest: true,
          mehediShortcode: shihabVideoId,
          mehediAltIds: [shihabVideoId, shihabYtGrp],
          shihabTitleHint: shihabFinalTitle,
          mehediAudioCompanionUrl: shihabSt.audioCompanionUrl || "",
          shihabAudioCompanionBytes: Number(shihabSt.audioCompanionBytes || -1),
          mehediCustomUserAgent: shihabSt.userAgent || "",
          shihabIsOriginalAudio: Boolean(shihabSt.isOriginalAudio),
          mehediIsHdr: Boolean(shihabSt.isHdr),
          shihabOrigin: "",
          mehediCapturedReferer: mehediWatchRef,
          mehediOmitCookies: true,
          shihabSkipBinaryProbe: true
        });
      }

      shihabPushUpdatedStreamsToTab(mehediTabId);
      return { ok: true, shihabUsePageFallback: false };
    } catch (_) {
      return { ok: false, shihabUsePageFallback: true };
    } finally {
      shihabRuntimeState.shihabInFlightYtResolves.delete(shihabInFlightKey);
    }
  })();

  shihabRuntimeState.shihabInFlightYtResolves.set(shihabInFlightKey, mehediWorkPromise);
  return mehediWorkPromise;
}

async function mehediEnsureYouTubeTabResolvedIfApplicable(shihabTabId, mehediForce) {
  if (typeof shihabTabId !== "number" || shihabTabId < 0) return;
  try {
    const shihabTabObj = await chrome.tabs.get(shihabTabId);
    const mehediVidId = shihabTabObj ? mehediExtractYouTubeVideoIdFromPageUrl(shihabTabObj.url || "") : "";
    if (!mehediVidId) return;
    const shihabYtGrp = `yt:${mehediVidId}`;
    const mehediExisting = shihabRuntimeState.mehediDetectedStreamsByTab.get(shihabTabId) || [];
    const shihabAlreadyHasGroup = mehediExisting.some(
      (mehediR) => mehediR && mehediR.shihabGroupKey === shihabYtGrp && Boolean(mehediR.mehediCustomUserAgent)
    );
    if (mehediForce || !shihabAlreadyHasGroup) {
      await shihabResolveYouTubeStreamsViaDesktopForTab(shihabTabId, mehediVidId, "", "", Boolean(mehediForce));
    }
  } catch (_) {}
}

chrome.runtime.onMessage.addListener((shihabMsg, mehediSender, shihabSendResponse) => {
  const mehediTabId = mehediSender && mehediSender.tab ? mehediSender.tab.id : -1;

  if (shihabMsg && shihabMsg.shihabCmd === "mehedi_resolve_youtube_video") {
    const shihabTargetTabId = typeof shihabMsg.mehediTabId === "number" && shihabMsg.mehediTabId >= 0
      ? shihabMsg.mehediTabId
      : mehediTabId;
    const mehediVidId = String(shihabMsg.shihabVideoId || "");
    shihabResolveYouTubeStreamsViaDesktopForTab(
      shihabTargetTabId,
      mehediVidId,
      String(shihabMsg.mehediTitleHint || ""),
      String(shihabMsg.shihabVisitorData || ""),
      Boolean(shihabMsg.mehediForce)
    ).then(async (shihabResObj) => {
      const mehediEnriched = shihabResObj && shihabResObj.ok
        ? await mehediEnrichStreamDisplayNamesForTab(shihabTargetTabId)
        : [];
      shihabSendResponse({
        ok: Boolean(shihabResObj && shihabResObj.ok),
        shihabUsePageFallback: Boolean(shihabResObj && shihabResObj.shihabUsePageFallback),
        mehediStreams: mehediEnriched || []
      });
    }).catch(() => {
      shihabSendResponse({ ok: false, shihabUsePageFallback: true, mehediStreams: [] });
    });
    return true;
  }

  if (shihabMsg && shihabMsg.shihabCmd === "mehedi_register_manifest_streams" && mehediTabId >= 0) {
    const shihabList = Array.isArray(shihabMsg.shihabStreams) ? shihabMsg.shihabStreams : [];
    for (const mehediItem of shihabList) {
      if (!mehediItem || !mehediItem.shihabUrl) continue;
      shihabUpsertTabStreamRecord(mehediTabId, {
        shihabStreamUrl: mehediItem.shihabUrl,
        mehediMimeType: mehediItem.mehediMimeType || "",
        shihabCodecs: mehediItem.shihabCodecs || "",
        shihabSizeBytes: Number(mehediItem.shihabSizeBytes || -1),
        mehediTrackRole: mehediItem.mehediTrackRole || "",
        shihabWidth: Number(mehediItem.shihabWidth || 0),
        mehediHeight: Number(mehediItem.mehediHeight || 0),
        mehediDurationSec: Number(mehediItem.mehediDurationSec || 0),
        shihabResolution: mehediItem.shihabResolution || "",
        mehediBandwidth: Number(mehediItem.mehediBandwidth || 0),
        shihabGroupKey: mehediItem.shihabGroupKey || "",
        shihabFromManifest: true,
        mehediShortcode: mehediItem.mehediShortcode || "",
        mehediAltIds: Array.isArray(mehediItem.mehediAltIds) ? mehediItem.mehediAltIds : [],
        shihabTitleHint: mehediItem.shihabTitleHint || "",
        mehediCustomUserAgent: mehediItem.mehediCustomUserAgent || "",
        shihabIsOriginalAudio: Boolean(mehediItem.shihabIsOriginalAudio),
        mehediIsHdr: Boolean(mehediItem.mehediIsHdr),
        shihabSkipBinaryProbe: Boolean(mehediItem.mehediCustomUserAgent)
      });
    }
    shihabPushUpdatedStreamsToTab(mehediTabId);
    shihabSendResponse({ ok: true });
    return false;
  }

  if (shihabMsg && shihabMsg.shihabCmd === "shihab_retrack_active_video") {
    const shihabTargetTabId = typeof shihabMsg.mehediTabId === "number" && shihabMsg.mehediTabId >= 0
      ? shihabMsg.mehediTabId
      : mehediTabId;
    if (shihabTargetTabId >= 0) {
      const mehediKeepUrls = new Set(Array.isArray(shihabMsg.shihabKeepUrls) ? shihabMsg.shihabKeepUrls : []);
      const shihabKeepGroup = String(shihabMsg.mehediKeepGroupKey || "");
      const mehediExistingList = shihabRuntimeState.mehediDetectedStreamsByTab.get(shihabTargetTabId) || [];
      if (mehediKeepUrls.size > 0 || shihabKeepGroup) {
        const shihabFiltered = mehediExistingList.filter((mehediRec) =>
          mehediKeepUrls.has(mehediRec.shihabStreamUrl) ||
          (shihabKeepGroup && mehediRec.shihabGroupKey === shihabKeepGroup)
        );
        shihabRuntimeState.mehediDetectedStreamsByTab.set(shihabTargetTabId, shihabFiltered);
      } else {
        // Keep only the single freshest group or streams detected within the last 12 seconds so stale earlier videos are purged!
        let mehediLatestGroup = "";
        let shihabLatestTs = -1;
        for (const mehediRec of mehediExistingList) {
          if (mehediRec.shihabGroupKey && (mehediRec.shihabDetectedAt || 0) > shihabLatestTs) {
            shihabLatestTs = mehediRec.shihabDetectedAt || 0;
            mehediLatestGroup = mehediRec.shihabGroupKey;
          }
        }
        const shihabCutoff = Date.now() - 12000;
        const mehediFreshOnly = mehediExistingList.filter((shihabRec) =>
          (mehediLatestGroup && shihabRec.shihabGroupKey === mehediLatestGroup) ||
          (!mehediLatestGroup && (shihabRec.shihabDetectedAt || 0) >= shihabCutoff)
        );
        shihabRuntimeState.mehediDetectedStreamsByTab.set(shihabTargetTabId, mehediFreshOnly);
      }
      chrome.tabs.sendMessage(shihabTargetTabId, { shihabCmd: "mehedi_trigger_page_retrack" }).catch(() => {});
      mehediEnsureYouTubeTabResolvedIfApplicable(shihabTargetTabId, true)
        .then(() => mehediEnrichStreamDisplayNamesForTab(shihabTargetTabId))
        .then((shihabRefreshed) => {
          shihabPushUpdatedStreamsToTab(shihabTargetTabId);
          shihabSendResponse({ ok: true, mehediStreams: shihabRefreshed || [] });
        })
        .catch(() => shihabSendResponse({ ok: true, mehediStreams: [] }));
      return true;
    }
    shihabSendResponse({ ok: false, mehediStreams: [] });
    return false;
  }

  if (
    shihabMsg &&
    (shihabMsg.shihabCmd === "mehedi_user_download_gesture" || shihabMsg.shihabCmd === "mehedi_key_modifier_state") &&
    mehediTabId >= 0
  ) {
    if (shihabMsg.shihabAltPressed) {
      shihabRuntimeState.mehediAltBypassUntilMsByTab.set(mehediTabId, Date.now() + 2500);
    }
    if (shihabMsg.mehediInsertPressed) {
      shihabRuntimeState.shihabForceCaptureUntilMsByTab.set(mehediTabId, Date.now() + 2500);
    }
    if (shihabMsg.shihabCmd === "mehedi_user_download_gesture" && shihabMsg.shihabIsGesture !== false) {
      mehediRecordTrustedUserGesture(
        mehediTabId,
        String(shihabMsg.shihabPageUrl || (mehediSender && mehediSender.tab ? mehediSender.tab.url : "") || ""),
        String(shihabMsg.mehediClickedHref || "")
      );
    }
    shihabSendResponse({ ok: true });
    return false;
  }

  if (shihabMsg && shihabMsg.shihabCmd === "shihab_query_tab_streams") {
    const shihabTargetTabId = typeof shihabMsg.mehediTabId === "number" ? shihabMsg.mehediTabId : mehediTabId;
    (async () => {
      const mehediPing = await shihabSyncDesktopConfiguration();
      await mehediEnsureYouTubeTabResolvedIfApplicable(shihabTargetTabId, false);
      const shihabEnrichedStreams = await mehediEnrichStreamDisplayNamesForTab(shihabTargetTabId);
      shihabSendResponse({
        mehediOnline: shihabRuntimeState.mehediDesktopOnline,
        shihabEnabled: shihabRuntimeState.shihabIntegrationEnabled,
        mehediShowVideoPanel: shihabRuntimeState.mehediShowVideoPanel !== false,
        shihabBridgeInfo: mehediPing,
        mehediStreams: shihabEnrichedStreams || []
      });
    })().catch(() => {
      shihabSendResponse({
        mehediOnline: shihabRuntimeState.mehediDesktopOnline,
        shihabEnabled: shihabRuntimeState.shihabIntegrationEnabled,
        mehediShowVideoPanel: true,
        shihabBridgeInfo: null,
        mehediStreams: []
      });
    });
    return true;
  }

  if (shihabMsg && shihabMsg.shihabCmd === "mehedi_send_download_now") {
    const shihabResolvedTabId = typeof shihabMsg.shihabTabId === "number" && shihabMsg.shihabTabId >= 0
      ? shihabMsg.shihabTabId
      : mehediTabId;

    shihabDispatchSingleUrlToDesktop({
      shihabUrl: shihabMsg.shihabUrl,
      mehediReferrer: shihabMsg.mehediReferrer || (mehediSender && mehediSender.tab ? mehediSender.tab.url : ""),
      shihabFileName: shihabMsg.shihabFileName || "",
      shihabFileSize: shihabMsg.shihabFileSize || -1,
      mehediTabTitle: shihabMsg.mehediTabTitle || (mehediSender && mehediSender.tab ? mehediSender.tab.title : ""),
      shihabOrigin: shihabMsg.shihabOrigin || "",
      mehediOmitCookies: shihabMsg.mehediOmitCookies,
      mehediUserAgent: shihabMsg.mehediUserAgent || "",
      mehediAudioCompanionUrl: shihabMsg.mehediAudioCompanionUrl || "",
      shihabAudioCompanionBytes: shihabMsg.shihabAudioCompanionBytes || -1,
      shihabQualityLabel: shihabMsg.shihabQualityLabel || "",
      shihabTabId: shihabResolvedTabId,
      shihabIncognito: Boolean(shihabMsg.shihabIncognito || (mehediSender && mehediSender.tab && mehediSender.tab.incognito))
    }).then((shihabSentOk) => {
      shihabSendResponse({
        ok: shihabSentOk,
        mehediError: shihabSentOk ? "" : "Launch mdm.exe (MHS Download Manager) first to receive downloads."
      });
    });
    return true;
  }

  if (shihabMsg && shihabMsg.shihabCmd === "shihab_relay_chunk_from_tab") {
    // Allow content script to forward base64 chunk to local bridge if page CSP blocks mixed-content fetch to 127.0.0.1
    (async () => {
      try {
        const mehediBinStr = atob(shihabMsg.mehediBase64Chunk || "");
        const shihabBytes = new Uint8Array(mehediBinStr.length);
        for (let mehediI = 0; mehediI < mehediBinStr.length; mehediI++) {
          shihabBytes[mehediI] = mehediBinStr.charCodeAt(mehediI);
        }
        const mehediResp = await fetch(
          `${SHIHAB_BRIDGE_BASE_URL}/shihab/relay-chunk?jobId=${encodeURIComponent(shihabMsg.shihabJobId)}&offset=${shihabMsg.mehediOffset}&total=${shihabMsg.shihabTotal}`,
          {
            method: "POST",
            headers: { "Content-Type": "application/octet-stream" },
            body: shihabBytes.buffer
          }
        );
        shihabSendResponse({ ok: mehediResp.ok, cancelled: mehediResp.status === 410 });
      } catch (shihabErr) {
        shihabSendResponse({ ok: false, cancelled: false });
      }
    })();
    return true;
  }

  if (shihabMsg && shihabMsg.shihabCmd === "shihab_relay_done_from_tab") {
    fetch(
      `${SHIHAB_BRIDGE_BASE_URL}/shihab/relay-done?jobId=${encodeURIComponent(shihabMsg.shihabJobId)}&ok=${shihabMsg.mehediOk ? "1" : "0"}&error=${encodeURIComponent(shihabMsg.shihabError || "")}`,
      { method: "POST", body: "{}" }
    ).then(() => shihabSendResponse({ ok: true })).catch(() => shihabSendResponse({ ok: false }));
    return true;
  }

  if (shihabMsg && shihabMsg.shihabCmd === "shihab_show_desktop_app") {
    mehediCallDesktopBridge("/shihab/show", "POST", {}).then((shihabAck) => {
      shihabSendResponse({ ok: Boolean(shihabAck && shihabAck.ok) });
    }).catch(() => shihabSendResponse({ ok: false }));
    return true;
  }

  if (shihabMsg && shihabMsg.shihabCmd === "shihab_toggle_integration") {
    mehediCallDesktopBridge("/shihab/toggle", "POST", {}).then(() => {
      return shihabSyncDesktopConfiguration();
    }).then((mehediStatus) => {
      shihabSendResponse({ ok: Boolean(mehediStatus), status: mehediStatus });
    });
    return true;
  }

  if (shihabMsg && shihabMsg.shihabCmd === "mehedi_grab_tab_links" && typeof shihabMsg.mehediTabId === "number") {
    chrome.tabs.sendMessage(shihabMsg.mehediTabId, {
      shihabCmd: "mehedi_collect_page_links",
      mehediSelectionOnly: false
    }).then(async (shihabCol) => {
      if (shihabCol && Array.isArray(shihabCol.shihabLinks) && shihabCol.shihabLinks.length > 0) {
        const mehediCookies = await mehediBuildCookieHeaderForUrl(shihabCol.mehediPageUrl || "");
        const shihabAck = await mehediCallDesktopBridge("/shihab/capture-batch", "POST", {
          pageUrl: shihabCol.mehediPageUrl || "",
          pageTitle: shihabCol.shihabPageTitle || "",
          cookies: mehediCookies,
          userAgent: navigator.userAgent,
          links: shihabCol.shihabLinks
        });
        shihabSendResponse({ ok: Boolean(shihabAck && shihabAck.ok), count: shihabCol.shihabLinks.length });
      } else {
        shihabSendResponse({ ok: false, count: 0 });
      }
    }).catch(() => shihabSendResponse({ ok: false, count: 0 }));
    return true;
  }

  return false;
});
