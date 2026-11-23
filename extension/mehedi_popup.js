"use strict";

function shihabFormatPopupBytes(mehediVal) {
  if (!mehediVal || mehediVal <= 0) return "";
  if (mehediVal < 1024) return mehediVal + " B";
  if (mehediVal < 1048576) return (mehediVal / 1024).toFixed(0) + " KB";
  if (mehediVal < 1073741824) return (mehediVal / 1048576).toFixed(1) + " MB";
  return (mehediVal / 1073741824).toFixed(2) + " GB";
}

document.addEventListener("DOMContentLoaded", async () => {
  const shihabDot = document.getElementById("shihabStatusDot");
  const mehediSub = document.getElementById("mehediConnectionSub");
  const shihabToggleBtn = document.getElementById("shihabToggleCaptureBtn");
  const mehediUrlInput = document.getElementById("shihabQuickUrlInput");
  const shihabSendBtn = document.getElementById("mehediSendUrlBtn");
  const mehediCountBadge = document.getElementById("shihabStreamCountBadge");
  const shihabRetrackBtn = document.getElementById("mehediRetrackTabBtn");
  const shihabStreamsBox = document.getElementById("mehediStreamsContainer");
  const mehediGrabAllBtn = document.getElementById("shihabGrabAllLinksBtn");
  const shihabOpenAppBtn = document.getElementById("shihabOpenDesktopAppBtn");
  const shihabUpdateBanner = document.getElementById("shihabUpdateBanner");
  const shihabUpdateBannerText = document.getElementById("shihabUpdateBannerText");
  const shihabUpdateBannerBtn = document.getElementById("shihabUpdateBannerBtn");
  const shihabServerSelect = document.getElementById("shihabServerSelect");
  const shihabUnpairBtn = document.getElementById("shihabUnpairServerBtn");
  const shihabTogglePairBtn = document.getElementById("shihabTogglePairBtn");
  const shihabPairBox = document.getElementById("shihabPairRemoteBox");
  const shihabRemoteUrlInp = document.getElementById("shihabRemoteUrlInput");
  const shihabRemoteCodeInp = document.getElementById("shihabRemoteCodeInput");
  const shihabPairSubmitBtn = document.getElementById("shihabPairSubmitBtn");
  const shihabPairStatusMsg = document.getElementById("shihabPairStatusMsg");
  const shihabHideFullscreenChk = document.getElementById("shihabHideFullscreenChk");
  let mehediLatestReleaseUrl = "";

  function shihabSendRuntimeMessageBounded(shihabPayload, mehediTimeoutMs) {
    const shihabLimit = Math.max(800, Number(mehediTimeoutMs || 3500));
    return new Promise((shihabResolve) => {
      let shihabDone = false;
      const shihabTimer = setTimeout(() => {
        if (!shihabDone) {
          shihabDone = true;
          shihabResolve({ __shihabTimedOut: true });
        }
      }, shihabLimit);
      try {
        chrome.runtime.sendMessage(shihabPayload).then((shihabResp) => {
          if (!shihabDone) {
            shihabDone = true;
            clearTimeout(shihabTimer);
            shihabResolve(shihabResp || null);
          }
        }).catch(() => {
          if (!shihabDone) {
            shihabDone = true;
            clearTimeout(shihabTimer);
            shihabResolve({ __shihabDisconnected: true });
          }
        });
      } catch (_) {
        if (!shihabDone) {
          shihabDone = true;
          clearTimeout(shihabTimer);
          shihabResolve({ __shihabDisconnected: true });
        }
      }
    });
  }

  if (shihabHideFullscreenChk) {
    chrome.storage.local.get(["shihabHideVideoInFullscreen"], (st) => {
      shihabHideFullscreenChk.checked = Boolean(st && st.shihabHideVideoInFullscreen);
    });
    shihabHideFullscreenChk.addEventListener("change", () => {
      chrome.storage.local.set({ shihabHideVideoInFullscreen: Boolean(shihabHideFullscreenChk.checked) }).catch(() => {});
    });
  }

  async function mehediRefreshServersDropdown() {
    if (!shihabServerSelect) return;
    try {
      const srvState = await shihabSendRuntimeMessageBounded({ shihabCmd: "shihab_get_servers_state" }, 2000);
      if (!srvState || !srvState.ok) return;
      shihabServerSelect.textContent = "";
      const localOpt = document.createElement("option");
      localOpt.value = "local";
      localOpt.textContent = "This computer (127.0.0.1)";
      shihabServerSelect.appendChild(localOpt);
      for (const s of (srvState.servers || [])) {
        const opt = document.createElement("option");
        opt.value = s.id;
        opt.textContent = `${s.name} (${s.baseUrl})`;
        shihabServerSelect.appendChild(opt);
      }
      shihabServerSelect.value = srvState.activeServerId || "local";
      if (shihabUnpairBtn) {
        shihabUnpairBtn.style.display = shihabServerSelect.value !== "local" ? "inline-block" : "none";
      }
    } catch (_) {}
  }

  if (shihabServerSelect) {
    shihabServerSelect.addEventListener("change", async () => {
      const chosen = shihabServerSelect.value || "local";
      await shihabSendRuntimeMessageBounded({ shihabCmd: "shihab_set_active_server", serverId: chosen }, 2000);
      if (shihabUnpairBtn) shihabUnpairBtn.style.display = chosen !== "local" ? "inline-block" : "none";
    });
  }
  if (shihabUnpairBtn) {
    shihabUnpairBtn.addEventListener("click", async () => {
      const chosen = shihabServerSelect ? shihabServerSelect.value : "local";
      if (chosen && chosen !== "local") {
        await shihabSendRuntimeMessageBounded({ shihabCmd: "shihab_unpair_remote_server", serverId: chosen }, 2000);
        await mehediRefreshServersDropdown();
      }
    });
  }
  if (shihabTogglePairBtn && shihabPairBox) {
    shihabTogglePairBtn.addEventListener("click", () => {
      shihabPairBox.style.display = shihabPairBox.style.display === "none" ? "flex" : "none";
    });
  }
  if (shihabPairSubmitBtn) {
    shihabPairSubmitBtn.addEventListener("click", async () => {
      if (shihabPairStatusMsg) shihabPairStatusMsg.textContent = "Pairing...";
      const res = await shihabSendRuntimeMessageBounded({
        shihabCmd: "shihab_pair_remote_server",
        baseUrl: shihabRemoteUrlInp ? shihabRemoteUrlInp.value : "",
        code: shihabRemoteCodeInp ? shihabRemoteCodeInp.value : ""
      }, 7000);
      if (res && res.ok) {
        if (shihabPairStatusMsg) shihabPairStatusMsg.textContent = "Paired & selected!";
        if (shihabRemoteCodeInp) shihabRemoteCodeInp.value = "";
        await mehediRefreshServersDropdown();
        setTimeout(() => { if (shihabPairBox) shihabPairBox.style.display = "none"; }, 900);
      } else if (shihabPairStatusMsg) {
        shihabPairStatusMsg.textContent = (res && res.error) || "Pairing failed.";
      }
    });
  }
  mehediRefreshServersDropdown();

  let shihabTabs = [];
  try {
    shihabTabs = await chrome.tabs.query({ active: true, currentWindow: true });
  } catch (_) {}
  const mehediActiveTab = shihabTabs && shihabTabs[0] ? shihabTabs[0] : null;
  const shihabActiveTabId = mehediActiveTab ? mehediActiveTab.id : -1;

  async function mehediResolveIsolatedSelectedStreams(shihabFallbackStreams) {
    if (shihabActiveTabId >= 0) {
      try {
        const mehediDomSel = await Promise.race([
          chrome.tabs.sendMessage(
            shihabActiveTabId,
            {
              shihabCmd: "mehedi_get_selected_video_streams",
              shihabPreloadedStreams: Array.isArray(shihabFallbackStreams) ? shihabFallbackStreams : []
            },
            { frameId: 0 }
          ),
          new Promise((resolve) => setTimeout(() => resolve(null), 1200))
        ]);
        if (mehediDomSel && mehediDomSel.ok && Array.isArray(mehediDomSel.mehediStreams) && mehediDomSel.mehediStreams.length > 0) {
          return mehediDomSel.mehediStreams;
        }
      } catch (shihabFrameErr) {
        // Fallback when top frame has no content script (e.g. direct media tab or embedded cross-origin player)
      }
    }

    const mehediAll = Array.isArray(shihabFallbackStreams) ? shihabFallbackStreams : [];
    if (mehediAll.length <= 1) return mehediAll;

    // Isolate strictly to the single freshest groupKey so unrelated videos on the page NEVER mix!
    let shihabNewestGrp = "";
    let mehediNewestTs = -1;
    for (const shihabIt of mehediAll) {
      if (shihabIt && shihabIt.shihabGroupKey && (shihabIt.shihabDetectedAt || 0) > mehediNewestTs) {
        mehediNewestTs = shihabIt.shihabDetectedAt || 0;
        shihabNewestGrp = shihabIt.shihabGroupKey;
      }
    }
    if (shihabNewestGrp) {
      return mehediAll.filter((mehediIt) => mehediIt && mehediIt.shihabGroupKey === shihabNewestGrp);
    }
    return mehediAll.slice(0, 6);
  }

  function mehediRenderUpdateBanner(shihabRes) {
    if (!shihabUpdateBanner) return;
    const bridgeUpd = shihabRes && shihabRes.shihabBridgeInfo && shihabRes.shihabBridgeInfo.updateAvailable
      ? shihabRes.shihabBridgeInfo
      : null;
    const webUpd = shihabRes && shihabRes.shihabUpdateNotice && shihabRes.shihabUpdateNotice.updateAvailable
      ? shihabRes.shihabUpdateNotice
      : null;
    const picked = bridgeUpd || webUpd;
    if (picked) {
      const latestVer = picked.latestVersion || "";
      mehediLatestReleaseUrl = picked.latestHtmlUrl || "https://github.com/sshihabb007/MHS-DOWNLOAD-MANAGER/releases";
      shihabUpdateBannerText.textContent = latestVer
        ? `New GitHub Release Ready: v${latestVer}`
        : "New GitHub Release Available!";
      shihabUpdateBanner.style.display = "flex";
    } else {
      mehediLatestReleaseUrl = "";
      shihabUpdateBanner.style.display = "none";
    }
  }

  async function mehediRefreshPopupState() {
    const shihabRes = await shihabSendRuntimeMessageBounded({
      shihabCmd: "shihab_query_tab_streams",
      mehediTabId: shihabActiveTabId,
      shihabForceBridgeSync: true
    }, 3500);
    mehediRenderUpdateBanner(shihabRes);

    if (!shihabRes || shihabRes.__shihabDisconnected || shihabRes.__shihabTimedOut || !shihabRes.mehediOnline) {
      shihabDot.classList.remove("online");
      mehediSub.textContent = (shihabRes && (shihabRes.__shihabDisconnected || shihabRes.__shihabTimedOut))
        ? "Worker busy — Reload extension or launch mdm.exe"
        : "Offline — Launch mdm.exe v1.2.0";
      shihabToggleBtn.textContent = "Offline";
      return;
    }

    const mehediBridgeVer = (shihabRes.shihabBridgeInfo && shihabRes.shihabBridgeInfo.version) || "1.2.0";
    shihabDot.classList.add("online");
    mehediSub.textContent = shihabRes.shihabEnabled
      ? `Connected · mdm.exe v${mehediBridgeVer}`
      : "Integration Paused";
    shihabToggleBtn.textContent = shihabRes.shihabEnabled ? "Enabled" : "Paused";

    const mehediStreams = await mehediResolveIsolatedSelectedStreams(shihabRes.mehediStreams);
    mehediCountBadge.textContent = String(mehediStreams.length);
    if (mehediStreams.length === 0) {
      shihabStreamsBox.textContent = "";
      const mehediEmptyDiv = document.createElement("div");
      mehediEmptyDiv.className = "shihab-empty";
      mehediEmptyDiv.textContent = "Hover or play the video you want on this tab to capture it with its exact name.";
      shihabStreamsBox.appendChild(mehediEmptyDiv);
      return;
    }
    if (mehediStreams.length > 0) {
      shihabStreamsBox.textContent = "";
      for (const shihabStream of mehediStreams) {
        const mehediDiv = document.createElement("div");
        mehediDiv.className = "shihab-stream-row";

        const shihabIsAudio = Boolean(shihabStream.shihabIsAudioOnly || shihabStream.mehediMediaKind === "AUDIO");
        const shihabRealFileName =
          shihabStream.mehediResolvedFileName ||
          shihabStream.shihabDisplayName ||
          "Video.mp4";
        const mehediCleanTitle =
          shihabStream.shihabDisplayName ||
          shihabRealFileName.replace(/\s*\[[^\]]+\](\.[a-z0-9]{2,5})?$/i, "").replace(/\.[a-z0-9]{2,5}$/i, "");

        const shihabDisplayBytes = shihabStream.mehediDisplaySizeBytes > 0
          ? shihabStream.mehediDisplaySizeBytes
          : shihabStream.shihabSizeBytes;
        const mehediSizeStr = shihabFormatPopupBytes(shihabDisplayBytes);

        const mehediLeftWrap = document.createElement("div");
        mehediLeftWrap.className = "mehedi-stream-left";

        const shihabKindBadge = document.createElement("span");
        shihabKindBadge.className = "shihab-kind-badge " + (shihabIsAudio ? "mehedi-kind-audio" : "mehedi-kind-video");
        shihabKindBadge.textContent = shihabIsAudio ? "AUDIO" : "VIDEO";

        const shihabNameSpan = document.createElement("span");
        shihabNameSpan.className = "mehedi-stream-name";
        shihabNameSpan.title = shihabRealFileName;
        shihabNameSpan.textContent = mehediCleanTitle;

        mehediLeftWrap.appendChild(shihabKindBadge);
        mehediLeftWrap.appendChild(shihabNameSpan);

        const shihabRightWrap = document.createElement("div");
        shihabRightWrap.className = "shihab-stream-right";

        const mehediTagSpan = document.createElement("span");
        mehediTagSpan.className = "shihab-stream-tag" + (shihabIsAudio ? " mehedi-tag-audio" : "");
        mehediTagSpan.textContent = shihabStream.mehediBadge || (shihabIsAudio ? "M4A Audio" : "MP4 HD");

        const shihabSizeSpan = document.createElement("span");
        shihabSizeSpan.className = "mehedi-stream-size";
        shihabSizeSpan.textContent = mehediSizeStr || (shihabStream.shihabExt === "m3u8" ? "HLS" : "Ready");

        shihabRightWrap.appendChild(mehediTagSpan);
        shihabRightWrap.appendChild(shihabSizeSpan);

        mehediDiv.appendChild(mehediLeftWrap);
        mehediDiv.appendChild(shihabRightWrap);

        mehediDiv.addEventListener("click", async () => {
          await chrome.runtime.sendMessage({
            shihabCmd: "mehedi_send_download_now",
            shihabUrl: shihabStream.shihabStreamUrl,
            shihabFileName: shihabRealFileName,
            mehediReferrer: shihabStream.mehediCapturedReferer || (mehediActiveTab ? mehediActiveTab.url : ""),
            shihabFileSize: shihabStream.shihabSizeBytes || -1,
            mehediTabTitle: mehediCleanTitle,
            shihabOrigin: shihabStream.shihabOrigin || "",
            mehediOmitCookies: shihabStream.mehediOmitCookies,
            mehediUserAgent: shihabStream.mehediCustomUserAgent || "",
            mehediAudioCompanionUrl: shihabStream.mehediAudioCompanionUrl || "",
            shihabAudioCompanionBytes: shihabStream.shihabAudioCompanionBytes || -1,
            shihabQualityLabel: shihabStream.shihabQualityLabel || shihabStream.shihabResolution || (shihabIsAudio ? "Audio" : ""),
            shihabTabId: shihabActiveTabId
          });
          window.close();
        });
        shihabStreamsBox.appendChild(mehediDiv);
      }
    }
  }

  shihabToggleBtn.addEventListener("click", async () => {
    await shihabSendRuntimeMessageBounded({ shihabCmd: "shihab_toggle_integration" }, 3000);
    await mehediRefreshPopupState();
  });

  shihabSendBtn.addEventListener("click", async () => {
    const mehediVal = (mehediUrlInput.value || "").trim();
    if (
      !mehediVal.startsWith("http://") &&
      !mehediVal.startsWith("https://") &&
      !mehediVal.startsWith("ftp://") &&
      !mehediVal.startsWith("ftps://") &&
      !mehediVal.startsWith("magnet:?")
    ) {
      return;
    }
    await shihabSendRuntimeMessageBounded({
      shihabCmd: "mehedi_send_download_now",
      shihabUrl: mehediVal,
      mehediReferrer: mehediActiveTab ? mehediActiveTab.url : "",
      mehediTabTitle: mehediActiveTab ? mehediActiveTab.title : "",
      shihabIncognito: Boolean(mehediActiveTab && mehediActiveTab.incognito),
      shihabTabId: shihabActiveTabId
    }, 5500);
    window.close();
  });

  if (shihabOpenAppBtn) {
    shihabOpenAppBtn.addEventListener("click", async () => {
      await shihabSendRuntimeMessageBounded({ shihabCmd: "shihab_show_desktop_app" }, 5500);
      window.close();
    });
  }

  if (shihabUpdateBannerBtn) {
    shihabUpdateBannerBtn.addEventListener("click", async () => {
      try {
        await shihabSendRuntimeMessageBounded({
          shihabCmd: "shihab_open_updater",
          latestHtmlUrl: mehediLatestReleaseUrl || ""
        }, 5500);
      } catch (_) {
        if (mehediLatestReleaseUrl && /^https?:\/\//i.test(mehediLatestReleaseUrl)) {
          chrome.tabs.create({ url: mehediLatestReleaseUrl }).catch(() => {});
        }
      }
      window.close();
    });
  }

  if (shihabRetrackBtn) {
    shihabRetrackBtn.addEventListener("click", async () => {
      if (shihabActiveTabId < 0) return;
      shihabRetrackBtn.classList.add("mehedi-spin");
      try {
        await shihabSendRuntimeMessageBounded({
          shihabCmd: "shihab_retrack_active_video",
          mehediTabId: shihabActiveTabId
        }, 3500);
        await new Promise((shihabResolve) => setTimeout(shihabResolve, 320));
        await mehediRefreshPopupState();
      } finally {
        setTimeout(() => shihabRetrackBtn.classList.remove("mehedi-spin"), 360);
      }
    });
  }

  mehediGrabAllBtn.addEventListener("click", async () => {
    if (shihabActiveTabId >= 0) {
      await shihabSendRuntimeMessageBounded({
        shihabCmd: "mehedi_grab_tab_links",
        mehediTabId: shihabActiveTabId
      }, 5500);
      window.close();
    }
  });

  await mehediRefreshPopupState();
});
