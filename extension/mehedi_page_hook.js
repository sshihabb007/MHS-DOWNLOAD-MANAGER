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
  const shihabMediaSourceToVideoUid = new WeakMap();
  const mehediBlobUrlToMediaSource = new Map();
  const mehediSourceBufferMeta = new WeakMap();
  const shihabRecentFetchedSlices = [];
  const shihabChunkHashToStreamUrl = new Map();
  const mehediEmittedManifestKeys = new Set();
  const shihabMediaUrlToGroupKey = new Map();
  const shihabVariantUrlToMasterInfo = new Map();
  const shihabSegmentPathToPlaylistUrl = new Map();
  const mehediRecentManifestPayloads = [];
  const shihabRecentBlobBindings = new Map();
  const shihabVideoUidRefMap = new Map();
  let mehediLastKnownHookHref = location.href;

  function shihabMarkEmittedManifestKey(mehediKey) {
    if (!mehediKey) return;
    mehediEmittedManifestKeys.add(mehediKey);
    if (mehediEmittedManifestKeys.size > 500) {
      const shihabOldest = mehediEmittedManifestKeys.keys().next().value;
      mehediEmittedManifestKeys.delete(shihabOldest);
    }
  }

  function shihabResetHookNavigationCachesIfChanged() {
    if (location.href !== mehediLastKnownHookHref) {
      mehediLastKnownHookHref = location.href;
      mehediEmittedManifestKeys.clear();
      mehediRecentManifestPayloads.length = 0;
      shihabRecentBlobBindings.clear();
    }
  }

  function shihabGetSafeHttpReferer() {
    if (location.href && /^https?:\/\//i.test(location.href)) return location.href;
    if (document.referrer && /^https?:\/\//i.test(document.referrer)) return document.referrer;
    return "";
  }

  function shihabGetSafeHttpOrigin() {
    if (location.origin && /^https?:\/\//i.test(location.origin)) return location.origin;
    if (document.referrer && /^https?:\/\//i.test(document.referrer)) {
      try {
        const shihabRefOrg = new URL(document.referrer).origin;
        if (/^https?:\/\//i.test(shihabRefOrg)) return shihabRefOrg;
      } catch (_) {}
    }
    return "";
  }

  function shihabIsHlsManifestEncryptedHook(mehediHlsText) {
    if (!mehediHlsText || typeof mehediHlsText !== "string") return false;
    const shihabLines = mehediHlsText.split(/\r?\n/);
    for (let mehediI = 0; mehediI < shihabLines.length; mehediI++) {
      const shihabL = shihabLines[mehediI].trim();
      if (/^#EXT-X-(?:SESSION-)?KEY:/i.test(shihabL)) {
        if (!/\bMETHOD=NONE\b/i.test(shihabL)) return true;
      }
    }
    return false;
  }

  const SHIHAB_HOOK_EPHEMERAL_HLS_PARAMS = new Set([
    "token", "sig", "signature", "exp", "expires", "hdnts", "hdnea",
    "t", "ts", "timestamp", "auth", "key", "hash", "nonce", "_", "cb"
  ]);

  function shihabCanonicalHlsGroupKeyUrl(mehediRawUrl) {
    if (!mehediRawUrl) return "";
    try {
      const shihabU = new URL(mehediRawUrl, location.href);
      for (const mehediK of Array.from(shihabU.searchParams.keys())) {
        if (SHIHAB_HOOK_EPHEMERAL_HLS_PARAMS.has(mehediK.toLowerCase())) {
          shihabU.searchParams.delete(mehediK);
        }
      }
      shihabU.hash = "";
      return shihabU.toString();
    } catch (_) {
      return String(mehediRawUrl).split("#")[0];
    }
  }

  function mehediSetBoundedMap(shihabMap, mehediKey, shihabVal, mehediMax) {
    if (!mehediKey) return;
    shihabMap.set(mehediKey, shihabVal);
    if (shihabMap.size > mehediMax) {
      const shihabFirst = shihabMap.keys().next().value;
      shihabMap.delete(shihabFirst);
    }
  }

  function shihabCollectVideosDeepHook(mehediRoot, shihabOut) {
    const shihabList = shihabOut || [];
    const mehediNode = mehediRoot || document;
    try {
      const mehediVids = mehediNode.querySelectorAll("video");
      for (let shihabI = 0; shihabI < mehediVids.length; shihabI++) {
        shihabList.push(mehediVids[shihabI]);
      }
      // Check common Web Component video player hosts without full-DOM '*' scanning unless needed
      const mehediKnownShadowHosts = mehediNode.querySelectorAll(
        "shreddit-player, shreddit-player-2, video-player, media-player, mux-player, vimeo-player, videojs-player, ytd-player, amp-video, faceplate-video, brightcove-player, jw-player, plyr-player"
      );
      for (let mehediJ = 0; mehediJ < mehediKnownShadowHosts.length; mehediJ++) {
        if (mehediKnownShadowHosts[mehediJ].shadowRoot) {
          shihabCollectVideosDeepHook(mehediKnownShadowHosts[mehediJ].shadowRoot, shihabList);
        }
      }
    } catch (_) {}
    return shihabList;
  }

  function shihabFindVideoByUidDeepHook(mehediUid) {
    if (!mehediUid) return null;
    const shihabCached = shihabVideoUidRefMap.get(mehediUid);
    if (shihabCached) {
      const mehediEl = typeof shihabCached.deref === "function" ? shihabCached.deref() : shihabCached;
      if (mehediEl && mehediEl.isConnected) return mehediEl;
      shihabVideoUidRefMap.delete(mehediUid);
    }
    try {
      const shihabDirect = document.querySelector(`video[data-shihab-vid-uid="${CSS.escape(mehediUid)}"]`);
      if (shihabDirect) return shihabDirect;
    } catch (_) {}
    const shihabAll = shihabCollectVideosDeepHook(document, []);
    for (let mehediI = 0; mehediI < shihabAll.length; mehediI++) {
      if (shihabAll[mehediI] && shihabAll[mehediI].dataset && shihabAll[mehediI].dataset.shihabVidUid === mehediUid) {
        return shihabAll[mehediI];
      }
    }
    return null;
  }

  function shihabExtractUrlPathKey(mehediRawUrl) {
    if (!mehediRawUrl) return "";
    try {
      const shihabU = new URL(mehediRawUrl, location.href);
      return shihabU.origin + shihabU.pathname;
    } catch (_) {
      return String(mehediRawUrl).split("?")[0].split("#")[0];
    }
  }

  function shihabEnsureVideoUidOnElement(mehediVidEl) {
    if (!mehediVidEl || mehediVidEl.tagName !== "VIDEO") return "";
    if (!mehediVidEl.dataset.shihabVidUid) {
      mehediVidEl.dataset.shihabVidUid = "shihab_vid_" + (mehediNextVideoUidSeq++) + "_" + Math.random().toString(36).slice(2, 7);
    }
    const shihabUid = mehediVidEl.dataset.shihabVidUid;
    mehediSetBoundedMap(shihabVideoUidRefMap, shihabUid, typeof WeakRef === "function" ? new WeakRef(mehediVidEl) : mehediVidEl, 120);
    return shihabUid;
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

  function mehediIndexChunkBytesForUrl(shihabRawUrl, mehediBytes, shihabContentType) {
    const shihabClean = mehediNormalizeHookMediaUrl(shihabRawUrl);
    if (!shihabClean || !shihabLooksLikeMediaSliceUrl(shihabClean, shihabContentType)) return;
    const shihabParentPlaylist = mehediResolvePlaylistParentForSegment(shihabClean);
    const mehediEffectiveUrl = shihabParentPlaylist || shihabClean;
    const mehediSigs = shihabComputeMediaChunkSignatures(mehediBytes);
    for (const shihabSig of mehediSigs) {
      shihabChunkHashToStreamUrl.set(shihabSig, mehediEffectiveUrl);
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
      if (mehediKind === "mehedi_manifest_streams" && shihabPayload && Array.isArray(shihabPayload.shihabStreams) && shihabPayload.shihabStreams.length > 0) {
        const shihabGrpKey = String(shihabPayload.mehediGroupKey || "");
        const mehediReplayIdx = shihabGrpKey
          ? mehediRecentManifestPayloads.findIndex((mehediP) => mehediP && mehediP.mehediGroupKey === shihabGrpKey)
          : -1;
        if (mehediReplayIdx >= 0) {
          mehediRecentManifestPayloads[mehediReplayIdx] = shihabPayload;
        } else {
          mehediRecentManifestPayloads.push(shihabPayload);
          if (mehediRecentManifestPayloads.length > 24) {
            mehediRecentManifestPayloads.shift();
          }
        }
      } else if (mehediKind === "shihab_mse_blob_binding" && shihabPayload && shihabPayload.shihabBlobUrl && shihabPayload.mehediStreamUrl) {
        const shihabBindKey = `${shihabPayload.shihabBlobUrl}|${shihabPayload.mehediStreamUrl}`;
        mehediSetBoundedMap(shihabRecentBlobBindings, shihabBindKey, shihabPayload, 80);
      }
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
      const shihabHostLower = (mehediUrlObj.hostname || "").toLowerCase();
      const mehediIsGv = shihabHostLower.endsWith(".googlevideo.com") || mehediUrlObj.pathname.includes("/videoplayback");
      const shihabIsFbIg =
        mehediUrlObj.searchParams.has("efg") ||
        shihabHostLower.includes("fbcdn.net") ||
        shihabHostLower.includes("cdninstagram.com");

      if (mehediIsGv || shihabIsFbIg) {
        for (const shihabParam of ["bytestart", "byteend", "alr"]) {
          mehediUrlObj.searchParams.delete(shihabParam);
        }
      }
      if (mehediIsGv) {
        mehediUrlObj.searchParams.delete("rn");
        mehediUrlObj.searchParams.delete("rbuf");
        if (mehediUrlObj.searchParams.has("range")) {
          const mehediSparams = (mehediUrlObj.searchParams.get("sparams") || "").split(",");
          if (!mehediSparams.includes("range")) {
            mehediUrlObj.searchParams.delete("range");
          }
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

  function shihabLooksLikeMediaSliceUrl(mehediUrl, shihabContentType) {
    if (!mehediUrl) return false;
    if (shihabIsPoisonedYouTubeWebUrl(mehediUrl)) return false;
    const shihabLower = String(mehediUrl).toLowerCase();
    if (shihabLower.includes("ump=1") || shihabLower.includes("sabr=1")) return false;
    const mehediCt = String(shihabContentType || "").toLowerCase();
    if (
      mehediCt.startsWith("video/") ||
      mehediCt.startsWith("audio/") ||
      mehediCt.includes("mpegurl") ||
      mehediCt.includes("dash+xml")
    ) {
      return true;
    }
    const shihabPathKey = shihabExtractUrlPathKey(mehediUrl);
    if (shihabPathKey && shihabSegmentPathToPlaylistUrl.has(shihabPathKey)) {
      return true;
    }
    return (
      shihabLower.includes("/videoplayback") ||
      /\.(mp4|webm|mkv|m4a|m4v|mov|flv|m3u8|m3u|mpd)(\?|#|$)/i.test(shihabLower) ||
      shihabLower.includes("efg=") ||
      shihabLower.includes("mime=video") ||
      shihabLower.includes("mime=audio") ||
      shihabLower.includes("mime_type=video") ||
      shihabLower.includes("mime_type=audio") ||
      shihabLower.includes("dms.licdn.com/playlist/vid") ||
      shihabLower.includes("video.twimg.com/")
    );
  }

  function mehediResolvePlaylistParentForSegment(shihabRawUrl) {
    if (!shihabRawUrl) return "";
    const mehediPathKey = shihabExtractUrlPathKey(shihabRawUrl);
    if (mehediPathKey && shihabSegmentPathToPlaylistUrl.has(mehediPathKey)) {
      return shihabSegmentPathToPlaylistUrl.get(mehediPathKey) || "";
    }
    const shihabClean = mehediNormalizeHookMediaUrl(shihabRawUrl);
    const mehediVarInfo = shihabVariantUrlToMasterInfo.get(shihabClean) || shihabVariantUrlToMasterInfo.get(mehediPathKey);
    if (mehediVarInfo && mehediVarInfo.masterUrl) {
      return mehediVarInfo.masterUrl;
    }
    return "";
  }

  function mehediRememberFetchedSlice(shihabRawUrl, mehediContentType) {
    const shihabClean = mehediNormalizeHookMediaUrl(shihabRawUrl);
    if (!shihabClean || !shihabLooksLikeMediaSliceUrl(shihabClean, mehediContentType)) return;
    const mehediCt = String(mehediContentType || "").toLowerCase();
    // Map HLS/DASH segments & child playlists back to their downloadable parent playlist URL so blob: bindings match cleanly!
    const shihabParentPlaylist = mehediResolvePlaylistParentForSegment(shihabClean);
    const mehediEffectiveBindUrl = shihabParentPlaylist || shihabClean;
    // Never bind raw unmapped .ts/.m4s segments directly if we don't know their parent playlist
    if (!shihabParentPlaylist && (/\.(ts|m4s|cmfv|cmfa)(\?|#|$)/i.test(mehediEffectiveBindUrl) || mehediCt.includes("mp2t"))) {
      return;
    }
    let shihabTrackType = "";
    if (!shihabParentPlaylist && (mehediCt.startsWith("audio/") || shihabClean.includes("mime=audio") || shihabClean.includes("mime_type=audio"))) {
      shihabTrackType = "audio";
    } else if (!shihabParentPlaylist && (mehediCt.startsWith("video/") || shihabClean.includes("mime=video") || shihabClean.includes("mime_type=video"))) {
      shihabTrackType = "video";
    }
    shihabRecentFetchedSlices.unshift({
      shihabUrl: mehediEffectiveBindUrl,
      mehediTrackType: shihabTrackType,
      shihabAtMs: Date.now()
    });
    if (shihabRecentFetchedSlices.length > 80) {
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

        let mehediInheritedBase = mehediBaseUrlHint || location.href;
        try {
          const shihabMpdBase = shihabMpdEl.querySelector(":scope > BaseURL");
          if (shihabMpdBase && shihabMpdBase.textContent) {
            mehediInheritedBase = new URL(shihabDecodeXmlEntities(shihabMpdBase.textContent.trim()), mehediInheritedBase).toString();
          }
          const shihabPeriodEl = shihabParentAdapt ? shihabParentAdapt.parentElement : null;
          const mehediPeriodBase = shihabPeriodEl ? shihabPeriodEl.querySelector(":scope > BaseURL") : null;
          if (mehediPeriodBase && mehediPeriodBase.textContent) {
            mehediInheritedBase = new URL(shihabDecodeXmlEntities(mehediPeriodBase.textContent.trim()), mehediInheritedBase).toString();
          }
          const shihabAdaptBase = shihabParentAdapt ? shihabParentAdapt.querySelector(":scope > BaseURL") : null;
          if (shihabAdaptBase && shihabAdaptBase.textContent) {
            mehediInheritedBase = new URL(shihabDecodeXmlEntities(shihabAdaptBase.textContent.trim()), mehediInheritedBase).toString();
          }
        } catch (_) {}

        const shihabRawBase = shihabDecodeXmlEntities(mehediBaseEl.textContent.trim());
        let mehediResolvedUrl = "";
        try {
          mehediResolvedUrl = new URL(shihabRawBase, mehediInheritedBase).toString();
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

  // Parse HLS (#EXTM3U) master & media playlists directly from the page's own fetch/XHR responses
  // (0 extra requests, 0 403/CORS issues on hotlink-protected streaming embeds, exact segment->playlist MSE correlation!)
  function mehediParseHlsPlaylistText(shihabHlsText, mehediSourceUrl) {
    shihabResetHookNavigationCachesIfChanged();
    if (!shihabHlsText || typeof shihabHlsText !== "string") return "";
    const shihabTrim = shihabHlsText.trimStart();
    if (!shihabTrim.startsWith("#EXTM3U")) return "";
    const shihabCleanSource = mehediNormalizeHookMediaUrl(mehediSourceUrl || "");
    if (!shihabCleanSource) return "";

    // Respect DRM / encrypted HLS streams (never bypass #EXT-X-KEY or #EXT-X-SESSION-KEY unless METHOD=NONE)
    if (shihabIsHlsManifestEncryptedHook(shihabTrim)) {
      return "";
    }

    const mehediLines = shihabTrim.split(/\r?\n/).map((shihabL) => shihabL.trim()).filter(Boolean);
    const shihabSourcePathKey = shihabExtractUrlPathKey(shihabCleanSource);
    const shihabCanonicalHlsKey = shihabCanonicalHlsGroupKeyUrl(shihabCleanSource);
    const mehediReferer = shihabGetSafeHttpReferer();
    const shihabOrigin = shihabGetSafeHttpOrigin();

    // A) Master Playlist (#EXT-X-STREAM-INF)
    if (shihabTrim.includes("#EXT-X-STREAM-INF")) {
      const shihabAudioGroups = new Map();
      for (let mehediI = 0; mehediI < mehediLines.length; mehediI++) {
        const shihabLine = mehediLines[mehediI];
        if (shihabLine.startsWith("#EXT-X-MEDIA:") && /TYPE=AUDIO/i.test(shihabLine)) {
          const shihabGrp = (shihabLine.match(/GROUP-ID="([^"]+)"/i) || [])[1];
          const mehediUri = (shihabLine.match(/URI="([^"]+)"/i) || [])[1];
          if (shihabGrp && mehediUri) {
            try {
              const shihabAbsAud = new URL(mehediUri, shihabCleanSource).toString();
              if (!shihabAudioGroups.has(shihabGrp) || /DEFAULT=YES/i.test(shihabLine)) {
                shihabAudioGroups.set(shihabGrp, shihabAbsAud);
              }
            } catch (_) {}
          }
        }
      }

      const shihabMappedGroup =
        shihabMediaUrlToGroupKey.get(shihabCleanSource) ||
        shihabMediaUrlToGroupKey.get(shihabCanonicalHlsKey) ||
        shihabMediaUrlToGroupKey.get(shihabSourcePathKey) ||
        "";
      const shihabLiHlsM = !shihabMappedGroup ? shihabCleanSource.match(/licdn\.com\/playlist\/vid\/(?:v\d+\/)?([A-Za-z0-9_-]{6,})\//i) : null;
      const shihabTwHlsM = (!shihabMappedGroup && !shihabLiHlsM) ? shihabCleanSource.match(/twimg\.com\/(?:ext_tw_video|amplify_video|tweet_video)\/([A-Za-z0-9_-]+)\//i) : null;
      const mehediHlsGroupKey =
        shihabMappedGroup ||
        (shihabLiHlsM ? `li:${shihabLiHlsM[1]}` : "") ||
        (shihabTwHlsM ? `twv:${shihabTwHlsM[1]}` : "") ||
        `hls:${shihabFnv1aStringHash(shihabCanonicalHlsKey || shihabSourcePathKey)}`;
      const shihabAltSet = new Set([mehediHlsGroupKey, shihabCleanSource, shihabCanonicalHlsKey]);
      if (shihabLiHlsM) shihabAltSet.add(shihabLiHlsM[1]);
      if (shihabTwHlsM) shihabAltSet.add(shihabTwHlsM[1]);

      const mehediVariants = [];
      for (let shihabI = 0; shihabI < mehediLines.length; shihabI++) {
        const mehediInf = mehediLines[shihabI];
        if (!mehediInf.startsWith("#EXT-X-STREAM-INF")) continue;

        // Advance past any intervening # tag/comment lines to locate the actual variant URI line (stop on next STREAM-INF)
        let shihabUriIdx = shihabI + 1;
        let mehediHitNextInf = false;
        while (shihabUriIdx < mehediLines.length && mehediLines[shihabUriIdx].startsWith("#")) {
          if (mehediLines[shihabUriIdx].startsWith("#EXT-X-STREAM-INF")) {
            mehediHitNextInf = true;
            break;
          }
          shihabUriIdx++;
        }
        if (mehediHitNextInf) {
          shihabI = shihabUriIdx - 1;
          continue;
        }
        if (shihabUriIdx >= mehediLines.length) break;
        const shihabRawVarUri = mehediLines[shihabUriIdx];
        shihabI = shihabUriIdx;

        let mehediVariantUrl = "";
        try {
          mehediVariantUrl = mehediNormalizeHookMediaUrl(new URL(shihabRawVarUri, shihabCleanSource).toString());
        } catch (_) {
          continue;
        }
        if (!mehediVariantUrl) continue;

        const shihabResAttr = mehediInf.match(/RESOLUTION=(\d+)x(\d+)/i);
        const mehediW = shihabResAttr ? parseInt(shihabResAttr[1], 10) : 0;
        const shihabH = shihabResAttr ? parseInt(shihabResAttr[2], 10) : 0;
        const mehediBwMatch = mehediInf.match(/BANDWIDTH=(\d+)/i);
        const shihabBw = mehediBwMatch ? parseInt(mehediBwMatch[1], 10) : 0;
        const mehediAudGrp = (mehediInf.match(/AUDIO="([^"]+)"/i) || [])[1] || "";
        const shihabPairedAudUrl = mehediAudGrp ? (shihabAudioGroups.get(mehediAudGrp) || "") : "";

        const mehediVarInfo = { masterUrl: shihabCleanSource, groupKey: mehediHlsGroupKey };
        mehediSetBoundedMap(shihabVariantUrlToMasterInfo, mehediVariantUrl, mehediVarInfo, 300);
        mehediSetBoundedMap(shihabVariantUrlToMasterInfo, shihabCanonicalHlsGroupKeyUrl(mehediVariantUrl), mehediVarInfo, 300);
        if (shihabPairedAudUrl) {
          mehediSetBoundedMap(shihabVariantUrlToMasterInfo, shihabPairedAudUrl, mehediVarInfo, 300);
          mehediSetBoundedMap(shihabVariantUrlToMasterInfo, shihabCanonicalHlsGroupKeyUrl(shihabPairedAudUrl), mehediVarInfo, 300);
        }

        mehediVariants.push({
          shihabUrl: mehediVariantUrl,
          mehediMimeType: "application/vnd.apple.mpegurl",
          shihabExt: "m3u8",
          shihabCodecs: "",
          mehediTrackRole: shihabPairedAudUrl ? "video_only" : "progressive",
          shihabWidth: mehediW,
          mehediHeight: shihabH,
          shihabResolution: mehediHeightToStandardLabel(mehediW, shihabH),
          mehediBandwidth: shihabBw,
          shihabSizeBytes: -1,
          mehediDurationSec: 0,
          shihabGroupKey: mehediHlsGroupKey,
          mehediShortcode: "",
          mehediAltIds: Array.from(shihabAltSet),
          mehediAudioCompanionUrl: shihabPairedAudUrl,
          mehediParentMasterUrl: shihabCleanSource,
          mehediCapturedReferer: mehediReferer,
          shihabOrigin: shihabOrigin,
          shihabSkipBinaryProbe: true,
          shihabTitleHint: ""
        });
      }

      if (mehediVariants.length > 0) {
        mehediSetBoundedMap(shihabMediaUrlToGroupKey, shihabCleanSource, mehediHlsGroupKey, 240);
        mehediSetBoundedMap(shihabMediaUrlToGroupKey, shihabCanonicalHlsKey, mehediHlsGroupKey, 240);
        mehediRememberFetchedSlice(shihabCleanSource, "application/vnd.apple.mpegurl");

        const shihabSig = `hls_m:${mehediHlsGroupKey}:${mehediVariants.length}`;
        if (!mehediEmittedManifestKeys.has(shihabSig)) {
          shihabMarkEmittedManifestKey(shihabSig);
          shihabPostToContentBridge("mehedi_manifest_streams", {
            shihabStreams: mehediVariants,
            mehediGroupKey: mehediHlsGroupKey,
            shihabShortcode: "",
            mehediAltIds: Array.from(shihabAltSet),
            mehediTitleHint: ""
          });
        }
        return mehediHlsGroupKey;
      }
      return "";
    }

    // B) Media Playlist (#EXTINF) — bind init/first segments to parent master (or self) for exact MSE correlation!
    if (shihabTrim.includes("#EXTINF:")) {
      const shihabParentInfo =
        shihabVariantUrlToMasterInfo.get(shihabCleanSource) ||
        shihabVariantUrlToMasterInfo.get(shihabCanonicalHlsKey) ||
        null;
      const mehediBindTargetUrl = (shihabParentInfo && shihabParentInfo.masterUrl) || shihabCleanSource;
      const shihabLiMediaM = shihabCleanSource.match(/licdn\.com\/playlist\/vid\/(?:v\d+\/)?([A-Za-z0-9_-]{6,})\//i);
      const shihabTwMediaM = !shihabLiMediaM ? shihabCleanSource.match(/twimg\.com\/(?:ext_tw_video|amplify_video|tweet_video)\/([A-Za-z0-9_-]+)\//i) : null;
      const shihabGroupKey =
        (shihabParentInfo && shihabParentInfo.groupKey) ||
        shihabMediaUrlToGroupKey.get(shihabCleanSource) ||
        shihabMediaUrlToGroupKey.get(shihabCanonicalHlsKey) ||
        (shihabLiMediaM ? `li:${shihabLiMediaM[1]}` : "") ||
        (shihabTwMediaM ? `twv:${shihabTwMediaM[1]}` : "") ||
        `hls:${shihabFnv1aStringHash(shihabCanonicalHlsKey || shihabSourcePathKey)}`;

      let mehediTotalDur = 0;
      let shihabMappedSegs = 0;
      for (let mehediI = 0; mehediI < mehediLines.length; mehediI++) {
        const shihabL = mehediLines[mehediI];
        if (shihabL.startsWith("#EXT-X-MAP:")) {
          const mehediMapUri = (shihabL.match(/URI="([^"]+)"/i) || [])[1];
          if (mehediMapUri) {
            try {
              const shihabAbsMap = new URL(mehediMapUri, shihabCleanSource).toString();
              mehediSetBoundedMap(shihabSegmentPathToPlaylistUrl, shihabExtractUrlPathKey(shihabAbsMap), mehediBindTargetUrl, 600);
            } catch (_) {}
          }
        } else if (shihabL.startsWith("#EXTINF:")) {
          const mehediDurM = shihabL.match(/^#EXTINF:\s*([\d.]+)/i);
          if (mehediDurM) {
            mehediTotalDur += parseFloat(mehediDurM[1]) || 0;
          }
          if (shihabMappedSegs < 5) {
            let shihabSegIdx = mehediI + 1;
            while (shihabSegIdx < mehediLines.length && mehediLines[shihabSegIdx].startsWith("#")) {
              shihabSegIdx++;
            }
            if (shihabSegIdx < mehediLines.length) {
              try {
                const shihabAbsSeg = new URL(mehediLines[shihabSegIdx], shihabCleanSource).toString();
                mehediSetBoundedMap(shihabSegmentPathToPlaylistUrl, shihabExtractUrlPathKey(shihabAbsSeg), mehediBindTargetUrl, 600);
                shihabMappedSegs++;
              } catch (_) {}
            }
          }
        }
      }

      mehediSetBoundedMap(shihabMediaUrlToGroupKey, mehediBindTargetUrl, shihabGroupKey, 240);
      mehediRememberFetchedSlice(mehediBindTargetUrl, "application/vnd.apple.mpegurl");

      // Emit standalone media playlist if not already covered by an expanded master playlist
      if (!shihabParentInfo) {
        const shihabPathDims = shihabCleanSource.match(/\/(\d{3,4})x(\d{3,4})\//);
        const mehediPathP = shihabCleanSource.match(/(?:^|[^0-9])(2160p|1440p|1080p|720p|640p|540p|480p|360p|240p|144p)(?:[^0-9]|$)/i);
        const shihabW = shihabPathDims ? parseInt(shihabPathDims[1], 10) : 0;
        const mehediH = shihabPathDims ? parseInt(shihabPathDims[2], 10) : 0;
        const shihabRes = mehediPathP ? mehediPathP[1].toLowerCase() : mehediHeightToStandardLabel(shihabW, mehediH);
        const mehediStandaloneAltSet = new Set([shihabGroupKey, shihabCleanSource, shihabCanonicalHlsKey]);
        if (shihabLiMediaM) mehediStandaloneAltSet.add(shihabLiMediaM[1]);
        if (shihabTwMediaM) mehediStandaloneAltSet.add(shihabTwMediaM[1]);
        const shihabStandaloneAlts = Array.from(mehediStandaloneAltSet);
        const shihabSig = `hls_s:${shihabGroupKey}:${shihabRes}`;
        if (!mehediEmittedManifestKeys.has(shihabSig)) {
          shihabMarkEmittedManifestKey(shihabSig);
          shihabPostToContentBridge("mehedi_manifest_streams", {
            shihabStreams: [{
              shihabUrl: shihabCleanSource,
              mehediMimeType: "application/vnd.apple.mpegurl",
              shihabExt: "m3u8",
              shihabCodecs: "",
              mehediTrackRole: "progressive",
              shihabWidth: shihabW,
              mehediHeight: mehediH,
              shihabResolution: shihabRes,
              mehediBandwidth: 0,
              shihabSizeBytes: -1,
              mehediDurationSec: mehediTotalDur > 0 ? Math.round(mehediTotalDur) : 0,
              shihabGroupKey: shihabGroupKey,
              mehediShortcode: "",
              mehediAltIds: shihabStandaloneAlts,
              mehediCapturedReferer: mehediReferer,
              shihabOrigin: shihabOrigin,
              shihabSkipBinaryProbe: true,
              shihabTitleHint: ""
            }],
            mehediGroupKey: shihabGroupKey,
            shihabShortcode: "",
            mehediAltIds: shihabStandaloneAlts,
            mehediTitleHint: ""
          });
        }
      }
      return shihabGroupKey;
    }
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
    if (mehediObj.is_video === true || mehediObj.media_type === 2 || mehediObj.type === "video" || mehediObj.type === "animated_gif") return true;
    if (
      mehediObj.video_id ||
      mehediObj.video_dash_manifest ||
      mehediObj.dash_manifest ||
      mehediObj.dash_manifests ||
      mehediObj.video_versions ||
      mehediObj.progressive_urls ||
      mehediObj.browser_native_hd_url ||
      mehediObj.playable_url ||
      (mehediObj.video_info && Array.isArray(mehediObj.video_info.variants)) ||
      Array.isArray(mehediObj.progressiveStreams) ||
      Array.isArray(mehediObj.adaptiveStreams)
    ) {
      return true;
    }
    return false;
  }

  function shihabExtractTwVideoAssetIdFromUrl(mehediUrl) {
    if (!mehediUrl || typeof mehediUrl !== "string") return "";
    const shihabM = mehediUrl.match(/\/(?:ext_tw_video|ext_tw_video_thumb|amplify_video|amplify_video_thumb|tweet_video|tweet_video_thumb)\/([A-Za-z0-9_-]+)/i);
    return shihabM && shihabM[1] ? shihabM[1] : "";
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
        shihabTweetRestId: "",
        mehediParentCode: "",
        shihabParentUser: "",
        mehediParentCaption: ""
      }
    }];
    let mehediVisited = 0;

    while (shihabStack.length > 0 && mehediVisited < 25000) {
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

      // Reset inherited story context at each distinct FeedUnit / Story / Post / Tweet boundary so sibling posts never bleed!
      const mehediTypeName = String(shihabNode.__typename || "");
      const shihabIsTweetBoundary =
        mehediTypeName === "Tweet" ||
        mehediTypeName === "TweetWithVisibilityResults" ||
        (typeof shihabNode.rest_id === "string" && /^\d{10,}$/.test(shihabNode.rest_id) && shihabNode.legacy && typeof shihabNode.legacy === "object");

      if ((/FeedUnit|Story|Timeline|Post|Tweet/i.test(mehediTypeName) || shihabIsTweetBoundary) && mehediVisited > 1) {
        shihabInherited = {
          shihabStoryVideoId: "",
          shihabTweetRestId: "",
          mehediParentCode: "",
          shihabParentUser: "",
          mehediParentCaption: ""
        };
      }

      const mehediLocalTweetId = (
        shihabIsTweetBoundary && shihabNode.rest_id && /^\d{8,}$/.test(String(shihabNode.rest_id))
          ? String(shihabNode.rest_id)
          : (shihabNode.conversation_id_str && shihabNode.id_str && typeof shihabNode.full_text === "string" && /^\d{8,}$/.test(String(shihabNode.id_str))
              ? String(shihabNode.id_str)
              : "")
      );
      const shihabTweetRestId = mehediLocalTweetId || shihabInherited.shihabTweetRestId || "";

      const mehediLocalCode = String(shihabNode.code || shihabNode.shortcode || "");
      const shihabDirectVideoId = shihabNode.video_id
        ? String(shihabNode.video_id)
        : (shihabIsGenuineVideoNode(shihabNode) && (shihabNode.pk || shihabNode.id || shihabNode.id_str)
            ? String(shihabNode.pk || shihabNode.id || shihabNode.id_str)
            : "");

      const mehediShortcode = mehediLocalCode || shihabInherited.mehediParentCode || "";
      const shihabVideoAssetId = shihabDirectVideoId || shihabInherited.shihabStoryVideoId || "";

      let mehediUsername = "";
      if (shihabNode.user && typeof shihabNode.user.username === "string") {
        mehediUsername = shihabNode.user.username;
      } else if (shihabNode.core && shihabNode.core.user_results && shihabNode.core.user_results.result &&
                 shihabNode.core.user_results.result.legacy && typeof shihabNode.core.user_results.result.legacy.screen_name === "string") {
        mehediUsername = shihabNode.core.user_results.result.legacy.screen_name;
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
      } else if (shihabNode.legacy && typeof shihabNode.legacy.full_text === "string") {
        shihabCaptionTxt = shihabNode.legacy.full_text;
      } else if (typeof shihabNode.full_text === "string") {
        shihabCaptionTxt = shihabNode.full_text;
      } else if (typeof shihabNode.desc === "string" && shihabNode.desc.length > 2) {
        shihabCaptionTxt = shihabNode.desc;
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
        shihabNode.playable_duration_in_ms ? (Number(shihabNode.playable_duration_in_ms) / 1000) : 0
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

      // 4) X / Twitter video_info.variants (progressive MP4 + HLS lineage mapping onto canonical tw:<tweetId> group!)
      if (shihabNode.video_info && Array.isArray(shihabNode.video_info.variants)) {
        const mehediTwMp4List = [];
        const shihabTwHlsList = [];
        const shihabTwDur = shihabNode.video_info.duration_millis ? (Number(shihabNode.video_info.duration_millis) / 1000) : 0;
        const mehediMediaVidId =
          shihabExtractTwVideoAssetIdFromUrl(shihabNode.media_url_https || "") ||
          shihabExtractTwVideoAssetIdFromUrl((shihabNode.video_info.variants[0] && shihabNode.video_info.variants[0].url) || "") ||
          String(shihabNode.id_str || "");
        const shihabResolvedTweetId = shihabTweetRestId || (shihabNode.source_status_id_str ? String(shihabNode.source_status_id_str) : "");
        const mehediTwGroup = shihabResolvedTweetId
          ? `tw:${shihabResolvedTweetId}`
          : (mehediMediaVidId
              ? `twv:${mehediMediaVidId}`
              : (shihabVideoAssetId
                  ? `tw:${shihabVideoAssetId}`
                  : `tw:${shihabFnv1aStringHash((shihabNode.video_info.variants[0] && shihabNode.video_info.variants[0].url) || "")}`));

        const shihabTwAltSet = new Set([...shihabNodeAltIds, mehediTwGroup]);
        if (shihabResolvedTweetId) {
          shihabTwAltSet.add(shihabResolvedTweetId);
          shihabTwAltSet.add(`tw:${shihabResolvedTweetId}`);
        }
        if (mehediMediaVidId) {
          shihabTwAltSet.add(mehediMediaVidId);
          shihabTwAltSet.add(`twv:${mehediMediaVidId}`);
        }
        if (shihabNode.id_str) {
          shihabTwAltSet.add(String(shihabNode.id_str));
        }

        const shihabTwAlts = Array.from(shihabTwAltSet).filter(Boolean);
        const mehediSafeTwRef = shihabGetSafeHttpReferer();
        const shihabSafeTwOrg = shihabGetSafeHttpOrigin();
        for (const shihabVar of shihabNode.video_info.variants) {
          if (!shihabVar || typeof shihabVar.url !== "string" || !shihabVar.url.startsWith("http")) continue;
          const mehediCleanTwUrl = mehediNormalizeHookMediaUrl(shihabVar.url);
          if (!mehediCleanTwUrl) continue;
          const mehediCt = String(shihabVar.content_type || "").toLowerCase();
          mehediSetBoundedMap(shihabMediaUrlToGroupKey, mehediCleanTwUrl, mehediTwGroup, 240);
          mehediSetBoundedMap(shihabMediaUrlToGroupKey, shihabExtractUrlPathKey(mehediCleanTwUrl), mehediTwGroup, 240);

          if (mehediCt.includes("mpegurl") || /\.m3u8(\?|#|$)/i.test(mehediCleanTwUrl)) {
            mehediSetBoundedMap(shihabMediaUrlToGroupKey, shihabCanonicalHlsGroupKeyUrl(mehediCleanTwUrl), mehediTwGroup, 240);
            shihabTwHlsList.push({
              shihabUrl: mehediCleanTwUrl,
              mehediMimeType: "application/vnd.apple.mpegurl",
              shihabExt: "m3u8",
              shihabCodecs: "",
              mehediTrackRole: "progressive",
              shihabWidth: 0,
              mehediHeight: 0,
              shihabResolution: "",
              mehediBandwidth: Number(shihabVar.bitrate || 0),
              shihabSizeBytes: -1,
              mehediDurationSec: shihabTwDur > 0 ? shihabTwDur : 0,
              shihabGroupKey: mehediTwGroup,
              mehediShortcode: shihabResolvedTweetId || mehediMediaVidId || shihabVideoAssetId,
              mehediAltIds: shihabTwAlts,
              mehediCapturedReferer: mehediSafeTwRef,
              shihabOrigin: shihabSafeTwOrg,
              shihabSkipBinaryProbe: true,
              shihabTitleHint: mehediCombinedTitle
            });
            continue;
          }
          const shihabDimMatch = mehediCleanTwUrl.match(/\/(\d{2,4})x(\d{2,4})\//);
          const mehediW = shihabDimMatch ? parseInt(shihabDimMatch[1], 10) : 0;
          const shihabH = shihabDimMatch ? parseInt(shihabDimMatch[2], 10) : 0;
          mehediTwMp4List.push({
            shihabUrl: mehediCleanTwUrl,
            mehediMimeType: mehediCt || "video/mp4",
            shihabExt: "mp4",
            shihabCodecs: "avc1,mp4a",
            mehediTrackRole: "progressive",
            shihabWidth: mehediW,
            mehediHeight: shihabH,
            shihabResolution: mehediHeightToStandardLabel(mehediW, shihabH),
            mehediBandwidth: Number(shihabVar.bitrate || 0),
            shihabSizeBytes: -1,
            mehediDurationSec: shihabTwDur > 0 ? shihabTwDur : 0,
            shihabGroupKey: mehediTwGroup,
            mehediShortcode: shihabResolvedTweetId || mehediMediaVidId || shihabVideoAssetId,
            mehediAltIds: shihabTwAlts,
            mehediCapturedReferer: mehediSafeTwRef,
            shihabOrigin: shihabSafeTwOrg,
            shihabTitleHint: mehediCombinedTitle
          });
        }
        const mehediTwEmitList = mehediTwMp4List.length > 0 ? mehediTwMp4List : shihabTwHlsList;
        if (mehediTwEmitList.length > 0) {
          shihabPostToContentBridge("mehedi_manifest_streams", {
            shihabStreams: mehediTwEmitList,
            mehediGroupKey: mehediTwGroup,
            shihabShortcode: shihabResolvedTweetId || mehediMediaVidId || shihabVideoAssetId,
            mehediAltIds: shihabTwAlts,
            mehediTitleHint: mehediCombinedTitle
          });
        }
      }

      // 5) LinkedIn Voyager progressiveStreams / adaptiveStreams
      if (Array.isArray(shihabNode.progressiveStreams) || Array.isArray(shihabNode.adaptiveStreams)) {
        const mehediLiStreams = [];
        const shihabLiHlsUrlsToMap = [];
        const shihabLiDur = shihabNode.duration ? (Number(shihabNode.duration) > 1000 ? Number(shihabNode.duration) / 1000 : Number(shihabNode.duration)) : mehediNodeDurationSec;
        const shihabLiSeed = String(shihabNode.entityUrn || shihabNode.trackingId || shihabNode.media || "");
        let mehediLiFirstUrl = "";
        const mehediSafeLiRef = shihabGetSafeHttpReferer();
        const shihabSafeLiOrg = shihabGetSafeHttpOrigin();

        if (Array.isArray(shihabNode.progressiveStreams)) {
          for (const shihabPs of shihabNode.progressiveStreams) {
            if (!shihabPs || !Array.isArray(shihabPs.streamingLocations)) continue;
            const mehediW = Number(shihabPs.width || 0);
            const shihabH = Number(shihabPs.height || 0);
            const mehediBw = Number(shihabPs.bitRate || 0);
            const shihabSz = Number(shihabPs.size || -1);
            for (const mehediLoc of shihabPs.streamingLocations) {
              if (!mehediLoc || typeof mehediLoc.url !== "string" || !mehediLoc.url.startsWith("http")) continue;
              const shihabCleanLi = mehediNormalizeHookMediaUrl(mehediLoc.url);
              if (!shihabCleanLi) continue;
              if (!mehediLiFirstUrl) mehediLiFirstUrl = shihabCleanLi;
              const mehediResM = shihabCleanLi.match(/mp4-(\d{3,4}p)/i);
              mehediLiStreams.push({
                shihabUrl: shihabCleanLi,
                mehediMimeType: String(shihabPs.mediaType || "video/mp4"),
                shihabExt: "mp4",
                shihabCodecs: "avc1,mp4a",
                mehediTrackRole: "progressive",
                shihabWidth: mehediW,
                mehediHeight: shihabH,
                shihabResolution: mehediHeightToStandardLabel(mehediW, shihabH) || (mehediResM ? mehediResM[1].toLowerCase() : ""),
                mehediBandwidth: mehediBw,
                shihabSizeBytes: shihabSz > 0 ? shihabSz : -1,
                mehediDurationSec: shihabLiDur > 0 ? shihabLiDur : 0,
                shihabGroupKey: "",
                mehediShortcode: "",
                mehediAltIds: [],
                mehediCapturedReferer: mehediSafeLiRef,
                shihabOrigin: shihabSafeLiOrg,
                shihabTitleHint: mehediCombinedTitle
              });
            }
          }
        }
        if (Array.isArray(shihabNode.adaptiveStreams)) {
          for (const shihabAs of shihabNode.adaptiveStreams) {
            if (!shihabAs || String(shihabAs.protocol || "").toUpperCase() !== "HLS") continue;
            const mehediMps = Array.isArray(shihabAs.masterPlaylists) ? shihabAs.masterPlaylists : [];
            for (const mehediMp of mehediMps) {
              if (!mehediMp || typeof mehediMp.url !== "string" || !mehediMp.url.startsWith("http")) continue;
              const shihabCleanHls = mehediNormalizeHookMediaUrl(mehediMp.url);
              if (!shihabCleanHls) continue;
              shihabLiHlsUrlsToMap.push(shihabCleanHls);
              if (!mehediLiFirstUrl) mehediLiFirstUrl = shihabCleanHls;
              if (mehediLiStreams.length === 0) {
                mehediLiStreams.push({
                  shihabUrl: shihabCleanHls,
                  mehediMimeType: "application/vnd.apple.mpegurl",
                  shihabExt: "m3u8",
                  shihabCodecs: "",
                  mehediTrackRole: "progressive",
                  shihabWidth: 0,
                  mehediHeight: 0,
                  shihabResolution: "",
                  mehediBandwidth: 0,
                  shihabSizeBytes: -1,
                  mehediDurationSec: shihabLiDur > 0 ? shihabLiDur : 0,
                  shihabGroupKey: "",
                  mehediShortcode: "",
                  mehediAltIds: [],
                  mehediCapturedReferer: mehediSafeLiRef,
                  shihabOrigin: shihabSafeLiOrg,
                  shihabSkipBinaryProbe: true,
                  shihabTitleHint: mehediCombinedTitle
                });
              }
            }
          }
        }
        if (mehediLiStreams.length > 0) {
          const shihabLiAssetMatch = mehediLiFirstUrl.match(/\/playlist\/vid\/(?:v\d+\/)?([A-Za-z0-9_-]{6,})\//i);
          const mehediLiGroup = shihabLiAssetMatch
            ? `li:${shihabLiAssetMatch[1]}`
            : `li:${shihabFnv1aStringHash(shihabLiSeed || shihabExtractUrlPathKey(mehediLiFirstUrl))}`;
          const shihabLiAlts = [mehediLiGroup];
          if (shihabLiAssetMatch) shihabLiAlts.push(shihabLiAssetMatch[1]);
          for (const shihabHlsU of shihabLiHlsUrlsToMap) {
            mehediSetBoundedMap(shihabMediaUrlToGroupKey, shihabHlsU, mehediLiGroup, 240);
            mehediSetBoundedMap(shihabMediaUrlToGroupKey, shihabCanonicalHlsGroupKeyUrl(shihabHlsU), mehediLiGroup, 240);
            shihabLiAlts.push(shihabHlsU);
          }
          for (const mehediItem of mehediLiStreams) {
            mehediItem.shihabGroupKey = mehediLiGroup;
            mehediItem.mehediAltIds = shihabLiAlts;
            mehediSetBoundedMap(shihabMediaUrlToGroupKey, mehediItem.shihabUrl, mehediLiGroup, 240);
            mehediSetBoundedMap(shihabMediaUrlToGroupKey, shihabExtractUrlPathKey(mehediItem.shihabUrl), mehediLiGroup, 240);
          }
          const shihabLiSig = `li:${mehediLiGroup}:${mehediLiStreams.length}`;
          if (!mehediEmittedManifestKeys.has(shihabLiSig)) {
            shihabMarkEmittedManifestKey(shihabLiSig);
            shihabPostToContentBridge("mehedi_manifest_streams", {
              shihabStreams: mehediLiStreams,
              mehediGroupKey: mehediLiGroup,
              shihabShortcode: shihabLiAssetMatch ? shihabLiAssetMatch[1] : "",
              mehediAltIds: shihabLiAlts,
              mehediTitleHint: mehediCombinedTitle
            });
          }
        }
      }

      // 6) TikTok bitrateInfo / playAddr / downloadAddr
      if (shihabNode.video && typeof shihabNode.video === "object" && (shihabNode.video.playAddr || shihabNode.video.downloadAddr || Array.isArray(shihabNode.video.bitrateInfo))) {
        const mehediTtStreams = [];
        const shihabTtId = String(shihabNode.id || shihabNode.video.id || "");
        const mehediTtGroup = shihabTtId ? `tt:${shihabTtId}` : `tt:${shihabFnv1aStringHash(String(shihabNode.video.playAddr || ""))}`;
        const shihabTtDur = Number(shihabNode.video.duration || 0);
        const mehediTtAlts = shihabTtId ? [mehediTtGroup, shihabTtId] : [mehediTtGroup];
        const shihabTtRef = shihabGetSafeHttpReferer();
        const mehediTtOrg = shihabGetSafeHttpOrigin();
        const mehediPushTtUrl = (rawU, w, h, bw, sz) => {
          if (typeof rawU !== "string" || !rawU.startsWith("http")) return;
          const cleanU = mehediNormalizeHookMediaUrl(rawU);
          if (!cleanU) return;
          mehediTtStreams.push({
            shihabUrl: cleanU,
            mehediMimeType: "video/mp4",
            shihabExt: "mp4",
            shihabCodecs: "avc1,mp4a",
            mehediTrackRole: "progressive",
            shihabWidth: Number(w || shihabNode.video.width || 0),
            mehediHeight: Number(h || shihabNode.video.height || 0),
            shihabResolution: mehediHeightToStandardLabel(w || shihabNode.video.width, h || shihabNode.video.height),
            mehediBandwidth: Number(bw || 0),
            shihabSizeBytes: Number(sz || -1),
            mehediDurationSec: shihabTtDur > 0 ? shihabTtDur : 0,
            shihabGroupKey: mehediTtGroup,
            mehediShortcode: shihabTtId,
            mehediAltIds: mehediTtAlts,
            mehediCapturedReferer: shihabTtRef,
            shihabOrigin: mehediTtOrg,
            shihabTitleHint: mehediCombinedTitle
          });
        };
        if (Array.isArray(shihabNode.video.bitrateInfo)) {
          for (const mehediBi of shihabNode.video.bitrateInfo) {
            const shihabPa = mehediBi && mehediBi.PlayAddr;
            if (shihabPa && Array.isArray(shihabPa.UrlList) && shihabPa.UrlList[0]) {
              mehediPushTtUrl(shihabPa.UrlList[0], shihabPa.Width, shihabPa.Height, mehediBi.Bitrate, shihabPa.DataSize);
            }
          }
        }
        if (typeof shihabNode.video.downloadAddr === "string") mehediPushTtUrl(shihabNode.video.downloadAddr, shihabNode.video.width, shihabNode.video.height, 0, -1);
        if (typeof shihabNode.video.playAddr === "string") mehediPushTtUrl(shihabNode.video.playAddr, shihabNode.video.width, shihabNode.video.height, 0, -1);
        if (mehediTtStreams.length > 0) {
          const shihabTtSig = `tt:${mehediTtGroup}:${mehediTtStreams.length}`;
          if (!mehediEmittedManifestKeys.has(shihabTtSig)) {
            shihabMarkEmittedManifestKey(shihabTtSig);
            shihabPostToContentBridge("mehedi_manifest_streams", {
              shihabStreams: mehediTtStreams,
              mehediGroupKey: mehediTtGroup,
              shihabShortcode: shihabTtId,
              mehediAltIds: mehediTtAlts,
              mehediTitleHint: mehediCombinedTitle
            });
          }
        }
      }

      // 7) Vimeo player config (matches BOTH root { video, request: { files } } and standalone { files })
      const shihabVmFiles =
        (shihabNode.request && typeof shihabNode.request === "object" && shihabNode.request.files && typeof shihabNode.request.files === "object")
          ? shihabNode.request.files
          : (shihabNode.files && typeof shihabNode.files === "object" ? shihabNode.files : null);
      if (shihabVmFiles && (Array.isArray(shihabVmFiles.progressive) || shihabVmFiles.hls)) {
        const mehediVmStreams = [];
        const shihabVmId = String((shihabNode.video && shihabNode.video.id) || shihabVideoAssetId || "");
        const mehediVmGroup = shihabVmId ? `vm:${shihabVmId}` : `vm:${shihabFnv1aStringHash(location.pathname)}`;
        const shihabVmTitle = (shihabNode.video && typeof shihabNode.video.title === "string" && shihabNode.video.title) || mehediCombinedTitle;
        const mehediVmDur = Number((shihabNode.video && shihabNode.video.duration) || 0);
        const shihabVmRef = shihabGetSafeHttpReferer();
        const mehediVmOrg = shihabGetSafeHttpOrigin();
        const shihabVmAlts = shihabVmId ? [mehediVmGroup, shihabVmId] : [mehediVmGroup];

        if (shihabVmFiles.hls && shihabVmFiles.hls.cdns) {
          const shihabDefCdn = shihabVmFiles.hls.default_cdn || Object.keys(shihabVmFiles.hls.cdns)[0];
          const mehediCdnObj = shihabDefCdn ? shihabVmFiles.hls.cdns[shihabDefCdn] : null;
          if (mehediCdnObj && typeof mehediCdnObj.url === "string" && mehediCdnObj.url.startsWith("http")) {
            const shihabCleanVmHls = mehediNormalizeHookMediaUrl(mehediCdnObj.url);
            if (shihabCleanVmHls) {
              mehediSetBoundedMap(shihabMediaUrlToGroupKey, shihabCleanVmHls, mehediVmGroup, 240);
              mehediSetBoundedMap(shihabMediaUrlToGroupKey, shihabCanonicalHlsGroupKeyUrl(shihabCleanVmHls), mehediVmGroup, 240);
            }
          }
        }
        if (Array.isArray(shihabVmFiles.progressive)) {
          for (const mehediP of shihabVmFiles.progressive) {
            if (!mehediP || typeof mehediP.url !== "string" || !mehediP.url.startsWith("http")) continue;
            mehediVmStreams.push({
              shihabUrl: mehediNormalizeHookMediaUrl(mehediP.url),
              mehediMimeType: String(mehediP.mime || "video/mp4"),
              shihabExt: "mp4",
              shihabCodecs: "",
              mehediTrackRole: "progressive",
              shihabWidth: Number(mehediP.width || 0),
              mehediHeight: Number(mehediP.height || 0),
              shihabResolution: String(mehediP.quality || "") || mehediHeightToStandardLabel(mehediP.width, mehediP.height),
              mehediBandwidth: 0,
              shihabSizeBytes: -1,
              mehediDurationSec: mehediVmDur,
              shihabGroupKey: mehediVmGroup,
              mehediShortcode: shihabVmId,
              mehediAltIds: shihabVmAlts,
              mehediCapturedReferer: shihabVmRef,
              shihabOrigin: mehediVmOrg,
              shihabTitleHint: shihabVmTitle
            });
          }
        }
        if (mehediVmStreams.length === 0 && shihabVmFiles.hls && shihabVmFiles.hls.cdns) {
          const shihabDefCdn = shihabVmFiles.hls.default_cdn || Object.keys(shihabVmFiles.hls.cdns)[0];
          const mehediCdnObj = shihabDefCdn ? shihabVmFiles.hls.cdns[shihabDefCdn] : null;
          if (mehediCdnObj && typeof mehediCdnObj.url === "string" && mehediCdnObj.url.startsWith("http")) {
            mehediVmStreams.push({
              shihabUrl: mehediNormalizeHookMediaUrl(mehediCdnObj.url),
              mehediMimeType: "application/vnd.apple.mpegurl",
              shihabExt: "m3u8",
              shihabCodecs: "",
              mehediTrackRole: "progressive",
              shihabWidth: 0,
              mehediHeight: 0,
              shihabResolution: "",
              mehediBandwidth: 0,
              shihabSizeBytes: -1,
              mehediDurationSec: mehediVmDur,
              shihabGroupKey: mehediVmGroup,
              mehediShortcode: shihabVmId,
              mehediAltIds: shihabVmAlts,
              mehediCapturedReferer: shihabVmRef,
              shihabOrigin: mehediVmOrg,
              shihabSkipBinaryProbe: true,
              shihabTitleHint: shihabVmTitle
            });
          }
        }
        if (mehediVmStreams.length > 0) {
          const shihabVmSig = `vm:${mehediVmGroup}:${mehediVmStreams.length}`;
          if (!mehediEmittedManifestKeys.has(shihabVmSig)) {
            shihabMarkEmittedManifestKey(shihabVmSig);
            shihabPostToContentBridge("mehedi_manifest_streams", {
              shihabStreams: mehediVmStreams,
              mehediGroupKey: mehediVmGroup,
              shihabShortcode: shihabVmId,
              mehediAltIds: shihabVmAlts,
              mehediTitleHint: shihabVmTitle
            });
          }
        }
      }

      const mehediNextParentCtx = {
        shihabStoryVideoId: shihabVideoAssetId,
        shihabTweetRestId: shihabTweetRestId,
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

    if (shihabTrim.startsWith("#EXTM3U")) {
      mehediParseHlsPlaylistText(shihabTrim, mehediSourceUrl || location.href);
      return;
    }

    if (shihabTrim.startsWith("<") && shihabText.includes("<MPD")) {
      shihabParseDashMpdXml(shihabText, "", "", "", mehediSourceUrl || location.href, []);
      return;
    }

    // Require a fast video/platform keyword pre-check before JSON.parse + 25k walk so unrelated API JSON never burns main-thread CPU
    const mehediHasMediaKeywords =
      shihabTrim.includes("video_dash_manifest") ||
      shihabTrim.includes("dash_manifests") ||
      shihabTrim.includes("manifest_xml") ||
      shihabTrim.includes("progressive_urls") ||
      shihabTrim.includes("video_versions") ||
      shihabTrim.includes("streamingData") ||
      shihabTrim.includes("browser_native_hd_url") ||
      shihabTrim.includes("playable_url") ||
      shihabTrim.includes("video_info") ||
      shihabTrim.includes("progressiveStreams") ||
      shihabTrim.includes("adaptiveStreams") ||
      shihabTrim.includes("bitrateInfo") ||
      shihabTrim.includes("playAddr") ||
      shihabTrim.includes("downloadAddr") ||
      shihabTrim.includes("\"progressive\"") ||
      shihabTrim.includes("\"hls\"") ||
      shihabTrim.includes(".m3u8");

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

      // A2) Direct player attributes & instances (LinkedIn data-sources, X poster thumb ID, Video.js, Plyr, JWPlayer, Shreddit)
      const mehediPosterVal = shihabVideoEl.getAttribute("poster") || shihabVideoEl.poster || "";
      if (mehediPosterVal) {
        const shihabTwThumbId = shihabExtractTwVideoAssetIdFromUrl(mehediPosterVal);
        if (shihabTwThumbId) {
          mehediAltIds.add(shihabTwThumbId);
          mehediAltIds.add(`twv:${shihabTwThumbId}`);
        }
      }

      // LinkedIn / generic data-sources JSON attribute on <video> or immediate player container
      const shihabDsHolder = shihabVideoEl.hasAttribute("data-sources")
        ? shihabVideoEl
        : (shihabVideoEl.parentElement && typeof shihabVideoEl.parentElement.closest === "function" ? shihabVideoEl.parentElement.closest("[data-sources]") : null);
      if (shihabDsHolder && shihabDsHolder.querySelectorAll("video").length <= 1) {
        const mehediDsRaw = shihabDsHolder.getAttribute("data-sources") || "";
        if (mehediDsRaw.trim().startsWith("[")) {
          try {
            const shihabDsArr = JSON.parse(mehediDsRaw);
            const mehediDsEmit = [];
            let shihabDsGroup = `dom:${mehediVidUid}`;
            const shihabDsAlts = [mehediVidUid];
            const mehediDsRef = shihabGetSafeHttpReferer();
            const shihabDsOrg = shihabGetSafeHttpOrigin();
            for (const mehediSrcObj of shihabDsArr) {
              const shihabSrcU = mehediSrcObj && typeof mehediSrcObj.src === "string" ? mehediNormalizeHookMediaUrl(mehediSrcObj.src) : "";
              if (!shihabSrcU) continue;
              const shihabLiAssetM = shihabSrcU.match(/\/playlist\/vid\/(?:v\d+\/)?([A-Za-z0-9_-]{6,})\//i);
              if (shihabLiAssetM && shihabDsGroup.startsWith("dom:")) {
                shihabDsGroup = `li:${shihabLiAssetM[1]}`;
                shihabDsAlts.push(shihabLiAssetM[1]);
              }
              shihabBoundUrls.add(shihabSrcU);
              const mehediTypeStr = String(mehediSrcObj.type || "video/mp4").toLowerCase();
              const shihabIsHlsItem = mehediTypeStr.includes("mpegurl") || /\.m3u8(\?|#|$)|\/hls-manifest\b/i.test(shihabSrcU);
              const mehediResM = shihabSrcU.match(/(?:mp4-|\/)(\d{3,4}p)(?:[^0-9]|$)/i);
              mehediDsEmit.push({
                shihabUrl: shihabSrcU,
                mehediMimeType: mehediTypeStr || "video/mp4",
                shihabExt: shihabIsHlsItem ? "m3u8" : "mp4",
                shihabCodecs: "",
                mehediTrackRole: "progressive",
                shihabWidth: Number(mehediSrcObj.width || 0),
                mehediHeight: Number(mehediSrcObj.height || 0),
                shihabResolution: mehediResM ? mehediResM[1].toLowerCase() : mehediHeightToStandardLabel(mehediSrcObj.width, mehediSrcObj.height),
                mehediBandwidth: Number(mehediSrcObj["data-bitrate"] || mehediSrcObj.bitrate || 0),
                shihabSizeBytes: -1,
                mehediDurationSec: Number(shihabVideoEl.duration) > 0 && Number.isFinite(shihabVideoEl.duration) ? shihabVideoEl.duration : 0,
                shihabGroupKey: "",
                mehediShortcode: mehediVidUid,
                mehediAltIds: [],
                mehediCapturedReferer: mehediDsRef,
                shihabOrigin: shihabDsOrg,
                shihabSkipBinaryProbe: shihabIsHlsItem,
                shihabTitleHint: mehediTitleHint
              });
            }
            if (mehediDsEmit.length > 0) {
              shihabDsAlts.push(shihabDsGroup);
              for (const mehediIt of mehediDsEmit) {
                mehediIt.shihabGroupKey = shihabDsGroup;
                mehediIt.mehediAltIds = shihabDsAlts;
              }
              shihabGroupKeys.add(shihabDsGroup);
              mehediAltIds.add(shihabDsGroup);
              const shihabDsSig = `ds:${shihabDsGroup}:${mehediDsEmit.length}`;
              if (!mehediEmittedManifestKeys.has(shihabDsSig)) {
                shihabMarkEmittedManifestKey(shihabDsSig);
                shihabPostToContentBridge("mehedi_manifest_streams", {
                  shihabStreams: mehediDsEmit,
                  mehediGroupKey: shihabDsGroup,
                  shihabShortcode: mehediVidUid,
                  mehediAltIds: shihabDsAlts,
                  mehediTitleHint: mehediTitleHint
                });
              }
            }
          } catch (_) {}
        }
      }

      // Video.js / Plyr / JWPlayer / custom data-src attributes on this <video>
      const mehediPlayerUrls = [];
      for (const shihabAttr of ["data-src", "data-hls", "data-video-url", "data-stream-url"]) {
        const mehediVal = shihabVideoEl.getAttribute(shihabAttr);
        if (mehediVal && mehediVal.startsWith("http")) mehediPlayerUrls.push(mehediVal);
      }
      try {
        const shihabVjsHost = shihabVideoEl.player || (shihabVideoEl.closest && shihabVideoEl.closest(".video-js") && shihabVideoEl.closest(".video-js").player);
        if (shihabVjsHost && typeof shihabVjsHost.currentSources === "function") {
          const mehediVjsSrcs = shihabVjsHost.currentSources() || [];
          for (const shihabS of mehediVjsSrcs) {
            if (shihabS && typeof shihabS.src === "string" && shihabS.src.startsWith("http")) {
              mehediPlayerUrls.push(shihabS.src);
            }
          }
        }
      } catch (_) {}
      try {
        if (typeof window.jwplayer === "function") {
          const shihabJwContainer = shihabVideoEl.closest(".jwplayer, [id]");
          const mehediJwInst = (shihabJwContainer && shihabJwContainer.id) ? window.jwplayer(shihabJwContainer.id) : window.jwplayer();
          if (mehediJwInst && typeof mehediJwInst.getPlaylistItem === "function") {
            const shihabItem = mehediJwInst.getPlaylistItem();
            if (shihabItem) {
              if (typeof shihabItem.file === "string" && shihabItem.file.startsWith("http")) mehediPlayerUrls.push(shihabItem.file);
              if (Array.isArray(shihabItem.sources)) {
                for (const mehediJs of shihabItem.sources) {
                  if (mehediJs && typeof mehediJs.file === "string" && mehediJs.file.startsWith("http")) mehediPlayerUrls.push(mehediJs.file);
                }
              }
            }
          }
        }
      } catch (_) {}
      for (const mehediPu of mehediPlayerUrls) {
        const shihabCleanPu = mehediNormalizeHookMediaUrl(mehediPu);
        if (shihabCleanPu) {
          shihabBoundUrls.add(shihabCleanPu);
          const mehediKnownGrp = shihabMediaUrlToGroupKey.get(shihabCleanPu) || shihabMediaUrlToGroupKey.get(shihabExtractUrlPathKey(shihabCleanPu));
          if (mehediKnownGrp) shihabGroupKeys.add(mehediKnownGrp);
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
            mehediProps.tweet,
            mehediProps.source,
            mehediProps.videoData,
            mehediProps.playerData,
            mehediProps.playerState,
            mehediProps.clip,
            mehediProps.item,
            mehediProps.videoDeliveryResponseResult
          ];

          for (const mehediBag of shihabCandidateBags) {
            if (!mehediBag || typeof mehediBag !== "object") continue;

            // X / Twitter tweet IDs directly on Tweet-shaped React props (NEVER use conversation_id_str or ancestor User rest_id!)
            const mehediIsTweetShapedBag = Boolean(
              mehediBag.__typename === "Tweet" ||
              mehediBag.tweetId ||
              mehediBag.statusId ||
              mehediBag.full_text ||
              mehediBag.video_info ||
              mehediBag.extended_entities ||
              (mehediBag.legacy && typeof mehediBag.legacy === "object" && (mehediBag.legacy.full_text || mehediBag.legacy.extended_entities))
            );
            if (mehediIsTweetShapedBag) {
              for (const shihabTwField of ["rest_id", "tweetId", "statusId"]) {
                if (mehediBag[shihabTwField] && /^\d{10,}$/.test(String(mehediBag[shihabTwField]))) {
                  const mehediTwStr = String(mehediBag[shihabTwField]);
                  mehediAltIds.add(mehediTwStr);
                  mehediAltIds.add(`tw:${mehediTwStr}`);
                  if (shihabGroupKeys.size === 0 && (location.hostname.includes("x.com") || location.hostname.includes("twitter.com"))) {
                    shihabGroupKeys.add(`tw:${mehediTwStr}`);
                  }
                }
              }
            }
            if (mehediBag.video_info && Array.isArray(mehediBag.video_info.variants)) {
              for (const shihabTv of mehediBag.video_info.variants) {
                if (shihabTv && typeof shihabTv.url === "string" && shihabTv.url.startsWith("http")) {
                  const mehediNormTv = mehediNormalizeHookMediaUrl(shihabTv.url);
                  if (mehediNormTv) {
                    shihabBoundUrls.add(mehediNormTv);
                    const shihabMappedTw = shihabMediaUrlToGroupKey.get(mehediNormTv) || shihabMediaUrlToGroupKey.get(shihabExtractUrlPathKey(mehediNormTv));
                    if (shihabMappedTw) shihabGroupKeys.add(shihabMappedTw);
                  }
                }
              }
            }

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

  // 1. Hook URL.createObjectURL, HTMLMediaElement.src/srcObject & MediaSource / SourceBuffer
  try {
    const shihabOrigCreateObjUrl = URL.createObjectURL;
    if (typeof shihabOrigCreateObjUrl === "function") {
      URL.createObjectURL = function (mehediObj) {
        const shihabBlobUrl = shihabOrigCreateObjUrl.apply(this, arguments);
        try {
          const shihabIsMs =
            (typeof MediaSource !== "undefined" && mehediObj instanceof MediaSource) ||
            (typeof ManagedMediaSource !== "undefined" && mehediObj instanceof ManagedMediaSource);
          if (shihabIsMs) {
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

    // Hook HTMLMediaElement.prototype.src & srcObject non-destructively to capture programmatic direct/MSE URL assignments
    if (typeof HTMLMediaElement !== "undefined" && HTMLMediaElement.prototype) {
      const shihabSrcDesc = Object.getOwnPropertyDescriptor(HTMLMediaElement.prototype, "src");
      if (shihabSrcDesc && typeof shihabSrcDesc.set === "function" && shihabSrcDesc.configurable) {
        Object.defineProperty(HTMLMediaElement.prototype, "src", {
          configurable: true,
          enumerable: shihabSrcDesc.enumerable,
          get: shihabSrcDesc.get,
          set: function (mehediVal) {
            try {
              if (this && this.tagName === "VIDEO" && typeof mehediVal === "string" && mehediVal.trim() && !/^(?:blob:|data:|javascript:|about:)/i.test(mehediVal.trim())) {
                const shihabNormSrc = mehediNormalizeHookMediaUrl(mehediVal);
                if (shihabNormSrc && !shihabIsPoisonedYouTubeWebUrl(shihabNormSrc)) {
                  const mehediUid = shihabEnsureVideoUidOnElement(this);
                  const shihabKnownGrp = shihabMediaUrlToGroupKey.get(shihabNormSrc) || shihabMediaUrlToGroupKey.get(shihabExtractUrlPathKey(shihabNormSrc)) || "";
                  shihabPostToContentBridge("shihab_video_element_identity", {
                    shihabVidUid: mehediUid,
                    mehediGroupKeys: shihabKnownGrp ? [shihabKnownGrp] : [],
                    shihabAltIds: [],
                    mehediBoundUrls: [shihabNormSrc],
                    shihabTitleHint: ""
                  });
                }
              }
            } catch (_) {}
            return shihabSrcDesc.set.call(this, mehediVal);
          }
        });
      }
      const shihabSrcObjDesc = Object.getOwnPropertyDescriptor(HTMLMediaElement.prototype, "srcObject");
      if (shihabSrcObjDesc && typeof shihabSrcObjDesc.set === "function" && shihabSrcObjDesc.configurable) {
        Object.defineProperty(HTMLMediaElement.prototype, "srcObject", {
          configurable: true,
          enumerable: shihabSrcObjDesc.enumerable,
          get: shihabSrcObjDesc.get,
          set: function (mehediObj) {
            try {
              if (this && this.tagName === "VIDEO" && mehediObj) {
                const mehediUid = shihabEnsureVideoUidOnElement(this);
                if (mehediUid) shihabMediaSourceToVideoUid.set(mehediObj, mehediUid);
              }
            } catch (_) {}
            return shihabSrcObjDesc.set.call(this, mehediObj);
          }
        });
      }
    }

    const mehediInstallMsProtoHook = (shihabMsCtor) => {
      if (!shihabMsCtor || !shihabMsCtor.prototype || typeof shihabMsCtor.prototype.addSourceBuffer !== "function") return;
      if (shihabMsCtor.prototype.__shihabHookedAddSb) return;
      shihabMsCtor.prototype.__shihabHookedAddSb = true;
      const mehediOrigAddSb = shihabMsCtor.prototype.addSourceBuffer;
      shihabMsCtor.prototype.addSourceBuffer = function (shihabMimeCodec) {
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
                  const shihabLiveUid = shihabMediaSourceToVideoUid.get(shihabMeta.mehediMediaSource) || "";
                  if (mehediLiveBlob || shihabLiveUid) {
                    if (mehediLiveBlob) shihabMeta.shihabBlobUrl = mehediLiveBlob;

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
                        if (mehediLiveBlob) {
                          shihabPostToContentBridge("shihab_mse_blob_binding", {
                            shihabBlobUrl: mehediLiveBlob,
                            mehediStreamUrl: shihabExactMatchedUrl,
                            shihabTrackKind: shihabMeta.shihabTrackKind,
                            mehediMimeCodec: shihabMeta.mehediMimeCodec,
                            mehediExactByteMatch: true
                          });
                        }
                        if (shihabLiveUid) {
                          const mehediGrp = shihabMediaUrlToGroupKey.get(shihabExactMatchedUrl) || "";
                          shihabPostToContentBridge("shihab_video_element_identity", {
                            shihabVidUid: shihabLiveUid,
                            mehediGroupKeys: mehediGrp ? [mehediGrp] : [],
                            shihabAltIds: [],
                            mehediBoundUrls: [shihabExactMatchedUrl],
                            shihabTitleHint: ""
                          });
                        }
                      }
                    } else if (shihabMeta.shihabExactReportedUrls.size === 0) {
                      const shihabNow = Date.now();
                      const mehediCandidate = shihabRecentFetchedSlices.find(
                        (shihabItem) =>
                          shihabNow - shihabItem.shihabAtMs < 8000 &&
                          (!shihabItem.mehediTrackType || shihabItem.mehediTrackType === shihabMeta.shihabTrackKind)
                      );
                      if (mehediCandidate && !shihabMeta.mehediReportedUrls.has(mehediCandidate.shihabUrl)) {
                        shihabMeta.mehediReportedUrls.add(mehediCandidate.shihabUrl);
                        if (mehediLiveBlob) {
                          shihabPostToContentBridge("shihab_mse_blob_binding", {
                            shihabBlobUrl: mehediLiveBlob,
                            mehediStreamUrl: mehediCandidate.shihabUrl,
                            shihabTrackKind: shihabMeta.shihabTrackKind,
                            mehediMimeCodec: shihabMeta.mehediMimeCodec,
                            mehediExactByteMatch: false
                          });
                        } else if (shihabLiveUid) {
                          const mehediCandGrp = shihabMediaUrlToGroupKey.get(mehediCandidate.shihabUrl) || "";
                          shihabPostToContentBridge("shihab_video_element_identity", {
                            shihabVidUid: shihabLiveUid,
                            mehediGroupKeys: mehediCandGrp ? [mehediCandGrp] : [],
                            shihabAltIds: [],
                            mehediBoundUrls: [mehediCandidate.shihabUrl],
                            shihabTitleHint: ""
                          });
                        }
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
    };

    if (typeof MediaSource !== "undefined") mehediInstallMsProtoHook(MediaSource);
    if (typeof ManagedMediaSource !== "undefined") mehediInstallMsProtoHook(ManagedMediaSource);
  } catch (_) {}

  // Bounded text reader for fetch responses (strictly caps chunked/SSE/infinite/binary streams in memory!)
  function mehediReadBoundedTextFromFetchResp(shihabRespClone, mehediMaxBytes, shihabBailOnBinary, shihabOnText) {
    try {
      if (!shihabRespClone) return;
      if (!shihabRespClone.body || typeof shihabRespClone.body.getReader !== "function") {
        if (typeof shihabRespClone.text === "function") {
          shihabRespClone.text().then((mehediT) => {
            if (mehediT && mehediT.length <= mehediMaxBytes) shihabOnText(mehediT);
          }).catch(() => {});
        }
        return;
      }
      const shihabReader = shihabRespClone.body.getReader();
      const mehediParts = [];
      let shihabTotal = 0;
      const shihabPump = () => {
        shihabReader.read().then((mehediRes) => {
          if (mehediRes && mehediRes.value && mehediRes.value.byteLength > 0) {
            mehediParts.push(mehediRes.value);
            shihabTotal += mehediRes.value.byteLength;
            if (shihabBailOnBinary && mehediParts.length === 1 && mehediRes.value.byteLength >= 7) {
              const b0 = mehediRes.value[0];
              if (b0 !== 0x23 && b0 !== 0x3c && b0 !== 0x7b && b0 !== 0x5b && b0 !== 0x20 && b0 !== 0x0a && b0 !== 0x0d && b0 !== 0x09 && b0 !== 0xef && b0 !== 0x66) {
                try { shihabReader.cancel().catch(() => {}); } catch (_) {}
                return;
              }
            }
            if (!mehediRes.done && shihabTotal < mehediMaxBytes) {
              shihabPump();
              return;
            }
          }
          try { shihabReader.cancel().catch(() => {}); } catch (_) {}
          if (shihabTotal >= 20 && shihabTotal <= mehediMaxBytes + 65536) {
            const shihabJoined = new Uint8Array(shihabTotal);
            let mehediOff = 0;
            for (const shihabP of mehediParts) {
              shihabJoined.set(shihabP, mehediOff);
              mehediOff += shihabP.byteLength;
            }
            const mehediDecoded = new TextDecoder("utf-8", { fatal: false }).decode(shihabJoined);
            shihabOnText(mehediDecoded);
          }
        }).catch(() => {
          try { shihabReader.cancel().catch(() => {}); } catch (_) {}
        });
      };
      shihabPump();
    } catch (_) {}
  }

  // 2. Hook window.fetch non-destructively (reads up to 128 KB of media clone for ftyp/moof box fingerprinting + byte-capped HLS/DASH/JSON manifests)
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
            const mehediLowerCt = shihabCt.toLowerCase();
            const shihabClHdr = parseInt((shihabResp.headers && shihabResp.headers.get("content-length")) || "-1", 10);

            const shihabIsExplicitManifest =
              mehediLowerCt.includes("mpegurl") ||
              mehediLowerCt.includes("dash+xml") ||
              /\.(m3u8|m3u|mpd)(\?|#|$)/i.test(mehediFinalUrl) ||
              /\/hls[-_]?manifest\//i.test(mehediFinalUrl);

            if (shihabLooksLikeMediaSliceUrl(mehediFinalUrl, mehediLowerCt)) {
              mehediRememberFetchedSlice(mehediFinalUrl, mehediLowerCt);
              // Fingerprint binary fMP4/MP4 slices only (skip explicit manifests and text/* so we never double-clone!)
              if (!shihabIsExplicitManifest && !mehediLowerCt.startsWith("text/")) {
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
                          mehediIndexChunkBytesForUrl(mehediFinalUrl, shihabJoined, mehediLowerCt);
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
            }

            const mehediIsJsonOrXml =
              !mehediLowerCt.includes("text/event-stream") &&
              (mehediLowerCt.includes("json") ||
               mehediLowerCt.includes("xml") ||
               mehediLowerCt.includes("javascript") ||
               /\/(graphql|api\/v1\/|youtubei\/v1\/player|ajax\/bz|voyager\/api)/i.test(mehediFinalUrl));

            const shihabIsAmbiguousPlaylist =
              !shihabIsExplicitManifest &&
              !mehediIsJsonOrXml &&
              (shihabClHdr <= 0 || shihabClHdr <= 524288) &&
              (mehediLowerCt === "" || mehediLowerCt.includes("text/plain") || mehediLowerCt.includes("octet-stream") || (mehediLowerCt.startsWith("text/") && !mehediLowerCt.includes("text/vtt"))) &&
              !mehediLowerCt.startsWith("image/") &&
              !mehediLowerCt.startsWith("font/") &&
              !mehediLowerCt.includes("text/css") &&
              !mehediLowerCt.includes("text/html") &&
              !mehediLowerCt.includes("text/vtt") &&
              /(m3u8|m3u|hls|manifest|playlist|master|index|chunklist|stream|video|media)/i.test(mehediFinalUrl);

            if (shihabIsExplicitManifest || mehediIsJsonOrXml) {
              if (shihabClHdr <= 0 || shihabClHdr <= 4 * 1024 * 1024) {
                mehediReadBoundedTextFromFetchResp(shihabResp.clone(), shihabIsExplicitManifest ? 524288 : 4 * 1024 * 1024, false, (mehediBodyText) => {
                  mehediInspectTextBodyIfManifest(mehediBodyText, mehediFinalUrl, mehediLowerCt);
                });
              }
            } else if (shihabIsAmbiguousPlaylist) {
              mehediReadBoundedTextFromFetchResp(shihabResp.clone(), 262144, true, (mehediPeekText) => {
                mehediInspectTextBodyIfManifest(mehediPeekText, mehediFinalUrl, mehediLowerCt);
              });
            }
          } catch (_) {}
        }).catch(() => {});

        return mehediPromise;
      };
    }
  } catch (_) {}

  // 3. Hook XMLHttpRequest non-destructively (handles HLS.js, Video.js, JWPlayer, Shaka, X, LinkedIn!)
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
            if (typeof this.status === "number" && this.status !== 0 && (this.status < 200 || this.status >= 300)) {
              return;
            }
            const mehediFinalUrl = this.responseURL || this.__shihabReqUrl || "";
            const shihabCt = (this.getResponseHeader && this.getResponseHeader("content-type")) || "";
            if (shihabLooksLikeMediaSliceUrl(mehediFinalUrl, shihabCt)) {
              mehediRememberFetchedSlice(mehediFinalUrl, shihabCt);
              if (this.responseType === "arraybuffer" && this.response instanceof ArrayBuffer && this.response.byteLength >= 28) {
                mehediIndexChunkBytesForUrl(mehediFinalUrl, new Uint8Array(this.response, 0, Math.min(196608, this.response.byteLength)), shihabCt);
              }
            }
            if ((this.responseType === "" || this.responseType === "text") && typeof this.responseText === "string") {
              mehediInspectTextBodyIfManifest(this.responseText, mehediFinalUrl, shihabCt);
            } else if (this.responseType === "json" && this.response && typeof this.response === "object") {
              shihabWalkJsonForMediaMetadata(this.response);
            } else if (this.responseType === "arraybuffer" && this.response instanceof ArrayBuffer && this.response.byteLength >= 20 && this.response.byteLength <= 524288) {
              // Some obfuscated HLS.js loaders request playlists with responseType="arraybuffer"
              const shihabHeadBytes = new Uint8Array(this.response, 0, Math.min(16, this.response.byteLength));
              if (shihabHeadBytes[0] === 0x23 && shihabHeadBytes[1] === 0x45 && shihabHeadBytes[2] === 0x58 && shihabHeadBytes[3] === 0x54) {
                const mehediHlsStr = new TextDecoder("utf-8", { fatal: false }).decode(new Uint8Array(this.response));
                mehediInspectTextBodyIfManifest(mehediHlsStr, mehediFinalUrl, shihabCt);
              }
            }
          } catch (_) {}
        });
      } catch (_) {}
      return mehediOrigXhrSend.apply(this, arguments);
    };
  } catch (_) {}

  // 4. Scan SSR embedded <script type="application/json"> / ld+json + Shadow DOM live <video> elements
  function shihabScanInitialDomPayloads() {
    try {
      if (window.ytInitialPlayerResponse && typeof window.ytInitialPlayerResponse === "object") {
        mehediInspectYouTubePlayerResponse(window.ytInitialPlayerResponse);
      }
      const mehediJsonScripts = document.querySelectorAll('script[type="application/json"], script[type="application/ld+json"], script#__UNIVERSAL_DATA_FOR_REHYDRATION__, script#SIGI_STATE, script#__NEXT_DATA__');
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
           mehediRaw.includes("playable_url") ||
           mehediRaw.includes("progressiveStreams") ||
           mehediRaw.includes("adaptiveStreams") ||
           mehediRaw.includes("bitrateInfo") ||
           mehediRaw.includes("playAddr") ||
           mehediRaw.includes("video_info") ||
           mehediRaw.includes("contentUrl"))
        ) {
          mehediInspectTextBodyIfManifest(mehediRaw, location.href, "application/json");
          // Also inspect Schema.org VideoObject contentUrl in application/ld+json (including @graph arrays)
          if (mehediRaw.includes("contentUrl")) {
            try {
              const shihabLd = JSON.parse(mehediRaw);
              const mehediRoots = Array.isArray(shihabLd) ? shihabLd : [shihabLd];
              const mehediItems = [];
              for (const shihabR of mehediRoots) {
                if (!shihabR || typeof shihabR !== "object") continue;
                mehediItems.push(shihabR);
                if (Array.isArray(shihabR["@graph"])) {
                  for (const mehediG of shihabR["@graph"]) {
                    if (mehediG && typeof mehediG === "object") mehediItems.push(mehediG);
                  }
                }
              }
              const mehediParseLdDim = (shihabVal) => {
                const mehediRawV = (shihabVal && typeof shihabVal === "object") ? (shihabVal.value || 0) : shihabVal;
                const shihabParsedN = parseInt(String(mehediRawV || "0"), 10);
                return Number.isFinite(shihabParsedN) && shihabParsedN > 0 ? shihabParsedN : 0;
              };
              const shihabLdRef = shihabGetSafeHttpReferer();
              const mehediLdOrg = shihabGetSafeHttpOrigin();
              for (const shihabEntry of mehediItems) {
                const mehediCu = shihabEntry && typeof shihabEntry.contentUrl === "string" ? shihabEntry.contentUrl : "";
                if (mehediCu.startsWith("http") && /\.(mp4|webm|mkv|mov|m4v|m3u8)(\?|#|$)/i.test(mehediCu)) {
                  const shihabCleanCu = mehediNormalizeHookMediaUrl(mehediCu);
                  if (shihabCleanCu) {
                    const mehediIsHlsCu = /\.m3u8(\?|#|$)/i.test(shihabCleanCu);
                    const shihabLdGrp = `ld:${shihabFnv1aStringHash(shihabExtractUrlPathKey(shihabCleanCu))}`;
                    const shihabLdW = mehediParseLdDim(shihabEntry.width);
                    const mehediLdH = mehediParseLdDim(shihabEntry.height);
                    const shihabLdSig = `ld:${shihabLdGrp}`;
                    if (!mehediEmittedManifestKeys.has(shihabLdSig)) {
                      shihabMarkEmittedManifestKey(shihabLdSig);
                      shihabPostToContentBridge("mehedi_manifest_streams", {
                        shihabStreams: [{
                          shihabUrl: shihabCleanCu,
                          mehediMimeType: mehediIsHlsCu ? "application/vnd.apple.mpegurl" : "video/mp4",
                          shihabExt: mehediIsHlsCu ? "m3u8" : "mp4",
                          mehediTrackRole: "progressive",
                          shihabWidth: shihabLdW,
                          mehediHeight: mehediLdH,
                          shihabResolution: mehediHeightToStandardLabel(shihabLdW, mehediLdH),
                          shihabSizeBytes: -1,
                          shihabGroupKey: shihabLdGrp,
                          mehediAltIds: [shihabLdGrp],
                          mehediCapturedReferer: shihabLdRef,
                          shihabOrigin: mehediLdOrg,
                          shihabSkipBinaryProbe: mehediIsHlsCu,
                          shihabTitleHint: String(shihabEntry.name || "")
                        }],
                        mehediGroupKey: shihabLdGrp,
                        shihabShortcode: "",
                        mehediAltIds: [shihabLdGrp],
                        mehediTitleHint: String(shihabEntry.name || "")
                      });
                    }
                  }
                }
              }
            } catch (_) {}
          }
        }
      }
      const shihabVideos = shihabCollectVideosDeepHook(document, []);
      for (const mehediVid of shihabVideos) {
        mehediInspectLiveDomVideoElement(mehediVid);
      }
    } catch (_) {}
  }

  // Detect ACTUAL Encrypted Media Extensions (EME / Widevine / PlayReady / FairPlay DRM) playback
  // (Never trigger false DRM on speculative navigator.requestMediaKeySystemAccess capability probes!)
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
    if (typeof HTMLMediaElement !== "undefined" && HTMLMediaElement.prototype && typeof HTMLMediaElement.prototype.setMediaKeys === "function") {
      const mehediOrigSetMk = HTMLMediaElement.prototype.setMediaKeys;
      HTMLMediaElement.prototype.setMediaKeys = function (shihabMediaKeys) {
        if (shihabMediaKeys) {
          shihabNotifyEmeDrmDetected("EME MediaKeys");
        }
        return mehediOrigSetMk.apply(this, arguments);
      };
    }
  } catch (_) {}

  document.addEventListener(
    "encrypted",
    (shihabEncEvt) => {
      const mehediTarget = (shihabEncEvt && typeof shihabEncEvt.composedPath === "function" ? shihabEncEvt.composedPath()[0] : shihabEncEvt.target) || (shihabEncEvt && shihabEncEvt.target);
      if (mehediTarget && mehediTarget.tagName === "VIDEO") {
        shihabNotifyEmeDrmDetected(shihabEncEvt.initDataType || "encrypted-video");
      }
    },
    { capture: true, passive: true }
  );

  for (const mehediEvtName of ["play", "loadedmetadata", "pointerover"]) {
    document.addEventListener(
      mehediEvtName,
      (shihabEvt) => {
        const shihabTarget = (shihabEvt && typeof shihabEvt.composedPath === "function" ? shihabEvt.composedPath()[0] : shihabEvt.target) || (shihabEvt && shihabEvt.target);
        if (shihabTarget && shihabTarget.tagName === "VIDEO") {
          if (shihabTarget.mediaKeys) {
            shihabNotifyEmeDrmDetected("video.mediaKeys");
          }
          mehediInspectLiveDomVideoElement(shihabTarget);
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
    mehediLastKnownHookHref = location.href;
    mehediEmittedManifestKeys.clear();
    mehediRecentManifestPayloads.length = 0;
    shihabRecentBlobBindings.clear();
    setTimeout(shihabScanInitialDomPayloads, 250);
  });
  window.addEventListener("message", (shihabEv) => {
    try {
      if (shihabEv.source !== window || !shihabEv.data) return;
      shihabResetHookNavigationCachesIfChanged();
      if (shihabEv.data.__shihabInspectVideoUid) {
        const mehediTargetUid = String(shihabEv.data.__shihabInspectVideoUid);
        const shihabMatchingVid = shihabFindVideoByUidDeepHook(mehediTargetUid);
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
        const mehediScripts = document.querySelectorAll('script[type="application/json"], script[type="application/ld+json"], script#__UNIVERSAL_DATA_FOR_REHYDRATION__, script#SIGI_STATE, script#__NEXT_DATA__');
        for (const shihabSc of mehediScripts) {
          shihabSc.__mehediScanned = false;
        }
        // Only replay buffered manifests & exact MSE blob bindings on initial late-attach sync (NOT on user re-track, so narrowed/stale SPA history isn't resurrected!)
        if (shihabEv.data.shihabIsInitialSync === true) {
          for (const shihabPayload of mehediRecentManifestPayloads) {
            try {
              window.postMessage({ __shihabMehediPageHook: true, mehediKind: "mehedi_manifest_streams", shihabPayload }, "*");
            } catch (_) {}
          }
          for (const mehediBindPayload of shihabRecentBlobBindings.values()) {
            if (!mehediBindPayload || !mehediBindPayload.mehediExactByteMatch) continue;
            try {
              window.postMessage({ __shihabMehediPageHook: true, mehediKind: "shihab_mse_blob_binding", shihabPayload: mehediBindPayload }, "*");
            } catch (_) {}
          }
        }
        shihabScanInitialDomPayloads();
      }
    } catch (_) {}
  });
})();
