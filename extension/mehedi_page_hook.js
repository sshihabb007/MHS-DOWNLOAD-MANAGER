"use strict";

(() => {
  if (window.__shihabMehediPageHookInstalled) {
    try {
      window.postMessage({ __shihabMehediContentPing: true }, "*");
    } catch (_) {}
    return;
  }
  window.__shihabMehediPageHookInstalled = true;

  let mehediNextVideoUidSeq = 1;
  const mehediTopOrigFetch = typeof window.fetch === "function" ? window.fetch.bind(window) : null;
  const shihabMediaSourceToBlobUrl = new WeakMap();
  const mehediBlobUrlToMediaSource = new Map();
  const mehediSourceBufferMeta = new WeakMap();
  const shihabRecentFetchedSlices = [];
  const shihabChunkHashToStreamUrl = new Map();
  const mehediEmittedManifestKeys = new Set();

  function shihabEnsureVideoUidOnElement(mehediVidEl) {
    if (!mehediVidEl || mehediVidEl.tagName !== "VIDEO") return "";
    if (!mehediVidEl.dataset.shihabVidUid) {
      mehediVidEl.dataset.shihabVidUid = "shihab_vid_" + (mehediNextVideoUidSeq++) + "_" + Math.random().toString(36).slice(2, 7);
    }
    return mehediVidEl.dataset.shihabVidUid;
  }

  function shihabFnv1aStringHash(mehediStr) {
    let shihabH = 0x811c9dc5;
    const mehediS = String(mehediStr || "");
    for (let shihabI = 0; shihabI < mehediS.length; shihabI++) {
      shihabH ^= mehediS.charCodeAt(shihabI);
      shihabH = Math.imul(shihabH, 0x01000193) >>> 0;
    }
    return shihabH.toString(16);
  }

  function shihabFnv1aHashSlice(mehediBytes, shihabStart, mehediLen, shihabPrefix) {
    if (!mehediBytes || mehediBytes.byteLength < shihabStart + 16) return "";
    const shihabEnd = Math.min(mehediBytes.byteLength, shihabStart + mehediLen);
    let mehediHash = 0x811c9dc5;
    for (let shihabI = shihabStart; shihabI < shihabEnd; shihabI++) {
      mehediHash ^= mehediBytes[shihabI];
      mehediHash = Math.imul(mehediHash, 0x01000193) >>> 0;
    }
    return `${shihabPrefix || shihabStart}:${shihabEnd - shihabStart}:${mehediHash.toString(16)}`;
  }

  // Compute FNV-1a signatures both at start offsets AND at ISO-BMFF ftyp/moof box boundaries
  // so sliced DASH sub-segments passed into SourceBuffer.appendBuffer match 100% accurately!
  function shihabComputeMediaChunkSignatures(mehediRawBuf) {
    const shihabSigs = [];
    if (!mehediRawBuf) return shihabSigs;
    let mehediUint8 = null;
    const SHIHAB_SCAN_MAX = 196608;
    if (mehediRawBuf instanceof Uint8Array) {
      mehediUint8 = mehediRawBuf.byteLength > SHIHAB_SCAN_MAX
        ? mehediRawBuf.subarray(0, SHIHAB_SCAN_MAX)
        : mehediRawBuf;
    } else if (ArrayBuffer.isView(mehediRawBuf)) {
      mehediUint8 = new Uint8Array(
        mehediRawBuf.buffer,
        mehediRawBuf.byteOffset,
        Math.min(SHIHAB_SCAN_MAX, mehediRawBuf.byteLength)
      );
    } else if (mehediRawBuf instanceof ArrayBuffer) {
      mehediUint8 = new Uint8Array(mehediRawBuf, 0, Math.min(SHIHAB_SCAN_MAX, mehediRawBuf.byteLength));
    }
    if (!mehediUint8 || mehediUint8.byteLength < 28) return shihabSigs;

    const shihabSig0 = shihabFnv1aHashSlice(mehediUint8, 0, 48, "s0");
    const mehediSig8 = shihabFnv1aHashSlice(mehediUint8, 8, 48, "s8");
    const shihabSig24 = shihabFnv1aHashSlice(mehediUint8, 24, 64, "s24");
    if (shihabSig0) shihabSigs.push(shihabSig0);
    if (mehediSig8) shihabSigs.push(mehediSig8);
    if (shihabSig24) shihabSigs.push(shihabSig24);

    // Scan for ISO-BMFF ftyp (0x66 0x74 0x79 0x70) and moof (0x6D 0x6F 0x6F 0x66) box headers
    let mehediBoxMatches = 0;
    const shihabLim = mehediUint8.byteLength - 40;
    for (let mehediPos = 4; mehediPos <= shihabLim && mehediBoxMatches < 6; mehediPos++) {
      const shihabB0 = mehediUint8[mehediPos];
      if (shihabB0 === 0x6d) {
        if (
          mehediUint8[mehediPos + 1] === 0x6f &&
          mehediUint8[mehediPos + 2] === 0x6f &&
          mehediUint8[mehediPos + 3] === 0x66
        ) {
          const shihabMoofSig = shihabFnv1aHashSlice(mehediUint8, mehediPos - 4, 36, "moof");
          if (shihabMoofSig) {
            shihabSigs.push(shihabMoofSig);
            mehediBoxMatches++;
            mehediPos += 32;
          }
        }
      } else if (shihabB0 === 0x66) {
        if (
          mehediUint8[mehediPos + 1] === 0x74 &&
          mehediUint8[mehediPos + 2] === 0x79 &&
          mehediUint8[mehediPos + 3] === 0x70
        ) {
          const mehediFtypSig = shihabFnv1aHashSlice(mehediUint8, mehediPos - 4, 28, "ftyp");
          if (mehediFtypSig) {
            shihabSigs.push(mehediFtypSig);
            mehediBoxMatches++;
            mehediPos += 24;
          }
        }
      }
    }

    return shihabSigs;
  }

  function mehediIndexChunkBytesForUrl(shihabRawUrl, mehediBytes) {
    const shihabClean = mehediNormalizeHookMediaUrl(shihabRawUrl);
    if (!shihabClean || !shihabLooksLikeMediaSliceUrl(shihabClean)) return;
    const mehediSigs = shihabComputeMediaChunkSignatures(mehediBytes);
    for (const shihabSig of mehediSigs) {
      shihabChunkHashToStreamUrl.set(shihabSig, shihabClean);
      if (shihabChunkHashToStreamUrl.size > 600) {
        const mehediOldestKey = shihabChunkHashToStreamUrl.keys().next().value;
        shihabChunkHashToStreamUrl.delete(mehediOldestKey);
      }
    }
  }

  // Extract authoritative per-video asset IDs from efg=<base64_json> or YouTube docid
  function mehediExtractEfgIds(shihabCleanUrl) {
    const shihabAltSet = new Set();
    let mehediCanonicalGroup = "";
    if (!shihabCleanUrl) return { mehediCanonicalGroup, shihabAltIds: [] };
    try {
      const shihabParsed = new URL(shihabCleanUrl, location.href);
      if (shihabParsed.hostname.includes("googlevideo.com") || shihabParsed.pathname.includes("/videoplayback")) {
        const mehediDocId = shihabParsed.searchParams.get("docid") || "";
        if (mehediDocId) {
          mehediCanonicalGroup = `yt:${mehediDocId}`;
          shihabAltSet.add(mehediDocId);
          shihabAltSet.add(mehediCanonicalGroup);
          return { mehediCanonicalGroup, shihabAltIds: Array.from(shihabAltSet) };
        }
      }

      const shihabEfg = shihabParsed.searchParams.get("efg");
      if (shihabEfg) {
        const mehediB64 = decodeURIComponent(shihabEfg).replace(/-/g, "+").replace(/_/g, "/");
        const shihabPad = mehediB64 + "=".repeat((4 - (mehediB64.length % 4)) % 4);
        const mehediJson = JSON.parse(atob(shihabPad));
        if (mehediJson && typeof mehediJson === "object") {
          const shihabXpv = mehediJson.xpv_asset_id ? String(mehediJson.xpv_asset_id) : "";
          const mehediVid = mehediJson.video_id ? String(mehediJson.video_id) : "";
          const shihabAst = mehediJson.asset_id ? String(mehediJson.asset_id) : "";

          if (shihabXpv) {
            mehediCanonicalGroup = `asset:${shihabXpv}`;
            shihabAltSet.add(shihabXpv);
            shihabAltSet.add(mehediCanonicalGroup);
          }
          if (mehediVid) {
            if (!mehediCanonicalGroup) mehediCanonicalGroup = `vid:${mehediVid}`;
            shihabAltSet.add(mehediVid);
            shihabAltSet.add(`vid:${mehediVid}`);
          }
          if (shihabAst) {
            if (!mehediCanonicalGroup) mehediCanonicalGroup = `asset:${shihabAst}`;
            shihabAltSet.add(shihabAst);
            shihabAltSet.add(`asset:${shihabAst}`);
          }
        }
      }
    } catch (_) {}
    return {
      mehediCanonicalGroup,
      shihabAltIds: Array.from(shihabAltSet)
    };
  }

  function shihabPostToContentBridge(mehediKind, shihabPayload) {
    try {
      window.postMessage(
        {
          __shihabMehediPageHook: true,
          mehediKind,
          shihabPayload
        },
        "*"
      );
    } catch (_) {}
  }

  function mehediNormalizeHookMediaUrl(shihabRawUrl) {
    if (!shihabRawUrl || typeof shihabRawUrl !== "string") return "";
    try {
      const mehediUrlObj = new URL(shihabRawUrl, location.href);
      if (mehediUrlObj.protocol !== "http:" && mehediUrlObj.protocol !== "https:") {
        return "";
      }
      for (const shihabParam of ["bytestart", "byteend", "alr", "rn", "rbuf"]) {
        mehediUrlObj.searchParams.delete(shihabParam);
      }
      if (mehediUrlObj.searchParams.has("range")) {
        const mehediSparams = (mehediUrlObj.searchParams.get("sparams") || "").split(",");
        if (!mehediSparams.includes("range")) {
          mehediUrlObj.searchParams.delete("range");
        }
      }
      return mehediUrlObj.toString();
    } catch (_) {
      return "";
    }
  }

  function shihabIsPoisonedYouTubeWebUrl(mehediRawUrl) {
    if (!mehediRawUrl || typeof mehediRawUrl !== "string") return false;
    try {
      const shihabU = new URL(mehediRawUrl, location.href);
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

  function shihabLooksLikeMediaSliceUrl(mehediUrl) {
    if (!mehediUrl) return false;
    if (shihabIsPoisonedYouTubeWebUrl(mehediUrl)) return false;
    const shihabLower = mehediUrl.toLowerCase();
    if (shihabLower.includes("ump=1") || shihabLower.includes("sabr=1")) return false;
    return (
      shihabLower.includes("/videoplayback") ||
      /\.(mp4|webm|m4a|m4v|mov|m3u8|mpd)(\?|$)/i.test(shihabLower) ||
      shihabLower.includes("efg=") ||
      shihabLower.includes("mime=video") ||
      shihabLower.includes("mime=audio")
    );
  }

  function mehediRememberFetchedSlice(shihabRawUrl, mehediContentType) {
    const shihabClean = mehediNormalizeHookMediaUrl(shihabRawUrl);
    if (!shihabClean || !shihabLooksLikeMediaSliceUrl(shihabClean)) return;
    const mehediCt = String(mehediContentType || "").toLowerCase();
    let shihabTrackType = "";
    if (mehediCt.startsWith("audio/") || shihabClean.includes("mime=audio")) {
      shihabTrackType = "audio";
    } else if (mehediCt.startsWith("video/") || shihabClean.includes("mime=video")) {
      shihabTrackType = "video";
    }
    shihabRecentFetchedSlices.unshift({
      shihabUrl: shihabClean,
      mehediTrackType: shihabTrackType,
      shihabAtMs: Date.now()
    });
    if (shihabRecentFetchedSlices.length > 60) {
      shihabRecentFetchedSlices.pop();
    }
  }

  function shihabDecodeXmlEntities(mehediStr) {
    return String(mehediStr || "")
      .replace(/&amp;/g, "&")
      .replace(/&lt;/g, "<")
      .replace(/&gt;/g, ">")
      .replace(/&quot;/g, '"')
      .replace(/&#39;/g, "'")
      .replace(/&#x2F;/gi, "/")
      .replace(/&#x3D;/gi, "=");
  }

  function mehediHeightToStandardLabel(shihabWidth, mehediHeight) {
    const shihabW = Number(shihabWidth || 0);
    const mehediH = Number(mehediHeight || 0);
    if (shihabW <= 0 && mehediH <= 0) return "";
    const shihabShortEdge = shihabW > 0 && mehediH > 0 ? Math.min(shihabW, mehediH) : Math.max(shihabW, mehediH);
    if (shihabShortEdge >= 2000) return "2160p";
    if (shihabShortEdge >= 1350) return "1440p";
    if (shihabShortEdge >= 1000) return "1080p";
    if (shihabShortEdge >= 680) return "720p";
    if (shihabShortEdge >= 510) return "540p";
    if (shihabShortEdge >= 440) return "480p";
    if (shihabShortEdge >= 320) return "360p";
    if (shihabShortEdge >= 210) return "240p";
    return "144p";
  }

  // Parse DASH MPD XML — efg asset ID inside the MPD ALWAYS takes priority over any external JSON hint!
  function shihabParseDashMpdXml(mehediXmlText, shihabDirectVideoIdHint, mehediTitleHint, shihabShortcodeHint, mehediBaseUrlHint, shihabExtraAltIds) {
    if (!mehediXmlText || typeof mehediXmlText !== "string" || !mehediXmlText.includes("<MPD")) {
      return "";
    }
    if (/<ContentProtection\b|cenc:pssh/i.test(mehediXmlText)) {
      return "";
    }

    try {
      const shihabParser = new DOMParser();
      const mehediDoc = shihabParser.parseFromString(mehediXmlText, "application/xml");
      const shihabMpdEl = mehediDoc.querySelector("MPD");
      if (!shihabMpdEl) return "";

      let mehediDurationSec = 0;
      const shihabDurAttr = shihabMpdEl.getAttribute("mediaPresentationDuration") || "";
      const mehediDurMatch = shihabDurAttr.match(/PT(?:(\d+)H)?(?:(\d+)M)?(?:([\d.]+)S)?/i);
      if (mehediDurMatch) {
        mehediDurationSec =
          (parseFloat(mehediDurMatch[1] || "0") * 3600) +
          (parseFloat(mehediDurMatch[2] || "0") * 60) +
          parseFloat(mehediDurMatch[3] || "0");
      }

      const shihabReps = Array.from(mehediDoc.querySelectorAll("Representation"));
      const mehediDiscoveredList = [];
      let mehediVideoEfgGroup = "";
      let shihabAudioEfgGroup = "";
      const mehediSharedAltSet = new Set(Array.isArray(shihabExtraAltIds) ? shihabExtraAltIds.filter(Boolean).map(String) : []);
      if (shihabDirectVideoIdHint) {
        mehediSharedAltSet.add(String(shihabDirectVideoIdHint));
        mehediSharedAltSet.add(`vid:${shihabDirectVideoIdHint}`);
      }
      if (shihabShortcodeHint) {
        mehediSharedAltSet.add(String(shihabShortcodeHint));
      }

      for (const mehediRep of shihabReps) {
        const shihabParentAdapt = mehediRep.parentElement;
        if (mehediRep.querySelector("ContentProtection") || (shihabParentAdapt && shihabParentAdapt.querySelector("ContentProtection"))) {
          continue;
        }
        const mehediBaseEl = mehediRep.querySelector("BaseURL");
        if (!mehediBaseEl || !mehediBaseEl.textContent) continue;

        const shihabRawBase = shihabDecodeXmlEntities(mehediBaseEl.textContent.trim());
        let mehediResolvedUrl = "";
        try {
          mehediResolvedUrl = new URL(shihabRawBase, mehediBaseUrlHint || location.href).toString();
        } catch (_) {
          continue;
        }
        const shihabCleanUrl = mehediNormalizeHookMediaUrl(mehediResolvedUrl);
        if (!shihabCleanUrl) continue;

        const mehediMime = (
          mehediRep.getAttribute("mimeType") ||
          (shihabParentAdapt && shihabParentAdapt.getAttribute("mimeType")) ||
          ""
        ).toLowerCase();
        const shihabCodecs = (
          mehediRep.getAttribute("codecs") ||
          (shihabParentAdapt && shihabParentAdapt.getAttribute("codecs")) ||
          ""
        ).toLowerCase();
        const mehediContentTypeAttr = (
          (shihabParentAdapt && shihabParentAdapt.getAttribute("contentType")) ||
          ""
        ).toLowerCase();

        const shihabWidth = parseInt(mehediRep.getAttribute("width") || "0", 10) || 0;
        const mehediHeight = parseInt(mehediRep.getAttribute("height") || "0", 10) || 0;
        const shihabBandwidth = parseInt(mehediRep.getAttribute("bandwidth") || "0", 10) || 0;
        const mehediQualityLabelAttr = mehediRep.getAttribute("FBQualityLabel") || mehediRep.getAttribute("quality_label") || "";

        const shihabIsAudio =
          mehediMime.startsWith("audio/") ||
          mehediContentTypeAttr === "audio" ||
          (shihabWidth === 0 && mehediHeight === 0 && (shihabCodecs.startsWith("mp4a") || shihabCodecs.includes("opus") || shihabCodecs.includes("aac")));

        let mehediEstBytes = -1;
        const shihabClenParam = (() => {
          try { return parseInt(new URL(shihabCleanUrl).searchParams.get("clen") || "-1", 10); } catch (_) { return -1; }
        })();
        if (shihabClenParam > 0) {
          mehediEstBytes = shihabClenParam;
        } else if (shihabBandwidth > 0 && mehediDurationSec > 0.5) {
          mehediEstBytes = Math.round((shihabBandwidth * mehediDurationSec) / 8);
        }

        const shihabEfgInfo = mehediExtractEfgIds(shihabCleanUrl);
        for (const mehediAlt of shihabEfgInfo.shihabAltIds) {
          mehediSharedAltSet.add(mehediAlt);
        }
        if (shihabEfgInfo.mehediCanonicalGroup) {
          if (!shihabIsAudio && !mehediVideoEfgGroup) {
            mehediVideoEfgGroup = shihabEfgInfo.mehediCanonicalGroup;
          } else if (shihabIsAudio && !shihabAudioEfgGroup) {
            shihabAudioEfgGroup = shihabEfgInfo.mehediCanonicalGroup;
          }
        }

        const mehediResLabel = shihabIsAudio
          ? ""
          : (mehediQualityLabelAttr.match(/\d{3,4}p/i)
              ? mehediQualityLabelAttr.match(/\d{3,4}p/i)[0].toLowerCase()
              : mehediHeightToStandardLabel(shihabWidth, mehediHeight));

        mehediDiscoveredList.push({
          shihabUrl: shihabCleanUrl,
          mehediMimeType: mehediMime || (shihabIsAudio ? "audio/mp4" : "video/mp4"),
          shihabCodecs: shihabCodecs,
          mehediTrackRole: shihabIsAudio ? "audio" : "video_only",
          shihabWidth: shihabWidth,
          mehediHeight: mehediHeight,
          shihabResolution: mehediResLabel,
          mehediBandwidth: shihabBandwidth,
          shihabSizeBytes: mehediEstBytes,
          mehediDurationSec: mehediDurationSec > 0 ? mehediDurationSec : 0,
          shihabGroupKey: "",
          mehediShortcode: shihabShortcodeHint || "",
          mehediAltIds: [],
          shihabTitleHint: mehediTitleHint || ""
        });
      }

      if (mehediDiscoveredList.length > 0) {
        const shihabFirstPathname = (() => {
          try { return new URL(mehediDiscoveredList[0].shihabUrl).pathname; } catch (_) { return mehediDiscoveredList[0].shihabUrl.slice(0, 80); }
        })();

        // Authoritative MPD group key: efg asset ID from video BaseURL ALWAYS wins over any JSON hint!
        const shihabFinalGroup =
          mehediVideoEfgGroup ||
          shihabAudioEfgGroup ||
          (shihabDirectVideoIdHint ? `vid:${shihabDirectVideoIdHint}` : "") ||
          (shihabShortcodeHint ? `sc:${shihabShortcodeHint}` : "") ||
          `mpd:${shihabFnv1aStringHash(shihabFirstPathname)}`;

        mehediSharedAltSet.add(shihabFinalGroup);
        const mehediFinalAltList = Array.from(mehediSharedAltSet);

        for (const shihabDiscItem of mehediDiscoveredList) {
          shihabDiscItem.shihabGroupKey = shihabFinalGroup;
          shihabDiscItem.mehediAltIds = mehediFinalAltList;
        }

        const shihabSig = `${shihabFinalGroup}:${mehediDiscoveredList.length}:${mehediTitleHint ? 1 : 0}`;
        if (!mehediEmittedManifestKeys.has(shihabSig)) {
          mehediEmittedManifestKeys.add(shihabSig);
          shihabPostToContentBridge("mehedi_manifest_streams", {
            shihabStreams: mehediDiscoveredList,
            mehediGroupKey: shihabFinalGroup,
            shihabShortcode: shihabShortcodeHint || "",
            mehediAltIds: mehediFinalAltList,
            mehediTitleHint: mehediTitleHint || ""
          });
        }
        return shihabFinalGroup;
      }
    } catch (_) {}
    return "";
  }

  // Extract YouTube plain unencrypted streamingData formats & notify desktop InnerTube resolver
  function mehediInspectYouTubePlayerResponse(shihabPlayerObj, mehediCustomUserAgent) {
    if (!shihabPlayerObj || typeof shihabPlayerObj !== "object") return "";
    const mehediStreaming = shihabPlayerObj.streamingData;
    const shihabDetails = shihabPlayerObj.videoDetails || {};

    const mehediVideoId = String(shihabDetails.videoId || "");
    const shihabVideoTitle = String(shihabDetails.title || "");
    const mehediAuthor = String(shihabDetails.author || "");
    const shihabDurationSec = Number(shihabDetails.lengthSeconds || 0);
    const shihabTitleHint = shihabVideoTitle || (mehediAuthor ? `${mehediAuthor} - YouTube Video` : "");
    const mehediYtGroupKey = mehediVideoId ? `yt:${mehediVideoId}` : "";

    if (mehediVideoId && /^[A-Za-z0-9_-]{11}$/.test(mehediVideoId) && !mehediCustomUserAgent) {
      const shihabDiscSig = `ytdisc:${mehediVideoId}:${shihabTitleHint ? 1 : 0}`;
      if (!mehediEmittedManifestKeys.has(shihabDiscSig)) {
        mehediEmittedManifestKeys.add(shihabDiscSig);
        let mehediVisData = "";
        try {
          mehediVisData = String(
            (shihabPlayerObj.responseContext && shihabPlayerObj.responseContext.visitorData) ||
            (window.ytcfg && typeof window.ytcfg.get === "function" && window.ytcfg.get("VISITOR_DATA")) ||
            ""
          );
          if (mehediVisData.includes("%")) {
            mehediVisData = decodeURIComponent(mehediVisData);
          }
        } catch (_) {}
        shihabPostToContentBridge("shihab_youtube_video_discovered", {
          shihabVideoId: mehediVideoId,
          mehediTitleHint: shihabTitleHint,
          shihabDurationSec: shihabDurationSec,
          mehediVisitorData: mehediVisData
        });
      }
    }

    if (!mehediStreaming || typeof mehediStreaming !== "object") return mehediYtGroupKey;

    const shihabAllRawFormats = [
      ...(Array.isArray(mehediStreaming.formats) ? mehediStreaming.formats : []),
      ...(Array.isArray(mehediStreaming.adaptiveFormats) ? mehediStreaming.adaptiveFormats : [])
    ];
    const mehediHasDefaultAudioFlag = shihabAllRawFormats.some(
      (shihabF) => shihabF && shihabF.audioTrack && shihabF.audioTrack.audioIsDefault === true
    );

    const mehediOut = [];
    const shihabFormatBuckets = [
      { mehediList: mehediStreaming.formats, shihabDefaultRole: "progressive" },
      { mehediList: mehediStreaming.adaptiveFormats, shihabDefaultRole: "adaptive" }
    ];

    for (const shihabBucket of shihabFormatBuckets) {
      if (!Array.isArray(shihabBucket.mehediList)) continue;
      for (const mehediFmt of shihabBucket.mehediList) {
        if (!mehediFmt || typeof mehediFmt.url !== "string" || !mehediFmt.url.startsWith("http")) {
          continue;
        }
        if (mehediFmt.drmFamilies || mehediFmt.type === "FORMAT_STREAM_TYPE_OTF") {
          continue;
        }
        if (mehediHasDefaultAudioFlag && mehediFmt.audioTrack && mehediFmt.audioTrack.audioIsDefault !== true) {
          continue;
        }
        const shihabCleanUrl = mehediNormalizeHookMediaUrl(mehediFmt.url);
        if (!shihabCleanUrl || shihabIsPoisonedYouTubeWebUrl(shihabCleanUrl)) continue;

        const mehediMimeRaw = String(mehediFmt.mimeType || "").toLowerCase();
        const shihabMimeBase = mehediMimeRaw.split(";")[0].trim();
        if (shihabMimeBase.includes("3gpp")) continue;
        const mehediIsAudio = shihabMimeBase.startsWith("audio/");
        const shihabRole = shihabBucket.shihabDefaultRole === "progressive"
          ? "progressive"
          : (mehediIsAudio ? "audio" : "video_only");

        const mehediW = Number(mehediFmt.width || 0);
        const shihabH = Number(mehediFmt.height || 0);
        let mehediRes = "";
        if (!mehediIsAudio) {
          const shihabQMatch = String(mehediFmt.qualityLabel || "").match(/(\d{3,4}p)/i);
          mehediRes = shihabQMatch ? shihabQMatch[1].toLowerCase() : mehediHeightToStandardLabel(mehediW, shihabH);
        }

        const mehediFmtDurSec = mehediFmt.approxDurationMs ? (Number(mehediFmt.approxDurationMs) / 1000) : shihabDurationSec;
        let shihabBytes = parseInt(String(mehediFmt.contentLength || "-1"), 10);
        if (!(shihabBytes > 0) && mehediFmt.bitrate && mehediFmtDurSec > 0) {
          shihabBytes = Math.round((Number(mehediFmt.bitrate) * mehediFmtDurSec) / 8);
        }
        const mehediIsOrigAud = Boolean(
          (mehediFmt.audioTrack && mehediFmt.audioTrack.audioIsDefault === true) ||
          (mehediFmt.audioTrack && /original/i.test(String(mehediFmt.audioTrack.displayName || ""))) ||
          (mehediIsAudio && !mehediHasDefaultAudioFlag)
        );
        const shihabIsHdr = Boolean(
          /HDR/i.test(String(mehediFmt.qualityLabel || "")) ||
          (mehediFmt.colorInfo && /SMPTEST2084|ARIB_STD_B67/i.test(String(mehediFmt.colorInfo.transferCharacteristics || "")))
        );

        mehediOut.push({
          shihabUrl: shihabCleanUrl,
          mehediMimeType: shihabMimeBase || (mehediIsAudio ? "audio/webm" : "video/mp4"),
          shihabCodecs: mehediMimeRaw,
          mehediTrackRole: shihabRole,
          shihabWidth: mehediW,
          mehediHeight: shihabH,
          shihabResolution: mehediRes,
          mehediBandwidth: Number(mehediFmt.bitrate || 0),
          shihabSizeBytes: shihabBytes > 0 ? shihabBytes : -1,
          mehediDurationSec: mehediFmtDurSec > 0 ? mehediFmtDurSec : 0,
          shihabGroupKey: mehediYtGroupKey,
          mehediShortcode: mehediVideoId,
          mehediAltIds: mehediVideoId ? [mehediVideoId, mehediYtGroupKey] : [],
          shihabTitleHint: shihabTitleHint,
          mehediCustomUserAgent: mehediCustomUserAgent || "",
          shihabIsOriginalAudio: mehediIsOrigAud,
          mehediIsHdr: shihabIsHdr
        });
      }
    }

    if (mehediOut.length > 0) {
      shihabPostToContentBridge("mehedi_manifest_streams", {
        shihabStreams: mehediOut,
        mehediGroupKey: mehediYtGroupKey,
        shihabShortcode: mehediVideoId,
        mehediAltIds: mehediVideoId ? [mehediVideoId, mehediYtGroupKey] : [],
        mehediTitleHint: shihabTitleHint
      });
    }
    return mehediYtGroupKey;
  }

  async function shihabResolveYouTubeInPageMainWorld(mehediVideoId, shihabVisitorData) {
    if (!mehediTopOrigFetch || !mehediVideoId || !/^[A-Za-z0-9_-]{11}$/.test(mehediVideoId)) return;
    if (!/youtube\.com$/i.test(location.hostname)) return;

    let mehediCleanVis = String(
      shihabVisitorData ||
      (window.ytcfg && typeof window.ytcfg.get === "function" && window.ytcfg.get("VISITOR_DATA")) ||
      ""
    );
    try {
      if (mehediCleanVis.includes("%")) {
        mehediCleanVis = decodeURIComponent(mehediCleanVis);
      }
    } catch (_) {}

    const mehediClients = [
      {
        shihabHeaderId: "101",
        mehediClientVer: "1.02",
        shihabUa: "Mozilla/5.0 (Macintosh; Intel Mac OS X 15_7_3) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/26.0 Safari/605.1.15",
        mehediClientCtx: {
          clientName: "VISIONOS",
          clientVersion: "1.02",
          deviceMake: "Apple",
          deviceModel: "RealityDevice17,1",
          osName: "visionOS",
          osVersion: "26.5.23O471",
          hl: "en",
          utcOffsetMinutes: 0
        }
      },
      {
        shihabHeaderId: "3",
        mehediClientVer: "20.10.38",
        shihabUa: "com.google.android.youtube/20.10.38 (Linux; U; Android 14) gzip",
        mehediClientCtx: {
          clientName: "ANDROID",
          clientVersion: "20.10.38",
          androidSdkVersion: 34,
          osName: "Android",
          osVersion: "14",
          hl: "en",
          utcOffsetMinutes: 0
        }
      },
      {
        shihabHeaderId: "5",
        mehediClientVer: "20.10.4",
        shihabUa: "com.google.ios.youtube/20.10.4 (iPhone16,2; U; CPU iOS 18_3_2 like Mac OS X;)",
        mehediClientCtx: {
          clientName: "IOS",
          clientVersion: "20.10.4",
          deviceMake: "Apple",
          deviceModel: "iPhone16,2",
          osName: "iPhone",
          osVersion: "18.3.2.22D82",
          hl: "en",
          utcOffsetMinutes: 0
        }
      }
    ];

    for (const shihabCfg of mehediClients) {
      try {
        const mehediClientPayload = Object.assign({}, shihabCfg.mehediClientCtx);
        if (mehediCleanVis) {
          mehediClientPayload.visitorData = mehediCleanVis;
        }
        const shihabHeadersObj = {
          "Content-Type": "application/json",
          "X-YouTube-Client-Name": shihabCfg.shihabHeaderId,
          "X-YouTube-Client-Version": shihabCfg.mehediClientVer,
          "X-Goog-Api-Format-Version": "2"
        };
        if (mehediCleanVis) {
          shihabHeadersObj["X-Goog-Visitor-Id"] = mehediCleanVis;
        }
        const shihabResp = await mehediTopOrigFetch("/youtubei/v1/player?prettyPrint=false", {
          method: "POST",
          credentials: "omit",
          headers: shihabHeadersObj,
          body: JSON.stringify({
            videoId: mehediVideoId,
            racyCheckOk: true,
            contentCheckOk: true,
            context: { client: mehediClientPayload },
            playbackContext: { contentPlaybackContext: { html5Preference: "HTML5_PREF_WANTS" } }
          })
        });
        if (!shihabResp || !shihabResp.ok) continue;
        const mehediData = await shihabResp.json();
        if (
          mehediData &&
          mehediData.playabilityStatus &&
          mehediData.playabilityStatus.status === "OK" &&
          mehediData.streamingData
        ) {
          mehediInspectYouTubePlayerResponse(mehediData, shihabCfg.shihabUa);
          return;
        }
      } catch (_) {}
    }
  }

  function mehediStripHookTranslationSuffix(shihabRawText) {
    if (!shihabRawText) return "";
    return String(shihabRawText)
      .replace(/\s*(?:Hide translation|See translation|See original|Translated from [A-Za-z ]+)\s*$/gi, "")
      .replace(/\s+/g, " ")
      .trim();
  }

  function shihabIsGenuineVideoNode(mehediObj) {
    if (!mehediObj || typeof mehediObj !== "object") return false;
    const shihabTypeName = String(mehediObj.__typename || "");
    if (/^(?:Video|XDTGraphVideo|GraphVideo|Media|Clip)$/i.test(shihabTypeName)) return true;
    if (mehediObj.is_video === true || mehediObj.media_type === 2) return true;
    if (
      mehediObj.video_id ||
      mehediObj.video_dash_manifest ||
      mehediObj.dash_manifest ||
      mehediObj.dash_manifests ||
      mehediObj.video_versions ||
      mehediObj.progressive_urls ||
      mehediObj.browser_native_hd_url ||
      mehediObj.playable_url
    ) {
      return true;
    }
    return false;
  }

  // Walk JSON payloads without ever leaking Viewer/Page/User IDs or sibling Story captions across different videos!
  function shihabWalkJsonForMediaMetadata(mehediRootJson) {
    if (!mehediRootJson || typeof mehediRootJson !== "object") return;

    if (mehediRootJson.streamingData && mehediRootJson.videoDetails) {
      mehediInspectYouTubePlayerResponse(mehediRootJson);
    }

    const shihabStack = [{
      shihabNode: mehediRootJson,
      mehediParentCtx: {
        shihabStoryVideoId: "",
        mehediParentCode: "",
        shihabParentUser: "",
        mehediParentCaption: ""
      }
    }];
    let mehediVisited = 0;

    while (shihabStack.length > 0 && mehediVisited < 5000) {
      const mehediFrame = shihabStack.pop();
      mehediVisited++;
      const shihabNode = mehediFrame && mehediFrame.shihabNode;
      let shihabInherited = mehediFrame ? mehediFrame.mehediParentCtx : {};
      if (!shihabNode || typeof shihabNode !== "object") continue;

      if (Array.isArray(shihabNode)) {
        for (let mehediI = shihabNode.length - 1; mehediI >= 0; mehediI--) {
          if (shihabNode[mehediI] && typeof shihabNode[mehediI] === "object") {
            shihabStack.push({
              shihabNode: shihabNode[mehediI],
              mehediParentCtx: shihabInherited
            });
          }
        }
        continue;
      }

      // Reset inherited story context at each distinct FeedUnit / Story / Post boundary so sibling posts never bleed!
      const mehediTypeName = String(shihabNode.__typename || "");
      if (/FeedUnit|Story|Timeline|Post|Tweet/i.test(mehediTypeName) && mehediVisited > 1) {
        shihabInherited = {
          shihabStoryVideoId: "",
          mehediParentCode: "",
          shihabParentUser: "",
          mehediParentCaption: ""
        };
      }

      const mehediLocalCode = String(shihabNode.code || shihabNode.shortcode || "");
      const shihabDirectVideoId = shihabNode.video_id
        ? String(shihabNode.video_id)
        : (shihabIsGenuineVideoNode(shihabNode) && (shihabNode.pk || shihabNode.id)
            ? String(shihabNode.pk || shihabNode.id)
            : "");

      const mehediShortcode = mehediLocalCode || shihabInherited.mehediParentCode || "";
      const shihabVideoAssetId = shihabDirectVideoId || shihabInherited.shihabStoryVideoId || "";

      let mehediUsername = "";
      if (shihabNode.user && typeof shihabNode.user.username === "string") {
        mehediUsername = shihabNode.user.username;
      } else if (shihabNode.owner && typeof shihabNode.owner.username === "string") {
        mehediUsername = shihabNode.owner.username;
      } else if (shihabNode.owner && typeof shihabNode.owner.name === "string") {
        mehediUsername = shihabNode.owner.name;
      } else if (Array.isArray(shihabNode.actors) && shihabNode.actors[0] && typeof shihabNode.actors[0].name === "string") {
        mehediUsername = shihabNode.actors[0].name;
      } else {
        mehediUsername = shihabInherited.shihabParentUser || "";
      }

      let shihabCaptionTxt = "";
      if (shihabNode.caption && typeof shihabNode.caption.text === "string") {
        shihabCaptionTxt = shihabNode.caption.text;
      } else if (shihabNode.edge_media_to_caption && Array.isArray(shihabNode.edge_media_to_caption.edges) && shihabNode.edge_media_to_caption.edges[0]) {
        const mehediCapNode = shihabNode.edge_media_to_caption.edges[0].node;
        if (mehediCapNode && typeof mehediCapNode.text === "string") shihabCaptionTxt = mehediCapNode.text;
      } else if (shihabNode.message && typeof shihabNode.message.text === "string") {
        shihabCaptionTxt = shihabNode.message.text;
      } else if (shihabNode.savable_description && typeof shihabNode.savable_description.text === "string") {
        shihabCaptionTxt = shihabNode.savable_description.text;
      } else if (shihabNode.creation_story && shihabNode.creation_story.message && typeof shihabNode.creation_story.message.text === "string") {
        shihabCaptionTxt = shihabNode.creation_story.message.text;
      } else if (typeof shihabNode.full_text === "string") {
        shihabCaptionTxt = shihabNode.full_text;
      } else if (typeof shihabNode.title === "string" && shihabNode.title.length > 2 && shihabIsGenuineVideoNode(shihabNode)) {
        shihabCaptionTxt = shihabNode.title;
      } else {
        shihabCaptionTxt = shihabInherited.mehediParentCaption || "";
      }
      shihabCaptionTxt = mehediStripHookTranslationSuffix(shihabCaptionTxt);

      // Prefer pure caption text when available (matches user's exact video title expectation); fall back to author handle if no caption
      const mehediCombinedTitle = shihabCaptionTxt
        ? shihabCaptionTxt.slice(0, 105)
        : (mehediUsername ? `${mehediUsername} - Video` : "");

      const mehediNodeDurationSec = Number(
        shihabNode.video_duration ||
        (shihabNode.playable_duration_in_ms ? (Number(shihabNode.playable_duration_in_ms) / 1000) : 0)
      ) || (Number(shihabNode.length_in_second || shihabNode.duration || 0));

      const shihabNodeAltIds = Array.from(new Set(
        [
          shihabDirectVideoId,
          shihabDirectVideoId ? `vid:${shihabDirectVideoId}` : "",
          shihabVideoAssetId,
          shihabVideoAssetId ? `vid:${shihabVideoAssetId}` : "",
          mehediLocalCode,
          mehediShortcode
        ]
          .filter((mehediV) => mehediV && String(mehediV).length >= 4)
          .map(String)
      ));

      // 1) Instagram / Facebook DASH XML manifest string + Facebook Comet dash_manifests array
      const shihabDashXmlList = [];
      if (typeof shihabNode.video_dash_manifest === "string" && shihabNode.video_dash_manifest.includes("<MPD")) {
        shihabDashXmlList.push(shihabNode.video_dash_manifest);
      }
      if (typeof shihabNode.dash_manifest === "string" && shihabNode.dash_manifest.includes("<MPD")) {
        shihabDashXmlList.push(shihabNode.dash_manifest);
      }
      if (shihabNode.dash_info && typeof shihabNode.dash_info.video_dash_manifest === "string" && shihabNode.dash_info.video_dash_manifest.includes("<MPD")) {
        shihabDashXmlList.push(shihabNode.dash_info.video_dash_manifest);
      }
      if (Array.isArray(shihabNode.dash_manifests)) {
        for (const mehediDmEntry of shihabNode.dash_manifests) {
          if (mehediDmEntry && typeof mehediDmEntry.manifest_xml === "string" && mehediDmEntry.manifest_xml.includes("<MPD")) {
            shihabDashXmlList.push(mehediDmEntry.manifest_xml);
          }
        }
      }
      let mehediMpdResolvedGroup = "";
      for (const mehediXmlItem of shihabDashXmlList) {
        const shihabGrp = shihabParseDashMpdXml(mehediXmlItem, shihabVideoAssetId, mehediCombinedTitle, mehediShortcode, location.href, shihabNodeAltIds);
        if (shihabGrp && !mehediMpdResolvedGroup) mehediMpdResolvedGroup = shihabGrp;
      }

      // 2) Instagram progressive video_versions array
      if (Array.isArray(shihabNode.video_versions) && shihabNode.video_versions.length > 0) {
        const mehediProgList = [];
        let shihabIgEfgGroup = "";
        const mehediSharedIgAlts = new Set(shihabNodeAltIds);
        for (const mehediVer of shihabNode.video_versions) {
          if (!mehediVer || typeof mehediVer.url !== "string") continue;
          const shihabClean = mehediNormalizeHookMediaUrl(mehediVer.url);
          if (!shihabClean) continue;
          const mehediEfgIds = mehediExtractEfgIds(shihabClean);
          for (const shihabAltId of mehediEfgIds.shihabAltIds) {
            mehediSharedIgAlts.add(shihabAltId);
          }
          if (!shihabIgEfgGroup && mehediEfgIds.mehediCanonicalGroup) {
            shihabIgEfgGroup = mehediEfgIds.mehediCanonicalGroup;
          }
          const shihabW = Number(mehediVer.width || 0);
          const mehediH = Number(mehediVer.height || 0);
          mehediProgList.push({
            shihabUrl: shihabClean,
            mehediMimeType: "video/mp4",
            shihabCodecs: "avc1,mp4a",
            mehediTrackRole: "progressive",
            shihabWidth: shihabW,
            mehediHeight: mehediH,
            shihabResolution: mehediHeightToStandardLabel(shihabW, mehediH),
            mehediBandwidth: 0,
            shihabSizeBytes: -1,
            mehediDurationSec: mehediNodeDurationSec > 0 ? mehediNodeDurationSec : 0,
            shihabGroupKey: "",
            mehediShortcode: mehediShortcode,
            mehediAltIds: [],
            shihabTitleHint: mehediCombinedTitle
          });
        }
        if (mehediProgList.length > 0) {
          const shihabFinalIgGroup =
            shihabIgEfgGroup ||
            mehediMpdResolvedGroup ||
            (shihabVideoAssetId ? `vid:${shihabVideoAssetId}` : "") ||
            (mehediShortcode ? `sc:${mehediShortcode}` : "") ||
            `ig:${shihabFnv1aStringHash(mehediProgList[0].shihabUrl.split("?")[0])}`;
          mehediSharedIgAlts.add(shihabFinalIgGroup);
          const mehediIgAltArray = Array.from(mehediSharedIgAlts);
          for (const shihabProgEntry of mehediProgList) {
            shihabProgEntry.shihabGroupKey = shihabFinalIgGroup;
            shihabProgEntry.mehediAltIds = mehediIgAltArray;
          }
          shihabPostToContentBridge("mehedi_manifest_streams", {
            shihabStreams: mehediProgList,
            mehediGroupKey: shihabFinalIgGroup,
            shihabShortcode: mehediShortcode,
            mehediAltIds: mehediIgAltArray,
            mehediTitleHint: mehediCombinedTitle
          });
        }
      }

      // 3) Facebook progressive browser_native_hd_url / playable_url + Facebook Comet progressive_urls array
      const mehediFbProgressiveFields = [
        { shihabField: "browser_native_hd_url", mehediRes: "720p" },
        { shihabField: "playable_url_quality_hd", mehediRes: "720p" },
        { shihabField: "hd_src_no_ratelimit", mehediRes: "720p" },
        { shihabField: "hd_src", mehediRes: "720p" },
        { shihabField: "browser_native_sd_url", mehediRes: "480p" },
        { shihabField: "playable_url", mehediRes: "480p" },
        { shihabField: "sd_src_no_ratelimit", mehediRes: "480p" },
        { shihabField: "sd_src", mehediRes: "480p" }
      ];
      const shihabFbFound = [];
      let mehediFbEfgGroup = "";
      const shihabFbAltSet = new Set(shihabNodeAltIds);
      const mehediFbOrigW = Number(shihabNode.original_width || shihabNode.width || 0);
      const shihabFbOrigH = Number(shihabNode.original_height || shihabNode.height || 0);

      for (const mehediFbSpec of mehediFbProgressiveFields) {
        const shihabVal = shihabNode[mehediFbSpec.shihabField];
        if (typeof shihabVal === "string" && shihabVal.startsWith("http")) {
          const mehediCleanFb = mehediNormalizeHookMediaUrl(shihabVal);
          if (mehediCleanFb) {
            const shihabFbEfg = mehediExtractEfgIds(mehediCleanFb);
            for (const mehediAlt of shihabFbEfg.shihabAltIds) shihabFbAltSet.add(mehediAlt);
            if (!mehediFbEfgGroup && shihabFbEfg.mehediCanonicalGroup) {
              mehediFbEfgGroup = shihabFbEfg.mehediCanonicalGroup;
            }
            shihabFbFound.push({
              shihabUrl: mehediCleanFb,
              mehediMimeType: "video/mp4",
              shihabCodecs: "avc1,mp4a",
              mehediTrackRole: "progressive",
              shihabWidth: mehediFbOrigW,
              mehediHeight: shihabFbOrigH,
              shihabResolution: mehediFbSpec.mehediRes,
              mehediBandwidth: 0,
              shihabSizeBytes: -1,
              mehediDurationSec: mehediNodeDurationSec > 0 ? mehediNodeDurationSec : 0,
              shihabGroupKey: "",
              mehediShortcode: mehediShortcode,
              mehediAltIds: [],
              shihabTitleHint: mehediCombinedTitle
            });
          }
        }
      }

      if (Array.isArray(shihabNode.progressive_urls)) {
        for (const mehediProgObj of shihabNode.progressive_urls) {
          const shihabProgRaw = mehediProgObj && typeof mehediProgObj.progressive_url === "string" ? mehediProgObj.progressive_url : "";
          if (!shihabProgRaw.startsWith("http")) continue;
          const mehediCleanProg = mehediNormalizeHookMediaUrl(shihabProgRaw);
          if (!mehediCleanProg) continue;
          const shihabProgEfg = mehediExtractEfgIds(mehediCleanProg);
          for (const mehediAlt of shihabProgEfg.shihabAltIds) shihabFbAltSet.add(mehediAlt);
          if (!mehediFbEfgGroup && shihabProgEfg.mehediCanonicalGroup) {
            mehediFbEfgGroup = shihabProgEfg.mehediCanonicalGroup;
          }
          const mehediQualStr = String((mehediProgObj.metadata && mehediProgObj.metadata.quality) || "").toUpperCase();
          shihabFbFound.push({
            shihabUrl: mehediCleanProg,
            mehediMimeType: "video/mp4",
            shihabCodecs: "avc1,mp4a",
            mehediTrackRole: "progressive",
            shihabWidth: mehediFbOrigW,
            mehediHeight: shihabFbOrigH,
            shihabResolution: mehediQualStr === "HD" ? "720p" : "480p",
            mehediBandwidth: 0,
            shihabSizeBytes: -1,
            mehediDurationSec: mehediNodeDurationSec > 0 ? mehediNodeDurationSec : 0,
            shihabGroupKey: "",
            mehediShortcode: mehediShortcode,
            mehediAltIds: [],
            shihabTitleHint: mehediCombinedTitle
          });
        }
      }

      if (shihabFbFound.length > 0) {
        const shihabFinalFbGroup =
          mehediFbEfgGroup ||
          mehediMpdResolvedGroup ||
          (shihabVideoAssetId ? `vid:${shihabVideoAssetId}` : "") ||
          (mehediShortcode ? `sc:${mehediShortcode}` : "") ||
          `fb:${shihabFnv1aStringHash(shihabFbFound[0].shihabUrl.split("?")[0])}`;
        shihabFbAltSet.add(shihabFinalFbGroup);
        const mehediFbAltArray = Array.from(shihabFbAltSet);
        for (const shihabFbItem of shihabFbFound) {
          shihabFbItem.shihabGroupKey = shihabFinalFbGroup;
          shihabFbItem.mehediAltIds = mehediFbAltArray;
        }
        shihabPostToContentBridge("mehedi_manifest_streams", {
          shihabStreams: shihabFbFound,
          mehediGroupKey: shihabFinalFbGroup,
          shihabShortcode: mehediShortcode || shihabVideoAssetId,
          mehediAltIds: mehediFbAltArray,
          mehediTitleHint: mehediCombinedTitle
        });
      }

      // 4) X / Twitter video_info.variants
      if (shihabNode.video_info && Array.isArray(shihabNode.video_info.variants)) {
        const mehediTwList = [];
        const shihabTwDur = shihabNode.video_info.duration_millis ? (Number(shihabNode.video_info.duration_millis) / 1000) : 0;
        const mehediTwGroup = shihabVideoAssetId
          ? `tw:${shihabVideoAssetId}`
          : `tw:${shihabFnv1aStringHash((shihabNode.video_info.variants[0] && shihabNode.video_info.variants[0].url) || "")}`;
        for (const shihabVar of shihabNode.video_info.variants) {
          if (!shihabVar || typeof shihabVar.url !== "string" || !shihabVar.url.startsWith("http")) continue;
          const mehediCt = String(shihabVar.content_type || "").toLowerCase();
          if (mehediCt.includes("mpegurl")) continue;
          const shihabDimMatch = shihabVar.url.match(/\/(\d{2,4})x(\d{2,4})\//);
          const mehediW = shihabDimMatch ? parseInt(shihabDimMatch[1], 10) : 0;
          const shihabH = shihabDimMatch ? parseInt(shihabDimMatch[2], 10) : 0;
          mehediTwList.push({
            shihabUrl: shihabVar.url,
            mehediMimeType: mehediCt || "video/mp4",
            shihabCodecs: "avc1,mp4a",
            mehediTrackRole: "progressive",
            shihabWidth: mehediW,
            mehediHeight: shihabH,
            shihabResolution: mehediHeightToStandardLabel(mehediW, shihabH),
            mehediBandwidth: Number(shihabVar.bitrate || 0),
            shihabSizeBytes: -1,
            mehediDurationSec: shihabTwDur > 0 ? shihabTwDur : 0,
            shihabGroupKey: mehediTwGroup,
            mehediShortcode: shihabVideoAssetId,
            mehediAltIds: [...shihabNodeAltIds, mehediTwGroup],
            shihabTitleHint: mehediCombinedTitle
          });
        }
        if (mehediTwList.length > 0) {
          shihabPostToContentBridge("mehedi_manifest_streams", {
            shihabStreams: mehediTwList,
            mehediGroupKey: mehediTwGroup,
            shihabShortcode: shihabVideoAssetId,
            mehediAltIds: [...shihabNodeAltIds, mehediTwGroup],
            mehediTitleHint: mehediCombinedTitle
          });
        }
      }

      const mehediNextParentCtx = {
        shihabStoryVideoId: shihabVideoAssetId,
        mehediParentCode: mehediShortcode,
        shihabParentUser: mehediUsername,
        mehediParentCaption: shihabCaptionTxt
      };

      const shihabKeys = Object.keys(shihabNode);
      for (let mehediK = 0; mehediK < shihabKeys.length; mehediK++) {
        const shihabKeyName = shihabKeys[mehediK];
        if (shihabKeyName === "extensions" || shihabKeyName === "viewer") continue;
        const shihabChild = shihabNode[shihabKeyName];
        if (shihabChild && typeof shihabChild === "object") {
          shihabStack.push({
            shihabNode: shihabChild,
            mehediParentCtx: mehediNextParentCtx
          });
        }
      }
    }
  }

  function mehediInspectTextBodyIfManifest(shihabText, mehediSourceUrl, shihabContentType) {
    if (!shihabText || shihabText.length < 20 || shihabText.length > 6 * 1024 * 1024) return;
    const mehediCt = String(shihabContentType || "").toLowerCase();
    const shihabTrim = shihabText.trimStart();

    if (shihabTrim.startsWith("<") && shihabText.includes("<MPD")) {
      shihabParseDashMpdXml(shihabText, "", "", "", mehediSourceUrl || location.href, []);
      return;
    }

    const mehediHasMediaKeywords =
      mehediCt.includes("json") ||
      shihabTrim.includes("video_dash_manifest") ||
      shihabTrim.includes("dash_manifests") ||
      shihabTrim.includes("manifest_xml") ||
      shihabTrim.includes("progressive_urls") ||
      shihabTrim.includes("video_versions") ||
      shihabTrim.includes("streamingData") ||
      shihabTrim.includes("browser_native_hd_url") ||
      shihabTrim.includes("playable_url") ||
      shihabTrim.includes("video_info");

    if (!mehediHasMediaKeywords) return;

    const mehediCleanedJson = shihabTrim.startsWith("for (;;);") ? shihabTrim.slice(9).trimStart() : shihabTrim;
    if (mehediCleanedJson.startsWith("{") || mehediCleanedJson.startsWith("[")) {
      try {
        const shihabParsed = JSON.parse(mehediCleanedJson);
        shihabWalkJsonForMediaMetadata(shihabParsed);
        return;
      } catch (_) {
        // Facebook /api/graphql/ frequently streams newline-delimited JSON objects (NDJSON)
        if (mehediCleanedJson.includes("\n")) {
          const shihabLines = mehediCleanedJson.split(/\r?\n/);
          for (let mehediIdx = 0; mehediIdx < shihabLines.length && mehediIdx < 80; mehediIdx++) {
            let shihabLine = shihabLines[mehediIdx].trim();
            if (shihabLine.startsWith("for (;;);")) shihabLine = shihabLine.slice(9).trim();
            if (shihabLine.length > 25 && (shihabLine.startsWith("{") || shihabLine.startsWith("["))) {
              try {
                shihabWalkJsonForMediaMetadata(JSON.parse(shihabLine));
              } catch (_) {}
            }
          }
        }
      }
    }
  }

  // Direct <video> DOM Element Inspector (walks React Fiber on Facebook/Instagram/X/TikTok & #movie_player on YouTube)
  // Binds the exact DOM <video> node (data-shihab-vid-uid) 1-to-1 to its exact video asset ID, manifest, and title!
  function mehediInspectLiveDomVideoElement(shihabVideoEl) {
    if (!shihabVideoEl || shihabVideoEl.tagName !== "VIDEO") return;
    const mehediVidUid = shihabEnsureVideoUidOnElement(shihabVideoEl);
    if (!mehediVidUid) return;

    const shihabGroupKeys = new Set();
    const mehediAltIds = new Set();
    const shihabBoundUrls = new Set();
    let mehediTitleHint = "";

    try {
      // A) YouTube player instance directly enclosing shihabVideoEl
      const shihabYtPlayer = shihabVideoEl.closest("#movie_player, .html5-video-player");
      if (shihabYtPlayer && typeof shihabYtPlayer.getPlayerResponse === "function") {
        const mehediYtResp = shihabYtPlayer.getPlayerResponse();
        if (mehediYtResp && mehediYtResp.videoDetails && mehediYtResp.videoDetails.videoId) {
          const shihabYtGrp = mehediInspectYouTubePlayerResponse(mehediYtResp) || `yt:${mehediYtResp.videoDetails.videoId}`;
          shihabGroupKeys.add(shihabYtGrp);
          mehediAltIds.add(String(mehediYtResp.videoDetails.videoId));
          mehediTitleHint = String(mehediYtResp.videoDetails.title || "");
        }
      }

      // B) React Fiber (__reactFiber$ / __reactInternalInstance$) directly attached to shihabVideoEl (Facebook, Instagram, X, TikTok, Reddit)
      let mehediStartNode = shihabVideoEl;
      let shihabFiber = null;
      for (let mehediUp = 0; mehediStartNode && mehediUp < 4 && !shihabFiber; mehediUp++) {
        const shihabOwnKeys = Object.keys(mehediStartNode);
        for (let mehediK = 0; mehediK < shihabOwnKeys.length; mehediK++) {
          const shihabPropName = shihabOwnKeys[mehediK];
          if (shihabPropName.startsWith("__reactFiber$") || shihabPropName.startsWith("__reactInternalInstance$")) {
            shihabFiber = mehediStartNode[shihabPropName];
            break;
          }
        }
        mehediStartNode = mehediStartNode.parentElement;
      }

      let shihabFiberDepth = 0;
      while (shihabFiber && shihabFiberDepth < 30) {
        shihabFiberDepth++;
        const mehediProps = shihabFiber.memoizedProps || shihabFiber.pendingProps;
        if (mehediProps && typeof mehediProps === "object") {
          const shihabCandidateBags = [
            mehediProps,
            mehediProps.video,
            mehediProps.media,
            mehediProps.videoData,
            mehediProps.playerData,
            mehediProps.clip,
            mehediProps.item,
            mehediProps.videoDeliveryResponseResult
          ];

          for (const mehediBag of shihabCandidateBags) {
            if (!mehediBag || typeof mehediBag !== "object") continue;

            // Direct IDs on this exact video player's React props
            for (const shihabIdField of ["xpv_asset_id", "xpvAssetId"]) {
              if (mehediBag[shihabIdField] && /^\d{5,}$/.test(String(mehediBag[shihabIdField]))) {
                const mehediXpvStr = String(mehediBag[shihabIdField]);
                shihabGroupKeys.add(`asset:${mehediXpvStr}`);
                mehediAltIds.add(mehediXpvStr);
                mehediAltIds.add(`asset:${mehediXpvStr}`);
              }
            }
            for (const mehediVidField of ["video_id", "videoId", "playable_id", "mediaId"]) {
              if (mehediBag[mehediVidField] && /^\d{5,}$/.test(String(mehediBag[mehediVidField]))) {
                const shihabVidStr = String(mehediBag[mehediVidField]);
                mehediAltIds.add(shihabVidStr);
                mehediAltIds.add(`vid:${shihabVidStr}`);
                if (shihabGroupKeys.size === 0) shihabGroupKeys.add(`vid:${shihabVidStr}`);
              }
            }
            for (const shihabCodeField of ["shortcode", "code"]) {
              if (typeof mehediBag[shihabCodeField] === "string" && mehediBag[shihabCodeField].length >= 5) {
                mehediAltIds.add(mehediBag[shihabCodeField]);
              }
            }

            if (!mehediTitleHint) {
              if (typeof mehediBag.videoTitle === "string" && mehediBag.videoTitle.trim()) {
                mehediTitleHint = mehediStripHookTranslationSuffix(mehediBag.videoTitle);
              } else if (mehediBag.caption && typeof mehediBag.caption.text === "string") {
                mehediTitleHint = mehediStripHookTranslationSuffix(mehediBag.caption.text);
              } else if (mehediBag.message && typeof mehediBag.message.text === "string") {
                mehediTitleHint = mehediStripHookTranslationSuffix(mehediBag.message.text);
              }
            }

            // Direct DASH XML manifests on this exact video player's React props
            const shihabXmlCandidates = [];
            for (const mehediXmlField of ["dash_manifest", "dashManifest", "dashManifestXml", "video_dash_manifest", "videoDashManifest", "manifest_xml"]) {
              if (typeof mehediBag[mehediXmlField] === "string" && mehediBag[mehediXmlField].includes("<MPD")) {
                shihabXmlCandidates.push(mehediBag[mehediXmlField]);
              }
            }
            if (Array.isArray(mehediBag.dash_manifests)) {
              for (const shihabDm of mehediBag.dash_manifests) {
                if (shihabDm && typeof shihabDm.manifest_xml === "string" && shihabDm.manifest_xml.includes("<MPD")) {
                  shihabXmlCandidates.push(shihabDm.manifest_xml);
                }
              }
            }
            for (const mehediXmlStr of shihabXmlCandidates) {
              const shihabParsedGrp = shihabParseDashMpdXml(mehediXmlStr, "", mehediTitleHint, "", location.href, Array.from(mehediAltIds));
              if (shihabParsedGrp) {
                shihabGroupKeys.add(shihabParsedGrp);
                mehediAltIds.add(shihabParsedGrp);
              }
            }

            // Direct progressive URLs on this exact video player's React props
            for (const shihabUrlProp of ["hdSrc", "sdSrc", "hd_src", "sd_src", "browser_native_hd_url", "browser_native_sd_url", "playable_url", "playableUrl", "playable_url_quality_hd", "src"]) {
              const mehediRawU = mehediBag[shihabUrlProp];
              if (typeof mehediRawU === "string" && mehediRawU.startsWith("http")) {
                const shihabNormU = mehediNormalizeHookMediaUrl(mehediRawU);
                if (shihabNormU) {
                  shihabBoundUrls.add(shihabNormU);
                  const mehediUInfo = mehediExtractEfgIds(shihabNormU);
                  if (mehediUInfo.mehediCanonicalGroup) shihabGroupKeys.add(mehediUInfo.mehediCanonicalGroup);
                  for (const shihabA of mehediUInfo.shihabAltIds) mehediAltIds.add(shihabA);
                }
              }
            }
            if (Array.isArray(mehediBag.progressive_urls)) {
              for (const mehediPo of mehediBag.progressive_urls) {
                if (mehediPo && typeof mehediPo.progressive_url === "string" && mehediPo.progressive_url.startsWith("http")) {
                  const shihabNormPo = mehediNormalizeHookMediaUrl(mehediPo.progressive_url);
                  if (shihabNormPo) {
                    shihabBoundUrls.add(shihabNormPo);
                    const mehediPoInfo = mehediExtractEfgIds(shihabNormPo);
                    if (mehediPoInfo.mehediCanonicalGroup) shihabGroupKeys.add(mehediPoInfo.mehediCanonicalGroup);
                    for (const shihabA of mehediPoInfo.shihabAltIds) mehediAltIds.add(shihabA);
                  }
                }
              }
            }
            if (Array.isArray(mehediBag.video_versions)) {
              for (const shihabVv of mehediBag.video_versions) {
                if (shihabVv && typeof shihabVv.url === "string" && shihabVv.url.startsWith("http")) {
                  const mehediNormVv = mehediNormalizeHookMediaUrl(shihabVv.url);
                  if (mehediNormVv) {
                    shihabBoundUrls.add(mehediNormVv);
                    const shihabVvInfo = mehediExtractEfgIds(mehediNormVv);
                    if (shihabVvInfo.mehediCanonicalGroup) shihabGroupKeys.add(shihabVvInfo.mehediCanonicalGroup);
                    for (const mehediA of shihabVvInfo.shihabAltIds) mehediAltIds.add(mehediA);
                  }
                }
              }
            }
          }

          // Stop climbing as soon as we resolved this specific player's video identity so we NEVER touch feed list ancestors!
          if (shihabGroupKeys.size > 0 || shihabBoundUrls.size > 0 || mehediAltIds.size > 0) {
            break;
          }
        }
        shihabFiber = shihabFiber.return;
      }
    } catch (_) {}

    if (shihabGroupKeys.size > 0 || mehediAltIds.size > 0 || shihabBoundUrls.size > 0 || mehediTitleHint) {
      shihabPostToContentBridge("shihab_video_element_identity", {
        shihabVidUid: mehediVidUid,
        mehediGroupKeys: Array.from(shihabGroupKeys),
        shihabAltIds: Array.from(mehediAltIds),
        mehediBoundUrls: Array.from(shihabBoundUrls),
        shihabTitleHint: mehediTitleHint
      });
    }
  }

  // 1. Hook URL.createObjectURL, HTMLMediaElement.src & MediaSource / SourceBuffer
  try {
    const shihabOrigCreateObjUrl = URL.createObjectURL;
    if (typeof shihabOrigCreateObjUrl === "function") {
      URL.createObjectURL = function (mehediObj) {
        const shihabBlobUrl = shihabOrigCreateObjUrl.apply(this, arguments);
        try {
          if (typeof MediaSource !== "undefined" && mehediObj instanceof MediaSource) {
            shihabMediaSourceToBlobUrl.set(mehediObj, shihabBlobUrl);
            mehediBlobUrlToMediaSource.set(shihabBlobUrl, mehediObj);
            if (mehediBlobUrlToMediaSource.size > 100) {
              const shihabOldBlob = mehediBlobUrlToMediaSource.keys().next().value;
              mehediBlobUrlToMediaSource.delete(shihabOldBlob);
            }
          }
        } catch (_) {}
        return shihabBlobUrl;
      };
    }

    if (typeof MediaSource !== "undefined" && MediaSource.prototype && typeof MediaSource.prototype.addSourceBuffer === "function") {
      const mehediOrigAddSb = MediaSource.prototype.addSourceBuffer;
      MediaSource.prototype.addSourceBuffer = function (shihabMimeCodec) {
        const mehediSb = mehediOrigAddSb.apply(this, arguments);
        try {
          const shihabBlobUrl = shihabMediaSourceToBlobUrl.get(this) || "";
          const mehediMimeStr = String(shihabMimeCodec || "").toLowerCase();
          const shihabTrackKind = mehediMimeStr.startsWith("audio/") ? "audio" : "video";
          mehediSourceBufferMeta.set(mehediSb, {
            mehediMediaSource: this,
            shihabBlobUrl: shihabBlobUrl,
            mehediMimeCodec: mehediMimeStr,
            shihabTrackKind: shihabTrackKind,
            mehediReportedUrls: new Set(),
            shihabExactReportedUrls: new Set()
          });

          const shihabOrigAppend = mehediSb.appendBuffer;
          if (typeof shihabOrigAppend === "function" && !mehediSb.__shihabHookedAppend) {
            mehediSb.__shihabHookedAppend = true;
            mehediSb.appendBuffer = function (mehediBuffer) {
              try {
                const shihabMeta = mehediSourceBufferMeta.get(this);
                if (shihabMeta) {
                  const mehediLiveBlob = shihabMeta.shihabBlobUrl || shihabMediaSourceToBlobUrl.get(shihabMeta.mehediMediaSource) || "";
                  if (mehediLiveBlob) {
                    shihabMeta.shihabBlobUrl = mehediLiveBlob;

                    let shihabExactMatchedUrl = "";
                    const mehediBufSigs = shihabComputeMediaChunkSignatures(mehediBuffer);
                    for (const shihabSig of mehediBufSigs) {
                      const mehediHit = shihabChunkHashToStreamUrl.get(shihabSig);
                      if (mehediHit) {
                        shihabExactMatchedUrl = mehediHit;
                        break;
                      }
                    }

                    if (shihabExactMatchedUrl) {
                      if (!shihabMeta.shihabExactReportedUrls.has(shihabExactMatchedUrl)) {
                        shihabMeta.shihabExactReportedUrls.add(shihabExactMatchedUrl);
                        shihabMeta.mehediReportedUrls.add(shihabExactMatchedUrl);
                        shihabPostToContentBridge("shihab_mse_blob_binding", {
                          shihabBlobUrl: mehediLiveBlob,
                          mehediStreamUrl: shihabExactMatchedUrl,
                          shihabTrackKind: shihabMeta.shihabTrackKind,
                          mehediMimeCodec: shihabMeta.mehediMimeCodec,
                          mehediExactByteMatch: true
                        });
                      }
                    } else if (shihabMeta.shihabExactReportedUrls.size === 0) {
                      const shihabNow = Date.now();
                      const mehediCandidate = shihabRecentFetchedSlices.find(
                        (shihabItem) =>
                          shihabNow - shihabItem.shihabAtMs < 4000 &&
                          (!shihabItem.mehediTrackType || shihabItem.mehediTrackType === shihabMeta.shihabTrackKind)
                      );
                      if (mehediCandidate && !shihabMeta.mehediReportedUrls.has(mehediCandidate.shihabUrl)) {
                        shihabMeta.mehediReportedUrls.add(mehediCandidate.shihabUrl);
                        shihabPostToContentBridge("shihab_mse_blob_binding", {
                          shihabBlobUrl: mehediLiveBlob,
                          mehediStreamUrl: mehediCandidate.shihabUrl,
                          shihabTrackKind: shihabMeta.shihabTrackKind,
                          mehediMimeCodec: shihabMeta.mehediMimeCodec,
                          mehediExactByteMatch: false
                        });
                      }
                    }
                  }
                }
              } catch (_) {}
              return shihabOrigAppend.apply(this, arguments);
            };
          }
        } catch (_) {}
        return mehediSb;
      };
    }
  } catch (_) {}

  // 2. Hook window.fetch non-destructively (reads up to 128 KB of media clone for ftyp/moof box fingerprinting)
  try {
    const mehediOrigFetch = window.fetch;
    if (typeof mehediOrigFetch === "function") {
      window.fetch = function (shihabInput) {
        let shihabReqUrl = "";
        try {
          if (typeof shihabInput === "string") shihabReqUrl = shihabInput;
          else if (shihabInput && typeof shihabInput.url === "string") shihabReqUrl = shihabInput.url;
          else if (shihabInput && typeof shihabInput.href === "string") shihabReqUrl = shihabInput.href;
        } catch (_) {}

        const mehediPromise = mehediOrigFetch.apply(this, arguments);
        mehediPromise.then((shihabResp) => {
          try {
            if (!shihabResp || (!shihabResp.ok && shihabResp.status !== 206)) return;
            const mehediFinalUrl = shihabResp.url || shihabReqUrl;
            const shihabCt = (shihabResp.headers && shihabResp.headers.get("content-type")) || "";
            if (shihabLooksLikeMediaSliceUrl(mehediFinalUrl)) {
              mehediRememberFetchedSlice(mehediFinalUrl, shihabCt);
              try {
                const mehediMediaClone = shihabResp.clone();
                if (mehediMediaClone.body && typeof mehediMediaClone.body.getReader === "function") {
                  const shihabReader = mehediMediaClone.body.getReader();
                  const mehediParts = [];
                  let shihabTotalRead = 0;
                  const mehediPump = () => {
                    shihabReader.read().then((mehediChunkRes) => {
                      if (mehediChunkRes && mehediChunkRes.value && mehediChunkRes.value.byteLength > 0) {
                        mehediParts.push(mehediChunkRes.value);
                        shihabTotalRead += mehediChunkRes.value.byteLength;
                        if (!mehediChunkRes.done && shihabTotalRead < 131072) {
                          mehediPump();
                          return;
                        }
                      }
                      if (shihabTotalRead >= 28) {
                        const shihabJoined = new Uint8Array(shihabTotalRead);
                        let mehediOff = 0;
                        for (const shihabP of mehediParts) {
                          shihabJoined.set(shihabP, mehediOff);
                          mehediOff += shihabP.byteLength;
                        }
                        mehediIndexChunkBytesForUrl(mehediFinalUrl, shihabJoined);
                      }
                      try { shihabReader.cancel().catch(() => {}); } catch (_) {}
                    }).catch(() => {
                      try { shihabReader.cancel().catch(() => {}); } catch (_) {}
                    });
                  };
                  mehediPump();
                }
              } catch (_) {}
            }
            const mehediLowerCt = shihabCt.toLowerCase();
            if (
              mehediLowerCt.includes("json") ||
              mehediLowerCt.includes("dash+xml") ||
              mehediLowerCt.includes("xml") ||
              mehediLowerCt.includes("javascript") ||
              mehediLowerCt.includes("text/plain") ||
              /\/(graphql|api\/v1\/|youtubei\/v1\/player|ajax\/bz)/i.test(mehediFinalUrl) ||
              /\.mpd(\?|$)/i.test(mehediFinalUrl)
            ) {
              shihabResp.clone().text().then((mehediBodyText) => {
                mehediInspectTextBodyIfManifest(mehediBodyText, mehediFinalUrl, mehediLowerCt);
              }).catch(() => {});
            }
          } catch (_) {}
        }).catch(() => {});

        return mehediPromise;
      };
    }
  } catch (_) {}

  // 3. Hook XMLHttpRequest non-destructively
  try {
    const shihabOrigXhrOpen = XMLHttpRequest.prototype.open;
    const mehediOrigXhrSend = XMLHttpRequest.prototype.send;
    XMLHttpRequest.prototype.open = function (_shihabMethod, mehediUrl) {
      try {
        this.__shihabReqUrl = typeof mehediUrl === "string" ? mehediUrl : String(mehediUrl || "");
      } catch (_) {}
      return shihabOrigXhrOpen.apply(this, arguments);
    };
    XMLHttpRequest.prototype.send = function () {
      try {
        this.addEventListener("load", function () {
          try {
            const mehediFinalUrl = this.responseURL || this.__shihabReqUrl || "";
            const shihabCt = (this.getResponseHeader && this.getResponseHeader("content-type")) || "";
            if (shihabLooksLikeMediaSliceUrl(mehediFinalUrl)) {
              mehediRememberFetchedSlice(mehediFinalUrl, shihabCt);
              if (this.responseType === "arraybuffer" && this.response instanceof ArrayBuffer && this.response.byteLength >= 28) {
                mehediIndexChunkBytesForUrl(mehediFinalUrl, new Uint8Array(this.response, 0, Math.min(196608, this.response.byteLength)));
              }
            }
            if ((this.responseType === "" || this.responseType === "text") && typeof this.responseText === "string") {
              mehediInspectTextBodyIfManifest(this.responseText, mehediFinalUrl, shihabCt);
            } else if (this.responseType === "json" && this.response && typeof this.response === "object") {
              shihabWalkJsonForMediaMetadata(this.response);
            }
          } catch (_) {}
        });
      } catch (_) {}
      return mehediOrigXhrSend.apply(this, arguments);
    };
  } catch (_) {}

  // 4. Scan SSR embedded <script type="application/json"> + live <video> elements
  function shihabScanInitialDomPayloads() {
    try {
      if (window.ytInitialPlayerResponse && typeof window.ytInitialPlayerResponse === "object") {
        mehediInspectYouTubePlayerResponse(window.ytInitialPlayerResponse);
      }
      const mehediJsonScripts = document.querySelectorAll('script[type="application/json"]');
      for (const shihabScript of mehediJsonScripts) {
        if (shihabScript.__mehediScanned) continue;
        shihabScript.__mehediScanned = true;
        const mehediRaw = shihabScript.textContent || "";
        if (
          mehediRaw.length > 40 &&
          mehediRaw.length < 6 * 1024 * 1024 &&
          (mehediRaw.includes("video_dash_manifest") ||
           mehediRaw.includes("dash_manifests") ||
           mehediRaw.includes("manifest_xml") ||
           mehediRaw.includes("progressive_urls") ||
           mehediRaw.includes("video_versions") ||
           mehediRaw.includes("browser_native_hd_url") ||
           mehediRaw.includes("playable_url"))
        ) {
          mehediInspectTextBodyIfManifest(mehediRaw, location.href, "application/json");
        }
      }
      const shihabVideos = document.querySelectorAll("video");
      for (const mehediVid of shihabVideos) {
        mehediInspectLiveDomVideoElement(mehediVid);
      }
    } catch (_) {}
  }

  // Detect Encrypted Media Extensions (EME / Widevine / PlayReady / FairPlay DRM) so UI clearly labels DRM streams
  function shihabNotifyEmeDrmDetected(mehediKeySystem) {
    try {
      window.postMessage(
        {
          __shihabMehediPageHook: true,
          mehediKind: "shihab_eme_drm_detected",
          shihabPayload: { keySystem: String(mehediKeySystem || "EME") }
        },
        "*"
      );
    } catch (_) {}
  }

  try {
    if (navigator && typeof navigator.requestMediaKeySystemAccess === "function") {
      const mehediOrigReqKeySys = navigator.requestMediaKeySystemAccess.bind(navigator);
      navigator.requestMediaKeySystemAccess = function (shihabKeySystem, mehediSupportedConfigs) {
        const shihabPromise = mehediOrigReqKeySys(shihabKeySystem, mehediSupportedConfigs);
        if (shihabPromise && typeof shihabPromise.then === "function") {
          shihabPromise.then(() => shihabNotifyEmeDrmDetected(shihabKeySystem)).catch(() => {});
        }
        return shihabPromise;
      };
    }
  } catch (_) {}

  document.addEventListener(
    "encrypted",
    (shihabEncEvt) => {
      if (shihabEncEvt && shihabEncEvt.target && shihabEncEvt.target.tagName === "VIDEO") {
        shihabNotifyEmeDrmDetected(shihabEncEvt.initDataType || "encrypted-video");
      }
    },
    { capture: true, passive: true }
  );

  for (const mehediEvtName of ["play", "loadedmetadata", "pointerover"]) {
    document.addEventListener(
      mehediEvtName,
      (shihabEvt) => {
        if (shihabEvt.target && shihabEvt.target.tagName === "VIDEO") {
          if (shihabEvt.target.mediaKeys) {
            shihabNotifyEmeDrmDetected("video.mediaKeys");
          }
          mehediInspectLiveDomVideoElement(shihabEvt.target);
        }
      },
      { capture: true, passive: true }
    );
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", shihabScanInitialDomPayloads, { once: true });
  } else {
    shihabScanInitialDomPayloads();
  }
  window.addEventListener("yt-navigate-finish", () => {
    mehediEmittedManifestKeys.clear();
    setTimeout(shihabScanInitialDomPayloads, 250);
  });
  window.addEventListener("message", (shihabEv) => {
    try {
      if (shihabEv.source !== window || !shihabEv.data) return;
      if (shihabEv.data.__shihabInspectVideoUid) {
        const mehediTargetUid = String(shihabEv.data.__shihabInspectVideoUid);
        const shihabMatchingVid = document.querySelector(`video[data-shihab-vid-uid="${CSS.escape(mehediTargetUid)}"]`);
        if (shihabMatchingVid) {
          mehediInspectLiveDomVideoElement(shihabMatchingVid);
        }
        return;
      }
      if (shihabEv.data.__shihabResolveYtInMainWorld) {
        shihabResolveYouTubeInPageMainWorld(
          String(shihabEv.data.__shihabResolveYtInMainWorld || ""),
          String(shihabEv.data.mehediVisitorData || "")
        );
        return;
      }
      if (shihabEv.data.__shihabMehediContentPing === true) {
        mehediEmittedManifestKeys.clear();
        const mehediScripts = document.querySelectorAll('script[type="application/json"]');
        for (const shihabSc of mehediScripts) {
          shihabSc.__mehediScanned = false;
        }
        shihabScanInitialDomPayloads();
      }
    } catch (_) {}
  });
})();
