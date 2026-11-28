"use strict";

(() => {
  const SHIHAB_HOST_ID = "shihab-mehedi-idm-video-panel-host";
  const SHIHAB_HOST_ATTR = "data-shihab-mehedi-idm-host";
  const shihabInstanceToken = "shihab_idm_" + Date.now() + "_" + Math.random().toString(36).slice(2, 10);
  let mehediInstanceDisposed = false;

  // Cleanly tear down any older injected instance purely in isolated-world JS (zero document_start DOM mutations!)
  if (typeof window.__shihabMehediIdmCleanupPrev === "function") {
    try {
      window.__shihabMehediIdmCleanupPrev();
    } catch (mehediPrevErr) {
      // Ignore cleanup errors
    }
  }

  function shihabSafeSameWindowOrigin() {
    try {
      return /^https?:\/\//i.test(location.origin) ? location.origin : "*";
    } catch (_) {
      return "*";
    }
  }

  function shihabMakeWeakRef(mehediEl) {
    if (!mehediEl) return null;
    return typeof WeakRef === "function" ? new WeakRef(mehediEl) : mehediEl;
  }

  function shihabReadWeakRef(mehediRef) {
    if (!mehediRef) return null;
    return typeof mehediRef.deref === "function" ? (mehediRef.deref() || null) : mehediRef;
  }

  function shihabSetBoundedMap(shihabMap, mehediKey, shihabVal, mehediMax) {
    if (!mehediKey) return;
    shihabMap.set(mehediKey, shihabVal);
    while (shihabMap.size > mehediMax) {
      const shihabFirst = shihabMap.keys().next().value;
      shihabMap.delete(shihabFirst);
    }
  }

  function shihabIsExtensionContextAlive() {
    try {
      return typeof chrome !== "undefined" && Boolean(chrome && chrome.runtime && chrome.runtime.id);
    } catch (_) {
      return false;
    }
  }

  function shihabSweepForeignHostElements(mehediKeepHost) {
    try {
      const shihabNodes = document.querySelectorAll(`#${SHIHAB_HOST_ID}, [${SHIHAB_HOST_ATTR}]`);
      for (const mehediNode of shihabNodes) {
        if (mehediNode !== mehediKeepHost) {
          mehediNode.remove();
        }
      }
    } catch (_) {}
  }

  // Official MHS Download Manager 24x24 icon embedded inline so strict site CSP img-src never blocks it
  const SHIHAB_MHS_ICON_DATA_URI =
    "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAABgAAAAYCAYAAADgdz34AAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAZGSURBVEhL3dBpUBN2HsZx3nU7ba0hBxSrGJUWBBISAgQ55QiEMxFMgIRwJITLEEIMoAmE1KCIhAiEgAkgWlGUs4BQq5VKD0ZUMAVKa0cZrT1GO53dmR136jHPjnnhTH21+7bf17//85n5u7j8beqYWGF3TN7ld86sZ7dPrxe0TayXmS/8qG7qul2oNlxhvn7/f8VKrFElygYfCyxr/8ma+v1P4dijZ8IzvzzPstx9IW64/VSonn0klE/UvP7uf2r+0R/vSM1fzCdWzSGw/2cEDT0Ge+A37LLeR6TxO8SqriNJegXpWcP/3Jc9bD5WdclwtPyiqbFwpNuYc/ZUQ7ptWBdnGtLu0tcZaIqA1/dd1gBi7chdR45pDYzu+2Da7oNlWUfI4e8RVr2IyOI5xAgmkZBwBryYPuRwTkIU1Q1xyHHkMpqR529Evncdir1qofZVLHWnVrv/BXAAxJrBO47cQyuIPHYPnNaH4Db9hDTdPWQo1yAsWII442sUcC6jJHoK5eHjqGQPozr4PLSsc/go8GOYmF04QDNgH3v/E3mSpiIvVrnzFXD9+Quipm/VIVMtIV/5EIXlDyFVr0JjeQCV+T4qjT+iSuNAY80ibHoHerW30KeeR1/ZF+jNv4TenIs4nTmJvtQLMKT1Q8GzPM1M0D6sSDpU7ASu/vsZseL4DYc09yuU712HSDCNWL4ZnJbvEN+xjnjjHaSoV5EldUCeswiVYAlq7jwUIdMoDxiFwncYB73HYPW+hI9YM6iJHURC6EHIuZYb9tLJDS5jvz4hyo3XHJK0acjjb6LddAMxyjnQDXfA1K6CrbiN3ZKb2MNbgJh3E2Wyb2GqXkWb4jpUkSOQ7ziN6m3nYKN+giN+IzgQ2Yc0tg4V0k+v2sw/veliX/6NmF877RDFDKI0+gqsrYuIKPgSNPnXYBR8iWDBLKKSr4AfcxVxETOITh+F1rqAnrlf0CAaR/FmK6o3dcO+aQCtXqdRH9qBbLYe1eJPbc4vMnxyiyQsOb8sDLZBGTuKJv0cghPHQEsdRwB3FEGxw4gMG0JK0CjC6WcQEWVHZdstHLn2GPWZ57HvvWbUuh1FD6UTVk8LGgMPQxashy51TOMESkzj5DTBiZVMvxaokwewv2QMjKAToIXawQixISywB3H0fnB8+xHs1YYIdgtU7YtoufwrtBw71JQ61JN0OElqRL+HEWb/g1AGaJ/qw3oynECGspvMiW9a4e/QoyrdDlFyF2jeRtB8DyPAx4gI71YIAofAY54Da/Mh7GYbsd96G8dnHsCwqx11bho0EipxxrUWw5Qa2D9Qoe5DzR9GvyMsJxCXZSRFsw8u8zyrUcI9hkRmPfy37geNqgHdswoB7ysQTa1FergNghQbiksGoD25ihOXH8CumELbrma0uJZgkLAPM2QFzlLLcNSz4vtj1BoPJxDKqSKF0SqWU7dUICu8FuHUMvh5yEHzKALdvRA+ZCG83k1FsJsYxYVdaD61gNKiITSe/QEDU2toZupwfKMEQ4R8zJIKMblJig73os+aN5f+wwkwQmWkEG/5MndLKXiMMgS558KPIgKdkgMmORt0Yga83uWC/nYS+P5q1BsuQl40hubhe2jNtcNEyYOVIMAYQYhvSDmYdZfATpZYneMv8w8Qk4K2F37L9ZSD7yMDgyyAPykTDNJeBBEzEeLKR+DGZLDeiUXmBh4UnBZ0jt9Bu34CRzcXoJOYgT4CDxOufCyQ9uIrtyycJAkqXwF+vgISiypd5lKLkLFD5hymk/Yg0JUPtisf4YR0RG9Mxu4NHOx9Ox5KjyLUJR2B8cMitBJScYKQjAHXFHzmIYSDWoC5LeJnFrc9Ka+AkJ0iQsRO1VJ2gA7ZPmoEUkQIouQglCxCNDkXCSQJ+EQJcol5KHcVQ0/Kg9lNhn5KIUbc8jHtLsHse2IsbpNiJewARiIb5hv8iza9Al4mjKjpUQssLwQxvc8jgzv/jGN1PklmWv+VSe/8XeJr+bnEu/2eeodptYHadKONeuhaP1V3cWSrZnBqa4X9ErW49fNtBQ0LfqVVi0Jz/qjy7Pa/jL/MkGOmHq66oEhPOl0cH/dxNjfqVEpKWG8UP6SLJWB2+OT6mrbKPmh0V24zEIyeurfs26veeH3j79t/AWa8dlz2H5A+AAAAAElFTkSuQmCC";

  const SHIHAB_SHADOW_VIDEO_HOSTS_SELECTOR =
    "shreddit-player, shreddit-player-2, video-player, media-player, mux-player, vimeo-player, videojs-player, ytd-player, amp-video, faceplate-video, brightcove-player, jw-player, plyr-player";
  const SHIHAB_SHADOW_HOST_TAG_SET = new Set([
    "SHREDDIT-PLAYER", "SHREDDIT-PLAYER-2", "VIDEO-PLAYER", "MEDIA-PLAYER",
    "MUX-PLAYER", "VIMEO-PLAYER", "VIDEOJS-PLAYER", "YTD-PLAYER",
    "AMP-VIDEO", "FACEPLATE-VIDEO", "BRIGHTCOVE-PLAYER", "JW-PLAYER", "PLYR-PLAYER"
  ]);
  const MEHEDI_RESERVED_IG_PATHS = new Set([
    "explore", "reels", "reel", "p", "tv", "stories", "direct", "accounts", "audio", "ar", "about", "legal"
  ]);

  let shihabTabStreamsCache = [];
  let shihabStreamCacheVersion = 0;
  const shihabStreamsByUrlMap = new Map();
  const shihabStreamsByParentMasterMap = new Map();
  const shihabStreamsByGroupKeyMap = new Map();
  const shihabStreamsByAltIdMap = new Map();
  const shihabMaxDetectedAtByGroupMap = new Map();

  function shihabSetTabStreamsCache(mehediNextStreams) {
    const shihabList = Array.isArray(mehediNextStreams) ? mehediNextStreams : [];
    shihabTabStreamsCache = shihabList;
    shihabStreamCacheVersion++;
    shihabStreamsByUrlMap.clear();
    shihabStreamsByParentMasterMap.clear();
    shihabStreamsByGroupKeyMap.clear();
    shihabStreamsByAltIdMap.clear();
    shihabMaxDetectedAtByGroupMap.clear();

    const shihabPushIdx = (map, key, rec) => {
      if (!key) return;
      let arr = map.get(key);
      if (!arr) {
        arr = [];
        map.set(key, arr);
      }
      arr.push(rec);
    };

    for (let i = 0; i < shihabList.length; i++) {
      const rec = shihabList[i];
      if (!rec) continue;
      if (rec.shihabStreamUrl) shihabPushIdx(shihabStreamsByUrlMap, rec.shihabStreamUrl, rec);
      if (rec.mehediParentMasterUrl) shihabPushIdx(shihabStreamsByParentMasterMap, rec.mehediParentMasterUrl, rec);
      if (rec.shihabGroupKey) {
        shihabPushIdx(shihabStreamsByGroupKeyMap, rec.shihabGroupKey, rec);
        shihabPushIdx(shihabStreamsByAltIdMap, rec.shihabGroupKey, rec);
        const ts = rec.shihabDetectedAt || 0;
        const prevTs = shihabMaxDetectedAtByGroupMap.get(rec.shihabGroupKey);
        if (prevTs === undefined || ts > prevTs) {
          shihabMaxDetectedAtByGroupMap.set(rec.shihabGroupKey, ts);
        }
      }
      if (rec.mehediShortcode) shihabPushIdx(shihabStreamsByAltIdMap, String(rec.mehediShortcode), rec);
      if (Array.isArray(rec.mehediAltIds)) {
        for (let j = 0; j < rec.mehediAltIds.length; j++) {
          const alt = rec.mehediAltIds[j];
          if (alt) shihabPushIdx(shihabStreamsByAltIdMap, String(alt), rec);
        }
      }
    }
  }

  let mehediShowFloatingBar = true;
  let mehediPanelMiniMode = false;
  let shihabHideVideoInFullscreen = false;
  let shihabConfiguredPreventKey = "Alt";
  let mehediConfiguredForceKey = "Ins";
  let shihabPanelDismissedUrl = "";

  let mehediShadowHost = null;
  let shihabRefs = null;
  let shihabDropdownOpen = false;
  let mehediLockedDropdownVideoRef = null;
  let shihabLastRenderedMenuSig = "";
  let shihabRenderedMenuRows = [];
  let shihabRenderedMenuTargetVideoRef = null;
  let shihabLastDataTargetVideoRef = null;
  let shihabLastDataStreamVersion = -1;
  let shihabCachedTargetStreams = [];
  let shihabCachedSnifferRows = [];
  let mehediPointerDownInMenuAtMs = 0;
  let shihabPointerReleaseTimerId = null;
  let shihabDeferredMenuRender = false;
  let shihabHasHiddenVideoInDom = false;
  let mehediHoveredVideoRef = null;
  let mehediLastSelectedVideoRef = null;
  let shihabHoveredUntilMs = 0;
  let shihabHoverExpireTimerId = null;
  let shihabSubframeExpireTimerId = null;
  let shihabTopActiveExpireTimerId = null;
  let mehediPointerInsideBar = false;
  let shihabLastMeasuredFullBarW = 178;
  let shihabLastMeasuredBarH = 28;
  let shihabBarDimensionsMeasured = false;
  let shihabLastTransformCss = "";
  let mehediSubframeHoveredUntilMs = 0;
  let shihabTopFrameActiveUntilMs = 0;
  let shihabTopFrameVideoArea = 0;
  let mehediMyFrameId = window === window.top ? 0 : -1;
  let shihabLastNotifiedTopActiveKey = "";
  let mehediLastBgSyncMs = 0;
  let shihabToastClearTimer = null;
  let shihabDomObserver = null;
  let shihabVideoIntersectionObserver = null;
  let shihabVideoResizeObserver = null;
  let shihabVideoAttrObserver = null;
  const shihabObservedVideosSet = new WeakSet();
  let shihabVideoFeaturesActivated = false;
  let shihabVideoModeListenersAttached = false;
  let shihabDropdownListenersAttached = false;
  let shihabLastKnownLightVideoCount = 0;
  let shihabPageHasDrmProtectedMedia = false;
  let mehediDetectedDrmKeySystem = "";
  let mehediLastObservedPageHref = location.href;
  let shihabLocalVidUidCounter = 1;
  let mehediLastSentGestureMs = 0;
  let shihabLastSentGestureHref = "";
  let mehediLastSentGesturePageUrl = "";
  let mehediLastSentAltState = false;
  let shihabLastSentForceState = false;

  // Live bindings from mehedi_page_hook.js (direct DOM <video> fiber identity, byte-exact MSE blob -> URLs, shortcode/altId -> groupKey & caption)
  const shihabVidUidIdentityMap = new Map();
  const mehediVidLastInspectMs = new WeakMap();
  const shihabExactBlobToStreamUrlsMap = new Map();
  const shihabBlobToStreamUrlsMap = new Map();
  const mehediShortcodeMetaMap = new Map();
  const shihabGroupKeyTitleMap = new Map();

  function shihabArmHoverExpiryTimer(mehediUntilMs) {
    if (shihabHoverExpireTimerId) {
      clearTimeout(shihabHoverExpireTimerId);
      shihabHoverExpireTimerId = null;
    }
    const shihabDelay = Math.max(50, mehediUntilMs - Date.now() + 40);
    shihabHoverExpireTimerId = setTimeout(() => {
      shihabHoverExpireTimerId = null;
      if (!mehediInstanceDisposed && !mehediPointerInsideBar && !shihabDropdownOpen) {
        shihabScheduleFloatingPanelRender(false);
      }
    }, shihabDelay);
  }

  function shihabFormatBytesHuman(mehediBytes) {
    if (!mehediBytes || mehediBytes <= 0) return "";
    if (mehediBytes < 1024) return mehediBytes + " B";
    const shihabKb = mehediBytes / 1024;
    if (shihabKb < 1024) return shihabKb.toFixed(0) + " KB";
    const mehediMb = shihabKb / 1024;
    if (mehediMb < 1024) return mehediMb.toFixed(1) + " MB";
    return (mehediMb / 1024).toFixed(2) + " GB";
  }

  function mehediHeightToQualityLabel(shihabWidth, mehediHeight) {
    const shihabW = Number(shihabWidth || 0);
    const mehediH = Number(mehediHeight || 0);
    if (shihabW <= 0 && mehediH <= 0) return "";
    const shihabShortEdge = (shihabW > 0 && mehediH > 0) ? Math.min(shihabW, mehediH) : Math.max(shihabW, mehediH);
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

  function shihabSafeHttpRef() {
    try {
      const shihabH = String(location.href || "");
      if (/^https?:\/\//i.test(shihabH)) return shihabH;
      const mehediRef = String(document.referrer || "");
      if (/^https?:\/\//i.test(mehediRef)) return mehediRef;
    } catch (_) {}
    return "";
  }

  function shihabSafeHttpOrg() {
    try {
      const shihabO = String(location.origin || "");
      if (/^https?:\/\//i.test(shihabO)) return shihabO;
      const mehediRef = String(document.referrer || "");
      if (/^https?:\/\//i.test(mehediRef)) return new URL(mehediRef).origin;
    } catch (_) {}
    return "";
  }

  function shihabNumericResRank(mehediLabel) {
    const shihabMatch = String(mehediLabel || "").match(/(\d{3,4})p/i);
    return shihabMatch ? parseInt(shihabMatch[1], 10) : 0;
  }

  function mehediNormalizeClientStreamUrl(shihabRawUrl) {
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
          const mehediSigned = (mehediUrlObj.searchParams.get("sparams") || "").split(",");
          if (!mehediSigned.includes("range")) {
            mehediUrlObj.searchParams.delete("range");
          }
        }
      }
      return mehediUrlObj.toString();
    } catch (shihabErr) {
      return "";
    }
  }

  function shihabExtractUrlGroupAndAltIds(mehediStreamUrl) {
    const shihabAltIds = [];
    let mehediGroupKey = "";
    if (!mehediStreamUrl) return { mehediGroupKey, shihabAltIds };
    try {
      const shihabParsed = new URL(mehediStreamUrl, location.href);
      if (shihabParsed.hostname.includes("googlevideo.com") || shihabParsed.pathname.includes("/videoplayback")) {
        const mehediDocId = shihabParsed.searchParams.get("docid") || "";
        if (mehediDocId) {
          mehediGroupKey = `yt:${mehediDocId}`;
          shihabAltIds.push(mehediDocId, mehediGroupKey);
          return { mehediGroupKey, shihabAltIds };
        }
      }
      if (shihabParsed.hostname.includes("licdn.com")) {
        const shihabLiMatch = shihabParsed.pathname.match(/\/playlist\/vid\/(?:v\d+\/)?([A-Za-z0-9_-]{6,})\//i);
        if (shihabLiMatch && shihabLiMatch[1]) {
          mehediGroupKey = `li:${shihabLiMatch[1]}`;
          shihabAltIds.push(shihabLiMatch[1], mehediGroupKey);
          return { mehediGroupKey, shihabAltIds };
        }
      }
      if (shihabParsed.hostname.includes("twimg.com")) {
        const mehediTwMatch = shihabParsed.pathname.match(/\/(?:ext_tw_video|amplify_video|tweet_video)\/([A-Za-z0-9_-]+)\//i);
        if (mehediTwMatch && mehediTwMatch[1]) {
          mehediGroupKey = `twv:${mehediTwMatch[1]}`;
          shihabAltIds.push(mehediTwMatch[1], mehediGroupKey);
          return { mehediGroupKey, shihabAltIds };
        }
      }
      const shihabEfg = shihabParsed.searchParams.get("efg");
      if (shihabEfg) {
        const mehediB64 = decodeURIComponent(shihabEfg).replace(/-/g, "+").replace(/_/g, "/");
        const shihabPadded = mehediB64 + "=".repeat((4 - (mehediB64.length % 4)) % 4);
        const mehediObj = JSON.parse(atob(shihabPadded));
        if (mehediObj && typeof mehediObj === "object") {
          const shihabXpv = mehediObj.xpv_asset_id ? String(mehediObj.xpv_asset_id) : "";
          const mehediVidId = mehediObj.video_id ? String(mehediObj.video_id) : "";
          const shihabAstId = mehediObj.asset_id ? String(mehediObj.asset_id) : "";
          if (shihabXpv) {
            mehediGroupKey = `asset:${shihabXpv}`;
            shihabAltIds.push(shihabXpv, mehediGroupKey);
          }
          if (mehediVidId) {
            if (!mehediGroupKey) mehediGroupKey = `vid:${mehediVidId}`;
            shihabAltIds.push(mehediVidId, `vid:${mehediVidId}`);
          }
          if (shihabAstId) {
            if (!mehediGroupKey) mehediGroupKey = `asset:${shihabAstId}`;
            shihabAltIds.push(shihabAstId, `asset:${shihabAstId}`);
          }
        }
      }
    } catch (mehediErr) {
      // Ignore parse errors
    }
    return { mehediGroupKey, shihabAltIds };
  }

  function mehediIndexIdentifierToGroup(shihabIdKey, mehediGroupKey, shihabTitleHint) {
    if (!shihabIdKey) return;
    const mehediKeyStr = String(shihabIdKey);
    const shihabPrev = mehediShortcodeMetaMap.get(mehediKeyStr) || {};
    mehediShortcodeMetaMap.set(mehediKeyStr, {
      mehediGroupKey: mehediGroupKey || shihabPrev.mehediGroupKey || "",
      shihabTitleHint: shihabTitleHint || shihabPrev.shihabTitleHint || ""
    });
    if (mehediShortcodeMetaMap.size > 400) {
      const shihabOldest = mehediShortcodeMetaMap.keys().next().value;
      mehediShortcodeMetaMap.delete(shihabOldest);
    }
  }

  const shihabRequestedYtVideoIdsMs = new Map();

  function shihabExtractCurrentPageYouTubeVideoId() {
    try {
      const mehediHost = (location.hostname || "").toLowerCase();
      if (mehediHost === "youtu.be" || mehediHost.endsWith(".youtu.be")) {
        const shihabFirst = (location.pathname || "").replace(/^\/+/, "").split("/")[0] || "";
        return /^[A-Za-z0-9_-]{11}$/.test(shihabFirst) ? shihabFirst : "";
      }
      if (
        mehediHost === "youtube.com" ||
        mehediHost.endsWith(".youtube.com") ||
        mehediHost === "youtube-nocookie.com" ||
        mehediHost.endsWith(".youtube-nocookie.com")
      ) {
        const shihabV = new URLSearchParams(location.search).get("v") || "";
        if (/^[A-Za-z0-9_-]{11}$/.test(shihabV)) return shihabV;
        const mehediPathMatch = (location.pathname || "").match(/^\/(?:shorts|embed|live|v)\/([A-Za-z0-9_-]{11})(?:[/?#]|$)/i);
        if (mehediPathMatch && mehediPathMatch[1]) return mehediPathMatch[1];
      }
    } catch (_) {}
    return "";
  }

  function mehediRequestYouTubeResolutionForVideoId(shihabVidId, mehediTitleHint, shihabVisitorData, mehediForceRefresh) {
    if (!shihabVidId || !/^[A-Za-z0-9_-]{11}$/.test(shihabVidId)) return;
    const mehediNow = Date.now();
    const shihabPrevMs = shihabRequestedYtVideoIdsMs.get(shihabVidId) || 0;
    if (!mehediForceRefresh && mehediNow - shihabPrevMs < 8000) {
      return;
    }
    shihabSetBoundedMap(shihabRequestedYtVideoIdsMs, shihabVidId, mehediNow, 100);

    const mehediYtGrp = `yt:${shihabVidId}`;
    if (mehediTitleHint) {
      shihabSetBoundedMap(shihabGroupKeyTitleMap, mehediYtGrp, mehediTitleHint, 150);
    }
    mehediIndexIdentifierToGroup(shihabVidId, mehediYtGrp, mehediTitleHint || "");

    try {
      chrome.runtime.sendMessage({
        shihabCmd: "mehedi_resolve_youtube_video",
        shihabVideoId: shihabVidId,
        mehediTitleHint: mehediTitleHint || "",
        shihabVisitorData: shihabVisitorData || "",
        mehediForce: Boolean(mehediForceRefresh)
      }).then((shihabAck) => {
        if (shihabAck && Array.isArray(shihabAck.mehediStreams) && shihabAck.mehediStreams.length > 0) {
          shihabSetTabStreamsCache(shihabAck.mehediStreams);
          shihabRenderFloatingIdmVideoPanel(false);
        } else if (shihabAck && shihabAck.shihabUsePageFallback) {
          try {
            window.postMessage({
              __shihabResolveYtInMainWorld: shihabVidId,
              mehediVisitorData: shihabVisitorData || ""
            }, shihabSafeSameWindowOrigin());
          } catch (_) {}
        }
      }).catch(() => {});
    } catch (_) {}
  }

  function mehediEnsureVideoUidAndInspect(shihabVidEl, mehediForceInspect) {
    if (!shihabVidEl || shihabVidEl.tagName !== "VIDEO") return "";
    if (!shihabVidEl.dataset.shihabVidUid) {
      shihabVidEl.dataset.shihabVidUid =
        "shihab_c_" + (shihabLocalVidUidCounter++) + "_" + Math.random().toString(36).slice(2, 7);
    }
    const mehediUid = shihabVidEl.dataset.shihabVidUid;
    const shihabNow = Date.now();
    const mehediLastMs = mehediVidLastInspectMs.get(shihabVidEl) || 0;
    if (mehediForceInspect || (!shihabVidUidIdentityMap.has(mehediUid) && shihabNow - mehediLastMs > 1400)) {
      mehediVidLastInspectMs.set(shihabVidEl, shihabNow);
      try {
        window.postMessage({ __shihabInspectVideoUid: mehediUid }, shihabSafeSameWindowOrigin());
      } catch (_) {}
    }
    return mehediUid;
  }

  // Receive real-time MSE blob bindings, direct <video> fiber identities & manifest stream metadata from mehedi_page_hook.js
  function mehediOnPageHookWindowMessage(shihabEvent) {
    if (!shihabEvent || !shihabEvent.data || typeof shihabEvent.data !== "object") {
      return;
    }
    if (shihabEvent.data.__shihabMehediTopPanelActive === true && window !== window.top) {
      if (shihabEvent.source === window.top) {
        shihabTopFrameActiveUntilMs = Date.now() + 2200;
        shihabTopFrameVideoArea = Number(shihabEvent.data.shihabTopVideoArea || 0);
        if (shihabTopActiveExpireTimerId) clearTimeout(shihabTopActiveExpireTimerId);
        shihabTopActiveExpireTimerId = setTimeout(() => {
          shihabTopActiveExpireTimerId = null;
          if (!mehediInstanceDisposed && shihabVideoFeaturesActivated) {
            shihabScheduleFloatingPanelRender(false);
          }
        }, 2250);
      }
      return;
    }
    if (shihabEvent.data.__shihabMehediSubframeVideoHovered === true && window === window.top) {
      if (shihabEvent.source && shihabEvent.source !== window) {
        mehediSubframeHoveredUntilMs = Date.now() + 4500;
        if (!mehediPointerInsideBar && !shihabDropdownOpen && mehediShadowHost && mehediShadowHost.style.display !== "none") {
          mehediShadowHost.style.display = "none";
        }
        if (shihabSubframeExpireTimerId) clearTimeout(shihabSubframeExpireTimerId);
        shihabSubframeExpireTimerId = setTimeout(() => {
          shihabSubframeExpireTimerId = null;
          if (!mehediInstanceDisposed && shihabVideoFeaturesActivated) {
            shihabScheduleFloatingPanelRender(false);
          }
        }, 4550);
      }
      return;
    }
    if (shihabEvent.source !== window || shihabEvent.data.__shihabMehediPageHook !== true) {
      return;
    }
    const mehediKind = shihabEvent.data.mehediKind;
    const shihabPayload = shihabEvent.data.shihabPayload;
    if (!shihabPayload) return;

    shihabActivateVideoFeaturesOnce();

    if (mehediKind === "shihab_eme_drm_detected") {
      shihabPageHasDrmProtectedMedia = true;
      mehediDetectedDrmKeySystem = String(shihabPayload.keySystem || "EME");
      shihabStreamCacheVersion++;
      shihabRenderFloatingIdmVideoPanel(false);
      return;
    }

    if (mehediKind === "shihab_video_element_identity") {
      const mehediVidUid = String(shihabPayload.shihabVidUid || "");
      if (mehediVidUid) {
        const shihabPrevIdent = shihabVidUidIdentityMap.get(mehediVidUid) || {
          mehediGroupKeys: [],
          shihabAltIds: [],
          mehediBoundUrls: [],
          shihabTitleHint: ""
        };
        const mehediMergedGroups = Array.from(new Set([
          ...shihabPrevIdent.mehediGroupKeys,
          ...(Array.isArray(shihabPayload.mehediGroupKeys) ? shihabPayload.mehediGroupKeys.map(String) : [])
        ].filter(Boolean)));
        const shihabMergedAlts = Array.from(new Set([
          ...shihabPrevIdent.shihabAltIds,
          ...(Array.isArray(shihabPayload.shihabAltIds) ? shihabPayload.shihabAltIds.map(String) : [])
        ].filter(Boolean)));
        const mehediMergedUrls = Array.from(new Set([
          ...shihabPrevIdent.mehediBoundUrls,
          ...(Array.isArray(shihabPayload.mehediBoundUrls)
            ? shihabPayload.mehediBoundUrls.map(mehediNormalizeClientStreamUrl)
            : [])
        ].filter(Boolean)));
        const shihabTitleHint = String(shihabPayload.shihabTitleHint || shihabPrevIdent.shihabTitleHint || "");

        shihabSetBoundedMap(shihabVidUidIdentityMap, mehediVidUid, {
          mehediGroupKeys: mehediMergedGroups,
          shihabAltIds: shihabMergedAlts,
          mehediBoundUrls: mehediMergedUrls,
          shihabTitleHint
        }, 120);
        for (const mehediGrp of mehediMergedGroups) {
          if (shihabTitleHint) shihabSetBoundedMap(shihabGroupKeyTitleMap, mehediGrp, shihabTitleHint, 150);
          for (const shihabAlt of shihabMergedAlts) {
            mehediIndexIdentifierToGroup(shihabAlt, mehediGrp, shihabTitleHint);
          }
        }
        shihabStreamCacheVersion++;
        shihabScheduleFloatingPanelRender(false);
      }
      return;
    }

    if (mehediKind === "shihab_mse_blob_binding") {
      const mehediBlobUrl = String(shihabPayload.shihabBlobUrl || "");
      const shihabStreamClean = mehediNormalizeClientStreamUrl(shihabPayload.mehediStreamUrl || "");
      const mehediIsExact = Boolean(shihabPayload.mehediExactByteMatch);
      if (mehediBlobUrl && shihabStreamClean) {
        if (mehediIsExact) {
          let shihabExactSet = shihabExactBlobToStreamUrlsMap.get(mehediBlobUrl);
          if (!shihabExactSet) {
            shihabExactSet = new Set();
            shihabSetBoundedMap(shihabExactBlobToStreamUrlsMap, mehediBlobUrl, shihabExactSet, 150);
          }
          shihabExactSet.add(shihabStreamClean);
          // Overwrite fallback set with exact byte-matched URLs (bounded at 150) so any earlier preloaded-Reel guess is purged!
          shihabSetBoundedMap(shihabBlobToStreamUrlsMap, mehediBlobUrl, new Set(shihabExactSet), 150);
        } else if (!shihabExactBlobToStreamUrlsMap.has(mehediBlobUrl)) {
          let mehediUrlSet = shihabBlobToStreamUrlsMap.get(mehediBlobUrl);
          if (!mehediUrlSet) {
            mehediUrlSet = new Set();
            shihabSetBoundedMap(shihabBlobToStreamUrlsMap, mehediBlobUrl, mehediUrlSet, 150);
          }
          mehediUrlSet.add(shihabStreamClean);
        }
        shihabStreamCacheVersion++;
        shihabScheduleFloatingPanelRender(false);
      }
      return;
    }

    if (mehediKind === "shihab_youtube_video_discovered") {
      const mehediYtId = String(shihabPayload.shihabVideoId || "");
      const shihabYtTitle = String(shihabPayload.mehediTitleHint || "");
      const mehediVisData = String(shihabPayload.mehediVisitorData || "");
      if (mehediYtId && /^[A-Za-z0-9_-]{11}$/.test(mehediYtId)) {
        mehediRequestYouTubeResolutionForVideoId(mehediYtId, shihabYtTitle, mehediVisData, false);
      }
      return;
    }

    if (mehediKind === "mehedi_manifest_streams") {
      const shihabGroupKey = String(shihabPayload.mehediGroupKey || "");
      const mehediShortcode = String(shihabPayload.shihabShortcode || "");
      const shihabTitleHint = String(shihabPayload.mehediTitleHint || "");
      const mehediTopAlts = Array.isArray(shihabPayload.mehediAltIds) ? shihabPayload.mehediAltIds : [];

      if (shihabGroupKey && shihabTitleHint) {
        shihabSetBoundedMap(shihabGroupKeyTitleMap, shihabGroupKey, shihabTitleHint, 150);
      }
      if (mehediShortcode) {
        mehediIndexIdentifierToGroup(mehediShortcode, shihabGroupKey, shihabTitleHint);
      }
      for (const shihabAlt of mehediTopAlts) {
        mehediIndexIdentifierToGroup(shihabAlt, shihabGroupKey, shihabTitleHint);
      }

      if (Array.isArray(shihabPayload.shihabStreams) && shihabPayload.shihabStreams.length > 0) {
        for (const mehediItem of shihabPayload.shihabStreams) {
          if (!mehediItem) continue;
          const shihabItemGrp = mehediItem.shihabGroupKey || shihabGroupKey;
          const mehediItemTitle = mehediItem.shihabTitleHint || shihabTitleHint;
          if (shihabItemGrp && mehediItemTitle) {
            shihabSetBoundedMap(shihabGroupKeyTitleMap, shihabItemGrp, mehediItemTitle, 150);
          }
          if (mehediItem.mehediShortcode) {
            mehediIndexIdentifierToGroup(mehediItem.mehediShortcode, shihabItemGrp, mehediItemTitle);
          }
          if (Array.isArray(mehediItem.mehediAltIds)) {
            for (const shihabSubAlt of mehediItem.mehediAltIds) {
              mehediIndexIdentifierToGroup(shihabSubAlt, shihabItemGrp, mehediItemTitle);
            }
          }
        }
        try {
          chrome.runtime.sendMessage({
            shihabCmd: "mehedi_register_manifest_streams",
            shihabStreams: shihabPayload.shihabStreams
          }).catch(() => {});
        } catch (shihabRuntimeErr) {
          // Extension reloaded
        }
      }
    }
  }

  function shihabIsGibberishStreamFileLeaf(mehediLeaf) {
    if (!mehediLeaf) return true;
    const shihabStem = String(mehediLeaf).split(/[\\/]/).pop().replace(/\.[a-z0-9]{2,5}$/i, "").trim();
    if (!shihabStem || shihabStem.length < 3) return true;
    if (/^(video|videoplayback|playback|media|master|index|playlist|chunklist|manifest|stream|download|file|watch|reel|reels|audio|clip)$/i.test(shihabStem)) {
      return true;
    }
    if (/^(AQ[A-Za-z0-9_-]{8,}|An[A-Za-z0-9_-]{8,}|[0-9]{6,}_[0-9]{6,}[0-9_a-z]*|[0-9a-f-]{16,})$/i.test(shihabStem)) {
      return true;
    }
    if (!/\s/.test(shihabStem) && shihabStem.length >= 22 && /^[A-Za-z0-9_-]+$/.test(shihabStem)) {
      const mehediTrans = (shihabStem.match(/[a-z][A-Z]|[A-Z][a-z][0-9]|[0-9][A-Za-z]/g) || []).length;
      if (mehediTrans >= 3) return true;
    }
    return false;
  }

  // Clean and validate candidate video caption / title text (supports Unicode / Bengali / English / Emoji & strips translation/feed noise)
  function mehediCleanCaptionText(shihabRaw) {
    if (!shihabRaw) return "";
    let mehediTxt = String(shihabRaw)
      .replace(/^\(\d+\+?\)\s*/, "")
      .replace(/^[▶►•·\s]+/, "")
      .replace(/^(?:Suggested for you|Sponsored|Reels)\s*[·•\-]?\s*/gi, "")
      .replace(/\s*(?:[|\-·•—:]\s*(?:Facebook|Watch|Reels|YouTube|Instagram|TikTok|X|Twitter|Reddit|Vimeo|Dailymotion|Twitch))+\s*$/gi, "")
      .replace(/\s*(?:Hide translation|See translation|See original|Translated from [A-Za-z ]+)\s*$/gi, "")
      .replace(/\b(?:Hide translation|See translation|See original)\s*$/gi, "")
      .replace(/\s+/g, " ")
      .replace(/\s*(?:\.\.\.|…)?\s*(?:more|See more|See less|আরও দেখুন|কম দেখুন)\s*$/i, "")
      .replace(/\s*(?:Hide translation|See translation|See original)\s*$/gi, "")
      .replace(/[\\/:*?"<>|\x00-\x1f]+/g, " ")
      .replace(/\s+/g, " ")
      .trim();

    if (mehediTxt.length < 2) return "";
    const shihabUiNoise = /^(facebook|reels?|watch|youtube|instagram|tiktok|x|twitter|reddit|home|explore|notifications|messages|chats|menu|profile|search|like|likes|comment|comments|share|save|send|follow|following|sponsored|suggested for you|original audio|audio is muted|muted|mute|unmute|play|pause|live|more|verified|see translation|hide translation|see original|translate|add a comment\.{0,3}|view all \d* ?comments|yesterday(?:\s+at\s+.*)?|just now|\d+\s*(?:w|d|h|m|s|hrs?|mins?|secs?|days?|weeks?|months?|years?|likes?|views?|comments?)(?:\s+ago)?|\d+:\d+(?:\s*\/\s*\d+:\d+)?|\d+(?:[.,]\d+)?[kmb]?)$/i;
    if (shihabUiNoise.test(mehediTxt)) {
      return "";
    }
    if (/^(?:liked by\s+\S+|suggested for you\b)/i.test(mehediTxt)) {
      return "";
    }
    if (mehediTxt.length > 105) {
      mehediTxt = mehediTxt.slice(0, 105).trim();
    }
    return mehediTxt;
  }

  function shihabCountVideosInSubtree(mehediEl) {
    if (!mehediEl || typeof mehediEl.querySelectorAll !== "function") return 0;
    let shihabTotal = mehediEl.querySelectorAll("video").length;
    try {
      const mehediShadowHosts = mehediEl.querySelectorAll(SHIHAB_SHADOW_VIDEO_HOSTS_SELECTOR);
      for (const shihabH of mehediShadowHosts) {
        if (shihabH && shihabH.shadowRoot && typeof shihabH.shadowRoot.querySelectorAll === "function") {
          shihabTotal += shihabH.shadowRoot.querySelectorAll("video").length;
        }
      }
    } catch (_) {}
    return shihabTotal;
  }

  // Locate the single-post / single-player DOM card enclosing a specific <video> (pierces Shadow DOM cleanly!)
  function shihabFindScopedCardForVideo(mehediVid, shihabMaxVideosAllowed) {
    if (!mehediVid) return null;
    const mehediAllowed = shihabMaxVideosAllowed || 1;
    const shihabStepParent = (shihabEl) => {
      if (!shihabEl) return null;
      if (shihabEl.parentElement) return shihabEl.parentElement;
      const mehediRoot = shihabEl.getRootNode && shihabEl.getRootNode();
      if (mehediRoot && mehediRoot.host && mehediRoot.host !== shihabEl) {
        return mehediRoot.host;
      }
      return null;
    };

    let shihabSemWalk = mehediVid;
    for (let shihabD = 0; shihabSemWalk && shihabD < 18; shihabD++) {
      if (shihabSemWalk.matches && shihabSemWalk.matches("article, shreddit-post, ytd-reel-video-renderer, [data-testid='tweet'], [role='article'], .feed-shared-update-v2, [data-urn^='urn:li:activity'], [data-id^='urn:li:activity']")) {
        if (shihabCountVideosInSubtree(shihabSemWalk) <= mehediAllowed) {
          return shihabSemWalk;
        }
        break;
      }
      shihabSemWalk = shihabStepParent(shihabSemWalk);
    }

    let mehediBestCard = null;
    let shihabCur = shihabStepParent(mehediVid);
    for (let mehediDepth = 0; shihabCur && shihabCur !== document.body && shihabCur !== document.documentElement && mehediDepth < 16; mehediDepth++) {
      if (shihabCur.tagName === "MAIN" || shihabCur.getAttribute("role") === "feed" || shihabCur.getAttribute("role") === "main") break;
      const shihabVidCount = shihabCountVideosInSubtree(shihabCur);
      if (shihabVidCount > mehediAllowed) break;
      mehediBestCard = shihabCur;
      shihabCur = shihabStepParent(shihabCur);
    }
    return mehediBestCard;
  }

  function mehediMapBoundUrlsToGroups(shihabUrlIterable, mehediTargetGroupSet, shihabTargetUrlSet) {
    if (!shihabUrlIterable) return;
    const shihabAddGroupsFromList = (arr) => {
      if (!arr) return;
      for (let i = 0; i < arr.length; i++) {
        if (arr[i] && arr[i].shihabGroupKey) mehediTargetGroupSet.add(arr[i].shihabGroupKey);
      }
    };
    for (const mehediBoundUrl of shihabUrlIterable) {
      shihabTargetUrlSet.add(mehediBoundUrl);
      const mehediBoundPathKey = mehediBoundUrl.split("?")[0].split("#")[0];
      const shihabExtracted = shihabExtractUrlGroupAndAltIds(mehediBoundUrl);
      if (shihabExtracted.mehediGroupKey) {
        mehediTargetGroupSet.add(shihabExtracted.mehediGroupKey);
      }
      for (const mehediAlt of shihabExtracted.shihabAltIds) {
        const shihabMapped = mehediShortcodeMetaMap.get(mehediAlt);
        if (shihabMapped && shihabMapped.mehediGroupKey) {
          mehediTargetGroupSet.add(shihabMapped.mehediGroupKey);
        }
        shihabAddGroupsFromList(shihabStreamsByAltIdMap.get(String(mehediAlt)));
      }
      const shihabDirectMap = mehediShortcodeMetaMap.get(mehediBoundUrl) || mehediShortcodeMetaMap.get(mehediBoundPathKey);
      if (shihabDirectMap && shihabDirectMap.mehediGroupKey) {
        mehediTargetGroupSet.add(shihabDirectMap.mehediGroupKey);
      }
      shihabAddGroupsFromList(shihabStreamsByUrlMap.get(mehediBoundUrl));
      shihabAddGroupsFromList(shihabStreamsByParentMasterMap.get(mehediBoundUrl));
      shihabAddGroupsFromList(shihabStreamsByAltIdMap.get(mehediBoundUrl));
      shihabAddGroupsFromList(shihabStreamsByAltIdMap.get(mehediBoundPathKey));
    }
  }

  // Resolve exact identity keys (direct DOM React Fiber / player binding, byte-exact MSE blob URLs, route/card shortcodes & asset groupKeys)
  function shihabResolveVideoContext(mehediVid) {
    const shihabDirectDomGroupKeys = new Set();
    const shihabExactBoundUrls = new Set();
    const shihabExactGroupKeys = new Set();
    const shihabBoundUrls = new Set();
    const mehediFallbackBlobGroupKeys = new Set();
    const mehediRouteAndCardGroupKeys = new Set();
    const shihabShortcodes = new Set();
    let mehediDirectFiberTitleHint = "";

    const mehediHost = (location.hostname || "").toLowerCase();
    const shihabPath = location.pathname || "";

    // 0. Direct DOM <video> React Fiber / YouTube #movie_player binding (100% specific to this exact <video> node!)
    if (mehediVid) {
      const shihabVidUid = mehediEnsureVideoUidAndInspect(mehediVid, false);
      const mehediIdent = shihabVidUid ? shihabVidUidIdentityMap.get(shihabVidUid) : null;
      if (mehediIdent) {
        if (mehediIdent.shihabTitleHint) {
          mehediDirectFiberTitleHint = mehediIdent.shihabTitleHint;
        }
        for (const shihabG of mehediIdent.mehediGroupKeys) {
          if (shihabG) shihabDirectDomGroupKeys.add(shihabG);
        }
        for (const mehediAlt of mehediIdent.shihabAltIds) {
          if (mehediAlt) shihabShortcodes.add(String(mehediAlt));
        }
        if (mehediIdent.mehediBoundUrls && mehediIdent.mehediBoundUrls.length > 0) {
          mehediMapBoundUrlsToGroups(mehediIdent.mehediBoundUrls, shihabDirectDomGroupKeys, shihabExactBoundUrls);
          for (const shihabBu of shihabExactBoundUrls) shihabBoundUrls.add(shihabBu);
        }
        // O(1) cross-check fiber altIds against indexed streams map
        for (const shihabAlt of mehediIdent.shihabAltIds) {
          const mehediHits = shihabStreamsByAltIdMap.get(String(shihabAlt));
          if (mehediHits) {
            for (let i = 0; i < mehediHits.length; i++) {
              if (mehediHits[i] && mehediHits[i].shihabGroupKey) {
                shihabDirectDomGroupKeys.add(mehediHits[i].shihabGroupKey);
              }
            }
          }
        }
      }

      // 1. Direct or MSE blob: currentSrc binding (Exact FNV-1a ftyp/moof byte-matched bindings take priority over fallback)
      const shihabRawSrc = mehediVid.currentSrc || mehediVid.src || "";
      if (shihabRawSrc.startsWith("blob:")) {
        const mehediExactSet = shihabExactBlobToStreamUrlsMap.get(shihabRawSrc);
        if (mehediExactSet && mehediExactSet.size > 0) {
          mehediMapBoundUrlsToGroups(mehediExactSet, shihabExactGroupKeys, shihabExactBoundUrls);
          for (const shihabU of shihabExactBoundUrls) shihabBoundUrls.add(shihabU);
        } else {
          const mehediFallbackSet = shihabBlobToStreamUrlsMap.get(shihabRawSrc);
          if (mehediFallbackSet) {
            mehediMapBoundUrlsToGroups(mehediFallbackSet, mehediFallbackBlobGroupKeys, shihabBoundUrls);
          }
        }
      } else if (shihabRawSrc.startsWith("http://") || shihabRawSrc.startsWith("https://")) {
        const mehediCleanDirect = mehediNormalizeClientStreamUrl(shihabRawSrc);
        if (mehediCleanDirect) {
          mehediMapBoundUrlsToGroups([mehediCleanDirect], shihabExactGroupKeys, shihabExactBoundUrls);
          shihabBoundUrls.add(mehediCleanDirect);
        }
      }

      // 1b. Extract X/Twitter poster thumb video ID (e.g. ext_tw_video_thumb/<id>) & LinkedIn poster/data-sources asset ID
      const mehediPosterUrl = mehediVid.getAttribute("poster") || mehediVid.poster || "";
      if (mehediPosterUrl) {
        const shihabTwThumbM = mehediPosterUrl.match(/\/(?:ext_tw_video_thumb|amplify_video_thumb|tweet_video_thumb)\/([A-Za-z0-9_-]+)/i);
        if (shihabTwThumbM && shihabTwThumbM[1]) {
          shihabShortcodes.add(shihabTwThumbM[1]);
          shihabShortcodes.add(`twv:${shihabTwThumbM[1]}`);
        }
        const mehediLiPosterM = mehediPosterUrl.match(/\/playlist\/vid\/(?:v\d+\/)?([A-Za-z0-9_-]{6,})\//i);
        if (mehediLiPosterM && mehediLiPosterM[1]) {
          shihabShortcodes.add(mehediLiPosterM[1]);
          shihabShortcodes.add(`li:${mehediLiPosterM[1]}`);
          shihabDirectDomGroupKeys.add(`li:${mehediLiPosterM[1]}`);
        }
      }
      const shihabCtxDsHolder = mehediVid.hasAttribute("data-sources")
        ? mehediVid
        : (mehediVid.parentElement && typeof mehediVid.parentElement.closest === "function" ? mehediVid.parentElement.closest("[data-sources]") : null);
      if (shihabCtxDsHolder && shihabCountVideosInSubtree(shihabCtxDsHolder) <= 1) {
        const mehediCtxDsRaw = shihabCtxDsHolder.getAttribute("data-sources") || "";
        const shihabCtxLiM = mehediCtxDsRaw.match(/\\?\/playlist\\?\/vid\\?\/(?:v\d+\\?\/)?([A-Za-z0-9_-]{6,})\\?\//i);
        if (shihabCtxLiM && shihabCtxLiM[1]) {
          shihabShortcodes.add(shihabCtxLiM[1]);
          shihabShortcodes.add(`li:${shihabCtxLiM[1]}`);
          shihabDirectDomGroupKeys.add(`li:${shihabCtxLiM[1]}`);
        }
      }
    }

    // 2. Single-video page route parameters (YouTube Watch/Shorts/Embed/Live, Instagram Reel/Post, Facebook Reel/Watch/Videos, X Status, TikTok Video)
    if (mehediHost.includes("youtube.com") || mehediHost.includes("youtu.be") || mehediHost.includes("youtube-nocookie.com")) {
      const mehediPageYtId = shihabExtractCurrentPageYouTubeVideoId();
      if (mehediPageYtId) {
        shihabShortcodes.add(mehediPageYtId);
        mehediRouteAndCardGroupKeys.add(`yt:${mehediPageYtId}`);
      }
    } else {
      const mehediRouteMatch = shihabPath.match(/\/(?:reel|reels|p|tv|video|videos|status)\/([A-Za-z0-9_-]+)/i);
      if (mehediRouteMatch && mehediRouteMatch[1] && !/^(?:home|feed|for_you|following)$/i.test(mehediRouteMatch[1])) {
        shihabShortcodes.add(mehediRouteMatch[1]);
        if (mehediHost.includes("x.com") || mehediHost.includes("twitter.com")) {
          mehediRouteAndCardGroupKeys.add(`tw:${mehediRouteMatch[1]}`);
        } else if (mehediHost.includes("tiktok.com")) {
          mehediRouteAndCardGroupKeys.add(`tt:${mehediRouteMatch[1]}`);
        }
      }
      if (mehediHost.includes("facebook.com")) {
        const shihabWatchVid = new URLSearchParams(location.search).get("v") || "";
        if (/^\d{5,}$/.test(shihabWatchVid)) {
          shihabShortcodes.add(shihabWatchVid);
          shihabShortcodes.add(`vid:${shihabWatchVid}`);
        }
      }
    }

    // 3. Permalink links inside this specific <video>'s single-video feed card (strictly max 1 <video> per card!)
    const mehediCardRoot = shihabFindScopedCardForVideo(mehediVid, 1);
    if (mehediCardRoot) {
      const shihabCardAnchors = mehediCardRoot.querySelectorAll("a[href]");
      for (const mehediAnchor of shihabCardAnchors) {
        const shihabHref = mehediAnchor.getAttribute("href") || "";
        const mehediCodeMatch = shihabHref.match(/\/(?:reel|reels|p|tv|video|videos|status|shorts)\/([A-Za-z0-9_-]+)/i);
        if (mehediCodeMatch && mehediCodeMatch[1] && !/^(?:home|feed|audio)$/i.test(mehediCodeMatch[1])) {
          shihabShortcodes.add(mehediCodeMatch[1]);
          if (/^\d{5,}$/.test(mehediCodeMatch[1])) {
            shihabShortcodes.add(`vid:${mehediCodeMatch[1]}`);
          }
          if (mehediHost.includes("x.com") || mehediHost.includes("twitter.com")) {
            mehediRouteAndCardGroupKeys.add(`tw:${mehediCodeMatch[1]}`);
          } else if (mehediHost.includes("tiktok.com")) {
            mehediRouteAndCardGroupKeys.add(`tt:${mehediCodeMatch[1]}`);
          }
        }
        const shihabWatchHrefMatch = shihabHref.match(/[?&]v=(\d{5,})/);
        if (shihabWatchHrefMatch && shihabWatchHrefMatch[1]) {
          shihabShortcodes.add(shihabWatchHrefMatch[1]);
          shihabShortcodes.add(`vid:${shihabWatchHrefMatch[1]}`);
        }
        if (mehediHost.includes("youtube.com")) {
          const mehediYtHrefMatch = shihabHref.match(/(?:[?&]v=|\/shorts\/)([A-Za-z0-9_-]{11})(?:[&#?/]|$)/);
          if (mehediYtHrefMatch && mehediYtHrefMatch[1]) {
            shihabShortcodes.add(mehediYtHrefMatch[1]);
            mehediRouteAndCardGroupKeys.add(`yt:${mehediYtHrefMatch[1]}`);
          }
        }
      }
    }

    // O(1) cross-resolve shortcodes & route IDs -> manifest groupKeys
    for (const shihabCode of shihabShortcodes) {
      const mehediMeta = mehediShortcodeMetaMap.get(shihabCode);
      if (mehediMeta && mehediMeta.mehediGroupKey) {
        mehediRouteAndCardGroupKeys.add(mehediMeta.mehediGroupKey);
      }
      const shihabIndexedHits = shihabStreamsByAltIdMap.get(String(shihabCode));
      if (shihabIndexedHits) {
        for (let i = 0; i < shihabIndexedHits.length; i++) {
          if (shihabIndexedHits[i] && shihabIndexedHits[i].shihabGroupKey) {
            mehediRouteAndCardGroupKeys.add(shihabIndexedHits[i].shihabGroupKey);
          }
        }
      }
    }

    const mehediGroupKeys = new Set([
      ...shihabDirectDomGroupKeys,
      ...shihabExactGroupKeys,
      ...mehediRouteAndCardGroupKeys,
      ...mehediFallbackBlobGroupKeys
    ]);

    // Resolve manifest/fiber titleHint strictly for this exact video's group
    let shihabManifestTitleHint = mehediDirectFiberTitleHint || "";
    if (!shihabManifestTitleHint) {
      for (const mehediGrp of mehediGroupKeys) {
        const shihabHint = shihabGroupKeyTitleMap.get(mehediGrp);
        if (shihabHint) {
          shihabManifestTitleHint = shihabHint;
          break;
        }
      }
    }
    if (!shihabManifestTitleHint) {
      for (const mehediGrp of mehediGroupKeys) {
        const shihabGrpStreams = shihabStreamsByGroupKeyMap.get(mehediGrp);
        if (shihabGrpStreams) {
          for (let i = 0; i < shihabGrpStreams.length; i++) {
            if (shihabGrpStreams[i] && shihabGrpStreams[i].shihabTitleHint) {
              shihabManifestTitleHint = shihabGrpStreams[i].shihabTitleHint;
              break;
            }
          }
          if (shihabManifestTitleHint) break;
        }
      }
    }
    if (!shihabManifestTitleHint) {
      for (const mehediSc of shihabShortcodes) {
        const shihabMeta = mehediShortcodeMetaMap.get(mehediSc);
        if (shihabMeta && shihabMeta.shihabTitleHint) {
          shihabManifestTitleHint = shihabMeta.shihabTitleHint;
          break;
        }
      }
    }

    return {
      mehediCardRoot,
      shihabDirectDomGroupKeys,
      shihabExactBoundUrls,
      shihabExactGroupKeys,
      shihabBoundUrls,
      mehediRouteAndCardGroupKeys,
      mehediFallbackBlobGroupKeys,
      mehediGroupKeys,
      shihabShortcodes,
      shihabManifestTitleHint
    };
  }

  // Extract 100% exact video name or scoped post/Reel caption for the selected <video> ONLY (never bleeding from sibling feed videos!)
  function shihabExtractBestVideoTitle(mehediTargetVideo, mehediPrimaryStream) {
    const shihabHost = (location.hostname || "").toLowerCase();
    const mehediVid = mehediTargetVideo || mehediFindPrimaryVisibleVideo();
    const shihabIsMultiVideoFeed = shihabCollectAllVideosDeep(document, []).length > 1;
    const shihabCtx = shihabResolveVideoContext(mehediVid);

    // 0. If the stream URL itself carries a genuine human-readable filename (e.g. direct MP4/MKV host), keep it ready
    let mehediHumanStreamLeaf = "";
    const shihabProbeStreamUrl = (mehediPrimaryStream && mehediPrimaryStream.shihabStreamUrl) ||
      (mehediVid && (mehediVid.currentSrc || mehediVid.src) && /^https?:/i.test(mehediVid.currentSrc || mehediVid.src) ? (mehediVid.currentSrc || mehediVid.src) : "");
    if (shihabProbeStreamUrl) {
      try {
        const mehediRawLeaf = decodeURIComponent(new URL(shihabProbeStreamUrl, location.href).pathname.split("/").pop() || "");
        if (mehediRawLeaf.includes(".") && !shihabIsGibberishStreamFileLeaf(mehediRawLeaf)) {
          mehediHumanStreamLeaf = mehediCleanCaptionText(mehediRawLeaf.replace(/\.[a-z0-9]{2,5}$/i, ""));
        }
      } catch (_) {}
    }

    // 1. YouTube Watch & Shorts
    if (shihabHost.includes("youtube.com") || shihabHost.includes("youtu.be")) {
      const mehediHintClean = mehediCleanCaptionText(shihabCtx.shihabManifestTitleHint);
      const mehediYtSelectors = location.pathname.startsWith("/shorts/")
        ? [
            "ytd-reel-video-renderer[is-active] h2",
            "ytd-reel-video-renderer[is-active] .ytShortsVideoTitleViewModelShortsVideoTitle",
            "ytd-reel-video-renderer[is-active] yt-formatted-string#text"
          ]
        : [
            "ytd-watch-metadata h1 yt-formatted-string",
            "#above-the-fold #title h1",
            "h1.ytd-video-primary-info-renderer"
          ];
      for (const shihabSel of mehediYtSelectors) {
        const mehediEl = document.querySelector(shihabSel);
        const shihabCandidate = mehediCleanCaptionText(mehediEl ? mehediEl.textContent : "");
        if (shihabCandidate) return shihabCandidate;
      }
      if (mehediHintClean) return mehediHintClean;
      if (!shihabIsMultiVideoFeed) {
        const shihabDocClean = mehediCleanCaptionText(document.title);
        if (shihabDocClean) return shihabDocClean;
      }
    }

    // 2. Instagram Home Feed, Reels, Posts & Stories (strictly scoped to the selected <video>'s fiber/manifest & single-video card!)
    if (shihabHost.includes("instagram.com")) {
      const mehediHintIg = mehediCleanCaptionText(shihabCtx.shihabManifestTitleHint);
      if (mehediHintIg) return mehediHintIg;

      const shihabIgCard = shihabCtx.mehediCardRoot;
      let mehediAuthorHandle = "";
      let shihabBestCaption = "";

      if (shihabIgCard) {
        const mehediLinks = shihabIgCard.querySelectorAll("header a[href^='/'], a[href^='/']");
        for (const shihabLink of mehediLinks) {
          const mehediHref = shihabLink.getAttribute("href") || "";
          const shihabUserMatch = mehediHref.match(/^\/([A-Za-z0-9._]{2,30})\/?$/);
          if (shihabUserMatch && !MEHEDI_RESERVED_IG_PATHS.has(shihabUserMatch[1].toLowerCase())) {
            const mehediLinkText = (shihabLink.textContent || "").trim();
            if (mehediLinkText && /^[A-Za-z0-9._]{2,30}$/.test(mehediLinkText)) {
              mehediAuthorHandle = mehediLinkText;
              break;
            }
            if (!mehediAuthorHandle) {
              mehediAuthorHandle = shihabUserMatch[1];
            }
          }
        }

        const shihabH1 = shihabIgCard.querySelector("h1");
        if (shihabH1) {
          shihabBestCaption = mehediCleanCaptionText(shihabH1.textContent || "");
        }

        if (!shihabBestCaption) {
          const mehediTextNodes = shihabIgCard.querySelectorAll('span[dir="auto"], div[dir="auto"]');
          for (const shihabNode of mehediTextNodes) {
            if (shihabNode.closest("header, button, nav")) continue;
            if (shihabNode.querySelectorAll('div[dir="auto"], span[dir="auto"]').length > 3) continue;

            let mehediRawTxt = (shihabNode.textContent || "").trim();
            if (!mehediRawTxt) continue;
            if (mehediAuthorHandle && mehediRawTxt.toLowerCase() === mehediAuthorHandle.toLowerCase()) continue;
            if (mehediAuthorHandle && mehediRawTxt.toLowerCase().startsWith(mehediAuthorHandle.toLowerCase() + " ")) {
              mehediRawTxt = mehediRawTxt.slice(mehediAuthorHandle.length).trim();
            }
            if (/^(original audio|audio is muted|sponsored|follow|following|liked by\b|view all\b|add a comment|hide translation|see translation)/i.test(mehediRawTxt)) {
              continue;
            }

            const shihabCleaned = mehediCleanCaptionText(mehediRawTxt);
            if (!shihabCleaned) continue;
            if (mehediAuthorHandle && shihabCleaned.toLowerCase() === mehediAuthorHandle.toLowerCase()) continue;

            if (shihabCleaned.length > shihabBestCaption.length) {
              shihabBestCaption = shihabCleaned;
            }
          }
        }
      }

      if (shihabBestCaption) return shihabBestCaption.slice(0, 105).trim();
      if (mehediAuthorHandle) {
        const shihabFirstCode = shihabCtx.shihabShortcodes.values().next().value || "";
        return shihabFirstCode ? `${mehediAuthorHandle} - ${shihabFirstCode}` : `${mehediAuthorHandle} - Instagram Video`;
      }

      if (!shihabIsMultiVideoFeed) {
        const mehediOgIg = document.querySelector('meta[property="og:title"]');
        const shihabOgIgClean = mehediCleanCaptionText(mehediOgIg ? mehediOgIg.getAttribute("content") : "");
        if (shihabOgIgClean) return shihabOgIgClean;
      }

      const mehediCodeFallback = shihabCtx.shihabShortcodes.values().next().value || "";
      if (mehediCodeFallback) return `Instagram Video ${mehediCodeFallback}`;
    }

    // 3. Facebook Reels, Watch & Feed Videos (NEVER query document-wide on multi-video feeds!)
    if (shihabHost.includes("facebook.com") || shihabHost.includes("fb.watch")) {
      const mehediFbHint = mehediCleanCaptionText(shihabCtx.shihabManifestTitleHint);
      const shihabFbCard = shihabCtx.mehediCardRoot;

      if (shihabFbCard) {
        const mehediMsgSelectors = [
          '[data-ad-preview="message"]',
          '[data-ad-comet-preview="message"]',
          '[data-testid="post_message"]'
        ];
        for (const shihabSel of mehediMsgSelectors) {
          const mehediEl = shihabFbCard.querySelector(shihabSel);
          const shihabCap = mehediCleanCaptionText(mehediEl ? mehediEl.textContent : "");
          if (shihabCap) return shihabCap;
        }
      }

      if (mehediFbHint) return mehediFbHint;

      if (shihabFbCard) {
        const mehediAutoDirs = shihabFbCard.querySelectorAll('div[dir="auto"], span[dir="auto"]');
        let shihabBestFbCap = "";
        let mehediFbAuthor = "";
        for (const shihabNode of mehediAutoDirs) {
          if (shihabNode.closest("nav, [role='banner'], [role='navigation'], button, [role='button']")) continue;
          if (shihabNode.querySelectorAll('div[dir="auto"], span[dir="auto"]').length > 2) continue;
          const mehediRect = shihabNode.getBoundingClientRect();
          if (mehediRect.width < 10 || mehediRect.height < 8) continue;
          const shihabClean = mehediCleanCaptionText(shihabNode.textContent || "");
          if (!shihabClean) continue;
          if (shihabNode.closest("h2, h3, strong, a[role='link']") && !mehediFbAuthor && shihabClean.length <= 45) {
            mehediFbAuthor = shihabClean;
            continue;
          }
          if (shihabClean.length >= 4 && shihabClean.length > shihabBestFbCap.length) {
            shihabBestFbCap = shihabClean;
          }
        }
        if (shihabBestFbCap) return shihabBestFbCap;
        if (mehediFbAuthor) {
          const shihabReelId = Array.from(shihabCtx.shihabShortcodes).find((mehediS) => !mehediS.includes(":")) || "";
          return shihabReelId ? `${mehediFbAuthor} - ${shihabReelId}` : `${mehediFbAuthor} - Facebook Video`;
        }
      }

      if (!shihabIsMultiVideoFeed) {
        const shihabDocFb = mehediCleanCaptionText(document.title);
        if (shihabDocFb) return shihabDocFb;
      }
      const mehediFbIdFallback = Array.from(shihabCtx.shihabShortcodes).find((shihabS) => !shihabS.includes(":")) || "";
      if (mehediFbIdFallback) return `Facebook Video ${mehediFbIdFallback}`;
    }

    // 4. X / Twitter
    if (shihabHost.includes("x.com") || shihabHost.includes("twitter.com")) {
      const mehediTweetCard = shihabCtx.mehediCardRoot || (mehediVid && mehediVid.closest("article"));
      if (mehediTweetCard) {
        const shihabTweetTxtEl = mehediTweetCard.querySelector('[data-testid="tweetText"]');
        const mehediUserEl = mehediTweetCard.querySelector('[data-testid="User-Name"]');
        const shihabTweetClean = mehediCleanCaptionText(shihabTweetTxtEl ? shihabTweetTxtEl.textContent : "");
        const mehediHandleMatch = (mehediUserEl ? mehediUserEl.textContent : "").match(/@([A-Za-z0-9_]{1,20})/);
        const shihabHandle = mehediHandleMatch ? mehediHandleMatch[1] : "";
        if (shihabTweetClean) return shihabTweetClean;
        if (shihabHandle) return `${shihabHandle} - X Video`;
      }
      const mehediTwHint = mehediCleanCaptionText(shihabCtx.shihabManifestTitleHint);
      if (mehediTwHint) return mehediTwHint;
    }

    // 4b. LinkedIn Feed & Posts
    if (shihabHost.includes("linkedin.com")) {
      const mehediLiCard = shihabCtx.mehediCardRoot || (mehediVid && mehediVid.closest(".feed-shared-update-v2, article, [data-urn]"));
      if (mehediLiCard) {
        const shihabLiTextEl = mehediLiCard.querySelector('.feed-shared-update-v2__description, .update-components-text, [data-test-id="main-feed-activity-card__commentary"]');
        const mehediLiClean = mehediCleanCaptionText(shihabLiTextEl ? shihabLiTextEl.textContent : "");
        if (mehediLiClean) return mehediLiClean;
        const shihabLiAuthorEl = mehediLiCard.querySelector('.update-components-actor__title, .feed-shared-actor__name');
        const mehediLiAuthor = mehediCleanCaptionText(shihabLiAuthorEl ? shihabLiAuthorEl.textContent : "");
        if (mehediLiAuthor) return `${mehediLiAuthor} - LinkedIn Video`;
      }
      const mehediLiHint = mehediCleanCaptionText(shihabCtx.shihabManifestTitleHint);
      if (mehediLiHint) return mehediLiHint;
    }

    // 5. TikTok
    if (shihabHost.includes("tiktok.com")) {
      const mehediTtScope = shihabCtx.mehediCardRoot || (!shihabIsMultiVideoFeed ? document : null);
      if (mehediTtScope) {
        const shihabDescEl = mehediTtScope.querySelector('[data-e2e="browse-video-desc"], [data-e2e="video-desc"], [data-e2e="new-desc-span"]');
        const mehediAuthorEl = mehediTtScope.querySelector('[data-e2e="video-author-uniqueid"], [data-e2e="browse-username"]');
        const shihabDescClean = mehediCleanCaptionText(shihabDescEl ? shihabDescEl.textContent : "");
        const mehediAuthorClean = mehediCleanCaptionText(mehediAuthorEl ? mehediAuthorEl.textContent : "");
        if (shihabDescClean) return shihabDescClean;
        if (mehediAuthorClean) return `${mehediAuthorClean} - TikTok Video`;
      }
    }

    // 6. Reddit
    if (shihabHost.includes("reddit.com")) {
      const mehediPostEl = mehediVid ? mehediVid.closest("shreddit-post") : (!shihabIsMultiVideoFeed ? document.querySelector("shreddit-post") : null);
      if (mehediPostEl) {
        const shihabPostTitle = mehediCleanCaptionText(mehediPostEl.getAttribute("post-title") || "");
        if (shihabPostTitle) return shihabPostTitle;
      }
      if (shihabCtx.mehediCardRoot) {
        const mehediCardH = shihabCtx.mehediCardRoot.querySelector("h1, h2, [slot='title']");
        const shihabCardHClean = mehediCleanCaptionText(mehediCardH ? mehediCardH.textContent : "");
        if (shihabCardHClean) return shihabCardHClean;
      }
    }

    // 7. Universal Manifest Hint + Genuine Stream Filename + Scoped HTML5 / Course / News Players
    const mehediGenericHint = mehediCleanCaptionText(shihabCtx.shihabManifestTitleHint);
    if (mehediGenericHint) return mehediGenericHint;
    if (mehediHumanStreamLeaf) return mehediHumanStreamLeaf;

    if (mehediVid) {
      const shihabVidAttr = mehediCleanCaptionText(
        mehediVid.getAttribute("title") ||
        mehediVid.getAttribute("aria-label") ||
        mehediVid.getAttribute("data-title") ||
        ""
      );
      if (shihabVidAttr) return shihabVidAttr;

      const mehediFig = mehediVid.closest("figure");
      if (mehediFig) {
        const shihabCapEl = mehediFig.querySelector("figcaption");
        const mehediFigTxt = mehediCleanCaptionText(shihabCapEl ? shihabCapEl.textContent : "");
        if (mehediFigTxt) return mehediFigTxt;
      }

      if (shihabCtx.mehediCardRoot) {
        const mehediHeading = shihabCtx.mehediCardRoot.querySelector("h1, h2, h3, [itemprop='name']");
        const shihabHeadingTxt = mehediCleanCaptionText(mehediHeading ? mehediHeading.textContent : "");
        if (shihabHeadingTxt) return shihabHeadingTxt;
      }
    }

    if (!shihabIsMultiVideoFeed) {
      const mehediOg = document.querySelector('meta[property="og:title"], meta[name="twitter:title"]');
      const shihabOgTxt = mehediCleanCaptionText(mehediOg ? mehediOg.getAttribute("content") : "");
      if (shihabOgTxt) return shihabOgTxt;

      const mehediH1 = document.querySelector("h1");
      const shihabH1Txt = mehediCleanCaptionText(mehediH1 ? mehediH1.textContent : "");
      if (shihabH1Txt) return shihabH1Txt;

      const mehediDocFallback = mehediCleanCaptionText(document.title);
      if (mehediDocFallback) return mehediDocFallback;
    }

    const shihabSiteSlug = shihabHost.replace(/^www\./i, "").split(".")[0] || "Web";
    return shihabSiteSlug.charAt(0).toUpperCase() + shihabSiteSlug.slice(1) + " Video";
  }

  function mehediCheckConfiguredModifier(shihabEvent, mehediKeySetting) {
    const shihabNorm = String(mehediKeySetting || "").trim().toLowerCase();
    if (!shihabNorm || shihabNorm === "none") return false;
    if (shihabNorm === "alt") return Boolean(shihabEvent.altKey);
    if (shihabNorm === "ctrl" || shihabNorm === "control") return Boolean(shihabEvent.ctrlKey);
    if (shihabNorm === "shift") return Boolean(shihabEvent.shiftKey);
    if (shihabNorm === "ins" || shihabNorm === "insert") return shihabEvent.key === "Insert";
    return false;
  }

  function mehediExtractClickedAnchorHref(shihabEvent) {
    try {
      const mehediPath = typeof shihabEvent.composedPath === "function"
        ? shihabEvent.composedPath()
        : (shihabEvent.target ? [shihabEvent.target] : []);
      for (const shihabNode of mehediPath) {
        if (!shihabNode || typeof shihabNode.tagName !== "string") continue;
        const mehediTag = shihabNode.tagName.toUpperCase();
        if (mehediTag === "A" || mehediTag === "AREA") {
          const shihabRawHref = shihabNode.getAttribute("href") || shihabNode.href || "";
          if (!shihabRawHref || shihabRawHref.startsWith("javascript:") || shihabRawHref.startsWith("#")) {
            continue;
          }
          if (/^magnet:\?/i.test(shihabRawHref.trim())) {
            return shihabRawHref.trim();
          }
          const mehediResolved = new URL(shihabRawHref, location.href).href;
          if (mehediResolved.startsWith("http://") || mehediResolved.startsWith("https://")) {
            return mehediResolved;
          }
        }
      }
    } catch (_) {}
    return "";
  }

  function mehediReportTrustedUserGesture(shihabEvent) {
    if (mehediInstanceDisposed) return;
    if (!shihabIsExtensionContextAlive()) {
      mehediDisposeCurrentInstance();
      return;
    }
    // Strictly require real physical user input (synthetic script clicks have isTrusted === false)
    if (!shihabEvent || shihabEvent.isTrusted !== true) {
      return;
    }

    const shihabPreventHit = mehediCheckConfiguredModifier(shihabEvent, shihabConfiguredPreventKey);
    const mehediForceHit = mehediCheckConfiguredModifier(shihabEvent, mehediConfiguredForceKey);

    const shihabEvtType = String(shihabEvent.type || "").toLowerCase();
    let mehediIsDownloadGesture = false;
    if (shihabEvtType === "pointerdown" || shihabEvtType === "mousedown") {
      mehediIsDownloadGesture =
        shihabEvent.button === 0 || shihabEvent.button === 1 || shihabEvent.button === 2;
    } else if (shihabEvtType === "click" || shihabEvtType === "auxclick" || shihabEvtType === "contextmenu") {
      mehediIsDownloadGesture = true;
    } else if (shihabEvtType === "keydown") {
      const shihabKey = String(shihabEvent.key || "");
      const shihabKeyTarget = shihabEvent.target;
      const shihabTargetTag = shihabKeyTarget && shihabKeyTarget.tagName ? shihabKeyTarget.tagName.toUpperCase() : "";
      const shihabIsEditableTarget = Boolean(
        shihabTargetTag === "INPUT" ||
        shihabTargetTag === "TEXTAREA" ||
        shihabTargetTag === "SELECT" ||
        (shihabKeyTarget && shihabKeyTarget.isContentEditable)
      );
      mehediIsDownloadGesture =
        (!shihabIsEditableTarget && (shihabKey === "Enter" || shihabKey === " ")) ||
        mehediForceHit ||
        ((shihabEvent.ctrlKey || shihabEvent.metaKey) &&
          (shihabKey === "s" || shihabKey === "S" || shihabKey === "j" || shihabKey === "J"));
    }

    if (!shihabPreventHit && !mehediForceHit && !mehediIsDownloadGesture) {
      return;
    }

    const mehediClickedHref = mehediIsDownloadGesture ? mehediExtractClickedAnchorHref(shihabEvent) : "";
    // Intercept direct left-clicks on magnet:? links when Prevent key (Alt) is not held
    if (
      shihabEvtType === "click" &&
      shihabEvent.button === 0 &&
      !shihabPreventHit &&
      mehediClickedHref &&
      /^magnet:\?/i.test(mehediClickedHref)
    ) {
      try {
        shihabEvent.preventDefault();
        shihabEvent.stopPropagation();
        chrome.runtime.sendMessage({
          shihabCmd: "mehedi_send_download_now",
          shihabUrl: mehediClickedHref,
          mehediReferrer: location.href,
          mehediTabTitle: document.title || ""
        }).catch(() => {});
      } catch (_) {}
      return;
    }
    const shihabNow = Date.now();
    const shihabCurrentPageUrl = location.href;
    let shihabIsActionButtonClick = false;
    try {
      const shihabTargetEl = shihabEvent.target && shihabEvent.target.nodeType === 1
        ? shihabEvent.target
        : (shihabEvent.target && shihabEvent.target.parentElement);
      if (shihabTargetEl && typeof shihabTargetEl.closest === "function") {
        shihabIsActionButtonClick = Boolean(
          shihabTargetEl.closest('button, [role="button"], input[type="button"], input[type="submit"], [download]')
        );
      }
    } catch (_) {}
    const shihabPlainNoHrefGesture = !mehediClickedHref && !shihabIsActionButtonClick && !shihabPreventHit && !mehediForceHit;
    if (
      shihabPlainNoHrefGesture &&
      !mehediLastSentAltState &&
      !shihabLastSentForceState &&
      shihabCurrentPageUrl === mehediLastSentGesturePageUrl &&
      shihabNow - mehediLastSentGestureMs < 1500
    ) {
      return;
    }
    if (
      shihabNow - mehediLastSentGestureMs < 160 &&
      mehediClickedHref === shihabLastSentGestureHref &&
      shihabCurrentPageUrl === mehediLastSentGesturePageUrl &&
      shihabPreventHit === mehediLastSentAltState &&
      mehediForceHit === shihabLastSentForceState
    ) {
      return;
    }

    mehediLastSentGestureMs = shihabNow;
    shihabLastSentGestureHref = mehediClickedHref;
    mehediLastSentGesturePageUrl = shihabCurrentPageUrl;
    mehediLastSentAltState = shihabPreventHit;
    shihabLastSentForceState = mehediForceHit;

    try {
      chrome.runtime.sendMessage({
        shihabCmd: "mehedi_user_download_gesture",
        shihabPageUrl: shihabCurrentPageUrl,
        mehediClickedHref: mehediClickedHref,
        shihabAltPressed: shihabPreventHit,
        mehediInsertPressed: mehediForceHit,
        shihabIsGesture: mehediIsDownloadGesture
      }).catch(() => {});
    } catch (mehediRuntimeErr) {
      // Extension context reloaded
    }
  }

  function shihabCollectAllVideosDeep(mehediRootNode, shihabOutList) {
    const mehediRoot = mehediRootNode || document;
    const shihabList = shihabOutList || [];
    try {
      const mehediDirect = mehediRoot.querySelectorAll("video");
      for (const shihabVid of mehediDirect) {
        shihabList.push(shihabVid);
      }
      const mehediShadowHosts = mehediRoot.querySelectorAll(SHIHAB_SHADOW_VIDEO_HOSTS_SELECTOR);
      for (const shihabEl of mehediShadowHosts) {
        if (shihabEl && shihabEl.shadowRoot && shihabEl !== mehediShadowHost) {
          shihabCollectAllVideosDeep(shihabEl.shadowRoot, shihabList);
        }
      }
    } catch (mehediDomErr) {
      // Ignore cross-origin shadow traversal errors
    }
    return shihabList;
  }

  function shihabPushRichStreamEntry(mehediTargetArray, shihabEntryObj) {
    if (!shihabEntryObj || !shihabEntryObj.shihabStreamUrl) return;
    const mehediClean = mehediNormalizeClientStreamUrl(shihabEntryObj.shihabStreamUrl);
    if (!mehediClean) return;
    if (/\.(m4s|ts)(\?|$)/i.test(mehediClean)) return;

    const shihabExisting = mehediTargetArray.find((mehediItem) => mehediItem.shihabStreamUrl === mehediClean);
    if (shihabExisting) {
      if (!shihabExisting.shihabResolution && shihabEntryObj.shihabResolution) {
        shihabExisting.shihabResolution = shihabEntryObj.shihabResolution;
        shihabExisting.shihabQualityLabel = shihabEntryObj.shihabQualityLabel || shihabEntryObj.shihabResolution;
        shihabExisting.mehediBadge = `${(shihabExisting.shihabExt || "mp4").toUpperCase()} ${shihabEntryObj.shihabResolution}`;
      }
      if (shihabExisting.mehediDisplaySizeBytes <= 0 && shihabEntryObj.mehediDisplaySizeBytes > 0) {
        shihabExisting.mehediDisplaySizeBytes = shihabEntryObj.mehediDisplaySizeBytes;
        shihabExisting.shihabSizeBytes = shihabEntryObj.shihabSizeBytes;
      }
      return;
    }

    const mehediIsAud = Boolean(shihabEntryObj.shihabIsAudioOnly || shihabEntryObj.mehediMediaKind === "AUDIO");
    const mehediMimeLower = String(shihabEntryObj.mehediMimeType || "").toLowerCase();
    const shihabExtGuess = shihabEntryObj.shihabExt ||
      (mehediMimeLower.includes("mpegurl") || /\.m3u8(\?|#|$)/i.test(mehediClean)
        ? "m3u8"
        : (mehediMimeLower.includes("webm") || /\.webm(\?|#|$)/i.test(mehediClean)
            ? "webm"
            : (mehediMimeLower.includes("matroska") || /\.mkv(\?|#|$)/i.test(mehediClean)
                ? "mkv"
                : (mehediMimeLower.includes("quicktime") || /\.mov(\?|#|$)/i.test(mehediClean)
                    ? "mov"
                    : (/\.m4v(\?|#|$)/i.test(mehediClean)
                        ? "m4v"
                        : (mehediIsAud ? "m4a" : "mp4"))))));
    const mehediRes = shihabEntryObj.shihabResolution || "";
    const shihabFmtUpper = (shihabExtGuess === "m3u8" ? "MP4" : shihabExtGuess).toUpperCase();
    const mehediDefaultBadge = mehediIsAud
      ? `${shihabFmtUpper} ${mehediRes || "Audio"}`
      : `${shihabFmtUpper} ${mehediRes || (shihabExtGuess === "m3u8" ? "HLS" : "HD")}`;

    const shihabPrimaryBytes = typeof shihabEntryObj.shihabSizeBytes === "number" ? shihabEntryObj.shihabSizeBytes : -1;
    const mehediDisplayBytes = typeof shihabEntryObj.mehediDisplaySizeBytes === "number" && shihabEntryObj.mehediDisplaySizeBytes > 0
      ? shihabEntryObj.mehediDisplaySizeBytes
      : shihabPrimaryBytes;

    mehediTargetArray.push({
      shihabStreamUrl: mehediClean,
      mehediMimeType: shihabEntryObj.mehediMimeType || (shihabExtGuess === "m3u8" ? "application/vnd.apple.mpegurl" : ""),
      shihabExt: shihabExtGuess,
      mehediMediaKind: mehediIsAud ? "AUDIO" : "VIDEO",
      mehediTrackRole: shihabEntryObj.mehediTrackRole || (mehediIsAud ? "audio" : "progressive"),
      shihabWidth: Number(shihabEntryObj.shihabWidth || 0),
      mehediHeight: Number(shihabEntryObj.mehediHeight || 0),
      mehediDurationSec: Number(shihabEntryObj.mehediDurationSec || 0),
      shihabResolution: mehediRes,
      shihabQualityLabel: shihabEntryObj.shihabQualityLabel || (mehediIsAud ? "Audio" : mehediRes),
      mehediBadge: shihabEntryObj.mehediBadge || mehediDefaultBadge,
      shihabSizeBytes: shihabPrimaryBytes,
      mehediDisplaySizeBytes: mehediDisplayBytes,
      shihabDisplayName: shihabEntryObj.shihabDisplayName || "",
      shihabTitleHint: shihabEntryObj.shihabTitleHint || "",
      mehediResolvedFileName: shihabEntryObj.mehediResolvedFileName || "",
      shihabGroupKey: shihabEntryObj.shihabGroupKey || "",
      mehediShortcode: shihabEntryObj.mehediShortcode || "",
      mehediAltIds: Array.isArray(shihabEntryObj.mehediAltIds) ? shihabEntryObj.mehediAltIds : [],
      mehediAudioCompanionUrl: shihabEntryObj.mehediAudioCompanionUrl || "",
      shihabAudioCompanionBytes: typeof shihabEntryObj.shihabAudioCompanionBytes === "number" ? shihabEntryObj.shihabAudioCompanionBytes : -1,
      mehediParentMasterUrl: shihabEntryObj.mehediParentMasterUrl || "",
      shihabFrameId: typeof shihabEntryObj.shihabFrameId === "number" ? shihabEntryObj.shihabFrameId : mehediMyFrameId,
      shihabOrigin: shihabEntryObj.shihabOrigin || "",
      mehediCapturedReferer: shihabEntryObj.mehediCapturedReferer || "",
      mehediOmitCookies: Boolean(shihabEntryObj.mehediOmitCookies),
      shihabIsAudioOnly: mehediIsAud
    });
  }

  function shihabPickFreshestMatchingGroup(mehediCandidateSet) {
    if (!mehediCandidateSet || mehediCandidateSet.size === 0) return "";
    let shihabBestGroup = "";
    let mehediBestTs = -1;
    for (const shihabGrp of mehediCandidateSet) {
      if (!shihabGrp) continue;
      const mehediGrpMaxTs = shihabMaxDetectedAtByGroupMap.get(shihabGrp);
      if (mehediGrpMaxTs !== undefined && mehediGrpMaxTs >= mehediBestTs) {
        mehediBestTs = mehediGrpMaxTs;
        shihabBestGroup = shihabGrp;
      }
    }
    // CRITICAL: NEVER return a phantom groupKey that has 0 entries in shihabTabStreamsCache!
    return shihabBestGroup;
  }

  function mehediIsStreamCompatibleWithVideoElement(shihabStreamItem, mehediVidEl) {
    if (!shihabStreamItem || !mehediVidEl) return true;
    const shihabIsAud = Boolean(shihabStreamItem.shihabIsAudioOnly || shihabStreamItem.mehediMediaKind === "AUDIO");

    // 1. Duration guard: reject streams from shorter/longer unrelated feed videos (e.g. 35s Reel vs 3:01 clip)
    const mehediLiveDur = Number(mehediVidEl.duration);
    const shihabStreamDur = Number(shihabStreamItem.mehediDurationSec || 0);
    if (Number.isFinite(mehediLiveDur) && mehediLiveDur > 2 && shihabStreamDur > 2) {
      const mehediMaxDrift = Math.max(4.5, mehediLiveDur * 0.10);
      if (Math.abs(shihabStreamDur - mehediLiveDur) > mehediMaxDrift) {
        return false;
      }
    }

    // 2. Aspect-ratio guard on video tracks: reject portrait 9:16 streams when <video> is 1:1 square or 16:9 landscape (and vice versa)
    if (!shihabIsAud) {
      const shihabVidW = Number(mehediVidEl.videoWidth || 0);
      const mehediVidH = Number(mehediVidEl.videoHeight || 0);
      const shihabStrW = Number(shihabStreamItem.shihabWidth || 0);
      const mehediStrH = Number(shihabStreamItem.mehediHeight || 0);
      if (shihabVidW > 0 && mehediVidH > 0 && shihabStrW > 0 && mehediStrH > 0) {
        const shihabLiveRatio = shihabVidW / mehediVidH;
        const mehediStrRatio = shihabStrW / mehediStrH;
        if (Math.abs(mehediStrRatio - shihabLiveRatio) / shihabLiveRatio > 0.20) {
          return false;
        }
      }
    }

    return true;
  }

  // Resolve ONE winning shihabGroupKey for mehediActiveVideo so unrelated preloaded/earlier videos NEVER mix!
  function mehediResolveWinningGroupForVideo(mehediActiveVideo, shihabCtx, mehediIsMultiVideoFeed, mehediHasDirectDomItems) {
    // Priority 0: Direct DOM <video> React Fiber / YouTube #movie_player binding on this exact <video> node!
    if (shihabCtx.shihabDirectDomGroupKeys && shihabCtx.shihabDirectDomGroupKeys.size > 0) {
      const shihabDirectExactSet = new Set();
      for (const mehediDirGrp of shihabCtx.shihabDirectDomGroupKeys) {
        if (shihabCtx.shihabExactGroupKeys.has(mehediDirGrp)) {
          shihabDirectExactSet.add(mehediDirGrp);
        }
      }
      const mehediPickedDirectExact = shihabPickFreshestMatchingGroup(shihabDirectExactSet);
      if (mehediPickedDirectExact) return mehediPickedDirectExact;
      const shihabPickedDirect = shihabPickFreshestMatchingGroup(shihabCtx.shihabDirectDomGroupKeys);
      if (shihabPickedDirect) return shihabPickedDirect;
    }

    // Priority 1: Byte-exact FNV-1a ftyp/moof MSE SourceBuffer.appendBuffer match
    if (shihabCtx.shihabExactGroupKeys.size > 0) {
      const shihabExactRouteSet = new Set();
      for (const mehediExGrp of shihabCtx.shihabExactGroupKeys) {
        if (shihabCtx.mehediRouteAndCardGroupKeys.has(mehediExGrp)) {
          shihabExactRouteSet.add(mehediExGrp);
        }
      }
      const mehediPickedExactRoute = shihabPickFreshestMatchingGroup(shihabExactRouteSet);
      if (mehediPickedExactRoute) return mehediPickedExactRoute;
      const shihabPickedExact = shihabPickFreshestMatchingGroup(shihabCtx.shihabExactGroupKeys);
      if (shihabPickedExact) return shihabPickedExact;
    }

    // Priority 2: Route URL (/reel/..., /p/..., ?v=...) or single-video card permalink shortcode / altId match
    if (shihabCtx.mehediRouteAndCardGroupKeys.size > 0) {
      const mehediPickedRoute = shihabPickFreshestMatchingGroup(shihabCtx.mehediRouteAndCardGroupKeys);
      if (mehediPickedRoute) return mehediPickedRoute;
    }

    // Priority 3: Fallback MSE blob binding
    if (shihabCtx.mehediFallbackBlobGroupKeys.size > 0) {
      const shihabPickedBlob = shihabPickFreshestMatchingGroup(shihabCtx.mehediFallbackBlobGroupKeys);
      if (shihabPickedBlob) return shihabPickedBlob;
    }

    // Priority 4: Live scoped video caption match against manifest titleHints
    const mehediLiveCaption = shihabExtractBestVideoTitle(mehediActiveVideo).toLowerCase();
    if (mehediLiveCaption && mehediLiveCaption.length >= 8) {
      const shihabProbeSlice = mehediLiveCaption.slice(0, 24);
      for (const [mehediGrpKey, shihabHintVal] of shihabGroupKeyTitleMap.entries()) {
        const mehediHintLower = mehediCleanCaptionText(shihabHintVal).toLowerCase();
        if (mehediHintLower && mehediHintLower.length >= 8 &&
            (mehediLiveCaption.includes(mehediHintLower.slice(0, 24)) || mehediHintLower.includes(shihabProbeSlice))) {
          const shihabMatchingStreams = shihabTabStreamsCache.filter(
            (mehediIt) => mehediIt && mehediIt.shihabGroupKey === mehediGrpKey && mehediIsStreamCompatibleWithVideoElement(mehediIt, mehediActiveVideo)
          );
          if (shihabMatchingStreams.length > 0) return mehediGrpKey;
        }
      }
    }

    const shihabHostLower = (location.hostname || "").toLowerCase();
    const shihabIsStrictSocialFeedHost =
      shihabHostLower.includes("facebook.com") ||
      shihabHostLower.includes("fb.watch") ||
      shihabHostLower.includes("instagram.com") ||
      shihabHostLower.includes("youtube.com");

    // NEVER guess a random/newest stream group on strict Facebook/Instagram/YouTube feeds,
    // or when this video ALREADY has direct DOM streams (e.g. LinkedIn data-sources) or an explicit feed identity not yet in cache!
    const shihabHasExplicitIdentity = Boolean(
      (shihabCtx.shihabDirectDomGroupKeys && shihabCtx.shihabDirectDomGroupKeys.size > 0) ||
      (shihabCtx.shihabExactGroupKeys && shihabCtx.shihabExactGroupKeys.size > 0) ||
      (shihabCtx.mehediRouteAndCardGroupKeys && shihabCtx.mehediRouteAndCardGroupKeys.size > 0)
    );
    if (mehediHasDirectDomItems) {
      return "";
    }
    if (mehediIsMultiVideoFeed && (shihabIsStrictSocialFeedHost || shihabHasExplicitIdentity)) {
      return "";
    }

    // Priority 5: Aspect/duration-compatible newest group in shihabTabStreamsCache (preferring streams from THIS frame!)
    // On non-FB/IG/YT multi-video feeds (X, LinkedIn, Reddit, Vimeo, movie grids), allow Priority 5 when this <video>
    // is actively playing or MSE blob:-backed or explicitly selected, so real streams are never dropped to 0!
    const shihabHoveredVidCur = shihabReadWeakRef(mehediHoveredVideoRef);
    const shihabLockedVidCur = shihabReadWeakRef(mehediLockedDropdownVideoRef);
    const mehediAllowMultiFeedMatch =
      !mehediIsMultiVideoFeed ||
      Boolean(
        mehediActiveVideo &&
        ((!mehediActiveVideo.paused && !mehediActiveVideo.ended) ||
         String(mehediActiveVideo.currentSrc || mehediActiveVideo.src || "").startsWith("blob:") ||
         mehediActiveVideo === shihabHoveredVidCur ||
         mehediActiveVideo === shihabLockedVidCur)
      );
    if (!mehediAllowMultiFeedMatch) {
      return "";
    }

    const mehediSameFramePool = mehediMyFrameId >= 0
      ? shihabTabStreamsCache.filter((shihabR) => shihabR && typeof shihabR.shihabFrameId === "number" && shihabR.shihabFrameId === mehediMyFrameId)
      : [];
    const shihabSearchPool = mehediSameFramePool.length > 0 ? mehediSameFramePool : shihabTabStreamsCache;

    let shihabNewestCompatGroup = "";
    let mehediNewestCompatTs = -1;
    for (const shihabRec of shihabSearchPool) {
      if (!shihabRec || !shihabRec.shihabGroupKey) continue;
      if (!mehediIsStreamCompatibleWithVideoElement(shihabRec, mehediActiveVideo)) continue;
      const mehediTs = shihabRec.shihabDetectedAt || 0;
      if (mehediTs > mehediNewestCompatTs) {
        mehediNewestCompatTs = mehediTs;
        shihabNewestCompatGroup = shihabRec.shihabGroupKey;
      }
    }
    return shihabNewestCompatGroup;
  }

  function shihabCountSubstantiveDomVideos(mehediVidList) {
    if (!Array.isArray(mehediVidList) || mehediVidList.length <= 1) {
      return Array.isArray(mehediVidList) ? mehediVidList.length : 0;
    }
    let shihabCount = 0;
    for (const shihabV of mehediVidList) {
      if (!shihabV || !shihabV.isConnected) continue;
      try {
        const shihabR = shihabV.getBoundingClientRect();
        if (!((shihabR.width >= 100 && shihabR.height >= 60) || shihabV.readyState >= 1)) {
          continue;
        }
        const mehediSt = window.getComputedStyle(shihabV);
        if (mehediSt.display === "none" || mehediSt.visibility === "hidden" || parseFloat(mehediSt.opacity || "1") < 0.05) {
          continue;
        }
        shihabCount++;
      } catch (_) {
        shihabCount++;
      }
    }
    return shihabCount;
  }

  // Collect streams with Strict Single-Video Isolation on FB/IG/YT and accurate universal matching on ALL other sites!
  function shihabCollectMediaFromDomAndSniffer(mehediActiveVideo) {
    const mehediCombined = [];
    // Strict DRM/EME compliance: NEVER expose streams for a video element with active EME mediaKeys!
    if (mehediActiveVideo && mehediActiveVideo.mediaKeys) {
      mehediCombined.mehediRawSniffed = [];
      return mehediCombined;
    }
    const shihabAllDomVideos = shihabCollectAllVideosDeep(document, []);
    const mehediIsMultiVideoFeed = shihabCountSubstantiveDomVideos(shihabAllDomVideos) > 1;
    const shihabHostLower = (location.hostname || "").toLowerCase();
    const shihabIsStrictSocialFeedHost =
      shihabHostLower.includes("facebook.com") ||
      shihabHostLower.includes("fb.watch") ||
      shihabHostLower.includes("instagram.com") ||
      shihabHostLower.includes("youtube.com");
    const shihabCtx = shihabResolveVideoContext(mehediActiveVideo);

    const mehediIntrinsicRes = (mehediActiveVideo && mehediActiveVideo.videoWidth > 0 && mehediActiveVideo.videoHeight > 0)
      ? mehediHeightToQualityLabel(mehediActiveVideo.videoWidth, mehediActiveVideo.videoHeight)
      : "";

    // 1. Direct HTML5 <video src="https://...">, <source> children, and LinkedIn / generic data-sources JSON on mehediActiveVideo
    const mehediDirectDomItems = [];
    if (mehediActiveVideo) {
      const shihabDirectSrc = mehediActiveVideo.currentSrc || mehediActiveVideo.src || "";
      if (shihabDirectSrc.startsWith("http://") || shihabDirectSrc.startsWith("https://")) {
        mehediDirectDomItems.push({
          shihabStreamUrl: shihabDirectSrc,
          shihabResolution: mehediIntrinsicRes,
          mehediTrackRole: "progressive"
        });
      }
      const mehediSourceTags = mehediActiveVideo.querySelectorAll("source[src]");
      for (const shihabSrcTag of mehediSourceTags) {
        const mehediRawLbl = shihabSrcTag.getAttribute("label") || shihabSrcTag.getAttribute("res") || shihabSrcTag.getAttribute("size") || "";
        const shihabParsedRes = mehediRawLbl.match(/^\d{3,4}$/)
          ? `${mehediRawLbl}p`
          : ((mehediRawLbl.match(/(\d{3,4}p)/i) || [])[1] || "");
        mehediDirectDomItems.push({
          shihabStreamUrl: shihabSrcTag.src || "",
          mehediMimeType: shihabSrcTag.getAttribute("type") || "",
          shihabResolution: shihabParsedRes ? shihabParsedRes.toLowerCase() : "",
          mehediTrackRole: "progressive"
        });
      }
      // LinkedIn / generic HTML5 data-sources JSON directly attached to this exact <video> or its single-video wrapper
      const shihabDsHolder = mehediActiveVideo.hasAttribute("data-sources")
        ? mehediActiveVideo
        : (mehediActiveVideo.parentElement && typeof mehediActiveVideo.parentElement.closest === "function" ? mehediActiveVideo.parentElement.closest("[data-sources]") : null);
      if (shihabDsHolder && shihabCountVideosInSubtree(shihabDsHolder) <= 1) {
        const mehediDsRaw = shihabDsHolder.getAttribute("data-sources") || "";
        if (mehediDsRaw.trim().startsWith("[")) {
          try {
            const shihabDsArr = JSON.parse(mehediDsRaw);
            const mehediDsRef = shihabSafeHttpRef();
            const shihabDsOrg = shihabSafeHttpOrg();
            for (const mehediDsItem of shihabDsArr) {
              if (!mehediDsItem || typeof mehediDsItem.src !== "string" || !mehediDsItem.src.startsWith("http")) continue;
              const mehediDsType = String(mehediDsItem.type || "video/mp4").toLowerCase();
              const shihabDsResM = mehediDsItem.src.match(/(?:mp4-|\/)(\d{3,4}p)(?:[^0-9]|$)/i);
              const mehediDsLiM = mehediDsItem.src.match(/\/playlist\/vid\/(?:v\d+\/)?([A-Za-z0-9_-]{6,})\//i);
              const mehediDsIsHls = mehediDsType.includes("mpegurl") || /\.m3u8(\?|#|$)|\/hls-manifest\b/i.test(mehediDsItem.src);
              mehediDirectDomItems.push({
                shihabStreamUrl: mehediDsItem.src,
                mehediMimeType: mehediDsIsHls ? "application/vnd.apple.mpegurl" : mehediDsType,
                shihabExt: mehediDsIsHls ? "m3u8" : "mp4",
                shihabResolution: shihabDsResM ? shihabDsResM[1].toLowerCase() : mehediHeightToQualityLabel(mehediDsItem.width, mehediDsItem.height),
                shihabGroupKey: mehediDsLiM ? `li:${mehediDsLiM[1]}` : "",
                mehediTrackRole: "progressive",
                mehediCapturedReferer: mehediDsRef,
                shihabOrigin: shihabDsOrg
              });
            }
          } catch (_) {}
        }
      }
    }

    // 2. Strict Single-Group Isolation across background-sniffed & manifest-extracted streams
    let shihabScopedBgStreams = [];
    const mehediWinningGroupKey = mehediResolveWinningGroupForVideo(
      mehediActiveVideo,
      shihabCtx,
      mehediIsMultiVideoFeed,
      mehediDirectDomItems.length > 0
    );

    if (mehediWinningGroupKey) {
      const shihabGroupCandidates = shihabStreamsByGroupKeyMap.get(mehediWinningGroupKey) || [];
      for (let i = 0; i < shihabGroupCandidates.length; i++) {
        const mehediItem = shihabGroupCandidates[i];
        if (mehediItem && mehediItem.shihabStreamUrl && mehediIsStreamCompatibleWithVideoElement(mehediItem, mehediActiveVideo)) {
          shihabScopedBgStreams.push(mehediItem);
        }
      }
      if (shihabCtx.shihabExactBoundUrls.size > 0) {
        for (const shihabExUrl of shihabCtx.shihabExactBoundUrls) {
          const shihabUrlHits = shihabStreamsByUrlMap.get(shihabExUrl);
          if (!shihabUrlHits) continue;
          for (let i = 0; i < shihabUrlHits.length; i++) {
            const mehediItem = shihabUrlHits[i];
            if (mehediItem && !mehediItem.shihabGroupKey && mehediIsStreamCompatibleWithVideoElement(mehediItem, mehediActiveVideo)) {
              shihabScopedBgStreams.push(mehediItem);
            }
          }
        }
      }
    } else if (shihabCtx.shihabBoundUrls.size > 0 || shihabCtx.shihabShortcodes.size > 0) {
      shihabScopedBgStreams = shihabTabStreamsCache.filter((mehediItem) => {
        if (!mehediItem || !mehediItem.shihabStreamUrl) return false;
        if (!mehediIsStreamCompatibleWithVideoElement(mehediItem, mehediActiveVideo)) return false;
        if (shihabCtx.shihabBoundUrls.has(mehediItem.shihabStreamUrl)) return true;
        if (mehediItem.mehediParentMasterUrl && shihabCtx.shihabBoundUrls.has(mehediItem.mehediParentMasterUrl)) return true;
        if (mehediItem.mehediShortcode && shihabCtx.shihabShortcodes.has(mehediItem.mehediShortcode)) return true;
        if (Array.isArray(mehediItem.mehediAltIds) && mehediItem.mehediAltIds.some((shihabId) => shihabCtx.shihabShortcodes.has(String(shihabId)))) {
          return true;
        }
        // Also match video asset ID substring in stream URL (e.g. X poster ext_tw_video_thumb/<id> vs video.twimg.com/ext_tw_video/<id>/...)
        for (const shihabCode of shihabCtx.shihabShortcodes) {
          if (shihabCode.length >= 8 && !shihabCode.includes(":") && mehediItem.shihabStreamUrl.includes(shihabCode)) {
            return true;
          }
        }
        return false;
      });
    }

    // Fallback for ungrouped sniffed streams (preferring streams from THIS frame; NEVER blocked by unrelated stale groups!)
    const shihabCtxHasExplicitId = Boolean(
      (shihabCtx.shihabDirectDomGroupKeys && shihabCtx.shihabDirectDomGroupKeys.size > 0) ||
      (shihabCtx.shihabExactGroupKeys && shihabCtx.shihabExactGroupKeys.size > 0) ||
      (shihabCtx.mehediRouteAndCardGroupKeys && shihabCtx.mehediRouteAndCardGroupKeys.size > 0)
    );
    const shihabHovVid = shihabReadWeakRef(mehediHoveredVideoRef);
    const shihabLckVid = shihabReadWeakRef(mehediLockedDropdownVideoRef);
    const mehediAllowMultiFeedFallback =
      !mehediIsMultiVideoFeed ||
      Boolean(
        !shihabIsStrictSocialFeedHost &&
        !shihabCtxHasExplicitId &&
        mehediActiveVideo &&
        ((!mehediActiveVideo.paused && !mehediActiveVideo.ended) ||
         String(mehediActiveVideo.currentSrc || mehediActiveVideo.src || "").startsWith("blob:") ||
         mehediActiveVideo === shihabHovVid ||
         mehediActiveVideo === shihabLckVid)
      );

    const mehediCanUseUngroupedFallback =
      !shihabPageHasDrmProtectedMedia &&
      shihabScopedBgStreams.length === 0 &&
      mehediDirectDomItems.length === 0 &&
      shihabTabStreamsCache.length > 0 &&
      mehediAllowMultiFeedFallback;

    if (mehediCanUseUngroupedFallback) {
      const mehediSameFramePool = mehediMyFrameId >= 0
        ? shihabTabStreamsCache.filter((mehediIt) => mehediIt && typeof mehediIt.shihabFrameId === "number" && mehediIt.shihabFrameId === mehediMyFrameId)
        : [];
      const shihabPool = mehediSameFramePool.length > 0 ? mehediSameFramePool : shihabTabStreamsCache;

      const mehediCompatUngrouped = shihabPool.filter(
        (mehediIt) => mehediIt && !mehediIt.shihabGroupKey && mehediIsStreamCompatibleWithVideoElement(mehediIt, mehediActiveVideo)
      );
      if (mehediCompatUngrouped.length > 0) {
        shihabScopedBgStreams = mehediCompatUngrouped;
      } else if (!shihabIsStrictSocialFeedHost) {
        // Last resort on non-FB/IG/YT pages: pick ONE single freshest compatible group (NEVER flatten multiple groups together!)
        let shihabBestGrp = "";
        let mehediBestTs = -1;
        for (const mehediIt of shihabPool) {
          if (!mehediIt || !mehediIt.shihabGroupKey) continue;
          if (!mehediIsStreamCompatibleWithVideoElement(mehediIt, mehediActiveVideo)) continue;
          const shihabTs = mehediIt.shihabDetectedAt || 0;
          if (shihabTs >= mehediBestTs) {
            mehediBestTs = shihabTs;
            shihabBestGrp = mehediIt.shihabGroupKey;
          }
        }
        if (shihabBestGrp) {
          shihabScopedBgStreams = shihabPool.filter(
            (mehediIt) => mehediIt && mehediIt.shihabGroupKey === shihabBestGrp && mehediIsStreamCompatibleWithVideoElement(mehediIt, mehediActiveVideo)
          );
        }
      }
    }

    for (const shihabBgItem of shihabScopedBgStreams) {
      shihabPushRichStreamEntry(mehediCombined, shihabBgItem);
    }
    for (const mehediDomItem of mehediDirectDomItems) {
      shihabPushRichStreamEntry(mehediCombined, mehediDomItem);
    }

    // 3. Single-video / iframe page fallbacks (Performance Resource Timing + OpenGraph video meta)
    if ((!mehediIsMultiVideoFeed || window !== window.top) && !shihabIsStrictSocialFeedHost && !shihabPageHasDrmProtectedMedia && mehediCombined.length === 0) {
      try {
        const mehediPerfEntries = performance.getEntriesByType("resource") || [];
        for (let shihabIdx = mehediPerfEntries.length - 1; shihabIdx >= 0 && mehediCombined.length < 8; shihabIdx--) {
          const mehediRes = mehediPerfEntries[shihabIdx];
          const shihabResName = mehediRes && mehediRes.name ? mehediRes.name : "";
          if (/\.(ts|m4s|cmfv|cmfa|vtt|srt|js|css|png|jpg|jpeg|webp|svg|woff2?)(\?|#|$)/i.test(shihabResName)) continue;
          if (/(?:thumbnail|poster|sprite|vtt|caption)/i.test(shihabResName)) continue;
          const shihabLooksLikeMediaRes =
            /\.(mp4|mkv|webm|mov|m4v|m3u8)(\?|#|$)/i.test(shihabResName) ||
            /dms\.licdn\.com\/playlist\/vid\/.*(?:mp4-|hls-manifest)/i.test(shihabResName) ||
            /video\.twimg\.com\/.*\.(mp4|m3u8)/i.test(shihabResName) ||
            /mime_type=video/i.test(shihabResName);
          if (shihabLooksLikeMediaRes) {
            const shihabTransferBytes = mehediRes.encodedBodySize > 65536 ? mehediRes.encodedBodySize : -1;
            const mehediIsHlsPerf = /\.m3u8(\?|#|$)|hls-manifest/i.test(shihabResName);
            shihabPushRichStreamEntry(mehediCombined, {
              shihabStreamUrl: shihabResName,
              mehediMimeType: mehediIsHlsPerf ? "application/vnd.apple.mpegurl" : "",
              shihabExt: mehediIsHlsPerf ? "m3u8" : "",
              shihabSizeBytes: shihabTransferBytes,
              shihabResolution: mehediIntrinsicRes
            });
          }
        }
      } catch (shihabPerfErr) {
        // Ignore performance entry restrictions
      }

      const mehediMetaSelectors = [
        'meta[property="og:video:secure_url"]',
        'meta[property="og:video:url"]',
        'meta[property="og:video"]',
        'meta[name="twitter:player:stream"]'
      ];
      for (const shihabSel of mehediMetaSelectors) {
        const mehediMeta = document.querySelector(shihabSel);
        const shihabContent = mehediMeta ? (mehediMeta.getAttribute("content") || "") : "";
        if (/\.(mp4|mkv|webm|mov|m3u8)(\?|#|$)/i.test(shihabContent)) {
          shihabPushRichStreamEntry(mehediCombined, {
            shihabStreamUrl: shihabContent,
            shihabResolution: mehediIntrinsicRes
          });
        }
      }
    }

    // If there is only a single VIDEO stream and its resolution wasn't in the URL yet, enrich from HTMLVideoElement.videoHeight
    const shihabVideoItems = mehediCombined.filter((mehediIt) => !mehediIt.shihabIsAudioOnly);
    if (shihabVideoItems.length === 1 && !shihabVideoItems[0].shihabResolution && mehediIntrinsicRes) {
      shihabVideoItems[0].shihabResolution = mehediIntrinsicRes;
      shihabVideoItems[0].shihabQualityLabel = mehediIntrinsicRes;
      const mehediFmt = (shihabVideoItems[0].shihabExt === "m3u8" ? "MP4" : (shihabVideoItems[0].shihabExt || "mp4")).toUpperCase();
      shihabVideoItems[0].mehediBadge = `${mehediFmt} ${mehediIntrinsicRes}`;
    }

    // Sort: VIDEO tracks first (highest resolution -> progressive/audio-paired -> MP4 -> largest), then single best AUDIO
    mehediCombined.sort((shihabA, mehediB) => {
      if (shihabA.shihabIsAudioOnly !== mehediB.shihabIsAudioOnly) {
        return shihabA.shihabIsAudioOnly ? 1 : -1;
      }
      const shihabResDiff = shihabNumericResRank(mehediB.shihabResolution) - shihabNumericResRank(shihabA.shihabResolution);
      if (shihabResDiff !== 0) return shihabResDiff;
      const mehediSoundA = (shihabA.mehediTrackRole === "progressive" || Boolean(shihabA.mehediAudioCompanionUrl)) ? 1 : 0;
      const shihabSoundB = (mehediB.mehediTrackRole === "progressive" || Boolean(mehediB.mehediAudioCompanionUrl)) ? 1 : 0;
      if (mehediSoundA !== shihabSoundB) return shihabSoundB - mehediSoundA;
      const mehediMp4A = (shihabA.shihabExt === "mp4" || shihabA.shihabExt === "m4a") ? 1 : 0;
      const shihabMp4B = (mehediB.shihabExt === "mp4" || mehediB.shihabExt === "m4a") ? 1 : 0;
      if (mehediMp4A !== shihabMp4B) return shihabMp4B - mehediMp4A;
      return (mehediB.mehediDisplaySizeBytes || 0) - (shihabA.mehediDisplaySizeBytes || 0);
    });

    // Strict per-resolution deduplication so each quality of THIS selected video appears once (zero duplicate rows!)
    const shihabUniqueQualities = [];
    const mehediSeenQualKeys = new Set();
    for (const shihabCandidate of mehediCombined) {
      const mehediQualKey = shihabCandidate.shihabIsAudioOnly
        ? "AUDIO"
        : (shihabCandidate.shihabResolution ? `VIDEO:${shihabCandidate.shihabResolution}` : `URL:${shihabCandidate.shihabStreamUrl}`);
      if (mehediSeenQualKeys.has(mehediQualKey)) continue;
      mehediSeenQualKeys.add(mehediQualKey);
      shihabUniqueQualities.push(shihabCandidate);
    }

    const mehediRawDedupe = [];
    const shihabSeenRawUrls = new Set();
    for (const mehediRawItem of mehediCombined) {
      if (!mehediRawItem || !mehediRawItem.shihabStreamUrl) continue;
      if (shihabSeenRawUrls.has(mehediRawItem.shihabStreamUrl)) continue;
      shihabSeenRawUrls.add(mehediRawItem.shihabStreamUrl);
      mehediRawDedupe.push(mehediRawItem);
    }
    shihabUniqueQualities.mehediRawSniffed = mehediRawDedupe;

    return shihabUniqueQualities;
  }

  // Single getBoundingClientRect() read FIRST before getComputedStyle() — avoids style recalculations on off-screen/tiny videos!
  function mehediGetVisibleVideoRect(shihabVid) {
    if (!shihabVid || !shihabVid.isConnected) return null;
    const shihabRect = shihabVid.getBoundingClientRect();
    if (shihabRect.width < 120 || shihabRect.height < 70) {
      return null;
    }
    if (shihabRect.bottom < 30 || shihabRect.top > window.innerHeight - 30 ||
        shihabRect.right < 30 || shihabRect.left > window.innerWidth - 30) {
      return null;
    }
    const mehediStyle = window.getComputedStyle(shihabVid);
    if (mehediStyle.display === "none" || mehediStyle.visibility === "hidden" || parseFloat(mehediStyle.opacity || "1") < 0.05) {
      return null;
    }
    return shihabRect;
  }

  function mehediIsVideoVisibleInViewport(shihabVid) {
    return Boolean(mehediGetVisibleVideoRect(shihabVid));
  }

  function mehediFindPrimaryVisibleVideoWithRect() {
    const shihabLockedVid = shihabReadWeakRef(mehediLockedDropdownVideoRef);
    if (shihabDropdownOpen && shihabLockedVid) {
      const shihabLockedRect = mehediGetVisibleVideoRect(shihabLockedVid);
      if (shihabLockedRect) return { video: shihabLockedVid, rect: shihabLockedRect };
    }

    const shihabHoveredVid = shihabReadWeakRef(mehediHoveredVideoRef);
    const shihabLastSelVid = shihabReadWeakRef(mehediLastSelectedVideoRef);

    if (mehediPointerInsideBar) {
      const shihabPinnedBarVid = shihabHoveredVid || shihabLastSelVid;
      if (shihabPinnedBarVid) {
        const shihabPinnedRect = mehediGetVisibleVideoRect(shihabPinnedBarVid);
        if (shihabPinnedRect) return { video: shihabPinnedBarVid, rect: shihabPinnedRect };
      }
    }

    if (shihabHoveredVid && Date.now() < shihabHoveredUntilMs) {
      const shihabHovRect = mehediGetVisibleVideoRect(shihabHoveredVid);
      if (shihabHovRect) {
        mehediLastSelectedVideoRef = shihabMakeWeakRef(shihabHoveredVid);
        return { video: shihabHoveredVid, rect: shihabHovRect };
      }
    }

    const shihabVideos = shihabCollectAllVideosDeep(document, []);
    if (shihabLastSelVid && shihabLastSelVid.isConnected && !shihabVideos.includes(shihabLastSelVid)) {
      shihabVideos.push(shihabLastSelVid);
    }
    let mehediPlayingCandidate = null;
    let mehediPlayingRect = null;
    let shihabPlayingArea = 0;
    let mehediLargestCandidate = null;
    let mehediLargestRect = null;
    let shihabLargestArea = 0;
    let mehediSawHidden = false;

    for (const mehediVid of shihabVideos) {
      const shihabRect = mehediGetVisibleVideoRect(mehediVid);
      if (!shihabRect) {
        if (mehediVid && mehediVid.isConnected) mehediSawHidden = true;
        continue;
      }
      const mehediVisibleTop = Math.max(0, shihabRect.top);
      const shihabVisibleBottom = Math.min(window.innerHeight, shihabRect.bottom);
      const mehediVisibleHeight = Math.max(0, shihabVisibleBottom - mehediVisibleTop);
      const mehediArea = shihabRect.width * mehediVisibleHeight;

      if (!mehediVid.paused && !mehediVid.ended) {
        if (mehediArea > shihabPlayingArea) {
          shihabPlayingArea = mehediArea;
          mehediPlayingCandidate = mehediVid;
          mehediPlayingRect = shihabRect;
        }
      }
      if (mehediArea > shihabLargestArea) {
        shihabLargestArea = mehediArea;
        mehediLargestCandidate = mehediVid;
        mehediLargestRect = shihabRect;
      }
    }
    shihabHasHiddenVideoInDom = mehediSawHidden;

    const shihabChosen = mehediPlayingCandidate || mehediLargestCandidate;
    const shihabChosenRect = mehediPlayingCandidate ? mehediPlayingRect : mehediLargestRect;
    if (shihabChosen) {
      mehediLastSelectedVideoRef = shihabMakeWeakRef(shihabChosen);
    }
    return { video: shihabChosen, rect: shihabChosenRect };
  }

  function mehediFindPrimaryVisibleVideo() {
    return mehediFindPrimaryVisibleVideoWithRect().video;
  }

  let shihabBgSyncInFlight = null;

  function shihabRefreshBackgroundStreamsThrottled(mehediForceNow) {
    const shihabNow = Date.now();
    if (!mehediForceNow && shihabNow - mehediLastBgSyncMs < 1600) {
      return Promise.resolve(shihabTabStreamsCache);
    }
    if (shihabBgSyncInFlight) {
      return shihabBgSyncInFlight;
    }
    mehediLastBgSyncMs = shihabNow;
    try {
      const shihabWork = Promise.race([
        chrome.runtime.sendMessage({ shihabCmd: "shihab_query_tab_streams" }),
        new Promise((resolve) => setTimeout(() => resolve(null), 2500))
      ]).then((mehediState) => {
        if (mehediState) {
          if (typeof mehediState.shihabSenderFrameId === "number") {
            mehediMyFrameId = mehediState.shihabSenderFrameId;
          }
          if (Array.isArray(mehediState.mehediStreams)) {
            shihabSetTabStreamsCache(mehediState.mehediStreams);
          }
          if (typeof mehediState.mehediShowVideoPanel === "boolean") {
            mehediShowFloatingBar = mehediState.mehediShowVideoPanel;
          }
          if (mehediState.shihabBridgeInfo) {
            mehediPanelMiniMode = Boolean(mehediState.shihabBridgeInfo.panelMiniMode);
            if (mehediState.shihabBridgeInfo.preventKey) {
              shihabConfiguredPreventKey = String(mehediState.shihabBridgeInfo.preventKey);
            }
            if (mehediState.shihabBridgeInfo.forceKey) {
              mehediConfiguredForceKey = String(mehediState.shihabBridgeInfo.forceKey);
            }
          }
        }
        return shihabTabStreamsCache;
      }).catch(() => shihabTabStreamsCache).finally(() => {
        if (shihabBgSyncInFlight === shihabWork) {
          shihabBgSyncInFlight = null;
        }
      });
      shihabBgSyncInFlight = shihabWork;
      return shihabWork;
    } catch (shihabSendErr) {
      return Promise.resolve(shihabTabStreamsCache);
    }
  }

  function mehediShowPanelStatusToast(shihabText, mehediIsError) {
    if (!shihabRefs || !shihabRefs.mehediToastEl) return;
    shihabRefs.mehediToastEl.textContent = shihabText;
    shihabRefs.mehediToastEl.style.color = mehediIsError ? "#fca5a5" : "#86efac";
    shihabRefs.mehediToastEl.style.display = "block";
    if (shihabToastClearTimer) clearTimeout(shihabToastClearTimer);
    shihabToastClearTimer = setTimeout(() => {
      if (shihabRefs && shihabRefs.mehediToastEl) {
        shihabRefs.mehediToastEl.style.display = "none";
      }
    }, 3600);
  }

  // Re-track the currently visible <video> on user click of the corner ↻ reloader icon (or popup ↻ button)
  async function mehediTriggerActiveVideoRetrack(shihabOpenDropdownAfter) {
    if (shihabRefs && shihabRefs.mehediRetrackBtn) {
      shihabRefs.mehediRetrackBtn.classList.add("mehedi-spinning");
    }
    shihabPanelDismissedUrl = "";

    try {
      const mehediActiveVid = mehediFindPrimaryVisibleVideo();
      const shihabActiveSrc = mehediActiveVid ? (mehediActiveVid.currentSrc || mehediActiveVid.src || "") : "";

      // Purge non-exact fallback blob guesses while preserving byte-exact binding for the live <video>
      for (const mehediBlobKey of Array.from(shihabBlobToStreamUrlsMap.keys())) {
        if (mehediBlobKey !== shihabActiveSrc || !shihabExactBlobToStreamUrlsMap.has(mehediBlobKey)) {
          shihabBlobToStreamUrlsMap.delete(mehediBlobKey);
        }
      }

      const shihabCtxBefore = shihabResolveVideoContext(mehediActiveVid);
      let mehediKnownKeepGroup = "";
      if (shihabCtxBefore.shihabDirectDomGroupKeys && shihabCtxBefore.shihabDirectDomGroupKeys.size > 0) {
        mehediKnownKeepGroup = shihabPickFreshestMatchingGroup(shihabCtxBefore.shihabDirectDomGroupKeys);
      } else if (shihabCtxBefore.shihabExactGroupKeys.size > 0) {
        mehediKnownKeepGroup = shihabPickFreshestMatchingGroup(shihabCtxBefore.shihabExactGroupKeys);
      } else if (shihabCtxBefore.mehediRouteAndCardGroupKeys.size > 0) {
        mehediKnownKeepGroup = shihabPickFreshestMatchingGroup(shihabCtxBefore.mehediRouteAndCardGroupKeys);
      }
      const shihabKnownKeepUrls = Array.from(shihabCtxBefore.shihabExactBoundUrls);

      // Ask MAIN-world hook to re-scan current page manifests, scripts & this exact <video> fiber
      if (mehediActiveVid) {
        mehediEnsureVideoUidAndInspect(mehediActiveVid, true);
      }
      try {
        window.postMessage({ __shihabMehediContentPing: true, shihabIsInitialSync: false }, shihabSafeSameWindowOrigin());
      } catch (_) {}

      await chrome.runtime.sendMessage({
        shihabCmd: "shihab_retrack_active_video",
        shihabKeepUrls: shihabKnownKeepUrls,
        mehediKeepGroupKey: mehediKnownKeepGroup
      }).catch(() => {});

      await new Promise((shihabResolve) => setTimeout(shihabResolve, 240));
      await shihabRefreshBackgroundStreamsThrottled(true);

      if (shihabOpenDropdownAfter) {
        shihabSetDropdownOpen(true);
      }
      shihabRenderFloatingIdmVideoPanel(false);
      if (shihabOpenDropdownAfter) {
        mehediShowPanelStatusToast("↻ Re-tracked active video!", false);
      }
    } finally {
      setTimeout(() => {
        if (shihabRefs && shihabRefs.mehediRetrackBtn) {
          shihabRefs.mehediRetrackBtn.classList.remove("mehedi-spinning");
        }
      }, 380);
    }
  }

  function shihabVerifyActiveSingletonOwnership() {
    if (mehediInstanceDisposed || !shihabIsExtensionContextAlive()) {
      mehediDisposeCurrentInstance();
      return false;
    }
    return true;
  }

  function shihabSetDropdownOpen(mehediNextOpen) {
    const shihabNext = Boolean(mehediNextOpen);
    shihabDropdownOpen = shihabNext;
    if (shihabNext && !shihabDropdownListenersAttached && !mehediInstanceDisposed) {
      shihabDropdownListenersAttached = true;
      window.addEventListener("pointerdown", mehediCloseDropdownOnOutsideEvent, { capture: true, passive: true });
      window.addEventListener("keydown", mehediCloseDropdownOnOutsideEvent, { capture: true, passive: true });
      window.addEventListener("pointerup", shihabReleaseMenuPointerLock, { capture: true, passive: true });
      window.addEventListener("pointercancel", shihabReleaseMenuPointerLock, { capture: true, passive: true });
      window.addEventListener("mouseup", shihabReleaseMenuPointerLock, { capture: true, passive: true });
      window.addEventListener("blur", shihabReleaseMenuPointerLock, { passive: true });
    } else if (!shihabNext && shihabDropdownListenersAttached) {
      shihabDropdownListenersAttached = false;
      window.removeEventListener("pointerdown", mehediCloseDropdownOnOutsideEvent, true);
      window.removeEventListener("keydown", mehediCloseDropdownOnOutsideEvent, true);
      window.removeEventListener("pointerup", shihabReleaseMenuPointerLock, true);
      window.removeEventListener("pointercancel", shihabReleaseMenuPointerLock, true);
      window.removeEventListener("mouseup", shihabReleaseMenuPointerLock, true);
      window.removeEventListener("blur", shihabReleaseMenuPointerLock);
    }
  }

  // Build Closed Shadow DOM 100% via createElement / textContent (ZERO innerHTML!) so YouTube & Facebook TrustedTypes CSP never blocks it!
  function shihabEnsureShadowHostMounted() {
    if (!shihabVerifyActiveSingletonOwnership()) return null;

    const mehediDesiredParent = document.fullscreenElement || document.webkitFullscreenElement || document.body || document.documentElement;
    if (!mehediDesiredParent) return null;

    if (!mehediShadowHost) {
      shihabSweepForeignHostElements(null);

      mehediShadowHost = document.createElement("div");
      mehediShadowHost.id = SHIHAB_HOST_ID;
      mehediShadowHost.setAttribute(SHIHAB_HOST_ATTR, shihabInstanceToken);
      mehediShadowHost.style.cssText =
        "position:fixed !important;top:0 !important;left:0 !important;width:0 !important;height:0 !important;overflow:visible !important;z-index:2147483647 !important;pointer-events:auto !important;margin:0 !important;padding:0 !important;border:0 !important;display:none;will-change:transform;";

      const shihabShadow = mehediShadowHost.attachShadow({ mode: "closed" });

      const mehediStyleEl = document.createElement("style");
      mehediStyleEl.textContent = `
        * { box-sizing: border-box; }
        @keyframes shihabSpin360 {
          from { transform: rotate(0deg); }
          to { transform: rotate(360deg); }
        }
        .shihab-wrap {
          position: absolute;
          top: 0;
          right: 0;
          left: auto;
          width: max-content;
          display: inline-flex;
          flex-direction: column;
          align-items: flex-end;
          font-family: 'Segoe UI', system-ui, -apple-system, BlinkMacSystemFont, sans-serif;
        }
        .shihab-wrap.shihab-anchor-left {
          left: 0;
          right: auto;
          align-items: flex-start;
        }
        .shihab-bar {
          display: inline-flex;
          align-items: center;
          background: rgba(18, 20, 26, 0.94);
          border: 1px solid rgba(255, 255, 255, 0.14);
          border-radius: 999px;
          padding: 4px 8px 4px 11px;
          color: #f8fafc;
          font-size: 12px;
          font-weight: 600;
          line-height: 1.2;
          box-shadow: 0 4px 16px rgba(0, 0, 0, 0.48);
          opacity: 0.90;
          transition: opacity 0.16s ease, background 0.16s ease, border-color 0.16s ease, box-shadow 0.16s ease;
          user-select: none;
        }
        .shihab-bar:hover,
        .shihab-bar.mehedi-open {
          opacity: 1;
          border-color: rgba(255, 255, 255, 0.24);
          background: rgba(20, 23, 30, 0.96);
          box-shadow: 0 8px 24px rgba(0, 0, 0, 0.65);
        }
        .shihab-bar.shihab-compact:not(:hover):not(.mehedi-open) {
          padding: 4px 7px;
        }
        .shihab-bar.shihab-compact:not(:hover):not(.mehedi-open) .mehedi-pill-label {
          display: none;
        }
        .mehedi-btn {
          background: transparent;
          border: none;
          color: inherit;
          font: inherit;
          cursor: pointer;
          display: inline-flex;
          align-items: center;
          gap: 7px;
          padding: 1px 0;
          white-space: nowrap;
          outline: none;
        }
        .mehedi-shield-icon {
          width: 14px;
          height: 14px;
          flex-shrink: 0;
          display: block;
          color: #1d8cf8;
        }
        .mehedi-pill-label {
          color: #f8fafc;
          font-size: 12px;
          font-weight: 700;
          letter-spacing: 0.1px;
        }
        .shihab-count {
          background: #0d74e7;
          color: #ffffff;
          border-radius: 999px;
          padding: 1.5px 8px;
          font-size: 10.5px;
          font-weight: 700;
          min-width: 22px;
          text-align: center;
          line-height: 1.25;
        }
        .mehedi-toast {
          position: absolute;
          top: calc(100% + 6px);
          right: 0;
          background: rgba(18, 20, 27, 0.96);
          border: 1px solid rgba(255, 255, 255, 0.14);
          border-radius: 8px;
          padding: 6px 11px;
          font-size: 11px;
          font-weight: 600;
          white-space: nowrap;
          box-shadow: 0 8px 22px rgba(0, 0, 0, 0.62);
          backdrop-filter: blur(10px);
          display: none;
          max-width: 380px;
          z-index: 2;
        }
        .mehedi-menu {
          position: absolute;
          top: calc(100% + 7px);
          right: 0;
          background: rgba(21, 23, 29, 0.97);
          border: 1px solid rgba(255, 255, 255, 0.10);
          border-radius: 12px;
          width: 356px;
          max-width: calc(100vw - 16px);
          max-height: 370px;
          overflow-y: auto;
          overflow-x: hidden;
          padding: 9px 6px 8px 6px;
          box-shadow: 0 18px 42px rgba(0, 0, 0, 0.78);
          backdrop-filter: blur(14px);
          -webkit-backdrop-filter: blur(14px);
          user-select: none;
          display: none;
          z-index: 3;
        }
        .mehedi-menu::-webkit-scrollbar { width: 5px; }
        .mehedi-menu::-webkit-scrollbar-thumb {
          background: rgba(120, 130, 152, 0.45);
          border-radius: 4px;
        }
        .shihab-menu-head {
          display: flex;
          align-items: center;
          justify-content: space-between;
          gap: 8px;
          padding: 3px 10px 9px 10px;
          margin-bottom: 3px;
          border-bottom: 1px solid rgba(255, 255, 255, 0.06);
          color: #8b93a7;
          font-size: 10.5px;
          font-weight: 700;
          letter-spacing: 0.65px;
          text-transform: uppercase;
        }
        .mehedi-head-right {
          display: inline-flex;
          align-items: center;
          gap: 7px;
        }
        .mehedi-mini-tool {
          background: transparent;
          border: none;
          color: #7c8599;
          font-size: 12px;
          font-weight: 700;
          line-height: 1;
          padding: 1px 3px;
          border-radius: 4px;
          cursor: pointer;
          display: inline-flex;
          align-items: center;
          justify-content: center;
        }
        .mehedi-mini-tool:hover {
          color: #e2e8f0;
          background: rgba(255, 255, 255, 0.08);
        }
        .mehedi-mini-tool.mehedi-spinning {
          animation: shihabSpin360 0.38s linear infinite;
        }
        .shihab-item {
          padding: 8px 10px;
          border-radius: 8px;
          color: #eef2f7;
          font-size: 12.2px;
          font-weight: 600;
          cursor: pointer;
          display: flex;
          justify-content: space-between;
          align-items: center;
          gap: 10px;
          transition: background 0.12s ease;
        }
        .shihab-item:hover {
          background: rgba(255, 255, 255, 0.06);
        }
        .shihab-item:hover .shihab-dl-action {
          color: #60a5fa;
        }
        .mehedi-left-group {
          display: flex;
          align-items: center;
          gap: 10px;
          min-width: 0;
          flex: 1;
        }
        .shihab-kind-pill {
          font-size: 10px;
          font-weight: 700;
          letter-spacing: 0.3px;
          padding: 3px 7px;
          border-radius: 5px;
          flex-shrink: 0;
          text-transform: uppercase;
          text-align: center;
          line-height: 1.15;
        }
        .mehedi-kind-video {
          background: rgba(29, 140, 248, 0.16);
          color: #4db8ff;
        }
        .mehedi-kind-audio {
          background: rgba(139, 92, 246, 0.20);
          color: #a78bfa;
        }
        .mehedi-name {
          color: #eef2f7;
          font-weight: 600;
          font-size: 12.2px;
          line-height: 1.3;
          min-width: 0;
          overflow: hidden;
          text-overflow: ellipsis;
          display: -webkit-box;
          -webkit-line-clamp: 2;
          -webkit-box-orient: vertical;
        }
        .shihab-dl-action {
          color: #7c8599;
          font-weight: 600;
          font-size: 11.2px;
          white-space: nowrap;
          flex-shrink: 0;
          transition: color 0.12s ease;
        }
      `;

      const shihabWrap = document.createElement("div");
      shihabWrap.className = "shihab-wrap";

      const mehediBar = document.createElement("div");
      mehediBar.className = "shihab-bar";

      const shihabPrimaryBtn = document.createElement("button");
      shihabPrimaryBtn.className = "mehedi-btn";
      shihabPrimaryBtn.type = "button";
      shihabPrimaryBtn.title = "Select quality or download video with MHS Download Manager";

      const shihabSvgNs = "http://www.w3.org/2000/svg";
      const mehediShieldSvg = document.createElementNS(shihabSvgNs, "svg");
      mehediShieldSvg.setAttribute("class", "mehedi-shield-icon");
      mehediShieldSvg.setAttribute("viewBox", "0 0 24 24");
      mehediShieldSvg.setAttribute("fill", "none");
      mehediShieldSvg.setAttribute("stroke", "currentColor");
      mehediShieldSvg.setAttribute("stroke-width", "2.1");
      mehediShieldSvg.setAttribute("stroke-linecap", "round");
      mehediShieldSvg.setAttribute("stroke-linejoin", "round");
      const shihabShieldPath = document.createElementNS(shihabSvgNs, "path");
      shihabShieldPath.setAttribute("d", "M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z");
      mehediShieldSvg.appendChild(shihabShieldPath);

      const mehediLabelSpan = document.createElement("span");
      mehediLabelSpan.className = "mehedi-pill-label";
      mehediLabelSpan.textContent = "Download Video";

      const mehediCountPill = document.createElement("span");
      mehediCountPill.className = "shihab-count";
      mehediCountPill.textContent = "0";

      shihabPrimaryBtn.appendChild(mehediShieldSvg);
      shihabPrimaryBtn.appendChild(mehediLabelSpan);
      shihabPrimaryBtn.appendChild(mehediCountPill);

      mehediBar.appendChild(shihabPrimaryBtn);

      const mehediRetrackBtn = document.createElement("button");
      mehediRetrackBtn.className = "mehedi-mini-tool";
      mehediRetrackBtn.type = "button";
      mehediRetrackBtn.title = "Re-track active video on this page";
      mehediRetrackBtn.textContent = "↻";

      const shihabCloseBtn = document.createElement("button");
      shihabCloseBtn.className = "mehedi-mini-tool";
      shihabCloseBtn.type = "button";
      shihabCloseBtn.title = "Hide download pill on this page";
      shihabCloseBtn.textContent = "×";

      const mehediToastEl = document.createElement("div");
      mehediToastEl.className = "mehedi-toast";

      const shihabMenuEl = document.createElement("div");
      shihabMenuEl.className = "mehedi-menu";

      // Mount header & rows container ONCE so re-renders NEVER detach buttons or break clicks/hover transitions!
      const shihabMenuHeaderDiv = document.createElement("div");
      shihabMenuHeaderDiv.className = "shihab-menu-head";
      shihabMenuHeaderDiv.title = "MHS Stream Sniffer";

      const mehediHeadLeft = document.createElement("span");
      mehediHeadLeft.textContent = "MHS STREAM SNIFFER";

      const shihabHeadRight = document.createElement("div");
      shihabHeadRight.className = "mehedi-head-right";
      const mehediQualTxt = document.createElement("span");
      mehediQualTxt.textContent = "SELECT QUALITY";
      shihabHeadRight.appendChild(mehediQualTxt);
      shihabHeadRight.appendChild(mehediRetrackBtn);
      shihabHeadRight.appendChild(shihabCloseBtn);

      shihabMenuHeaderDiv.appendChild(mehediHeadLeft);
      shihabMenuHeaderDiv.appendChild(shihabHeadRight);

      const shihabMenuRowsEl = document.createElement("div");
      shihabMenuRowsEl.className = "shihab-menu-rows";

      shihabMenuEl.appendChild(shihabMenuHeaderDiv);
      shihabMenuEl.appendChild(shihabMenuRowsEl);

      shihabWrap.appendChild(mehediBar);
      shihabWrap.appendChild(mehediToastEl);
      shihabWrap.appendChild(shihabMenuEl);

      shihabShadow.appendChild(mehediStyleEl);
      shihabShadow.appendChild(shihabWrap);

      shihabRefs = {
        shihabWrap,
        mehediBar,
        mehediCountPill,
        mehediRetrackBtn,
        shihabCloseBtn,
        mehediToastEl,
        shihabMenuEl,
        shihabMenuHeaderDiv,
        shihabMenuRowsEl
      };
      shihabLastRenderedMenuSig = "";
      shihabBarDimensionsMeasured = false;
      shihabLastTransformCss = "";

      // Prevent host-page pointerdown/mousedown handlers (e.g. LinkedIn feed / Video.js controls) from stealing focus or interfering with clicks
      shihabWrap.addEventListener("pointerdown", (mehediEv) => {
        mehediEv.stopPropagation();
      });
      shihabWrap.addEventListener("mousedown", (mehediEv) => {
        mehediEv.stopPropagation();
      });

      // Lock menu row layout while pointer is pressed inside the dropdown so clicks NEVER miss due to async stream updates
      const shihabAcquireMenuPointerLock = () => {
        if (shihabPointerReleaseTimerId) {
          clearTimeout(shihabPointerReleaseTimerId);
          shihabPointerReleaseTimerId = null;
        }
        mehediPointerDownInMenuAtMs = Date.now();
      };
      shihabMenuEl.addEventListener("pointerdown", shihabAcquireMenuPointerLock);
      shihabMenuEl.addEventListener("mousedown", shihabAcquireMenuPointerLock);

      // Single delegated click handler on persistent rows container — guarded with ev.isTrusted === true!
      shihabMenuRowsEl.addEventListener("click", (mehediClickEv) => {
        if (!mehediClickEv || mehediClickEv.isTrusted !== true) return;
        const shihabRowEl = mehediClickEv.target && typeof mehediClickEv.target.closest === "function"
          ? mehediClickEv.target.closest(".shihab-item")
          : null;
        if (!shihabRowEl) return;
        mehediClickEv.preventDefault();
        mehediClickEv.stopPropagation();
        shihabClearMenuPointerLockNow();
        if (shihabRowEl.dataset && shihabRowEl.dataset.shihabEmptyRow === "1") {
          mehediTriggerActiveVideoRetrack(true);
          return;
        }
        const shihabIdx = parseInt((shihabRowEl.dataset && shihabRowEl.dataset.shihabRowIdx) || "-1", 10);
        const mehediChosenEntry = (shihabIdx >= 0 && shihabIdx < shihabRenderedMenuRows.length)
          ? shihabRenderedMenuRows[shihabIdx]
          : null;
        if (!mehediChosenEntry || !mehediChosenEntry.mehediStream) return;
        const mehediTargetVidForDl =
          shihabReadWeakRef(shihabRenderedMenuTargetVideoRef) ||
          shihabReadWeakRef(mehediLockedDropdownVideoRef) ||
          mehediFindPrimaryVisibleVideo();
        shihabSetDropdownOpen(false);
        mehediLockedDropdownVideoRef = null;
        mehediTriggerStreamDownload(mehediChosenEntry.mehediStream, mehediTargetVidForDl);
        shihabRenderFloatingIdmVideoPanel(false);
      });

      mehediShadowHost.addEventListener("mouseenter", () => {
        mehediPointerInsideBar = true;
        if (shihabReadWeakRef(mehediHoveredVideoRef) || shihabReadWeakRef(mehediLastSelectedVideoRef)) {
          shihabHoveredUntilMs = Date.now() + 8000;
          shihabArmHoverExpiryTimer(shihabHoveredUntilMs);
        }
      });

      mehediShadowHost.addEventListener("mouseleave", () => {
        mehediPointerInsideBar = false;
        if (shihabReadWeakRef(mehediHoveredVideoRef)) {
          shihabHoveredUntilMs = Date.now() + 2500;
          shihabArmHoverExpiryTimer(shihabHoveredUntilMs);
        }
      });

      shihabPrimaryBtn.addEventListener("click", (mehediEv) => {
        if (!mehediEv || mehediEv.isTrusted !== true) return;
        mehediEv.preventDefault();
        mehediEv.stopPropagation();
        if (shihabDropdownOpen) {
          shihabSetDropdownOpen(false);
          mehediLockedDropdownVideoRef = null;
          shihabRenderFloatingIdmVideoPanel(false);
          return;
        }
        const shihabActiveVid = mehediFindPrimaryVisibleVideo();
        mehediLockedDropdownVideoRef = shihabMakeWeakRef(shihabActiveVid);
        if (shihabActiveVid) {
          mehediEnsureVideoUidAndInspect(shihabActiveVid, true);
        }
        // Open immediately using cached/DOM streams so the button NEVER freezes even if mdm.exe is offline/busy.
        shihabSetDropdownOpen(true);
        shihabRenderFloatingIdmVideoPanel(false);
        shihabRefreshBackgroundStreamsThrottled(true).then(() => {
          if (shihabDropdownOpen && !mehediInstanceDisposed) {
            shihabScheduleFloatingPanelRender(false);
          }
        }).catch(() => {});
      });

      mehediRetrackBtn.addEventListener("click", async (shihabRetrackEv) => {
        if (!shihabRetrackEv || shihabRetrackEv.isTrusted !== true) return;
        shihabRetrackEv.preventDefault();
        shihabRetrackEv.stopPropagation();
        shihabClearMenuPointerLockNow();
        await mehediTriggerActiveVideoRetrack(true);
      });

      shihabCloseBtn.addEventListener("click", (mehediEv) => {
        if (!mehediEv || mehediEv.isTrusted !== true) return;
        mehediEv.preventDefault();
        mehediEv.stopPropagation();
        shihabClearMenuPointerLockNow();
        shihabPanelDismissedUrl = location.href;
        shihabSetDropdownOpen(false);
        mehediLockedDropdownVideoRef = null;
        mehediShadowHost.style.display = "none";
      });
    }

    if (mehediShadowHost.parentNode !== mehediDesiredParent) {
      shihabSweepForeignHostElements(mehediShadowHost);
      mehediDesiredParent.appendChild(mehediShadowHost);
    }
    return mehediShadowHost;
  }

  function shihabClearElementChildren(mehediContainer) {
    while (mehediContainer.firstChild) {
      mehediContainer.removeChild(mehediContainer.firstChild);
    }
  }

  function mehediDetectStreamEndpointTag(shihabStream) {
    const mehediUrl = String((shihabStream && shihabStream.shihabStreamUrl) || "").toLowerCase();
    const shihabExt = String((shihabStream && shihabStream.shihabExt) || "").toLowerCase();
    if (mehediUrl.includes("videoplayback")) return { tag: "VIDEOPLAYBACK", sub: "videoplayback" };
    if (shihabExt === "m3u8" || mehediUrl.includes(".m3u8") || /\/hls-manifest\b/i.test(mehediUrl)) return { tag: "HLS", sub: "m3u8" };
    if (mehediUrl.includes(".mpd")) return { tag: "DASH", sub: "mpd" };
    if (shihabExt === "webm" || mehediUrl.includes(".webm")) return { tag: "WEBM", sub: "webm" };
    if (shihabExt === "m4a" || shihabStream.shihabIsAudioOnly) return { tag: "AUDIO", sub: "audio" };
    return { tag: "MP4", sub: shihabExt || "mp4" };
  }

  function mehediCloneStreamWithQuality(shihabBaseStream, mehediQualityLabel, shihabForceAudioOnly) {
    const mehediClone = Object.assign({}, shihabBaseStream);
    if (shihabForceAudioOnly) {
      if (shihabBaseStream.mehediAudioCompanionUrl) {
        mehediClone.shihabStreamUrl = shihabBaseStream.mehediAudioCompanionUrl;
        mehediClone.shihabSizeBytes = shihabBaseStream.shihabAudioCompanionBytes || -1;
        mehediClone.mehediAudioCompanionUrl = "";
        mehediClone.shihabAudioCompanionBytes = -1;
      }
      mehediClone.shihabIsAudioOnly = true;
      mehediClone.mehediMediaKind = "AUDIO";
      mehediClone.shihabExt = "mp3";
      mehediClone.shihabQualityLabel = "Audio";
    } else if (mehediQualityLabel) {
      mehediClone.shihabQualityLabel = mehediQualityLabel;
    }
    return mehediClone;
  }

  function shihabPickClosestVideoStreamForTier(mehediVideoStreams, shihabTargetHeightNum) {
    if (!mehediVideoStreams || mehediVideoStreams.length === 0) return null;
    for (const shihabSt of mehediVideoStreams) {
      if (shihabNumericResRank(shihabSt.shihabResolution) === shihabTargetHeightNum) {
        return shihabSt;
      }
    }
    for (const mehediSt of mehediVideoStreams) {
      const shihabRank = shihabNumericResRank(mehediSt.shihabResolution);
      if (shihabRank > 0 && shihabRank <= shihabTargetHeightNum) {
        return mehediSt;
      }
    }
    return mehediVideoStreams[0];
  }

  function shihabBuildStreamSnifferEntries(shihabStreams) {
    if (!Array.isArray(shihabStreams) || shihabStreams.length === 0) {
      return [];
    }
    const mehediEntries = [];
    const shihabVideoStreams = shihabStreams.filter((mehediIt) => !mehediIt.shihabIsAudioOnly && mehediIt.mehediMediaKind !== "AUDIO");
    const mehediAudioStreams = shihabStreams.filter((shihabIt) => Boolean(shihabIt.shihabIsAudioOnly || shihabIt.mehediMediaKind === "AUDIO"));
    const shihabBestVideo = shihabVideoStreams.length > 0 ? shihabVideoStreams[0] : null;

    if (shihabBestVideo) {
      const mehediBestRes = shihabBestVideo.shihabResolution || "Best";
      mehediEntries.push({
        shihabBadge: "MAX",
        mehediIsAudio: false,
        shihabLabel: "Best Available (4K / 1080p)",
        mehediStream: mehediCloneStreamWithQuality(shihabBestVideo, mehediBestRes, false)
      });

      const shihabStandardTiers = [
        { badge: "1080p", rank: 1080, title: "1080p Full HD" },
        { badge: "720p", rank: 720, title: "720p HD" },
        { badge: "480p", rank: 480, title: "480p Standard" }
      ];

      for (const mehediTier of shihabStandardTiers) {
        const shihabChosenVid = shihabPickClosestVideoStreamForTier(shihabVideoStreams, mehediTier.rank);
        if (shihabChosenVid) {
          mehediEntries.push({
            shihabBadge: mehediTier.badge,
            mehediIsAudio: false,
            shihabLabel: mehediTier.title,
            mehediStream: mehediCloneStreamWithQuality(shihabChosenVid, mehediTier.badge, false)
          });
        }
      }
    }

    const shihabAudioCandidate = mehediAudioStreams.length > 0
      ? mehediCloneStreamWithQuality(mehediAudioStreams[0], "Audio", true)
      : (shihabBestVideo ? mehediCloneStreamWithQuality(shihabBestVideo, "Audio", true) : null);

    if (shihabAudioCandidate) {
      mehediEntries.push({
        shihabBadge: "AUDIO",
        mehediIsAudio: true,
        shihabLabel: "Audio Only (MP3)",
        mehediStream: shihabAudioCandidate
      });
    }

    const mehediRawPool = Array.isArray(shihabStreams.mehediRawSniffed) && shihabStreams.mehediRawSniffed.length > 0
      ? shihabStreams.mehediRawSniffed
      : shihabStreams;

    let shihabSniffIdx = 1;
    for (const mehediRaw of mehediRawPool.slice(0, 5)) {
      const shihabEndpoint = mehediDetectStreamEndpointTag(mehediRaw);
      const mehediIsAudRaw = Boolean(mehediRaw.shihabIsAudioOnly || mehediRaw.mehediMediaKind === "AUDIO");
      const shihabResSuffix = mehediRaw.shihabResolution ? ` · ${mehediRaw.shihabResolution}` : "";
      mehediEntries.push({
        shihabBadge: shihabEndpoint.tag,
        mehediIsAudio: mehediIsAudRaw,
        shihabLabel: `Sniffed Stream #${shihabSniffIdx} (${shihabEndpoint.sub}${shihabResSuffix})`,
        mehediStream: mehediRaw
      });
      shihabSniffIdx++;
    }

    return mehediEntries;
  }

  let shihabRenderRafId = 0;
  let mehediRenderCooldownTimerId = null;
  let shihabLastRenderAtMs = 0;
  let shihabPendingFullRender = false;

  function shihabScheduleFloatingPanelRender(shihabPositionOnly) {
    if (mehediInstanceDisposed) return;
    if (!shihabPositionOnly) {
      shihabPendingFullRender = true;
    }
    if (shihabRenderRafId || mehediRenderCooldownTimerId) return;
    const mehediRunScheduled = () => {
      shihabRenderRafId = 0;
      shihabLastRenderAtMs = Date.now();
      const shihabDoFull = shihabPendingFullRender;
      shihabPendingFullRender = false;
      shihabRenderFloatingIdmVideoPanel(!shihabDoFull);
    };
    const mehediElapsed = Date.now() - shihabLastRenderAtMs;
    if (mehediElapsed < 65) {
      mehediRenderCooldownTimerId = setTimeout(() => {
        mehediRenderCooldownTimerId = null;
        if (!mehediInstanceDisposed && !shihabRenderRafId) {
          shihabRenderRafId = requestAnimationFrame(mehediRunScheduled);
        }
      }, 65 - mehediElapsed);
      return;
    }
    shihabRenderRafId = requestAnimationFrame(mehediRunScheduled);
  }

  function shihabSchedulePositionOnlyRender() {
    shihabScheduleFloatingPanelRender(true);
  }

  function shihabRenderFloatingIdmVideoPanel(shihabPositionOnly) {
    shihabLastRenderAtMs = Date.now();
    if (!shihabVerifyActiveSingletonOwnership()) return;

    if (location.href !== mehediLastObservedPageHref) {
      mehediLastObservedPageHref = location.href;
      shihabSetDropdownOpen(false);
      mehediLockedDropdownVideoRef = null;
      shihabClearMenuPointerLockNow();
      shihabPageHasDrmProtectedMedia = false;
      mehediDetectedDrmKeySystem = "";
      shihabStreamCacheVersion++;
      shihabPositionOnly = false;
      const shihabNavYtId = shihabExtractCurrentPageYouTubeVideoId();
      if (shihabNavYtId) {
        mehediRequestYouTubeResolutionForVideoId(shihabNavYtId, "", "", false);
      }
    }

    // Prevent tiny ad/tracker iframes from spawning duplicate icons; NEVER hide a real movie/embed player iframe when top frame only has a smaller banner video!
    if (window !== window.top) {
      if (window.innerWidth < 220 || window.innerHeight < 130) {
        if (mehediShadowHost) mehediShadowHost.style.display = "none";
        return;
      }
    } else if (Date.now() < mehediSubframeHoveredUntilMs && !mehediPointerInsideBar && !shihabDropdownOpen) {
      if (mehediShadowHost) mehediShadowHost.style.display = "none";
      return;
    }

    if (!mehediShowFloatingBar || (shihabPanelDismissedUrl && shihabPanelDismissedUrl === location.href)) {
      if (mehediShadowHost) mehediShadowHost.style.display = "none";
      return;
    }

    if (shihabHideVideoInFullscreen && (document.fullscreenElement || document.webkitFullscreenElement)) {
      if (mehediShadowHost) mehediShadowHost.style.display = "none";
      return;
    }

    const shihabFoundTarget = mehediFindPrimaryVisibleVideoWithRect();
    const shihabTargetVideo = shihabFoundTarget.video;
    const mehediRect = shihabFoundTarget.rect;
    if (!shihabTargetVideo || !mehediRect) {
      if (mehediShadowHost) mehediShadowHost.style.display = "none";
      if (window === window.top) {
        shihabLastNotifiedTopActiveKey = "";
      }
      return;
    }

    const mehediTargetAreaPre = Math.max(0, mehediRect.width * mehediRect.height);
    if (window !== window.top) {
      const mehediSubPlaying = !shihabTargetVideo.paused && !shihabTargetVideo.ended;
      if (
        Date.now() < shihabTopFrameActiveUntilMs &&
        shihabTopFrameVideoArea > mehediTargetAreaPre &&
        !mehediSubPlaying &&
        Date.now() >= shihabHoveredUntilMs &&
        !shihabDropdownOpen
      ) {
        if (mehediShadowHost) mehediShadowHost.style.display = "none";
        return;
      }
    }

    // On-change-only top-frame -> child-frame coordination (never broadcasts on a polling interval!)
    if (window === window.top && window.frames && window.frames.length > 0) {
      const shihabTopStateKey = `1:${Math.round(mehediTargetAreaPre / 4096)}:${window.frames.length}`;
      if (shihabTopStateKey !== shihabLastNotifiedTopActiveKey) {
        shihabLastNotifiedTopActiveKey = shihabTopStateKey;
        try {
          for (let shihabFi = 0; shihabFi < window.frames.length; shihabFi++) {
            try {
              window.frames[shihabFi].postMessage({
                __shihabMehediTopPanelActive: true,
                shihabTopVideoArea: mehediTargetAreaPre
              }, "*");
            } catch (_) {}
          }
        } catch (_) {}
      }
    }

    const mehediHost = shihabEnsureShadowHostMounted();
    if (!mehediHost || !shihabRefs) return;

    // Split Data/Stream Update vs Fast Position Update so scroll/resize NEVER re-collects streams or forces layout thrashing!
    const shihabPrevDataVid = shihabReadWeakRef(shihabLastDataTargetVideoRef);
    const shihabNeedsDataUpdate =
      !shihabPositionOnly ||
      shihabDropdownOpen ||
      shihabTargetVideo !== shihabPrevDataVid ||
      shihabStreamCacheVersion !== shihabLastDataStreamVersion;

    if (shihabNeedsDataUpdate) {
      const shihabStreams = shihabCollectMediaFromDomAndSniffer(shihabTargetVideo);
      mehediAppendGoogleDriveSharedVideoStreamIfApplicable(shihabStreams, shihabTargetVideo);
      shihabCachedTargetStreams = shihabStreams;
      shihabCachedSnifferRows = shihabBuildStreamSnifferEntries(shihabStreams);
      shihabLastDataTargetVideoRef = shihabMakeWeakRef(shihabTargetVideo);
      shihabLastDataStreamVersion = shihabStreamCacheVersion;

      const shihabNextCountStr = String(shihabCachedSnifferRows.length);
      if (shihabRefs.mehediCountPill.textContent !== shihabNextCountStr) {
        shihabRefs.mehediCountPill.textContent = shihabNextCountStr;
      }
    }

    const shihabStreams = shihabCachedTargetStreams;
    const mehediSnifferRows = shihabCachedSnifferRows;

    shihabRefs.mehediBar.classList.toggle("mehedi-open", shihabDropdownOpen);

    const shihabViewportW = document.documentElement.clientWidth || window.innerWidth;
    const mehediViewportH = document.documentElement.clientHeight || window.innerHeight;
    const SHIHAB_OUTSIDE_GAP = 5;
    const MEHEDI_VIEWPORT_PAD = 4;

    if (mehediHost.style.display !== "block") {
      mehediHost.style.display = "block";
    }

    // Measure pill dimensions ONCE after initial mount instead of calling getBoundingClientRect() on every scroll frame!
    if (!shihabBarDimensionsMeasured) {
      const mehediBarBox = shihabRefs.mehediBar.getBoundingClientRect();
      if (mehediBarBox.height > 10) {
        shihabLastMeasuredBarH = Math.max(26, Math.ceil(mehediBarBox.height));
      }
      if (!shihabRefs.mehediBar.classList.contains("shihab-compact") && mehediBarBox.width > 80) {
        shihabLastMeasuredFullBarW = Math.ceil(mehediBarBox.width);
        shihabBarDimensionsMeasured = true;
      }
    }
    const mehediBarH = shihabLastMeasuredBarH;
    const shihabFullBarW = shihabLastMeasuredFullBarW;

    const mehediSpaceAbove = mehediRect.top;
    const shihabSpaceRight = shihabViewportW - mehediRect.right;

    let shihabTopPx = MEHEDI_VIEWPORT_PAD;
    let shihabTranslateX = shihabViewportW - MEHEDI_VIEWPORT_PAD;
    let shihabAnchorLeft = false;
    let shihabUseCompact = Boolean(mehediPanelMiniMode);

    if (mehediSpaceAbove >= mehediBarH + SHIHAB_OUTSIDE_GAP + MEHEDI_VIEWPORT_PAD) {
      // 1) Outside ABOVE the top-right corner of the video (0% overlap with video)
      shihabTopPx = Math.max(MEHEDI_VIEWPORT_PAD, Math.round(mehediRect.top - mehediBarH - SHIHAB_OUTSIDE_GAP));
      let mehediDesiredRightX = Math.round(mehediRect.right);
      // Avoid covering YouTube's top-right account/notification buttons in Theater Mode
      if ((location.hostname || "").toLowerCase().includes("youtube.com") && shihabTopPx < 56 && shihabSpaceRight < 40) {
        mehediDesiredRightX = Math.min(mehediDesiredRightX, shihabViewportW - 168);
      }
      const shihabMinRightX = Math.min(shihabViewportW - MEHEDI_VIEWPORT_PAD, (shihabUseCompact ? 56 : shihabFullBarW) + MEHEDI_VIEWPORT_PAD);
      shihabTranslateX = Math.min(shihabViewportW - MEHEDI_VIEWPORT_PAD, Math.max(shihabMinRightX, mehediDesiredRightX));
      shihabAnchorLeft = false;
    } else if (shihabSpaceRight >= (shihabUseCompact ? 58 : shihabFullBarW) + SHIHAB_OUTSIDE_GAP + MEHEDI_VIEWPORT_PAD) {
      // 2) Outside TO THE RIGHT of the video's top-right corner (e.g. Instagram Reels, Facebook Reels, YouTube Shorts, TikTok)
      shihabTopPx = Math.round(
        Math.min(
          Math.max(MEHEDI_VIEWPORT_PAD, mehediRect.top),
          Math.max(MEHEDI_VIEWPORT_PAD, mehediRect.bottom - mehediBarH - MEHEDI_VIEWPORT_PAD)
        )
      );
      shihabTranslateX = Math.round(mehediRect.right + SHIHAB_OUTSIDE_GAP);
      shihabAnchorLeft = true;
    } else if (shihabSpaceRight >= 58) {
      // 3) Narrow right margin: auto-compact pill sits 100% outside to the right of the video and expands leftward on hover
      shihabUseCompact = true;
      shihabTopPx = Math.round(
        Math.min(
          Math.max(MEHEDI_VIEWPORT_PAD, mehediRect.top),
          Math.max(MEHEDI_VIEWPORT_PAD, mehediRect.bottom - mehediBarH - MEHEDI_VIEWPORT_PAD)
        )
      );
      const mehediCompactW = 54;
      shihabTranslateX = Math.min(
        shihabViewportW - MEHEDI_VIEWPORT_PAD,
        Math.round(mehediRect.right + SHIHAB_OUTSIDE_GAP + mehediCompactW)
      );
      shihabAnchorLeft = false;
    } else {
      // 4) True 100vw x 100vh fullscreen or zero-margin edge-to-edge player: compact pill tucked at extreme top-right corner
      shihabUseCompact = true;
      shihabTopPx = mehediSpaceAbove >= 14
        ? 2
        : Math.max(MEHEDI_VIEWPORT_PAD, Math.round(mehediRect.top + MEHEDI_VIEWPORT_PAD));
      shihabTranslateX = shihabViewportW - MEHEDI_VIEWPORT_PAD;
      shihabAnchorLeft = false;
    }

    shihabRefs.shihabWrap.classList.toggle("shihab-anchor-left", shihabAnchorLeft);
    shihabRefs.mehediBar.classList.toggle("shihab-compact", shihabUseCompact);
    const shihabNextTransform = `translate3d(${shihabTranslateX}px, ${shihabTopPx}px, 0)`;
    if (shihabLastTransformCss !== shihabNextTransform) {
      shihabLastTransformCss = shihabNextTransform;
      mehediHost.style.transform = shihabNextTransform;
    }

    const shihabMenuEl = shihabRefs.shihabMenuEl;
    const mehediToastEl = shihabRefs.mehediToastEl;
    const shihabAlignPopupLeft = shihabAnchorLeft && (shihabTranslateX + 370 <= shihabViewportW - 8);
    const mehediWantLeft = shihabAlignPopupLeft ? "0px" : "auto";
    const shihabWantRight = shihabAlignPopupLeft ? "auto" : "0px";
    if (shihabMenuEl.style.left !== mehediWantLeft) shihabMenuEl.style.left = mehediWantLeft;
    if (shihabMenuEl.style.right !== shihabWantRight) shihabMenuEl.style.right = shihabWantRight;
    if (mehediToastEl.style.left !== mehediWantLeft) mehediToastEl.style.left = mehediWantLeft;
    if (mehediToastEl.style.right !== shihabWantRight) mehediToastEl.style.right = shihabWantRight;

    const mehediOpenUpward = (mehediViewportH - (shihabTopPx + mehediBarH) < 240) && shihabTopPx > 260;
    const shihabWantMenuTop = mehediOpenUpward ? "auto" : "calc(100% + 7px)";
    const mehediWantMenuBottom = mehediOpenUpward ? "calc(100% + 7px)" : "auto";
    if (shihabMenuEl.style.top !== shihabWantMenuTop) shihabMenuEl.style.top = shihabWantMenuTop;
    if (shihabMenuEl.style.bottom !== mehediWantMenuBottom) shihabMenuEl.style.bottom = mehediWantMenuBottom;

    const shihabWantMenuDisplay = shihabDropdownOpen ? "block" : "none";
    if (shihabMenuEl.style.display !== shihabWantMenuDisplay) {
      shihabMenuEl.style.display = shihabWantMenuDisplay;
    }

    if (shihabDropdownOpen) {
      const mehediLiveCaption = shihabExtractBestVideoTitle(shihabTargetVideo, shihabStreams[0]);
      const shihabHeaderTitle = mehediLiveCaption || "MHS Stream Sniffer";
      if (shihabRefs.shihabMenuHeaderDiv && shihabRefs.shihabMenuHeaderDiv.title !== shihabHeaderTitle) {
        shihabRefs.shihabMenuHeaderDiv.title = shihabHeaderTitle;
      }

      const shihabIsDrmStream = Boolean(
        shihabPageHasDrmProtectedMedia || (shihabTargetVideo && shihabTargetVideo.mediaKeys)
      );
      let mehediNextMenuSig = "";
      if (mehediSnifferRows.length === 0) {
        mehediNextMenuSig = `EMPTY|${shihabIsDrmStream ? 1 : 0}|${mehediDetectedDrmKeySystem || ""}`;
      } else {
        const shihabSigParts = [shihabHeaderTitle];
        for (const shihabEntry of mehediSnifferRows) {
          const mehediS = shihabEntry.mehediStream || {};
          const shihabSz = mehediS.mehediDisplaySizeBytes > 0 ? mehediS.mehediDisplaySizeBytes : (mehediS.shihabSizeBytes || -1);
          shihabSigParts.push(
            `${shihabEntry.mehediIsAudio ? 1 : 0}:${shihabEntry.shihabBadge}:${shihabEntry.shihabLabel}:${mehediS.shihabStreamUrl || ""}:${shihabSz}:${mehediS.mehediAudioCompanionUrl || ""}`
          );
        }
        mehediNextMenuSig = shihabSigParts.join("|");
      }

      // If pointer is pressed inside the dropdown, defer DOM patching until pointerup (capped at 3s safety timeout) so clicks NEVER drop or shift!
      if (mehediPointerDownInMenuAtMs > 0 && (Date.now() - mehediPointerDownInMenuAtMs) < 3000) {
        if (mehediNextMenuSig !== shihabLastRenderedMenuSig) {
          shihabDeferredMenuRender = true;
        }
        return;
      }
      mehediPointerDownInMenuAtMs = 0;

      shihabRenderedMenuRows = mehediSnifferRows;
      shihabRenderedMenuTargetVideoRef = shihabMakeWeakRef(shihabTargetVideo);

      // Fast path: ZERO DOM mutations when menu contents are unchanged (eliminates hover blinking & scroll resets!)
      if (mehediNextMenuSig === shihabLastRenderedMenuSig) {
        return;
      }
      shihabLastRenderedMenuSig = mehediNextMenuSig;

      const shihabRowsBox = shihabRefs.shihabMenuRowsEl;
      if (!shihabRowsBox) return;

      if (mehediSnifferRows.length === 0) {
        shihabClearElementChildren(shihabRowsBox);
        const mehediEmptyRow = document.createElement("div");
        mehediEmptyRow.className = "shihab-item";
        mehediEmptyRow.dataset.shihabEmptyRow = "1";
        const shihabEmptySpan = document.createElement("span");
        shihabEmptySpan.className = "mehedi-name";
        shihabEmptySpan.textContent = shihabIsDrmStream
          ? (mehediDetectedDrmKeySystem
              ? `Protected (DRM — Encrypted Media Stream: ${mehediDetectedDrmKeySystem})`
              : "Protected (DRM — Encrypted Media Stream)")
          : "No direct HTTP stream exposed — click ↻ to re-track";
        mehediEmptyRow.appendChild(shihabEmptySpan);
        shihabRowsBox.appendChild(mehediEmptyRow);
      } else {
        // Clear stale empty-state placeholder if transitioning from 0 -> N rows
        if (shihabRowsBox.firstChild && shihabRowsBox.firstChild.dataset && shihabRowsBox.firstChild.dataset.shihabEmptyRow === "1") {
          shihabClearElementChildren(shihabRowsBox);
        }

        // Patch existing .shihab-item DOM nodes IN-PLACE so hover state & in-flight clicks NEVER reset!
        const shihabExistingCount = shihabRowsBox.children.length;
        for (let mehediIdx = 0; mehediIdx < mehediSnifferRows.length; mehediIdx++) {
          const shihabEntry = mehediSnifferRows[mehediIdx];
          const mehediStreamObj = shihabEntry.mehediStream;
          const shihabBytes = mehediStreamObj.mehediDisplaySizeBytes > 0
            ? mehediStreamObj.mehediDisplaySizeBytes
            : mehediStreamObj.shihabSizeBytes;
          const mehediSizeHint = shihabFormatBytesHuman(shihabBytes);
          const shihabRowTooltip = mehediLiveCaption
            ? (mehediSizeHint ? `${mehediLiveCaption} (${mehediSizeHint})` : mehediLiveCaption)
            : shihabEntry.shihabLabel;
          const mehediPillClass = "shihab-kind-pill " + (shihabEntry.mehediIsAudio ? "mehedi-kind-audio" : "mehedi-kind-video");

          let shihabRow = mehediIdx < shihabExistingCount ? shihabRowsBox.children[mehediIdx] : null;
          if (!shihabRow || !shihabRow.__shihabRowRefs) {
            shihabRow = document.createElement("div");
            shihabRow.className = "shihab-item";

            const mehediLeftGrp = document.createElement("div");
            mehediLeftGrp.className = "mehedi-left-group";

            const shihabKindPill = document.createElement("span");
            const mehediNameSpan = document.createElement("span");
            mehediNameSpan.className = "mehedi-name";

            mehediLeftGrp.appendChild(shihabKindPill);
            mehediLeftGrp.appendChild(mehediNameSpan);

            const shihabDlSpan = document.createElement("span");
            shihabDlSpan.className = "shihab-dl-action";
            shihabDlSpan.textContent = "Download";

            shihabRow.appendChild(mehediLeftGrp);
            shihabRow.appendChild(shihabDlSpan);
            shihabRow.__shihabRowRefs = { shihabKindPill, mehediNameSpan };
            shihabRowsBox.appendChild(shihabRow);
          }

          shihabRow.dataset.shihabRowIdx = String(mehediIdx);
          const mehediR = shihabRow.__shihabRowRefs;
          if (mehediR.shihabKindPill.className !== mehediPillClass) {
            mehediR.shihabKindPill.className = mehediPillClass;
          }
          if (mehediR.shihabKindPill.textContent !== shihabEntry.shihabBadge) {
            mehediR.shihabKindPill.textContent = shihabEntry.shihabBadge;
          }
          if (mehediR.mehediNameSpan.textContent !== shihabEntry.shihabLabel) {
            mehediR.mehediNameSpan.textContent = shihabEntry.shihabLabel;
          }
          if (mehediR.mehediNameSpan.title !== shihabRowTooltip) {
            mehediR.mehediNameSpan.title = shihabRowTooltip;
          }
        }

        // Remove only extra trailing rows if the list shrank
        while (shihabRowsBox.children.length > mehediSnifferRows.length) {
          shihabRowsBox.removeChild(shihabRowsBox.children[shihabRowsBox.children.length - 1]);
        }
      }
    }
  }

  function mehediTriggerStreamDownload(shihabStreamEntry, mehediTargetVideo) {
    const shihabRealTitle = shihabExtractBestVideoTitle(mehediTargetVideo, shihabStreamEntry);
    const mehediCleanStem = (shihabRealTitle || "Video")
      .replace(/[\\/:*?"<>|\x00-\x1f]+/g, " ")
      .replace(/\s+/g, " ")
      .replace(/[. ]+$/, "")
      .trim()
      .slice(0, 105) || "Video";

    const shihabIsAud = Boolean(shihabStreamEntry.shihabIsAudioOnly || shihabStreamEntry.mehediMediaKind === "AUDIO");
    const mehediQualTag = shihabStreamEntry.shihabQualityLabel ||
      (shihabIsAud ? "Audio" : (shihabStreamEntry.shihabResolution || ""));

    let shihabExt = ".mp4";
    const mehediRawExt = String(shihabStreamEntry.shihabExt || "").toLowerCase();
    if (mehediRawExt === "m3u8") shihabExt = ".mp4";
    else if (mehediRawExt === "webm") shihabExt = ".webm";
    else if (mehediRawExt === "mkv") shihabExt = ".mkv";
    else if (mehediRawExt === "mov") shihabExt = ".mov";
    else if (mehediRawExt === "m4v") shihabExt = ".m4v";
    else if (mehediRawExt === "mp3") shihabExt = ".mp3";
    else if (mehediRawExt === "m4a" || shihabIsAud) shihabExt = ".m4a";

    const mehediFinalFileName = mehediQualTag
      ? `${mehediCleanStem} [${mehediQualTag}]${shihabExt}`
      : `${mehediCleanStem}${shihabExt}`;

    try {
      mehediShowPanelStatusToast(`Sending ${mehediQualTag || "stream"} to MHS Download Manager...`, false);
      Promise.race([
        chrome.runtime.sendMessage({
          shihabCmd: "mehedi_send_download_now",
          shihabUrl: shihabStreamEntry.shihabStreamUrl,
          mehediReferrer: shihabStreamEntry.mehediCapturedReferer || shihabSafeHttpRef(),
          shihabFileName: mehediFinalFileName,
          mehediMimeType: shihabStreamEntry.mehediMimeType || (mehediRawExt === "m3u8" ? "application/vnd.apple.mpegurl" : ""),
          shihabFileSize: shihabStreamEntry.shihabSizeBytes || -1,
          mehediTabTitle: shihabRealTitle || document.title || "",
          shihabOrigin: shihabStreamEntry.shihabOrigin || shihabSafeHttpOrg(),
          mehediOmitCookies: shihabStreamEntry.mehediOmitCookies,
          mehediUserAgent: shihabStreamEntry.mehediCustomUserAgent || "",
          mehediAudioCompanionUrl: shihabStreamEntry.mehediAudioCompanionUrl || "",
          shihabAudioCompanionBytes: shihabStreamEntry.shihabAudioCompanionBytes || -1,
          shihabQualityLabel: mehediQualTag
        }),
        new Promise((resolve) => setTimeout(() => resolve({ ok: false, mehediError: "⚠ mdm.exe bridge timed out — launch MHS Download Manager" }), 5500))
      ]).then((shihabAck) => {
        if (shihabAck && shihabAck.ok) {
          mehediShowPanelStatusToast(`✓ Sent ${mehediQualTag || "stream"} to MHS Download Manager!`, false);
        } else {
          mehediShowPanelStatusToast(
            shihabAck && shihabAck.mehediError ? shihabAck.mehediError : "⚠ Launch mdm.exe first",
            true
          );
        }
      }).catch(() => {
        mehediShowPanelStatusToast("⚠ Refresh page once to reconnect extension", true);
      });
    } catch (mehediSendErr) {
      mehediShowPanelStatusToast("⚠ Refresh page once to reconnect extension", true);
    }
  }

  // Convert Uint8Array slice to Base64 fast without call-stack overflow
  function shihabUint8ToBase64(mehediBytes) {
    const SHIHAB_STEP = 32768;
    const mehediParts = [];
    for (let shihabIdx = 0; shihabIdx < mehediBytes.length; shihabIdx += SHIHAB_STEP) {
      const mehediSub = mehediBytes.subarray(shihabIdx, Math.min(mehediBytes.length, shihabIdx + SHIHAB_STEP));
      mehediParts.push(String.fromCharCode.apply(null, mehediSub));
    }
    return btoa(mehediParts.join(""));
  }

  // Execute Browser-Tab Relay Job inside the tab's authentic Origin/TLS context (eliminates 403 Forbidden!)
  async function mehediExecuteTabRelayJobInPageContext(shihabJob) {
    const mehediJobId = String((shihabJob && shihabJob.jobId) || "").trim();
    const shihabTargetUrl = String((shihabJob && shihabJob.url) || "").trim();
    if (!mehediJobId || !/^https?:\/\//i.test(shihabTargetUrl)) {
      throw new Error("Blocked invalid relay job target");
    }
    const mehediOmitCookies = Boolean(shihabJob.omitCookies);
    let shihabTotalBytes = Number(shihabJob.totalBytes || -1);
    const mehediIsGv = shihabTargetUrl.includes("googlevideo.com") || shihabTargetUrl.includes("/videoplayback");

    const SHIHAB_FETCH_SLICE = 2 * 1024 * 1024;
    const MEHEDI_IPC_SLICE = 512 * 1024;
    let mehediOffset = 0;

    while (true) {
      if (shihabTotalBytes > 0 && mehediOffset >= shihabTotalBytes) {
        break;
      }
      const shihabSliceEnd = shihabTotalBytes > 0
        ? Math.min(shihabTotalBytes - 1, mehediOffset + SHIHAB_FETCH_SLICE - 1)
        : (mehediOffset + SHIHAB_FETCH_SLICE - 1);

      const mehediSliceUrl = mehediIsGv
        ? (shihabTargetUrl + (shihabTargetUrl.includes("?") ? "&" : "?") + `range=${mehediOffset}-${shihabSliceEnd}`)
        : shihabTargetUrl;

      const shihabHeaders = {};
      if (!mehediIsGv) {
        shihabHeaders["Range"] = `bytes=${mehediOffset}-${shihabSliceEnd}`;
      }

      const mehediResp = await fetch(mehediSliceUrl, {
        method: "GET",
        headers: shihabHeaders,
        credentials: mehediOmitCookies ? "omit" : "include"
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

      const shihabAb = await mehediResp.arrayBuffer();
      if (shihabAb.byteLength === 0) {
        break;
      }

      const mehediFullChunk = new Uint8Array(shihabAb);
      for (let shihabSubOff = 0; shihabSubOff < mehediFullChunk.byteLength; shihabSubOff += MEHEDI_IPC_SLICE) {
        const mehediSubBytes = mehediFullChunk.subarray(
          shihabSubOff,
          Math.min(mehediFullChunk.byteLength, shihabSubOff + MEHEDI_IPC_SLICE)
        );
        const shihabAck = await chrome.runtime.sendMessage({
          shihabCmd: "shihab_relay_chunk_from_tab",
          shihabJobId: mehediJobId,
          mehediOffset: mehediOffset + shihabSubOff,
          shihabTotal: shihabTotalBytes,
          mehediBase64Chunk: shihabUint8ToBase64(mehediSubBytes)
        });
        if (shihabAck && shihabAck.cancelled) {
          return true;
        }
        if (!shihabAck || !shihabAck.ok) {
          throw new Error("Relay bridge write failed");
        }
      }

      mehediOffset += mehediFullChunk.byteLength;
      if (!mehediIsGv && mehediResp.status === 200) {
        break;
      }
      if (shihabTotalBytes <= 0 && mehediFullChunk.byteLength < SHIHAB_FETCH_SLICE) {
        break;
      }
    }

    await chrome.runtime.sendMessage({
      shihabCmd: "shihab_relay_done_from_tab",
      shihabJobId: mehediJobId,
      mehediOk: true,
      shihabError: ""
    });
    return true;
  }

  function shihabExtractLinksFromDocument(mehediSelectionOnly) {
    const shihabCollected = [];
    const mehediSeen = new Set();
    let shihabAnchors = [];

    if (mehediSelectionOnly) {
      const mehediSel = window.getSelection();
      if (mehediSel && mehediSel.rangeCount > 0) {
        const shihabFrag = mehediSel.getRangeAt(0).cloneContents();
        shihabAnchors = Array.from(shihabFrag.querySelectorAll("a[href]"));
      }
    } else {
      shihabAnchors = Array.from(document.querySelectorAll("a[href]"));
    }

    for (const mehediAnchor of shihabAnchors) {
      let shihabAbsHref = "";
      try {
        shihabAbsHref = new URL(mehediAnchor.getAttribute("href") || mehediAnchor.href, location.href).href;
      } catch (shihabErr) {
        continue;
      }
      if (
        !shihabAbsHref.startsWith("http://") &&
        !shihabAbsHref.startsWith("https://") &&
        !shihabAbsHref.startsWith("ftp://") &&
        !shihabAbsHref.startsWith("ftps://") &&
        !/^magnet:\?/i.test(shihabAbsHref)
      ) {
        continue;
      }
      if (mehediSeen.has(shihabAbsHref)) {
        continue;
      }
      mehediSeen.add(shihabAbsHref);
      shihabCollected.push({
        url: shihabAbsHref,
        text: (mehediAnchor.textContent || "").trim().slice(0, 120),
        fileType: ""
      });
    }
    return shihabCollected;
  }

  let mehediUpdateToastHost = null;
  let shihabUpdateToastTimerId = 0;

  function shihabRemovePageUpdateToast() {
    if (shihabUpdateToastTimerId) {
      clearTimeout(shihabUpdateToastTimerId);
      shihabUpdateToastTimerId = 0;
    }
    if (mehediUpdateToastHost) {
      try { mehediUpdateToastHost.remove(); } catch (_) {}
      mehediUpdateToastHost = null;
    }
  }

  function shihabShowPageUpdateToast(notice) {
    if (window !== window.top || mehediInstanceDisposed) return;
    if (!notice || !notice.updateAvailable || notice.dismissed || !notice.latestVersion) return;
    if (!document.documentElement) return;
    if (mehediUpdateToastHost && mehediUpdateToastHost.isConnected) return;

    const host = document.createElement("div");
    host.id = "__shihab_mehedi_update_toast_host";
    host.style.cssText = "position:fixed;right:18px;bottom:18px;z-index:2147483645;pointer-events:auto;";
    const shadow = host.attachShadow({ mode: "closed" });

    const style = document.createElement("style");
    style.textContent = `
      .card {
        display: flex; align-items: center; gap: 10px;
        background: rgba(15, 23, 42, 0.96); color: #f8fafc;
        border: 1px solid rgba(124, 58, 237, 0.65); border-radius: 10px;
        box-shadow: 0 10px 30px rgba(0, 0, 0, 0.45);
        padding: 10px 12px; font-family: "Segoe UI", system-ui, sans-serif; font-size: 12.5px;
        max-width: 340px; backdrop-filter: blur(8px);
      }
      .icon { width: 28px; height: 28px; flex-shrink: 0; border-radius: 6px; }
      .meta { flex: 1; min-width: 0; line-height: 1.3; }
      .title { font-weight: 600; color: #e2e8f0; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
      .sub { font-size: 11px; color: #94a3b8; margin-top: 2px; }
      .btn {
        border: 0; border-radius: 6px; padding: 6px 10px; font-size: 11.5px; font-weight: 600;
        cursor: pointer; background: linear-gradient(135deg, #2563eb, #7c3aed); color: #fff;
        white-space: nowrap;
      }
      .btn:hover { filter: brightness(1.1); }
      .close {
        border: 0; background: transparent; color: #94a3b8; font-size: 15px; line-height: 1;
        cursor: pointer; padding: 2px 5px; border-radius: 4px;
      }
      .close:hover { color: #fff; background: rgba(255, 255, 255, 0.08); }
    `;

    const card = document.createElement("div");
    card.className = "card";
    const img = document.createElement("img");
    img.className = "icon";
    img.src = SHIHAB_MHS_ICON_DATA_URI;
    img.alt = "MHS";

    const meta = document.createElement("div");
    meta.className = "meta";
    const title = document.createElement("div");
    title.className = "title";
    title.textContent = `MHS Download Manager v${notice.latestVersion}`;
    const sub = document.createElement("div");
    sub.className = "sub";
    sub.textContent = "New update ready — click Update to install";
    meta.appendChild(title);
    meta.appendChild(sub);

    const updBtn = document.createElement("button");
    updBtn.type = "button";
    updBtn.className = "btn";
    updBtn.textContent = "Update";
    updBtn.addEventListener("click", (ev) => {
      if (!ev || ev.isTrusted !== true) return;
      ev.stopPropagation();
      try {
        chrome.runtime.sendMessage({
          shihabCmd: "shihab_open_updater"
        }).catch(() => {});
        chrome.runtime.sendMessage({
          shihabCmd: "shihab_dismiss_update_notice",
          version: notice.latestVersion
        }).catch(() => {});
      } catch (_) {}
      shihabRemovePageUpdateToast();
    });

    const closeBtn = document.createElement("button");
    closeBtn.type = "button";
    closeBtn.className = "close";
    closeBtn.textContent = "×";
    closeBtn.title = "Dismiss update notice";
    closeBtn.addEventListener("click", (ev) => {
      if (!ev || ev.isTrusted !== true) return;
      ev.stopPropagation();
      try {
        chrome.runtime.sendMessage({
          shihabCmd: "shihab_dismiss_update_notice",
          version: notice.latestVersion
        }).catch(() => {});
      } catch (_) {}
      shihabRemovePageUpdateToast();
    });

    card.appendChild(img);
    card.appendChild(meta);
    card.appendChild(updBtn);
    card.appendChild(closeBtn);
    shadow.appendChild(style);
    shadow.appendChild(card);

    (document.body || document.documentElement).appendChild(host);
    mehediUpdateToastHost = host;
  }

  function mehediOnRuntimeMessage(shihabMsg, mehediSender, shihabReply) {
    if (mehediSender && mehediSender.id && chrome.runtime && chrome.runtime.id && mehediSender.id !== chrome.runtime.id) {
      return false;
    }
    if (shihabMsg && shihabMsg.shihabCmd === "shihab_update_available") {
      if (window === window.top && shihabMsg.notice) {
        shihabShowPageUpdateToast(shihabMsg.notice);
      }
      shihabReply({ ok: true });
      return false;
    }
    if (shihabMsg && shihabMsg.shihabCmd === "mehedi_streams_updated") {
      shihabSetTabStreamsCache(Array.isArray(shihabMsg.mehediStreams) ? shihabMsg.mehediStreams : []);
      if (typeof shihabMsg.shihabShowPanel === "boolean") {
        mehediShowFloatingBar = shihabMsg.shihabShowPanel;
      }
      if (typeof shihabMsg.shihabPanelMiniMode === "boolean") {
        mehediPanelMiniMode = shihabMsg.shihabPanelMiniMode;
      }
      if (shihabMsg.mehediPreventKey) {
        shihabConfiguredPreventKey = String(shihabMsg.mehediPreventKey);
      }
      if (shihabMsg.shihabForceKey) {
        mehediConfiguredForceKey = String(shihabMsg.shihabForceKey);
      }
      if (shihabTabStreamsCache.length > 0 || shihabHasAnyVideoInFrame()) {
        shihabActivateVideoFeaturesOnce();
        if (!shihabVideoModeListenersAttached) {
          shihabSyncDiscoveredVideoObservers();
        }
      }
      shihabScheduleFloatingPanelRender(false);
      shihabReply({ ok: true });
      return false;
    }

    if (shihabMsg && shihabMsg.shihabCmd === "mehedi_trigger_page_retrack") {
      shihabActivateVideoFeaturesOnce();
      try {
        window.postMessage({ __shihabMehediContentPing: true, shihabIsInitialSync: false }, shihabSafeSameWindowOrigin());
      } catch (_) {}
      const shihabRetrackYtId = shihabExtractCurrentPageYouTubeVideoId();
      if (shihabRetrackYtId) {
        mehediRequestYouTubeResolutionForVideoId(shihabRetrackYtId, "", "", true);
      }
      shihabPanelDismissedUrl = "";
      shihabRenderFloatingIdmVideoPanel(false);
      shihabReply({ ok: true });
      return false;
    }

    if (shihabMsg && shihabMsg.shihabCmd === "mehedi_exec_tab_relay_job") {
      mehediExecuteTabRelayJobInPageContext(shihabMsg.mehediJob)
        .then(() => shihabReply({ ok: true }))
        .catch(() => shihabReply({ ok: false }));
      return true;
    }

    if (shihabMsg && shihabMsg.shihabCmd === "mehedi_get_selected_video_streams") {
      if (window !== window.top) {
        return false;
      }
      const shihabLastSelNow = shihabReadWeakRef(mehediLastSelectedVideoRef);
      const mehediSelVid =
        mehediFindPrimaryVisibleVideo() ||
        (shihabLastSelNow && shihabLastSelNow.isConnected ? shihabLastSelNow : null);
      if (mehediSelVid) {
        shihabActivateVideoFeaturesOnce();
        mehediEnsureVideoUidAndInspect(mehediSelVid, true);
      }
      if (Array.isArray(shihabMsg.shihabPreloadedStreams)) {
        shihabSetTabStreamsCache(shihabMsg.shihabPreloadedStreams);
      }
      const shihabPrepPromise = Array.isArray(shihabMsg.shihabPreloadedStreams)
        ? Promise.resolve(shihabTabStreamsCache)
        : shihabRefreshBackgroundStreamsThrottled(true);
      shihabPrepPromise.then(() => {
        const shihabOnlySelectedStreams = mehediSelVid ? shihabCollectMediaFromDomAndSniffer(mehediSelVid) : [];
        const mehediExactTitle = shihabExtractBestVideoTitle(mehediSelVid, shihabOnlySelectedStreams[0]);
        const shihabCleanStem = (mehediExactTitle || "Video")
          .replace(/[\\/:*?"<>|\x00-\x1f]+/g, " ")
          .replace(/\s+/g, " ")
          .replace(/[. ]+$/, "")
          .trim()
          .slice(0, 105) || "Video";

        const mehediFormattedList = shihabOnlySelectedStreams.map((shihabIt) => {
          const mehediIsAud = Boolean(shihabIt.shihabIsAudioOnly || shihabIt.mehediMediaKind === "AUDIO");
          const shihabQual = shihabIt.shihabQualityLabel || (mehediIsAud ? "Audio" : (shihabIt.shihabResolution || ""));
          let mehediExtDot = ".mp4";
          const shihabRawE = String(shihabIt.shihabExt || "").toLowerCase();
          if (shihabRawE === "m3u8") mehediExtDot = ".mp4";
          else if (shihabRawE === "webm") mehediExtDot = ".webm";
          else if (shihabRawE === "mkv") mehediExtDot = ".mkv";
          else if (shihabRawE === "mov") mehediExtDot = ".mov";
          else if (shihabRawE === "m4v") mehediExtDot = ".m4v";
          else if (shihabRawE === "mp3") mehediExtDot = ".mp3";
          else if (shihabRawE === "m4a" || mehediIsAud) mehediExtDot = ".m4a";

          const shihabResolvedFile = shihabQual
            ? `${shihabCleanStem} [${shihabQual}]${mehediExtDot}`
            : `${shihabCleanStem}${mehediExtDot}`;

          return Object.assign({}, shihabIt, {
            shihabDisplayName: shihabCleanStem,
            mehediResolvedFileName: shihabResolvedFile
          });
        });

        shihabReply({
          ok: true,
          shihabHasSelectedVideo: Boolean(mehediSelVid),
          shihabVideoTitle: shihabCleanStem,
          mehediStreams: mehediFormattedList
        });
      }).catch(() => {
        shihabReply({
          ok: false,
          shihabHasSelectedVideo: false,
          shihabVideoTitle: "",
          mehediStreams: []
        });
      });
      return true;
    }

    if (shihabMsg && shihabMsg.shihabCmd === "mehedi_get_video_caption") {
      const mehediBestVid = mehediFindPrimaryVisibleVideo();
      const shihabCaption = shihabExtractBestVideoTitle(mehediBestVid);
      shihabReply({
        shihabVideoTitle: shihabCaption,
        mehediPageUrl: location.href
      });
      return false;
    }

    if (shihabMsg && shihabMsg.shihabCmd === "shihab_ping_content_alive") {
      shihabReply({ ok: !mehediInstanceDisposed });
      return false;
    }

    if (shihabMsg && shihabMsg.shihabCmd === "mehedi_collect_page_links") {
      const mehediLinks = shihabExtractLinksFromDocument(Boolean(shihabMsg.mehediSelectionOnly));
      shihabReply({
        mehediPageUrl: location.href,
        shihabPageTitle: shihabExtractBestVideoTitle(null) || document.title || "",
        shihabLinks: mehediLinks
      });
      return false;
    }
    return false;
  }

  function mehediNotifyTopIfSubframeHovered() {
    if (window !== window.top) {
      try {
        window.top.postMessage({ __shihabMehediSubframeVideoHovered: true }, "*");
      } catch (_) {}
    }
  }

  let shihabLiveVideosCollection = null;
  let shihabLiveShredditCollection1 = null;
  let shihabLiveShredditCollection2 = null;
  const shihabIsRedditHost = (() => {
    try { return (location.hostname || "").toLowerCase().includes("reddit.com"); } catch (_) { return false; }
  })();

  function shihabHasAnyVideoInFrame() {
    try {
      if (!shihabLiveVideosCollection && document && typeof document.getElementsByTagName === "function") {
        shihabLiveVideosCollection = document.getElementsByTagName("video");
      }
      if (shihabLiveVideosCollection && shihabLiveVideosCollection.length > 0) return true;
      if (shihabIsRedditHost && document && typeof document.getElementsByTagName === "function") {
        if (!shihabLiveShredditCollection1) shihabLiveShredditCollection1 = document.getElementsByTagName("shreddit-player");
        if (!shihabLiveShredditCollection2) shihabLiveShredditCollection2 = document.getElementsByTagName("shreddit-player-2");
        if (shihabLiveShredditCollection1.length > 0 || shihabLiveShredditCollection2.length > 0) return true;
      }
      const shihabLastSel = shihabReadWeakRef(mehediLastSelectedVideoRef);
      if (shihabLastSel && shihabLastSel.isConnected) return true;
    } catch (_) {}
    return false;
  }

  function shihabAttachVideoModeListeners() {
    if (shihabVideoModeListenersAttached || mehediInstanceDisposed) return;
    shihabVideoModeListenersAttached = true;
    window.addEventListener("pointerover", mehediTrackPointerOverVideo, { capture: true, passive: true });
    window.addEventListener("pointermove", mehediTrackPointerOverVideo, { capture: true, passive: true });
    window.addEventListener("scroll", shihabSchedulePositionOnlyRender, { capture: true, passive: true });
    window.addEventListener("resize", shihabSchedulePositionOnlyRender, { passive: true });
    document.addEventListener("fullscreenchange", shihabSchedulePositionOnlyRender, { passive: true });
  }

  function shihabDetachVideoModeListeners() {
    if (!shihabVideoModeListenersAttached) return;
    shihabVideoModeListenersAttached = false;
    window.removeEventListener("pointerover", mehediTrackPointerOverVideo, { capture: true });
    window.removeEventListener("pointermove", mehediTrackPointerOverVideo, { capture: true });
    window.removeEventListener("scroll", shihabSchedulePositionOnlyRender, { capture: true });
    window.removeEventListener("resize", shihabSchedulePositionOnlyRender);
    document.removeEventListener("fullscreenchange", shihabSchedulePositionOnlyRender);
  }

  function shihabEnsurePerVideoObserversCreated() {
    if (!shihabVideoIntersectionObserver && typeof IntersectionObserver === "function") {
      try {
        shihabVideoIntersectionObserver = new IntersectionObserver(() => {
          if (!mehediInstanceDisposed) shihabScheduleFloatingPanelRender(false);
        }, { threshold: [0, 0.1, 0.5, 1] });
      } catch (_) {}
    }
    if (!shihabVideoResizeObserver && typeof ResizeObserver === "function") {
      try {
        shihabVideoResizeObserver = new ResizeObserver(() => {
          if (!mehediInstanceDisposed) shihabScheduleFloatingPanelRender(true);
        });
      } catch (_) {}
    }
    if (!shihabVideoAttrObserver && typeof MutationObserver === "function") {
      try {
        shihabVideoAttrObserver = new MutationObserver(() => {
          if (!mehediInstanceDisposed) {
            shihabStreamCacheVersion++;
            shihabScheduleFloatingPanelRender(false);
          }
        });
      } catch (_) {}
    }
  }

  function shihabObserveVideoElementDirect(shihabVid) {
    if (!shihabVid || shihabVid.tagName !== "VIDEO" || shihabObservedVideosSet.has(shihabVid)) return;
    shihabObservedVideosSet.add(shihabVid);
    shihabEnsurePerVideoObserversCreated();
    try { if (shihabVideoIntersectionObserver) shihabVideoIntersectionObserver.observe(shihabVid); } catch (_) {}
    try { if (shihabVideoResizeObserver) shihabVideoResizeObserver.observe(shihabVid); } catch (_) {}
    try {
      if (shihabVideoAttrObserver) {
        shihabVideoAttrObserver.observe(shihabVid, { attributes: true, attributeFilter: ["src"] });
        const shihabSources = shihabVid.querySelectorAll("source");
        for (let i = 0; i < shihabSources.length; i++) {
          shihabVideoAttrObserver.observe(shihabSources[i], { attributes: true, attributeFilter: ["src"] });
        }
      }
    } catch (_) {}
  }

  function shihabSyncDiscoveredVideoObservers() {
    const shihabAllVids = shihabCollectAllVideosDeep(document, []);
    if (shihabAllVids.length > 0) {
      shihabActivateVideoFeaturesOnce();
      shihabAttachVideoModeListeners();
      for (let i = 0; i < shihabAllVids.length; i++) {
        shihabObserveVideoElementDirect(shihabAllVids[i]);
      }
      return true;
    }
    if (mehediShadowHost && mehediShadowHost.style.display !== "none" && !shihabDropdownOpen) {
      mehediShadowHost.style.display = "none";
    }
    shihabDetachVideoModeListeners();
    return false;
  }

  function shihabActivateVideoFeaturesOnce() {
    if (shihabVideoFeaturesActivated || mehediInstanceDisposed) return;
    if (!shihabVerifyActiveSingletonOwnership()) return;
    shihabVideoFeaturesActivated = true;

    try {
      chrome.storage.local.get(["shihabHideVideoInFullscreen"], (res) => {
        if (res && typeof res.shihabHideVideoInFullscreen === "boolean") {
          shihabHideVideoInFullscreen = res.shihabHideVideoInFullscreen;
        }
      });
      if (!mehediStorageChangeHandler && chrome.storage && chrome.storage.onChanged) {
        mehediStorageChangeHandler = (changes, area) => {
          if (area === "local" && changes && changes.shihabHideVideoInFullscreen) {
            shihabHideVideoInFullscreen = Boolean(changes.shihabHideVideoInFullscreen.newValue);
            shihabScheduleFloatingPanelRender(false);
          }
        };
        chrome.storage.onChanged.addListener(mehediStorageChangeHandler);
      }
    } catch (_) {}

    try {
      window.postMessage({ __shihabMehediContentPing: true, shihabIsInitialSync: true }, shihabSafeSameWindowOrigin());
    } catch (_) {}

    const shihabInitialYtId = shihabExtractCurrentPageYouTubeVideoId();
    if (shihabInitialYtId) {
      mehediRequestYouTubeResolutionForVideoId(shihabInitialYtId, "", "", false);
    }

    shihabRefreshBackgroundStreamsThrottled(true).then(() => {
      if (!mehediInstanceDisposed) {
        shihabScheduleFloatingPanelRender(false);
      }
    });
  }

  function shihabOnSpaNavigationEvent() {
    if (mehediInstanceDisposed) return;
    if (location.href !== mehediLastObservedPageHref) {
      mehediLastObservedPageHref = location.href;
      shihabSetDropdownOpen(false);
      mehediLockedDropdownVideoRef = null;
      shihabClearMenuPointerLockNow();
      shihabPageHasDrmProtectedMedia = false;
      mehediDetectedDrmKeySystem = "";
      shihabStreamCacheVersion++;
      const shihabNavYtId = shihabExtractCurrentPageYouTubeVideoId();
      if (shihabNavYtId) {
        shihabActivateVideoFeaturesOnce();
        mehediRequestYouTubeResolutionForVideoId(shihabNavYtId, "", "", false);
      }
    }
    if (shihabHasAnyVideoInFrame() || shihabVideoFeaturesActivated) {
      shihabSyncDiscoveredVideoObservers();
      shihabScheduleFloatingPanelRender(false);
    }
  }

  let mehediLastPointerDeepCheckMs = 0;
  function mehediTrackPointerOverVideo(shihabPointerEv) {
    if (mehediInstanceDisposed) return;
    if (!shihabHasAnyVideoInFrame()) return;

    const mehediPath = typeof shihabPointerEv.composedPath === "function" ? shihabPointerEv.composedPath() : [shihabPointerEv.target];
    if (
      mehediShadowHost &&
      (shihabPointerEv.target === mehediShadowHost ||
       mehediPath.includes(mehediShadowHost) ||
       mehediShadowHost.contains(shihabPointerEv.target))
    ) {
      mehediPointerInsideBar = true;
      shihabHoveredUntilMs = Date.now() + 8000;
      shihabArmHoverExpiryTimer(shihabHoveredUntilMs);
      mehediNotifyTopIfSubframeHovered();
      return;
    }

    const mehediNowMs = Date.now();
    const shihabCurHovered = shihabReadWeakRef(mehediHoveredVideoRef);
    for (const shihabNode of mehediPath) {
      if (shihabNode && shihabNode.tagName === "VIDEO") {
        const mehediChanged = shihabCurHovered !== shihabNode;
        if (!mehediChanged && (mehediNowMs - mehediLastPointerDeepCheckMs) < 120) {
          return;
        }
        mehediLastPointerDeepCheckMs = mehediNowMs;
        mehediHoveredVideoRef = shihabMakeWeakRef(shihabNode);
        mehediLastSelectedVideoRef = shihabMakeWeakRef(shihabNode);
        shihabHoveredUntilMs = mehediNowMs + 6000;
        shihabArmHoverExpiryTimer(shihabHoveredUntilMs);
        mehediNotifyTopIfSubframeHovered();
        mehediEnsureVideoUidAndInspect(shihabNode, false);
        if (mehediChanged && !shihabReadWeakRef(mehediLockedDropdownVideoRef)) {
          shihabSetDropdownOpen(false);
        }
        shihabRefreshBackgroundStreamsThrottled(false);
        shihabScheduleFloatingPanelRender(!mehediChanged);
        return;
      }
    }

    // Shared 120ms throttle for BOTH pointerover and pointermove bounding-box hit-testing
    if ((mehediNowMs - mehediLastPointerDeepCheckMs) < 120) {
      return;
    }
    mehediLastPointerDeepCheckMs = mehediNowMs;
    const mehediX = shihabPointerEv.clientX;
    const shihabY = shihabPointerEv.clientY;
    if (typeof mehediX === "number" && typeof shihabY === "number") {
      const mehediVideos = shihabCollectAllVideosDeep(document, []);
      for (const shihabVid of mehediVideos) {
        const mehediRect = mehediGetVisibleVideoRect(shihabVid);
        if (!mehediRect) continue;
        if (mehediX >= mehediRect.left && mehediX <= mehediRect.right &&
            shihabY >= mehediRect.top && shihabY <= mehediRect.bottom) {
          const mehediSwitched = shihabCurHovered !== shihabVid;
          mehediHoveredVideoRef = shihabMakeWeakRef(shihabVid);
          mehediLastSelectedVideoRef = shihabMakeWeakRef(shihabVid);
          shihabHoveredUntilMs = mehediNowMs + 6000;
          shihabArmHoverExpiryTimer(shihabHoveredUntilMs);
          mehediNotifyTopIfSubframeHovered();
          mehediEnsureVideoUidAndInspect(shihabVid, false);
          if (mehediSwitched && !shihabReadWeakRef(mehediLockedDropdownVideoRef)) {
            shihabSetDropdownOpen(false);
          }
          shihabRefreshBackgroundStreamsThrottled(false);
          shihabScheduleFloatingPanelRender(!mehediSwitched);
          return;
        }
      }
    }
  }

  function mehediMediaLifecycleListener(shihabEv) {
    if (mehediInstanceDisposed) return;
    const shihabTarget = (shihabEv && typeof shihabEv.composedPath === "function" ? shihabEv.composedPath()[0] : shihabEv.target) || (shihabEv && shihabEv.target);
    if (shihabTarget && shihabTarget.tagName === "VIDEO") {
      shihabActivateVideoFeaturesOnce();
      shihabAttachVideoModeListeners();
      shihabObserveVideoElementDirect(shihabTarget);
      mehediEnsureVideoUidAndInspect(shihabTarget, false);
      if (shihabEv.type === "play" || shihabEv.type === "playing") {
        mehediLastSelectedVideoRef = shihabMakeWeakRef(shihabTarget);
      }
      shihabRefreshBackgroundStreamsThrottled(false);
      shihabScheduleFloatingPanelRender(false);
    }
  }

  function mehediCloseDropdownOnOutsideEvent(shihabEv) {
    if (!shihabDropdownOpen || mehediInstanceDisposed) return;
    if (shihabEv.type === "keydown") {
      if (shihabEv.key === "Escape") {
        shihabSetDropdownOpen(false);
        mehediLockedDropdownVideoRef = null;
        shihabRenderFloatingIdmVideoPanel(false);
      }
      return;
    }
    const mehediPath = typeof shihabEv.composedPath === "function" ? shihabEv.composedPath() : [shihabEv.target];
    if (
      mehediShadowHost &&
      (shihabEv.target === mehediShadowHost ||
       mehediPath.includes(mehediShadowHost) ||
       mehediShadowHost.contains(shihabEv.target))
    ) {
      return;
    }
    shihabSetDropdownOpen(false);
    mehediLockedDropdownVideoRef = null;
    shihabRenderFloatingIdmVideoPanel(false);
  }

  function shihabClearMenuPointerLockNow() {
    if (shihabPointerReleaseTimerId) {
      clearTimeout(shihabPointerReleaseTimerId);
      shihabPointerReleaseTimerId = null;
    }
    mehediPointerDownInMenuAtMs = 0;
    shihabDeferredMenuRender = false;
  }

  // Keep pointer lock for a brief 80ms grace window after pointerup/mouseup so touch/pen compatibility `click` ALWAYS fires before deferred row updates!
  function shihabReleaseMenuPointerLock() {
    if (!mehediPointerDownInMenuAtMs) return;
    if (shihabPointerReleaseTimerId) {
      clearTimeout(shihabPointerReleaseTimerId);
    }
    shihabPointerReleaseTimerId = setTimeout(() => {
      shihabPointerReleaseTimerId = null;
      mehediPointerDownInMenuAtMs = 0;
      if (shihabDeferredMenuRender) {
        shihabDeferredMenuRender = false;
        if (!mehediInstanceDisposed && shihabDropdownOpen) {
          shihabScheduleFloatingPanelRender(false);
        }
      }
    }, 80);
  }

  function mehediDisposeCurrentInstance() {
    if (mehediInstanceDisposed) {
      if (mehediShadowHost) {
        try { mehediShadowHost.remove(); } catch (_) {}
        mehediShadowHost = null;
      }
      return;
    }
    mehediInstanceDisposed = true;
    mehediPointerInsideBar = false;
    shihabSetDropdownOpen(false);
    shihabClearMenuPointerLockNow();
    shihabHasHiddenVideoInDom = false;
    shihabLastRenderedMenuSig = "";
    shihabRenderedMenuRows = [];
    shihabRenderedMenuTargetVideoRef = null;
    shihabLastDataTargetVideoRef = null;
    mehediHoveredVideoRef = null;
    mehediLastSelectedVideoRef = null;
    mehediLockedDropdownVideoRef = null;
    shihabCachedTargetStreams = [];
    shihabCachedSnifferRows = [];
    if (shihabRenderRafId) {
      try { cancelAnimationFrame(shihabRenderRafId); } catch (_) {}
      shihabRenderRafId = 0;
    }
    if (mehediRenderCooldownTimerId) {
      clearTimeout(mehediRenderCooldownTimerId);
      mehediRenderCooldownTimerId = null;
    }
    if (shihabHoverExpireTimerId) {
      clearTimeout(shihabHoverExpireTimerId);
      shihabHoverExpireTimerId = null;
    }
    if (shihabSubframeExpireTimerId) {
      clearTimeout(shihabSubframeExpireTimerId);
      shihabSubframeExpireTimerId = null;
    }
    if (shihabTopActiveExpireTimerId) {
      clearTimeout(shihabTopActiveExpireTimerId);
      shihabTopActiveExpireTimerId = null;
    }
    if (shihabToastClearTimer) {
      clearTimeout(shihabToastClearTimer);
      shihabToastClearTimer = null;
    }
    if (shihabDomObserver) {
      shihabDomObserver.disconnect();
      shihabDomObserver = null;
    }
    if (shihabVideoIntersectionObserver) {
      shihabVideoIntersectionObserver.disconnect();
      shihabVideoIntersectionObserver = null;
    }
    if (shihabVideoResizeObserver) {
      shihabVideoResizeObserver.disconnect();
      shihabVideoResizeObserver = null;
    }
    if (shihabVideoAttrObserver) {
      shihabVideoAttrObserver.disconnect();
      shihabVideoAttrObserver = null;
    }
    window.removeEventListener("message", mehediOnPageHookWindowMessage, false);
    window.removeEventListener("popstate", shihabOnSpaNavigationEvent);
    window.removeEventListener("hashchange", shihabOnSpaNavigationEvent);
    window.removeEventListener("yt-navigate-finish", shihabOnSpaNavigationEvent);
    for (const shihabGestureEvt of ["pointerdown", "mousedown", "click", "auxclick", "contextmenu", "keydown"]) {
      window.removeEventListener(shihabGestureEvt, mehediReportTrustedUserGesture, true);
    }
    shihabDetachVideoModeListeners();
    for (const mehediMediaEvt of ["play", "playing", "loadedmetadata", "loadeddata", "canplay"]) {
      document.removeEventListener(mehediMediaEvt, mehediMediaLifecycleListener, true);
    }
    try {
      chrome.runtime.onMessage.removeListener(mehediOnRuntimeMessage);
    } catch (shihabRemErr) {
      // Context invalidated
    }
    try {
      if (mehediStorageChangeHandler && chrome.storage && chrome.storage.onChanged) {
        chrome.storage.onChanged.removeListener(mehediStorageChangeHandler);
      }
    } catch (_) {}
    shihabVidUidIdentityMap.clear();
    shihabExactBlobToStreamUrlsMap.clear();
    shihabBlobToStreamUrlsMap.clear();
    mehediShortcodeMetaMap.clear();
    shihabGroupKeyTitleMap.clear();
    shihabRequestedYtVideoIdsMs.clear();
    shihabStreamsByUrlMap.clear();
    shihabStreamsByParentMasterMap.clear();
    shihabStreamsByGroupKeyMap.clear();
    shihabStreamsByAltIdMap.clear();
    shihabMaxDetectedAtByGroupMap.clear();
    if (mehediShadowHost) {
      try { mehediShadowHost.remove(); } catch (_) {}
      mehediShadowHost = null;
    }
    shihabRemovePageUpdateToast();
    shihabRefs = null;
  }

  let mehediStorageChangeHandler = null;

  // Register IPC, trusted user-gesture & MAIN-world postMessage listeners IMMEDIATELY at document_start so zero early clicks or manifests are missed!
  window.addEventListener("message", mehediOnPageHookWindowMessage, false);
  for (const shihabGestureEvt of ["pointerdown", "mousedown", "auxclick", "contextmenu", "keydown"]) {
    window.addEventListener(shihabGestureEvt, mehediReportTrustedUserGesture, { capture: true, passive: true });
  }
  // Keep click non-passive so magnet:? anchor interception can call preventDefault() cleanly.
  window.addEventListener("click", mehediReportTrustedUserGesture, { capture: true, passive: false });
  try {
    chrome.runtime.onMessage.addListener(mehediOnRuntimeMessage);
  } catch (shihabInitErr) {
    // Ignore invalidated context
  }

  function mehediAppendGoogleDriveSharedVideoStreamIfApplicable(shihabStreams, shihabVideoEl) {
    try {
      const host = (location.hostname || "").toLowerCase();
      if (host !== "drive.google.com" && !host.endsWith(".drive.google.com")) return;
      let fileId = "";
      const pathM = (location.pathname || "").match(/\/file\/d\/([A-Za-z0-9_-]{10,})/i);
      if (pathM && pathM[1]) fileId = pathM[1];
      if (!fileId) {
        const sp = new URLSearchParams(location.search || "");
        fileId = sp.get("id") || sp.get("docid") || "";
      }
      if (!fileId && shihabVideoEl) {
        const srcUrl = shihabVideoEl.currentSrc || shihabVideoEl.src || "";
        const docM = srcUrl.match(/[?&](?:docid|id)=([A-Za-z0-9_-]{10,})/i);
        if (docM && docM[1]) fileId = docM[1];
      }
      if (!fileId) {
        const viewerNode = document.querySelector("[data-id][aria-label], [data-target='doc'][data-id], iframe[src*='drive.google.com/file/d/']");
        if (viewerNode) {
          const dataId = viewerNode.getAttribute("data-id") || "";
          if (/^[A-Za-z0-9_-]{15,}$/.test(dataId)) fileId = dataId;
          if (!fileId && viewerNode.tagName === "IFRAME") {
            const fm = String(viewerNode.getAttribute("src") || "").match(/\/file\/d\/([A-Za-z0-9_-]{10,})/i);
            if (fm && fm[1]) fileId = fm[1];
          }
        }
      }
      if (!fileId) return;
      const directUrl = `https://drive.usercontent.google.com/download?id=${encodeURIComponent(fileId)}&export=download&confirm=t`;
      const exists = Array.isArray(shihabStreams) && shihabStreams.some((s) => s && s.shihabStreamUrl === directUrl);
      if (!exists && Array.isArray(shihabStreams)) {
        const titleClean = (document.title || "Google_Drive_Video").replace(/\s*-\s*Google Drive$/i, "").trim() || "Google_Drive_Video";
        shihabStreams.unshift({
          shihabStreamUrl: directUrl,
          mehediMimeType: "video/mp4",
          shihabExt: "mp4",
          shihabSizeBytes: -1,
          shihabResolution: "Original",
          shihabQualityLabel: "Google Drive Original",
          shihabDisplayName: titleClean,
          mehediResolvedFileName: titleClean.endsWith(".mp4") ? titleClean : `${titleClean}.mp4`,
          mehediCapturedReferer: shihabSafeHttpRef(),
          mehediOmitCookies: false
        });
      }
    } catch (_) {}
  }

  function mehediInitDomObservers() {
    if (!shihabVerifyActiveSingletonOwnership()) return;

    shihabLiveVideosCollection = document.getElementsByTagName("video");
    shihabLastKnownLightVideoCount = shihabLiveVideosCollection.length;

    // Activate video features lazily ONLY if frame already has a video or is a YouTube video page;
    // zero-video pages & ad iframes skip startup storage.local.get, shihab_query_tab_streams & scroll/pointer listeners!
    const shihabHasInitialVideo = shihabSyncDiscoveredVideoObservers();
    if (!shihabHasInitialVideo && shihabExtractCurrentPageYouTubeVideoId()) {
      shihabActivateVideoFeaturesOnce();
    } else if (shihabHasInitialVideo) {
      shihabScheduleFloatingPanelRender(false);
    }

    // Lightweight childList-only MutationObserver (ZERO global style/class attribute observation, ZERO querySelector on non-video SPA text churn!)
    shihabDomObserver = new MutationObserver((mehediRecords) => {
      if (mehediInstanceDisposed || !Array.isArray(mehediRecords) || mehediRecords.length === 0) return;

      // O(1) live HTMLCollection check catches ANY light-DOM <video> addition/removal even inside nested wrappers
      const shihabCurLightCount = shihabLiveVideosCollection ? shihabLiveVideosCollection.length : 0;
      if (shihabCurLightCount !== shihabLastKnownLightVideoCount) {
        shihabLastKnownLightVideoCount = shihabCurLightCount;
        shihabSyncDiscoveredVideoObservers();
        shihabScheduleFloatingPanelRender(false);
        return;
      }

      // Fast path for 0-video pages (ChatGPT/Gmail/Slack/docs): only check if an added node itself is a known shadow video host or has shadowRoot!
      if (shihabCurLightCount === 0 && !shihabVideoModeListenersAttached) {
        for (let i = 0; i < mehediRecords.length; i++) {
          const shihabAdded = mehediRecords[i] && mehediRecords[i].addedNodes;
          if (!shihabAdded || shihabAdded.length === 0) continue;
          for (let j = 0; j < shihabAdded.length; j++) {
            const shihabN = shihabAdded[j];
            if (!shihabN || shihabN.nodeType !== 1) continue;
            if (shihabN === mehediShadowHost || shihabN === mehediUpdateToastHost) continue;
            const mehediTag = shihabN.tagName ? shihabN.tagName.toUpperCase() : "";
            if (
              mehediTag === "VIDEO" ||
              SHIHAB_SHADOW_HOST_TAG_SET.has(mehediTag) ||
              shihabN.shadowRoot
            ) {
              if (shihabSyncDiscoveredVideoObservers()) {
                shihabScheduleFloatingPanelRender(false);
              }
              return;
            }
          }
        }
        if (shihabIsRedditHost && shihabHasAnyVideoInFrame()) {
          if (shihabSyncDiscoveredVideoObservers()) {
            shihabScheduleFloatingPanelRender(false);
          }
        }
        return;
      }

      // Active-video page path: check if selected video was removed or media/shadow-host nodes were mutated
      const shihabLastSel = shihabReadWeakRef(mehediLastSelectedVideoRef);
      if (shihabLastSel && !shihabLastSel.isConnected) {
        shihabSyncDiscoveredVideoObservers();
        shihabScheduleFloatingPanelRender(false);
        return;
      }

      for (let i = 0; i < mehediRecords.length; i++) {
        const shihabRec = mehediRecords[i];
        const mehediT = shihabRec && shihabRec.target;
        if (!mehediT) continue;
        if (
          (mehediShadowHost && (mehediT === mehediShadowHost || mehediShadowHost.contains(mehediT))) ||
          (mehediUpdateToastHost && (mehediT === mehediUpdateToastHost || mehediUpdateToastHost.contains(mehediT)))
        ) {
          continue;
        }
        const mehediListCheck = (shihabNodeList) => {
          if (!shihabNodeList || shihabNodeList.length === 0) return false;
          for (let k = 0; k < shihabNodeList.length; k++) {
            const shihabN = shihabNodeList[k];
            if (!shihabN || shihabN.nodeType !== 1) continue;
            const mehediNt = shihabN.tagName ? shihabN.tagName.toUpperCase() : "";
            if (mehediNt === "VIDEO" || mehediNt === "SOURCE" || SHIHAB_SHADOW_HOST_TAG_SET.has(mehediNt) || shihabN.shadowRoot) {
              return true;
            }
          }
          return false;
        };
        if (mehediListCheck(shihabRec.addedNodes) || mehediListCheck(shihabRec.removedNodes)) {
          shihabSyncDiscoveredVideoObservers();
          shihabScheduleFloatingPanelRender(false);
          return;
        }
      }
    });

    if (document.documentElement) {
      shihabDomObserver.observe(document.documentElement, {
        childList: true,
        subtree: true
      });
    }

    for (const mehediMediaEvt of ["play", "playing", "loadedmetadata", "loadeddata", "canplay"]) {
      document.addEventListener(mehediMediaEvt, mehediMediaLifecycleListener, true);
    }

    window.addEventListener("popstate", shihabOnSpaNavigationEvent);
    window.addEventListener("hashchange", shihabOnSpaNavigationEvent);
    window.addEventListener("yt-navigate-finish", shihabOnSpaNavigationEvent);
  }

  window.__shihabMehediIdmCleanupPrev = mehediDisposeCurrentInstance;

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", mehediInitDomObservers, { once: true });
  } else {
    mehediInitDomObservers();
  }
})();
