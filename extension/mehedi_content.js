"use strict";

(() => {
  const SHIHAB_HOST_ID = "shihab-mehedi-idm-video-panel-host";
  const SHIHAB_HOST_ATTR = "data-shihab-mehedi-idm-host";
  const SHIHAB_DOM_OWNER_ATTR = "data-shihab-mehedi-idm-owner";
  const MEHEDI_SUPERSEDE_EVENT = "shihab-mehedi-idm-supersede";
  const shihabInstanceToken = "shihab_idm_" + Date.now() + "_" + Math.random().toString(36).slice(2, 10);
  let mehediInstanceDisposed = false;

  // Cleanly tear down any older injected instance (across both same-world and isolated-world reloads) so only ONE icon ever exists!
  if (typeof window.__shihabMehediIdmCleanupPrev === "function") {
    try {
      window.__shihabMehediIdmCleanupPrev();
    } catch (mehediPrevErr) {
      // Ignore cleanup errors
    }
  }
  try {
    document.dispatchEvent(new CustomEvent(MEHEDI_SUPERSEDE_EVENT, {
      detail: { shihabOwnerToken: shihabInstanceToken }
    }));
    if (document.documentElement) {
      document.documentElement.setAttribute(SHIHAB_DOM_OWNER_ATTR, shihabInstanceToken);
    }
  } catch (shihabDomClaimErr) {
    // Ignore early document_start edge cases
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
  shihabSweepForeignHostElements(null);

  // Official MHS Download Manager 24x24 icon embedded inline so strict site CSP img-src never blocks it
  const SHIHAB_MHS_ICON_DATA_URI =
    "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAABgAAAAYCAYAAADgdz34AAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAASlSURBVEhL7dNdTFNnGMBxsgWzmCzRC7JpQrZlH5HooobgGF8SUZiAVqBAgdID1BZpLaVQaCnQwylQSlsqLS20yIceikIRkWGR4nBi6gfDIARcRYbHAKOWxn24xGRu+iz1ou50N5rs0n/y5L16f8978/r5ve2NuwLv+k6k5/w/eqfjufa9nhfuzS1P3B/q3O6PTqy5v1A/dO/SrLhD6+9b96LzH/jeeaM2tP0pfx9/MR6gfzwe2PRo/DP18vgO5dJ4sNzxXWTD4rV96HSV753XywL+G41PKzd1POvcols//bFmFd+mIvBdigV8T+08HlU9jcdW3cIPVdgHqKLvz2UKRy15/IuWguPnLYKCXksF29wvZ+F4HcPwpS/9sg1tTyM29wFs7QEIbHL++rl62bld+ZMzWO5whstmnTHobefByhvOJPGEkya87EIEVld+4QUXn9PnKmWbXVXMLldTvnldSTfU+tov26T/LW7r6b8hULeuDGpcFu5WEdieurtYFHYHi636AUsst2MpZVdkdMGomlk0rObyBmSC/F6snIVjWF47Vs9oxbT0ZsyQrkLP0BqLcQpKIS3YonUnBjY9WgpSPijZ3foEQjWrEK1cgji5AyiyWUhHbwNSYYccgXUxv/DCYlnRMEh550FecBY0LBwMue3QkW2AM1laGM44ATaqHIYTykK8Cz7RrKV82rgyulNx/1Ro0xpEymZhPzoFCRXXIUV8FRjiq5BbdPHH45w+Gp/TRxOzzPM1bDPUI0bQ0JvBQGuEjlQFmJNkMHC4CkYSxDB2ULDDuyBI/SBtu2rJFFI7P7FXsUjCM4VjwC69DPm8wcniY2dxIdvcXZ3XflPF7PwXXv8S7z9cCbZDErDF8lescbwN3gU76+5RghX3aiOxmdUD2B0SniewAk9gBT7HMiVh4aaKo11tCkbrZDNiIr3cgw/Hi+Bmghiu7uOOeXFPoXVzYeE1s+h+9PbzxMobJPwY7zxIikdAxOpewHLbWjGGydiUqXWcRkzQTn31cg8+FieAuW+EcDOarSQtCEMdW2LQKdWRmjkSXsAbhLLCIZAU9NqkR7uOKxFTlzq75ZQ+o5HblaYa7U9VkPBrMVxY2M+D6fCcDNICT4kS+xkadoeEF3H6QMo9B9K8jhEFQ5+iR0zP2um6v9qp9dQeCmq9mFxDwqei8+FuZC7c+4r23w+XLLoyzZDYSbiYbQY0rxO0bDM00Jux1nTVjba0hltmirR6lCqHb+PLvPhkNBscUUxwhGY8mtlxYCMJTyqxBWQIx/5gltjIOLMT5IgRtIwWMGXqfulIVaT2JNVSLxypfnwpUULCZyJy4OcIBB6EpE6QcE9ZpZciuBXXobB4BEp5g1DF6YNathnUzE7QI0Y4maWDgWw9dCfJrg8kltvtyTUwcVAIkwf4MLuvABaimPAwLBtehCOwtpti8vX96CW2AE7h0Hgxb5AQsbsJlNlJyBEjocnSEYb0RuIktZ7Aj1QT/Ycr14fiReujsXxiIoZL3NrLImYiEMIRmkEsh6QSvwcnzz/eFf+1r+/NIrX4S6lS71hIQ/W3bHs18z4D26j+vt7bXqt/ALd76PrzDh/aAAAAAElFTkSuQmCC";

  let shihabTabStreamsCache = [];
  let mehediShowFloatingBar = true;
  let mehediPanelMiniMode = false;
  let shihabConfiguredPreventKey = "Alt";
  let mehediConfiguredForceKey = "Ins";
  let shihabPanelDismissedUrl = "";
  let mehediShadowHost = null;
  let shihabRefs = null;
  let shihabDropdownOpen = false;
  let mehediLockedDropdownVideoEl = null;
  let mehediHoveredVideoEl = null;
  let mehediLastSelectedVideoEl = null;
  let shihabHoveredUntilMs = 0;
  let mehediPointerInsideBar = false;
  let shihabLastMeasuredFullBarW = 178;
  let mehediSubframeHoveredUntilMs = 0;
  let shihabTopFrameActiveUntilMs = 0;
  let mehediLastTopFrameNotifyMs = 0;
  let mehediLastBgSyncMs = 0;
  let shihabToastClearTimer = null;
  let mehediPeriodicTimerId = null;
  let shihabDomObserver = null;
  let shihabPageHasDrmProtectedMedia = false;
  let mehediDetectedDrmKeySystem = "";
  let mehediLastObservedPageHref = location.href;
  let shihabLocalVidUidCounter = 1;
  let mehediLastSentGestureMs = 0;
  let shihabLastSentGestureHref = "";
  let mehediLastSentAltState = false;
  let shihabLastSentForceState = false;

  // Live bindings from mehedi_page_hook.js (direct DOM <video> fiber identity, byte-exact MSE blob -> URLs, shortcode/altId -> groupKey & caption)
  const shihabVidUidIdentityMap = new Map();
  const mehediVidLastInspectMs = new WeakMap();
  const shihabExactBlobToStreamUrlsMap = new Map();
  const shihabBlobToStreamUrlsMap = new Map();
  const mehediShortcodeMetaMap = new Map();
  const shihabGroupKeyTitleMap = new Map();

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
      for (const shihabParam of ["bytestart", "byteend", "alr", "rn", "rbuf"]) {
        mehediUrlObj.searchParams.delete(shihabParam);
      }
      if (mehediUrlObj.searchParams.has("range")) {
        const mehediSigned = (mehediUrlObj.searchParams.get("sparams") || "").split(",");
        if (!mehediSigned.includes("range")) {
          mehediUrlObj.searchParams.delete("range");
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
    shihabRequestedYtVideoIdsMs.set(shihabVidId, mehediNow);

    const mehediYtGrp = `yt:${shihabVidId}`;
    if (mehediTitleHint) {
      shihabGroupKeyTitleMap.set(mehediYtGrp, mehediTitleHint);
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
          shihabTabStreamsCache = shihabAck.mehediStreams;
          shihabRenderFloatingIdmVideoPanel();
        } else if (shihabAck && shihabAck.shihabUsePageFallback) {
          try {
            window.postMessage({
              __shihabResolveYtInMainWorld: shihabVidId,
              mehediVisitorData: shihabVisitorData || ""
            }, "*");
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
        window.postMessage({ __shihabInspectVideoUid: mehediUid }, "*");
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
      shihabTopFrameActiveUntilMs = Date.now() + 2200;
      if (Date.now() >= shihabHoveredUntilMs && mehediShadowHost) {
        mehediShadowHost.style.display = "none";
      }
      return;
    }
    if (shihabEvent.data.__shihabMehediSubframeVideoHovered === true && window === window.top) {
      mehediSubframeHoveredUntilMs = Date.now() + 4500;
      if (!mehediPointerInsideBar && !shihabDropdownOpen && mehediShadowHost) {
        mehediShadowHost.style.display = "none";
      }
      return;
    }
    if (shihabEvent.source !== window || shihabEvent.data.__shihabMehediPageHook !== true) {
      return;
    }
    const mehediKind = shihabEvent.data.mehediKind;
    const shihabPayload = shihabEvent.data.shihabPayload;
    if (!shihabPayload) return;

    if (mehediKind === "shihab_eme_drm_detected") {
      shihabPageHasDrmProtectedMedia = true;
      mehediDetectedDrmKeySystem = String(shihabPayload.keySystem || "EME");
      shihabRenderFloatingIdmVideoPanel();
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

        shihabVidUidIdentityMap.set(mehediVidUid, {
          mehediGroupKeys: mehediMergedGroups,
          shihabAltIds: shihabMergedAlts,
          mehediBoundUrls: mehediMergedUrls,
          shihabTitleHint
        });
        if (shihabVidUidIdentityMap.size > 120) {
          const mehediOldUid = shihabVidUidIdentityMap.keys().next().value;
          shihabVidUidIdentityMap.delete(mehediOldUid);
        }
        for (const mehediGrp of mehediMergedGroups) {
          if (shihabTitleHint) shihabGroupKeyTitleMap.set(mehediGrp, shihabTitleHint);
          for (const shihabAlt of shihabMergedAlts) {
            mehediIndexIdentifierToGroup(shihabAlt, mehediGrp, shihabTitleHint);
          }
        }
        shihabRenderFloatingIdmVideoPanel();
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
            shihabExactBlobToStreamUrlsMap.set(mehediBlobUrl, shihabExactSet);
            if (shihabExactBlobToStreamUrlsMap.size > 80) {
              const mehediFirstExact = shihabExactBlobToStreamUrlsMap.keys().next().value;
              shihabExactBlobToStreamUrlsMap.delete(mehediFirstExact);
            }
          }
          shihabExactSet.add(shihabStreamClean);
          // Overwrite fallback set with exact byte-matched URLs so any earlier preloaded-Reel guess is purged!
          shihabBlobToStreamUrlsMap.set(mehediBlobUrl, new Set(shihabExactSet));
        } else if (!shihabExactBlobToStreamUrlsMap.has(mehediBlobUrl)) {
          let mehediUrlSet = shihabBlobToStreamUrlsMap.get(mehediBlobUrl);
          if (!mehediUrlSet) {
            mehediUrlSet = new Set();
            shihabBlobToStreamUrlsMap.set(mehediBlobUrl, mehediUrlSet);
            if (shihabBlobToStreamUrlsMap.size > 80) {
              const shihabFirstKey = shihabBlobToStreamUrlsMap.keys().next().value;
              shihabBlobToStreamUrlsMap.delete(shihabFirstKey);
            }
          }
          mehediUrlSet.add(shihabStreamClean);
        }
        shihabRenderFloatingIdmVideoPanel();
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
        shihabGroupKeyTitleMap.set(shihabGroupKey, shihabTitleHint);
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
            shihabGroupKeyTitleMap.set(shihabItemGrp, mehediItemTitle);
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

  // Locate the single-post / single-player DOM card enclosing a specific <video> (strictly 1 <video> max so adjacent feed posts NEVER bleed!)
  function shihabFindScopedCardForVideo(mehediVid, shihabMaxVideosAllowed) {
    if (!mehediVid) return null;
    const mehediAllowed = shihabMaxVideosAllowed || 1;
    const shihabSemanticArticle = mehediVid.closest("article, shreddit-post, ytd-reel-video-renderer, [data-testid='tweet'], [role='article']");
    if (shihabSemanticArticle && shihabSemanticArticle.querySelectorAll("video").length <= mehediAllowed) {
      return shihabSemanticArticle;
    }

    let mehediBestCard = null;
    let shihabCur = mehediVid.parentElement;
    for (let mehediDepth = 0; shihabCur && shihabCur !== document.body && shihabCur !== document.documentElement && mehediDepth < 16; mehediDepth++) {
      if (shihabCur.tagName === "MAIN" || shihabCur.getAttribute("role") === "feed" || shihabCur.getAttribute("role") === "main") break;
      const shihabVidCount = shihabCur.querySelectorAll("video").length;
      if (shihabVidCount > mehediAllowed) break;
      mehediBestCard = shihabCur;
      shihabCur = shihabCur.parentElement;
    }
    return mehediBestCard;
  }

  function mehediMapBoundUrlsToGroups(shihabUrlIterable, mehediTargetGroupSet, shihabTargetUrlSet) {
    if (!shihabUrlIterable) return;
    for (const mehediBoundUrl of shihabUrlIterable) {
      shihabTargetUrlSet.add(mehediBoundUrl);
      const shihabExtracted = shihabExtractUrlGroupAndAltIds(mehediBoundUrl);
      if (shihabExtracted.mehediGroupKey) {
        mehediTargetGroupSet.add(shihabExtracted.mehediGroupKey);
      }
      for (const mehediAlt of shihabExtracted.shihabAltIds) {
        const shihabMapped = mehediShortcodeMetaMap.get(mehediAlt);
        if (shihabMapped && shihabMapped.mehediGroupKey) {
          mehediTargetGroupSet.add(shihabMapped.mehediGroupKey);
        }
      }
      for (const shihabCached of shihabTabStreamsCache) {
        if (!shihabCached) continue;
        if (shihabCached.shihabStreamUrl === mehediBoundUrl && shihabCached.shihabGroupKey) {
          mehediTargetGroupSet.add(shihabCached.shihabGroupKey);
        } else if (shihabExtracted.shihabAltIds.length > 0 && shihabCached.shihabGroupKey) {
          if (shihabExtracted.shihabAltIds.includes(shihabCached.shihabGroupKey) ||
              (Array.isArray(shihabCached.mehediAltIds) && shihabCached.mehediAltIds.some((mehediId) => shihabExtracted.shihabAltIds.includes(String(mehediId))))) {
            mehediTargetGroupSet.add(shihabCached.shihabGroupKey);
          }
        }
      }
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
        // Cross-check fiber altIds against shihabTabStreamsCache
        for (const shihabAlt of mehediIdent.shihabAltIds) {
          for (const mehediCached of shihabTabStreamsCache) {
            if (!mehediCached || !mehediCached.shihabGroupKey) continue;
            if (
              mehediCached.shihabGroupKey === shihabAlt ||
              (Array.isArray(mehediCached.mehediAltIds) && mehediCached.mehediAltIds.includes(shihabAlt))
            ) {
              shihabDirectDomGroupKeys.add(mehediCached.shihabGroupKey);
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
    }

    // 2. Single-video page route parameters (YouTube Watch/Shorts/Embed/Live, Instagram Reel/Post, Facebook Reel/Watch/Videos, X Status)
    if (mehediHost.includes("youtube.com") || mehediHost.includes("youtu.be") || mehediHost.includes("youtube-nocookie.com")) {
      const mehediPageYtId = shihabExtractCurrentPageYouTubeVideoId();
      if (mehediPageYtId) {
        shihabShortcodes.add(mehediPageYtId);
        mehediRouteAndCardGroupKeys.add(`yt:${mehediPageYtId}`);
      }
    } else {
      const mehediRouteMatch = shihabPath.match(/\/(?:reel|reels|p|tv|videos|status)\/([A-Za-z0-9_-]+)/i);
      if (mehediRouteMatch && mehediRouteMatch[1] && !/^(?:home|feed|for_you|following)$/i.test(mehediRouteMatch[1])) {
        shihabShortcodes.add(mehediRouteMatch[1]);
        if (mehediHost.includes("x.com") || mehediHost.includes("twitter.com")) {
          mehediRouteAndCardGroupKeys.add(`tw:${mehediRouteMatch[1]}`);
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
        const mehediCodeMatch = shihabHref.match(/\/(?:reel|reels|p|tv|videos|status|shorts)\/([A-Za-z0-9_-]+)/i);
        if (mehediCodeMatch && mehediCodeMatch[1] && !/^(?:home|feed|audio)$/i.test(mehediCodeMatch[1])) {
          shihabShortcodes.add(mehediCodeMatch[1]);
          if (/^\d{5,}$/.test(mehediCodeMatch[1])) {
            shihabShortcodes.add(`vid:${mehediCodeMatch[1]}`);
          }
          if (mehediHost.includes("x.com") || mehediHost.includes("twitter.com")) {
            mehediRouteAndCardGroupKeys.add(`tw:${mehediCodeMatch[1]}`);
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

    // Cross-resolve shortcodes & route IDs -> manifest groupKeys (via mehediShortcodeMetaMap + mehediAltIds in shihabTabStreamsCache)
    for (const shihabCode of shihabShortcodes) {
      const mehediMeta = mehediShortcodeMetaMap.get(shihabCode);
      if (mehediMeta && mehediMeta.mehediGroupKey) {
        mehediRouteAndCardGroupKeys.add(mehediMeta.mehediGroupKey);
      }
      for (const shihabCached of shihabTabStreamsCache) {
        if (!shihabCached || !shihabCached.shihabGroupKey) continue;
        if (
          shihabCached.shihabGroupKey === shihabCode ||
          shihabCached.mehediShortcode === shihabCode ||
          (Array.isArray(shihabCached.mehediAltIds) && shihabCached.mehediAltIds.some((mehediAlt) => String(mehediAlt) === shihabCode))
        ) {
          mehediRouteAndCardGroupKeys.add(shihabCached.shihabGroupKey);
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
        const shihabStreamWithHint = shihabTabStreamsCache.find(
          (mehediRec) => mehediRec && mehediRec.shihabGroupKey === mehediGrp && Boolean(mehediRec.shihabTitleHint)
        );
        if (shihabStreamWithHint) {
          shihabManifestTitleHint = shihabStreamWithHint.shihabTitleHint;
          break;
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
      const mehediReservedIgPaths = new Set([
        "explore", "reels", "reel", "p", "tv", "stories", "direct", "accounts", "audio", "ar", "about", "legal"
      ]);

      if (shihabIgCard) {
        const mehediLinks = shihabIgCard.querySelectorAll("header a[href^='/'], a[href^='/']");
        for (const shihabLink of mehediLinks) {
          const mehediHref = shihabLink.getAttribute("href") || "";
          const shihabUserMatch = mehediHref.match(/^\/([A-Za-z0-9._]{2,30})\/?$/);
          if (shihabUserMatch && !mehediReservedIgPaths.has(shihabUserMatch[1].toLowerCase())) {
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
      mehediIsDownloadGesture =
        shihabKey === "Enter" ||
        shihabKey === " " ||
        mehediForceHit ||
        ((shihabEvent.ctrlKey || shihabEvent.metaKey) &&
          (shihabKey === "s" || shihabKey === "S" || shihabKey === "j" || shihabKey === "J"));
    }

    if (!shihabPreventHit && !mehediForceHit && !mehediIsDownloadGesture) {
      return;
    }

    const mehediClickedHref = mehediIsDownloadGesture ? mehediExtractClickedAnchorHref(shihabEvent) : "";
    const shihabNow = Date.now();
    if (
      shihabNow - mehediLastSentGestureMs < 160 &&
      mehediClickedHref === shihabLastSentGestureHref &&
      shihabPreventHit === mehediLastSentAltState &&
      mehediForceHit === shihabLastSentForceState
    ) {
      return;
    }

    mehediLastSentGestureMs = shihabNow;
    shihabLastSentGestureHref = mehediClickedHref;
    mehediLastSentAltState = shihabPreventHit;
    shihabLastSentForceState = mehediForceHit;

    try {
      chrome.runtime.sendMessage({
        shihabCmd: "mehedi_user_download_gesture",
        shihabPageUrl: location.href,
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
      const mehediAllElems = mehediRoot.querySelectorAll("*");
      for (const shihabEl of mehediAllElems) {
        if (shihabEl.shadowRoot && shihabEl !== mehediShadowHost) {
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
    const shihabExtGuess = shihabEntryObj.shihabExt ||
      (/\.m3u8(\?|$)/i.test(mehediClean) ? "m3u8" : (/\.webm(\?|$)/i.test(mehediClean) ? "webm" : (mehediIsAud ? "m4a" : "mp4")));
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
      let mehediGrpMaxTs = -1;
      let shihabExistsInCache = false;
      for (const mehediRec of shihabTabStreamsCache) {
        if (mehediRec && mehediRec.shihabGroupKey === shihabGrp) {
          shihabExistsInCache = true;
          if ((mehediRec.shihabDetectedAt || 0) > mehediGrpMaxTs) {
            mehediGrpMaxTs = mehediRec.shihabDetectedAt || 0;
          }
        }
      }
      if (shihabExistsInCache && mehediGrpMaxTs >= mehediBestTs) {
        mehediBestTs = mehediGrpMaxTs;
        shihabBestGroup = shihabGrp;
      }
    }
    return shihabBestGroup || mehediCandidateSet.values().next().value || "";
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
  function mehediResolveWinningGroupForVideo(mehediActiveVideo, shihabCtx, mehediIsMultiVideoFeed) {
    // Priority 0: Direct DOM <video> React Fiber / YouTube #movie_player binding on this exact <video> node!
    if (shihabCtx.shihabDirectDomGroupKeys && shihabCtx.shihabDirectDomGroupKeys.size > 0) {
      for (const mehediDirGrp of shihabCtx.shihabDirectDomGroupKeys) {
        if (shihabCtx.shihabExactGroupKeys.has(mehediDirGrp)) {
          return mehediDirGrp;
        }
      }
      const shihabPickedDirect = shihabPickFreshestMatchingGroup(shihabCtx.shihabDirectDomGroupKeys);
      if (shihabPickedDirect) return shihabPickedDirect;
    }

    // Priority 1: Byte-exact FNV-1a ftyp/moof MSE SourceBuffer.appendBuffer match
    if (shihabCtx.shihabExactGroupKeys.size > 0) {
      for (const mehediExGrp of shihabCtx.shihabExactGroupKeys) {
        if (shihabCtx.mehediRouteAndCardGroupKeys.has(mehediExGrp)) {
          return mehediExGrp;
        }
      }
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

    // NEVER guess a random/newest stream group on multi-video feeds (Facebook/Instagram/X/TikTok feeds)!
    if (mehediIsMultiVideoFeed) {
      return "";
    }

    // Priority 5 (Single-video pages ONLY): Aspect/duration-compatible newest group in shihabTabStreamsCache
    let shihabNewestCompatGroup = "";
    let mehediNewestCompatTs = -1;
    for (const shihabRec of shihabTabStreamsCache) {
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

  // Collect streams with Strict Single-Video Isolation so Facebook/Instagram Reels & feeds NEVER mix videos!
  function shihabCollectMediaFromDomAndSniffer(mehediActiveVideo) {
    const mehediCombined = [];
    const shihabAllDomVideos = shihabCollectAllVideosDeep(document, []);
    const mehediIsMultiVideoFeed = shihabAllDomVideos.length > 1;
    const shihabCtx = shihabResolveVideoContext(mehediActiveVideo);

    const mehediIntrinsicRes = (mehediActiveVideo && mehediActiveVideo.videoWidth > 0 && mehediActiveVideo.videoHeight > 0)
      ? mehediHeightToQualityLabel(mehediActiveVideo.videoWidth, mehediActiveVideo.videoHeight)
      : "";

    // 1. Strict Single-Group Isolation across background-sniffed & manifest-extracted streams
    let shihabScopedBgStreams = [];
    const mehediWinningGroupKey = mehediResolveWinningGroupForVideo(mehediActiveVideo, shihabCtx, mehediIsMultiVideoFeed);

    if (mehediWinningGroupKey) {
      shihabScopedBgStreams = shihabTabStreamsCache.filter((mehediItem) => {
        if (!mehediItem || !mehediItem.shihabStreamUrl) return false;
        if (!mehediIsStreamCompatibleWithVideoElement(mehediItem, mehediActiveVideo)) return false;
        if (mehediItem.shihabGroupKey === mehediWinningGroupKey) return true;
        if (!mehediItem.shihabGroupKey && shihabCtx.shihabExactBoundUrls.has(mehediItem.shihabStreamUrl)) return true;
        return false;
      });
    } else if (shihabCtx.shihabBoundUrls.size > 0 || shihabCtx.shihabShortcodes.size > 0) {
      shihabScopedBgStreams = shihabTabStreamsCache.filter((mehediItem) => {
        if (!mehediItem || !mehediItem.shihabStreamUrl) return false;
        if (!mehediIsStreamCompatibleWithVideoElement(mehediItem, mehediActiveVideo)) return false;
        if (shihabCtx.shihabBoundUrls.has(mehediItem.shihabStreamUrl)) return true;
        if (mehediItem.mehediShortcode && shihabCtx.shihabShortcodes.has(mehediItem.mehediShortcode)) return true;
        if (Array.isArray(mehediItem.mehediAltIds) && mehediItem.mehediAltIds.some((shihabId) => shihabCtx.shihabShortcodes.has(String(shihabId)))) {
          return true;
        }
        return false;
      });
    }

    // Only include ungrouped streams on single-video pages when no grouped streams exist in cache
    if (shihabScopedBgStreams.length === 0 && shihabTabStreamsCache.length > 0 && !mehediIsMultiVideoFeed) {
      const shihabAnyGroupedInCache = shihabTabStreamsCache.some((mehediIt) => Boolean(mehediIt && mehediIt.shihabGroupKey));
      if (!shihabAnyGroupedInCache) {
        shihabScopedBgStreams = shihabTabStreamsCache.filter(
          (mehediIt) => mehediIt && !mehediIt.shihabGroupKey && mehediIsStreamCompatibleWithVideoElement(mehediIt, mehediActiveVideo)
        );
      }
    }

    for (const shihabBgItem of shihabScopedBgStreams) {
      shihabPushRichStreamEntry(mehediCombined, shihabBgItem);
    }

    // 2. Direct HTML5 <video src="https://..."> and <source> children on mehediActiveVideo
    if (mehediActiveVideo) {
      const shihabDirectSrc = mehediActiveVideo.currentSrc || mehediActiveVideo.src || "";
      if (shihabDirectSrc.startsWith("http://") || shihabDirectSrc.startsWith("https://")) {
        shihabPushRichStreamEntry(mehediCombined, {
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
        shihabPushRichStreamEntry(mehediCombined, {
          shihabStreamUrl: shihabSrcTag.src || "",
          shihabResolution: shihabParsedRes ? shihabParsedRes.toLowerCase() : "",
          mehediTrackRole: "progressive"
        });
      }
    }

    // 3. Single-video page fallbacks (Performance Resource Timing + OpenGraph video meta)
    if (!mehediIsMultiVideoFeed && mehediCombined.length === 0) {
      try {
        const mehediPerfEntries = performance.getEntriesByType("resource") || [];
        for (let shihabIdx = mehediPerfEntries.length - 1; shihabIdx >= 0 && mehediCombined.length < 8; shihabIdx--) {
          const mehediRes = mehediPerfEntries[shihabIdx];
          const shihabResName = mehediRes && mehediRes.name ? mehediRes.name : "";
          if (/\.(mp4|mkv|webm|mov|m4v|m3u8)(\?|$)/i.test(shihabResName)) {
            const shihabTransferBytes = mehediRes.encodedBodySize > 250000 ? mehediRes.encodedBodySize : -1;
            shihabPushRichStreamEntry(mehediCombined, {
              shihabStreamUrl: shihabResName,
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
        if (/\.(mp4|mkv|webm|mov|m3u8)(\?|$)/i.test(shihabContent)) {
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

  function mehediIsVideoVisibleInViewport(shihabVid) {
    if (!shihabVid || !shihabVid.isConnected) return false;
    const mehediStyle = window.getComputedStyle(shihabVid);
    if (mehediStyle.display === "none" || mehediStyle.visibility === "hidden" || parseFloat(mehediStyle.opacity || "1") < 0.05) {
      return false;
    }
    const shihabRect = shihabVid.getBoundingClientRect();
    if (shihabRect.width < 120 || shihabRect.height < 70) {
      return false;
    }
    if (shihabRect.bottom < 30 || shihabRect.top > window.innerHeight - 30 ||
        shihabRect.right < 30 || shihabRect.left > window.innerWidth - 30) {
      return false;
    }
    return true;
  }

  function mehediFindPrimaryVisibleVideo() {
    if (shihabDropdownOpen && mehediLockedDropdownVideoEl && mehediIsVideoVisibleInViewport(mehediLockedDropdownVideoEl)) {
      return mehediLockedDropdownVideoEl;
    }

    if (mehediPointerInsideBar) {
      const shihabPinnedBarVid = mehediHoveredVideoEl || mehediLastSelectedVideoEl;
      if (shihabPinnedBarVid && mehediIsVideoVisibleInViewport(shihabPinnedBarVid)) {
        return shihabPinnedBarVid;
      }
    }

    if (mehediHoveredVideoEl && Date.now() < shihabHoveredUntilMs && mehediIsVideoVisibleInViewport(mehediHoveredVideoEl)) {
      mehediLastSelectedVideoEl = mehediHoveredVideoEl;
      return mehediHoveredVideoEl;
    }

    const shihabVideos = shihabCollectAllVideosDeep(document, []);
    let mehediPlayingCandidate = null;
    let shihabPlayingArea = 0;
    let mehediLargestCandidate = null;
    let shihabLargestArea = 0;

    for (const mehediVid of shihabVideos) {
      if (!mehediIsVideoVisibleInViewport(mehediVid)) {
        continue;
      }
      const shihabRect = mehediVid.getBoundingClientRect();
      const mehediVisibleTop = Math.max(0, shihabRect.top);
      const shihabVisibleBottom = Math.min(window.innerHeight, shihabRect.bottom);
      const mehediVisibleHeight = Math.max(0, shihabVisibleBottom - mehediVisibleTop);
      const mehediArea = shihabRect.width * mehediVisibleHeight;

      if (!mehediVid.paused && !mehediVid.ended) {
        if (mehediArea > shihabPlayingArea) {
          shihabPlayingArea = mehediArea;
          mehediPlayingCandidate = mehediVid;
        }
      }
      if (mehediArea > shihabLargestArea) {
        shihabLargestArea = mehediArea;
        mehediLargestCandidate = mehediVid;
      }
    }

    const shihabChosen = mehediPlayingCandidate || mehediLargestCandidate;
    if (shihabChosen) {
      mehediLastSelectedVideoEl = shihabChosen;
    }
    return shihabChosen;
  }

  function shihabRefreshBackgroundStreamsThrottled(mehediForceNow) {
    const shihabNow = Date.now();
    if (!mehediForceNow && shihabNow - mehediLastBgSyncMs < 1600) {
      return Promise.resolve(shihabTabStreamsCache);
    }
    mehediLastBgSyncMs = shihabNow;
    try {
      return chrome.runtime.sendMessage({ shihabCmd: "shihab_query_tab_streams" }).then((mehediState) => {
        if (mehediState) {
          if (Array.isArray(mehediState.mehediStreams)) {
            shihabTabStreamsCache = mehediState.mehediStreams;
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
      }).catch(() => shihabTabStreamsCache);
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
        window.postMessage({ __shihabMehediContentPing: true }, "*");
      } catch (_) {}

      await chrome.runtime.sendMessage({
        shihabCmd: "shihab_retrack_active_video",
        shihabKeepUrls: shihabKnownKeepUrls,
        mehediKeepGroupKey: mehediKnownKeepGroup
      }).catch(() => {});

      await new Promise((shihabResolve) => setTimeout(shihabResolve, 240));
      await shihabRefreshBackgroundStreamsThrottled(true);

      if (shihabOpenDropdownAfter) {
        shihabDropdownOpen = true;
      }
      shihabRenderFloatingIdmVideoPanel();
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
    if (document.documentElement) {
      const mehediCurrentOwner = document.documentElement.getAttribute(SHIHAB_DOM_OWNER_ATTR);
      if (mehediCurrentOwner && mehediCurrentOwner !== shihabInstanceToken) {
        mehediDisposeCurrentInstance();
        return false;
      }
      if (!mehediCurrentOwner) {
        document.documentElement.setAttribute(SHIHAB_DOM_OWNER_ATTR, shihabInstanceToken);
      }
    }
    return true;
  }

  // Build Shadow DOM 100% via createElement / textContent (ZERO innerHTML!) so YouTube & Facebook TrustedTypes CSP never blocks it!
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
        "position:fixed !important;z-index:2147483647 !important;pointer-events:auto !important;margin:0 !important;padding:0 !important;border:0 !important;display:none;";

      const shihabShadow = mehediShadowHost.attachShadow({ mode: "open" });

      const mehediStyleEl = document.createElement("style");
      mehediStyleEl.textContent = `
        * { box-sizing: border-box; }
        @keyframes shihabSpin360 {
          from { transform: rotate(0deg); }
          to { transform: rotate(360deg); }
        }
        .shihab-wrap {
          position: relative;
          display: inline-flex;
          flex-direction: column;
          align-items: flex-end;
          font-family: 'Segoe UI', system-ui, -apple-system, BlinkMacSystemFont, sans-serif;
        }
        .shihab-bar {
          display: inline-flex;
          align-items: center;
          background: rgba(18, 20, 26, 0.86);
          border: 1px solid rgba(255, 255, 255, 0.14);
          border-radius: 999px;
          padding: 4px 8px 4px 11px;
          color: #f8fafc;
          font-size: 12px;
          font-weight: 600;
          line-height: 1.2;
          box-shadow: 0 4px 16px rgba(0, 0, 0, 0.48);
          backdrop-filter: blur(10px);
          -webkit-backdrop-filter: blur(10px);
          opacity: 0.84;
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

      shihabWrap.appendChild(mehediBar);
      shihabWrap.appendChild(mehediToastEl);
      shihabWrap.appendChild(shihabMenuEl);

      shihabShadow.appendChild(mehediStyleEl);
      shihabShadow.appendChild(shihabWrap);

      shihabRefs = {
        mehediBar,
        mehediCountPill,
        mehediRetrackBtn,
        shihabCloseBtn,
        mehediToastEl,
        shihabMenuEl
      };

      mehediShadowHost.addEventListener("mouseenter", () => {
        mehediPointerInsideBar = true;
        if (mehediHoveredVideoEl || mehediLastSelectedVideoEl) {
          shihabHoveredUntilMs = Date.now() + 8000;
        }
      });

      mehediShadowHost.addEventListener("mouseleave", () => {
        mehediPointerInsideBar = false;
        if (mehediHoveredVideoEl) {
          shihabHoveredUntilMs = Date.now() + 2500;
        }
      });

      shihabPrimaryBtn.addEventListener("click", async (mehediEv) => {
        mehediEv.preventDefault();
        mehediEv.stopPropagation();
        if (shihabDropdownOpen) {
          shihabDropdownOpen = false;
          mehediLockedDropdownVideoEl = null;
          shihabRenderFloatingIdmVideoPanel();
          return;
        }
        const shihabActiveVid = mehediFindPrimaryVisibleVideo();
        mehediLockedDropdownVideoEl = shihabActiveVid;
        if (shihabActiveVid) {
          mehediEnsureVideoUidAndInspect(shihabActiveVid, true);
        }
        await shihabRefreshBackgroundStreamsThrottled(true);
        shihabDropdownOpen = true;
        shihabRenderFloatingIdmVideoPanel();
      });

      mehediRetrackBtn.addEventListener("click", async (shihabRetrackEv) => {
        shihabRetrackEv.preventDefault();
        shihabRetrackEv.stopPropagation();
        await mehediTriggerActiveVideoRetrack(true);
      });

      shihabCloseBtn.addEventListener("click", (mehediEv) => {
        mehediEv.preventDefault();
        mehediEv.stopPropagation();
        shihabPanelDismissedUrl = location.href;
        shihabDropdownOpen = false;
        mehediLockedDropdownVideoEl = null;
        mehediShadowHost.style.display = "none";
      });
    }

    shihabSweepForeignHostElements(mehediShadowHost);
    if (mehediShadowHost.parentNode !== mehediDesiredParent) {
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
    if (shihabExt === "m3u8" || mehediUrl.includes(".m3u8")) return { tag: "HLS", sub: "m3u8" };
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

  function shihabRenderFloatingIdmVideoPanel() {
    if (!shihabVerifyActiveSingletonOwnership()) return;

    if (location.href !== mehediLastObservedPageHref) {
      mehediLastObservedPageHref = location.href;
      shihabDropdownOpen = false;
      const shihabNavYtId = shihabExtractCurrentPageYouTubeVideoId();
      if (shihabNavYtId) {
        mehediRequestYouTubeResolutionForVideoId(shihabNavYtId, "", "", false);
      }
    }

    // Prevent tiny ad/tracker iframes or background subframes from spawning duplicate icons when top frame is active
    if (window !== window.top) {
      if (window.innerWidth < 220 || window.innerHeight < 130) {
        if (mehediShadowHost) mehediShadowHost.style.display = "none";
        return;
      }
      if (Date.now() < shihabTopFrameActiveUntilMs && Date.now() >= shihabHoveredUntilMs && !shihabDropdownOpen) {
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

    const shihabTargetVideo = mehediFindPrimaryVisibleVideo();
    if (!shihabTargetVideo) {
      if (mehediShadowHost) mehediShadowHost.style.display = "none";
      return;
    }

    // Coordinate with child iframes so at most ONE download icon is shown on the tab
    if (window === window.top && Date.now() - mehediLastTopFrameNotifyMs > 800) {
      mehediLastTopFrameNotifyMs = Date.now();
      try {
        for (let shihabFi = 0; shihabFi < window.frames.length; shihabFi++) {
          try {
            window.frames[shihabFi].postMessage({ __shihabMehediTopPanelActive: true }, "*");
          } catch (_) {}
        }
      } catch (_) {}
    }

    const mehediHost = shihabEnsureShadowHostMounted();
    if (!mehediHost || !shihabRefs) return;

    const shihabStreams = shihabCollectMediaFromDomAndSniffer(shihabTargetVideo);
    const mehediSnifferRows = shihabBuildStreamSnifferEntries(shihabStreams);
    shihabRefs.mehediCountPill.textContent = String(mehediSnifferRows.length);

    shihabRefs.mehediBar.classList.toggle("mehedi-open", shihabDropdownOpen);

    const mehediRect = shihabTargetVideo.getBoundingClientRect();
    const shihabViewportW = document.documentElement.clientWidth || window.innerWidth;
    const mehediViewportH = document.documentElement.clientHeight || window.innerHeight;
    const SHIHAB_OUTSIDE_GAP = 5;
    const MEHEDI_VIEWPORT_PAD = 4;

    if (mehediHost.style.display !== "block") {
      mehediHost.style.display = "block";
    }

    const mehediBarBox = shihabRefs.mehediBar.getBoundingClientRect();
    const mehediBarH = Math.max(26, Math.ceil(mehediBarBox.height) || 28);
    if (!shihabRefs.mehediBar.classList.contains("shihab-compact") && mehediBarBox.width > 80) {
      shihabLastMeasuredFullBarW = Math.ceil(mehediBarBox.width);
    }
    const shihabFullBarW = shihabLastMeasuredFullBarW;

    const mehediSpaceAbove = mehediRect.top;
    const shihabSpaceRight = shihabViewportW - mehediRect.right;

    let shihabTopPx = MEHEDI_VIEWPORT_PAD;
    let mehediLeftCss = "auto";
    let shihabRightCss = MEHEDI_VIEWPORT_PAD + "px";
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
      const mehediClampedRightX = Math.min(shihabViewportW - MEHEDI_VIEWPORT_PAD, Math.max(shihabMinRightX, mehediDesiredRightX));
      shihabRightCss = Math.max(MEHEDI_VIEWPORT_PAD, shihabViewportW - mehediClampedRightX) + "px";
      mehediLeftCss = "auto";
    } else if (shihabSpaceRight >= (shihabUseCompact ? 58 : shihabFullBarW) + SHIHAB_OUTSIDE_GAP + MEHEDI_VIEWPORT_PAD) {
      // 2) Outside TO THE RIGHT of the video's top-right corner (e.g. Instagram Reels, Facebook Reels, YouTube Shorts, TikTok)
      shihabTopPx = Math.round(
        Math.min(
          Math.max(MEHEDI_VIEWPORT_PAD, mehediRect.top),
          Math.max(MEHEDI_VIEWPORT_PAD, mehediRect.bottom - mehediBarH - MEHEDI_VIEWPORT_PAD)
        )
      );
      mehediLeftCss = Math.round(mehediRect.right + SHIHAB_OUTSIDE_GAP) + "px";
      shihabRightCss = "auto";
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
      const shihabDesiredRightX = Math.min(
        shihabViewportW - MEHEDI_VIEWPORT_PAD,
        Math.round(mehediRect.right + SHIHAB_OUTSIDE_GAP + mehediCompactW)
      );
      shihabRightCss = Math.max(MEHEDI_VIEWPORT_PAD, shihabViewportW - shihabDesiredRightX) + "px";
      mehediLeftCss = "auto";
    } else {
      // 4) True 100vw x 100vh fullscreen or zero-margin edge-to-edge player: compact pill tucked at extreme top-right corner
      shihabUseCompact = true;
      shihabTopPx = mehediSpaceAbove >= 14
        ? 2
        : Math.max(MEHEDI_VIEWPORT_PAD, Math.round(mehediRect.top + MEHEDI_VIEWPORT_PAD));
      shihabRightCss = MEHEDI_VIEWPORT_PAD + "px";
      mehediLeftCss = "auto";
    }

    shihabRefs.mehediBar.classList.toggle("shihab-compact", shihabUseCompact);
    if (mehediHost.style.top !== shihabTopPx + "px") {
      mehediHost.style.top = shihabTopPx + "px";
    }
    if (mehediHost.style.left !== mehediLeftCss) {
      mehediHost.style.left = mehediLeftCss;
    }
    if (mehediHost.style.right !== shihabRightCss) {
      mehediHost.style.right = shihabRightCss;
    }

    const shihabMenuEl = shihabRefs.shihabMenuEl;
    const mehediToastEl = shihabRefs.mehediToastEl;
    const mehediPlacedLeft = mehediLeftCss !== "auto" ? parseInt(mehediLeftCss, 10) : -1;
    const shihabAlignPopupLeft = mehediPlacedLeft >= 0 && (mehediPlacedLeft + 370 <= shihabViewportW - 8);
    shihabMenuEl.style.left = shihabAlignPopupLeft ? "0px" : "auto";
    shihabMenuEl.style.right = shihabAlignPopupLeft ? "auto" : "0px";
    mehediToastEl.style.left = shihabAlignPopupLeft ? "0px" : "auto";
    mehediToastEl.style.right = shihabAlignPopupLeft ? "auto" : "0px";

    const mehediOpenUpward = (mehediViewportH - (shihabTopPx + mehediBarH) < 240) && shihabTopPx > 260;
    shihabMenuEl.style.top = mehediOpenUpward ? "auto" : "calc(100% + 7px)";
    shihabMenuEl.style.bottom = mehediOpenUpward ? "calc(100% + 7px)" : "auto";

    shihabMenuEl.style.display = shihabDropdownOpen ? "block" : "none";
    if (shihabDropdownOpen) {
      shihabClearElementChildren(shihabMenuEl);
      const mehediLiveCaption = shihabExtractBestVideoTitle(shihabTargetVideo, shihabStreams[0]);

      const shihabHeaderDiv = document.createElement("div");
      shihabHeaderDiv.className = "shihab-menu-head";
      shihabHeaderDiv.title = mehediLiveCaption || "MHS Stream Sniffer";

      const mehediHeadLeft = document.createElement("span");
      mehediHeadLeft.textContent = "MHS STREAM SNIFFER";

      const shihabHeadRight = document.createElement("div");
      shihabHeadRight.className = "mehedi-head-right";
      const mehediQualTxt = document.createElement("span");
      mehediQualTxt.textContent = "SELECT QUALITY";
      shihabHeadRight.appendChild(mehediQualTxt);
      shihabHeadRight.appendChild(shihabRefs.mehediRetrackBtn);
      shihabHeadRight.appendChild(shihabRefs.shihabCloseBtn);

      shihabHeaderDiv.appendChild(mehediHeadLeft);
      shihabHeaderDiv.appendChild(shihabHeadRight);
      shihabMenuEl.appendChild(shihabHeaderDiv);

      if (mehediSnifferRows.length === 0) {
        const mehediEmptyRow = document.createElement("div");
        mehediEmptyRow.className = "shihab-item";
        const shihabEmptySpan = document.createElement("span");
        shihabEmptySpan.className = "mehedi-name";
        const shihabIsDrmStream = Boolean(
          shihabPageHasDrmProtectedMedia || (shihabTargetVideo && shihabTargetVideo.mediaKeys)
        );
        shihabEmptySpan.textContent = shihabIsDrmStream
          ? (mehediDetectedDrmKeySystem
              ? `Protected (DRM — Encrypted Media Stream: ${mehediDetectedDrmKeySystem})`
              : "Protected (DRM — Encrypted Media Stream)")
          : "No direct HTTP stream exposed — click ↻ to re-track";
        mehediEmptyRow.appendChild(shihabEmptySpan);
        mehediEmptyRow.addEventListener("click", async (shihabEv) => {
          shihabEv.preventDefault();
          shihabEv.stopPropagation();
          await mehediTriggerActiveVideoRetrack(true);
        });
        shihabMenuEl.appendChild(mehediEmptyRow);
      } else {
        for (const shihabEntry of mehediSnifferRows) {
          const shihabRow = document.createElement("div");
          shihabRow.className = "shihab-item";

          const mehediLeftGrp = document.createElement("div");
          mehediLeftGrp.className = "mehedi-left-group";

          const shihabKindPill = document.createElement("span");
          shihabKindPill.className = "shihab-kind-pill " + (shihabEntry.mehediIsAudio ? "mehedi-kind-audio" : "mehedi-kind-video");
          shihabKindPill.textContent = shihabEntry.shihabBadge;

          const mehediNameSpan = document.createElement("span");
          mehediNameSpan.className = "mehedi-name";
          mehediNameSpan.textContent = shihabEntry.shihabLabel;

          const mehediStreamObj = shihabEntry.mehediStream;
          const shihabBytes = mehediStreamObj.mehediDisplaySizeBytes > 0
            ? mehediStreamObj.mehediDisplaySizeBytes
            : mehediStreamObj.shihabSizeBytes;
          const mehediSizeHint = shihabFormatBytesHuman(shihabBytes);
          mehediNameSpan.title = mehediLiveCaption
            ? (mehediSizeHint ? `${mehediLiveCaption} (${mehediSizeHint})` : mehediLiveCaption)
            : shihabEntry.shihabLabel;

          mehediLeftGrp.appendChild(shihabKindPill);
          mehediLeftGrp.appendChild(mehediNameSpan);

          const shihabDlSpan = document.createElement("span");
          shihabDlSpan.className = "shihab-dl-action";
          shihabDlSpan.textContent = "Download";

          shihabRow.appendChild(mehediLeftGrp);
          shihabRow.appendChild(shihabDlSpan);

          shihabRow.addEventListener("click", (mehediClickEv) => {
            mehediClickEv.preventDefault();
            mehediClickEv.stopPropagation();
            shihabDropdownOpen = false;
            mehediLockedDropdownVideoEl = null;
            mehediTriggerStreamDownload(mehediStreamObj, shihabTargetVideo);
            shihabRenderFloatingIdmVideoPanel();
          });
          shihabMenuEl.appendChild(shihabRow);
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
    else if (mehediRawExt === "mp3") shihabExt = ".mp3";
    else if (mehediRawExt === "m4a" || shihabIsAud) shihabExt = ".m4a";

    const mehediFinalFileName = mehediQualTag
      ? `${mehediCleanStem} [${mehediQualTag}]${shihabExt}`
      : `${mehediCleanStem}${shihabExt}`;

    try {
      chrome.runtime.sendMessage({
        shihabCmd: "mehedi_send_download_now",
        shihabUrl: shihabStreamEntry.shihabStreamUrl,
        mehediReferrer: shihabStreamEntry.mehediCapturedReferer || location.href,
        shihabFileName: mehediFinalFileName,
        shihabFileSize: shihabStreamEntry.shihabSizeBytes || -1,
        mehediTabTitle: shihabRealTitle || document.title || "",
        shihabOrigin: shihabStreamEntry.shihabOrigin || location.origin || "",
        mehediOmitCookies: shihabStreamEntry.mehediOmitCookies,
        mehediUserAgent: shihabStreamEntry.mehediCustomUserAgent || "",
        mehediAudioCompanionUrl: shihabStreamEntry.mehediAudioCompanionUrl || "",
        shihabAudioCompanionBytes: shihabStreamEntry.shihabAudioCompanionBytes || -1,
        shihabQualityLabel: mehediQualTag
      }).then((shihabAck) => {
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
    const mehediJobId = shihabJob.jobId;
    const shihabTargetUrl = shihabJob.url || "";
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

  function mehediOnRuntimeMessage(shihabMsg, _mehediSender, shihabReply) {
    if (shihabMsg && shihabMsg.shihabCmd === "mehedi_streams_updated") {
      shihabTabStreamsCache = Array.isArray(shihabMsg.mehediStreams) ? shihabMsg.mehediStreams : [];
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
      shihabRenderFloatingIdmVideoPanel();
      shihabReply({ ok: true });
      return false;
    }

    if (shihabMsg && shihabMsg.shihabCmd === "mehedi_trigger_page_retrack") {
      try {
        window.postMessage({ __shihabMehediContentPing: true }, "*");
      } catch (_) {}
      const shihabRetrackYtId = shihabExtractCurrentPageYouTubeVideoId();
      if (shihabRetrackYtId) {
        mehediRequestYouTubeResolutionForVideoId(shihabRetrackYtId, "", "", true);
      }
      shihabPanelDismissedUrl = "";
      shihabRenderFloatingIdmVideoPanel();
      shihabReply({ ok: true });
      return false;
    }

    if (shihabMsg && shihabMsg.shihabCmd === "mehedi_exec_tab_relay_job") {
      if (window !== window.top) {
        return false;
      }
      mehediExecuteTabRelayJobInPageContext(shihabMsg.mehediJob)
        .then(() => shihabReply({ ok: true }))
        .catch(() => shihabReply({ ok: false }));
      return true;
    }

    if (shihabMsg && shihabMsg.shihabCmd === "mehedi_get_selected_video_streams") {
      if (window !== window.top) {
        return false;
      }
      const mehediSelVid =
        mehediFindPrimaryVisibleVideo() ||
        (mehediLastSelectedVideoEl && mehediLastSelectedVideoEl.isConnected ? mehediLastSelectedVideoEl : null);
      if (mehediSelVid) {
        mehediEnsureVideoUidAndInspect(mehediSelVid, true);
      }
      shihabRefreshBackgroundStreamsThrottled(true).then(() => {
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

  function mehediTrackPointerOverVideo(shihabPointerEv) {
    if (mehediInstanceDisposed) return;
    const mehediPath = typeof shihabPointerEv.composedPath === "function" ? shihabPointerEv.composedPath() : [shihabPointerEv.target];
    if (
      mehediShadowHost &&
      (shihabPointerEv.target === mehediShadowHost ||
       mehediPath.includes(mehediShadowHost) ||
       mehediShadowHost.contains(shihabPointerEv.target))
    ) {
      mehediPointerInsideBar = true;
      shihabHoveredUntilMs = Date.now() + 8000;
      mehediNotifyTopIfSubframeHovered();
      return;
    }
    for (const shihabNode of mehediPath) {
      if (shihabNode && shihabNode.tagName === "VIDEO") {
        const mehediChanged = mehediHoveredVideoEl !== shihabNode;
        mehediHoveredVideoEl = shihabNode;
        mehediLastSelectedVideoEl = shihabNode;
        shihabHoveredUntilMs = Date.now() + 6000;
        mehediNotifyTopIfSubframeHovered();
        mehediEnsureVideoUidAndInspect(shihabNode, false);
        if (mehediChanged && !mehediLockedDropdownVideoEl) {
          shihabDropdownOpen = false;
        }
        shihabRefreshBackgroundStreamsThrottled(false);
        shihabRenderFloatingIdmVideoPanel();
        return;
      }
    }
    const mehediX = shihabPointerEv.clientX;
    const shihabY = shihabPointerEv.clientY;
    if (typeof mehediX === "number" && typeof shihabY === "number") {
      const mehediVideos = shihabCollectAllVideosDeep(document, []);
      for (const shihabVid of mehediVideos) {
        if (!mehediIsVideoVisibleInViewport(shihabVid)) continue;
        const mehediRect = shihabVid.getBoundingClientRect();
        if (mehediX >= mehediRect.left && mehediX <= mehediRect.right &&
            shihabY >= mehediRect.top && shihabY <= mehediRect.bottom) {
          const mehediSwitched = mehediHoveredVideoEl !== shihabVid;
          mehediHoveredVideoEl = shihabVid;
          mehediLastSelectedVideoEl = shihabVid;
          shihabHoveredUntilMs = Date.now() + 6000;
          mehediNotifyTopIfSubframeHovered();
          mehediEnsureVideoUidAndInspect(shihabVid, false);
          if (mehediSwitched && !mehediLockedDropdownVideoEl) {
            shihabDropdownOpen = false;
          }
          shihabRefreshBackgroundStreamsThrottled(false);
          shihabRenderFloatingIdmVideoPanel();
          return;
        }
      }
    }
  }

  function mehediMediaLifecycleListener(shihabEv) {
    if (mehediInstanceDisposed) return;
    if (shihabEv.target && shihabEv.target.tagName === "VIDEO") {
      mehediEnsureVideoUidAndInspect(shihabEv.target, false);
      if (shihabEv.type === "play" || shihabEv.type === "playing") {
        mehediLastSelectedVideoEl = shihabEv.target;
      }
      shihabRefreshBackgroundStreamsThrottled(false);
      shihabRenderFloatingIdmVideoPanel();
    }
  }

  function mehediCloseDropdownOnOutsideEvent(shihabEv) {
    if (!shihabDropdownOpen || mehediInstanceDisposed) return;
    if (shihabEv.type === "keydown") {
      if (shihabEv.key === "Escape") {
        shihabDropdownOpen = false;
        mehediLockedDropdownVideoEl = null;
        shihabRenderFloatingIdmVideoPanel();
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
    shihabDropdownOpen = false;
    mehediLockedDropdownVideoEl = null;
    shihabRenderFloatingIdmVideoPanel();
  }

  function mehediOnSupersedeEvent(shihabEv) {
    const mehediIncomingOwner = shihabEv && shihabEv.detail ? shihabEv.detail.shihabOwnerToken : "";
    if (mehediIncomingOwner && mehediIncomingOwner !== shihabInstanceToken) {
      mehediDisposeCurrentInstance();
    }
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
    if (mehediPeriodicTimerId) {
      clearInterval(mehediPeriodicTimerId);
      mehediPeriodicTimerId = null;
    }
    if (shihabToastClearTimer) {
      clearTimeout(shihabToastClearTimer);
      shihabToastClearTimer = null;
    }
    if (shihabDomObserver) {
      shihabDomObserver.disconnect();
      shihabDomObserver = null;
    }
    document.removeEventListener(MEHEDI_SUPERSEDE_EVENT, mehediOnSupersedeEvent);
    window.removeEventListener("message", mehediOnPageHookWindowMessage, false);
    for (const shihabGestureEvt of ["pointerdown", "mousedown", "click", "auxclick", "contextmenu", "keydown"]) {
      window.removeEventListener(shihabGestureEvt, mehediReportTrustedUserGesture, true);
    }
    window.removeEventListener("pointerdown", mehediCloseDropdownOnOutsideEvent, true);
    window.removeEventListener("keydown", mehediCloseDropdownOnOutsideEvent, true);
    window.removeEventListener("pointerover", mehediTrackPointerOverVideo, { capture: true });
    window.removeEventListener("pointermove", mehediTrackPointerOverVideo, { capture: true });
    window.removeEventListener("scroll", shihabRenderFloatingIdmVideoPanel, { capture: true });
    window.removeEventListener("resize", shihabRenderFloatingIdmVideoPanel);
    document.removeEventListener("fullscreenchange", shihabRenderFloatingIdmVideoPanel);
    for (const mehediMediaEvt of ["play", "playing", "loadedmetadata", "loadeddata", "canplay"]) {
      document.removeEventListener(mehediMediaEvt, mehediMediaLifecycleListener, true);
    }
    try {
      chrome.runtime.onMessage.removeListener(mehediOnRuntimeMessage);
    } catch (shihabRemErr) {
      // Context invalidated
    }
    if (mehediShadowHost) {
      try { mehediShadowHost.remove(); } catch (_) {}
      mehediShadowHost = null;
    }
    shihabRefs = null;
  }

  document.addEventListener(MEHEDI_SUPERSEDE_EVENT, mehediOnSupersedeEvent);

  // Register IPC, trusted user-gesture & MAIN-world postMessage listeners IMMEDIATELY at document_start so zero early clicks or manifests are missed!
  window.addEventListener("message", mehediOnPageHookWindowMessage, false);
  for (const shihabGestureEvt of ["pointerdown", "mousedown", "click", "auxclick", "contextmenu", "keydown"]) {
    window.addEventListener(shihabGestureEvt, mehediReportTrustedUserGesture, { capture: true, passive: true });
  }
  try {
    chrome.runtime.onMessage.addListener(mehediOnRuntimeMessage);
  } catch (shihabInitErr) {
    // Ignore invalidated context
  }

  function mehediInitDomObservers() {
    if (!shihabVerifyActiveSingletonOwnership()) return;

    try {
      window.postMessage({ __shihabMehediContentPing: true }, "*");
    } catch (_) {}

    const shihabInitialYtId = shihabExtractCurrentPageYouTubeVideoId();
    if (shihabInitialYtId) {
      mehediRequestYouTubeResolutionForVideoId(shihabInitialYtId, "", "", false);
    }

    shihabRefreshBackgroundStreamsThrottled(true).then(() => {
      shihabRenderFloatingIdmVideoPanel();
    });

    shihabDomObserver = new MutationObserver((mehediRecords) => {
      if (mehediInstanceDisposed) return;
      if (
        mehediShadowHost &&
        Array.isArray(mehediRecords) &&
        mehediRecords.length > 0 &&
        mehediRecords.every((shihabRec) => shihabRec.target === mehediShadowHost || mehediShadowHost.contains(shihabRec.target))
      ) {
        return;
      }
      shihabRenderFloatingIdmVideoPanel();
    });
    if (document.documentElement) {
      shihabDomObserver.observe(document.documentElement, {
        childList: true,
        subtree: true,
        attributes: true,
        attributeFilter: ["src", "style", "class"]
      });
    }

    for (const mehediMediaEvt of ["play", "playing", "loadedmetadata", "loadeddata", "canplay"]) {
      document.addEventListener(mehediMediaEvt, mehediMediaLifecycleListener, true);
    }

    window.addEventListener("pointerdown", mehediCloseDropdownOnOutsideEvent, { capture: true, passive: true });
    window.addEventListener("keydown", mehediCloseDropdownOnOutsideEvent, { capture: true, passive: true });
    window.addEventListener("pointerover", mehediTrackPointerOverVideo, { capture: true, passive: true });
    window.addEventListener("pointermove", mehediTrackPointerOverVideo, { capture: true, passive: true });
    window.addEventListener("scroll", shihabRenderFloatingIdmVideoPanel, { capture: true, passive: true });
    window.addEventListener("resize", shihabRenderFloatingIdmVideoPanel, { passive: true });
    document.addEventListener("fullscreenchange", shihabRenderFloatingIdmVideoPanel, { passive: true });

    mehediPeriodicTimerId = setInterval(() => {
      if (!shihabVerifyActiveSingletonOwnership()) return;
      if (!document.hidden) {
        shihabRenderFloatingIdmVideoPanel();
      }
    }, 900);
  }

  window.__shihabMehediIdmCleanupPrev = mehediDisposeCurrentInstance;

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", mehediInitDomObservers, { once: true });
  } else {
    mehediInitDomObservers();
  }
})();
