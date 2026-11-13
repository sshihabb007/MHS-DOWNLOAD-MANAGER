using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ShihabMehediIDM
{
    public partial class ShihabOptionsConfigForm : Form
    {
        // Tab 5: Connection
        private ComboBox _mehediConnTypeCombo;
        private NumericUpDown _mehediMaxConnNum;
        private CheckBox _shihabUnlock128Chk;
        private CheckBox _shihabShedSlowChk;
        private CheckBox _shihabOverlapChk;
        private CheckBox _shihabSparseChk;
        private NumericUpDown _shihabRetrySecNum;
        private NumericUpDown _shihabMaxRetNum;
        private CheckBox _shihabGlobalLimChk;
        private NumericUpDown _shihabGlobSpdNum;
        private TextBox _shihabUaBox;
        private CheckBox _shihabQuotaChk;
        private NumericUpDown _shihabQuotaMbNum;
        private NumericUpDown _shihabQuotaHrsNum;

        // Tab 6: Proxy / Socks / DNS
        private CheckBox _shihabUseProxyChk;
        private TextBox _shihabPrxHostBox;
        private NumericUpDown _shihabPrxPortNum;
        private TextBox _shihabPrxUserBox;
        private TextBox _shihabPrxPassBox;
        private CheckBox _mehediUseSocksChk;
        private TextBox _mehediSocksHostBox;
        private NumericUpDown _mehediSocksPortNum;
        private TextBox _shihabPacUrlBox;
        private ComboBox _shihabDnsModeCombo;
        private ComboBox _shihabDohProvCombo;
        private TextBox _shihabCustomDohBox;
        private TextBox _shihabPlainDnsBox;
        private TextBox _shihabNextDnsBox;

        // Tab 7 & 10 refresh hook
        private Action _refreshShihabLoginsCallback;

        // Tab 8: Dial Up / VPN
        private TextBox _shihabDialConnBox;
        private CheckBox _mehediRedialChk;
        private CheckBox _shihabHangUpChk;
        private ComboBox _shihabBindAdapterCombo;
        private CheckBox _shihabNetwatchChk;

        // Tab 9: Sounds
        private CheckBox _mehediSndCompChk;
        private TextBox _shihabSndCompBox;
        private CheckBox _shihabSndErrChk;
        private TextBox _mehediSndErrBox;

        // Tab 11: Updates & P2P
        private CheckBox _shihabAutoUpdChk;
        private CheckBox _shihabPreRelChk;
        private CheckBox _shihabTorSeqDefChk;
        private CheckBox _shihabTorSeedDefChk;
        private TextBox _shihabBlkUrlBox;
        private CheckBox _shihabCrashMonChk;

        private void BuildConnectionTab(TabPage mehediConnTab, bool shihabDark)
        {
            AddShihabSectionBanner(mehediConnTab, "Connection type and Dynamic Segmentation (1..128)", shihabDark);

            Label shihabConnTypeLbl = new Label { Text = "Connection Type / Speed:", Location = new Point(18, 42), AutoSize = true };
            _mehediConnTypeCombo = new ComboBox
            {
                Location = new Point(185, 39),
                Width = 420,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = _surfColor,
                ForeColor = ForeColor
            };
            _mehediConnTypeCombo.Items.Add("High speed: Direct connection (Ethernet/Cable) / Wi-Fi / Mobile 4G/5G");
            _mehediConnTypeCombo.Items.Add("Medium speed: DSL / Cable 2M / Mobile 3G");
            _mehediConnTypeCombo.Items.Add("Low speed / Metered connection");
            int shihabConnProfIdx = _mehediConnTypeCombo.FindStringExact(_shihabConfig.MehediConnectionProfile ?? "");
            _mehediConnTypeCombo.SelectedIndex = shihabConnProfIdx >= 0 ? shihabConnProfIdx : 0;

            Label shihabMaxConnLbl = new Label { Text = "Default max. conn. number (Dynamic Segmentation 1..128):", Location = new Point(18, 76), AutoSize = true };
            _mehediMaxConnNum = new NumericUpDown { Location = new Point(495, 73), Width = 95, Minimum = 1, Maximum = 128, Value = Math.Max(1, Math.Min(128, _shihabConfig.ShihabDefaultMaxConnections)) };

            _shihabUnlock128Chk = new CheckBox { Text = "Unlock >32 connections per file (up to 128)", Location = new Point(18, 104), AutoSize = true, Checked = _shihabConfig.ShihabAllowMoreThan32Connections };
            _shihabShedSlowChk = new CheckBox { Text = "Shed slow connections (< 1/4 peak)", Location = new Point(310, 104), AutoSize = true, Checked = _shihabConfig.ShihabShedSlowConnections };
            _shihabOverlapChk = new CheckBox { Text = "Verify 64-byte resume overlap", Location = new Point(18, 130), AutoSize = true, Checked = _shihabConfig.ShihabVerifyResumeOverlap };
            _shihabSparseChk = new CheckBox { Text = "Use NTFS Sparse files", Location = new Point(310, 130), AutoSize = true, Checked = _shihabConfig.ShihabEnableSparseFiles };

            Label mehediRetrySecLbl = new Label { Text = "Pause between auto-retry attempts (seconds):", Location = new Point(18, 160), AutoSize = true };
            _shihabRetrySecNum = new NumericUpDown { Location = new Point(335, 157), Width = 80, Minimum = 1, Maximum = 3600, Value = Math.Max(1, Math.Min(3600, _shihabConfig.ShihabAutoRetryIntervalSec > 0 ? _shihabConfig.ShihabAutoRetryIntervalSec : 10)) };
            Label mehediMaxRetLbl = new Label { Text = "Max retries:", Location = new Point(430, 160), AutoSize = true };
            _shihabMaxRetNum = new NumericUpDown { Location = new Point(510, 157), Width = 80, Minimum = 0, Maximum = 999, Value = Math.Max(0, Math.Min(999, _shihabConfig.MehediMaxRetryAttempts > 0 ? _shihabConfig.MehediMaxRetryAttempts : 99)) };

            _shihabGlobalLimChk = new CheckBox { Text = "Enable Global Speed Limiter across all active downloads", Location = new Point(18, 194), AutoSize = true, Checked = _shihabConfig.MehediGlobalSpeedLimiterEnabled };
            Label mehediGlobSpdLbl = new Label { Text = "Maximum global download transfer rate (KBytes/sec):", Location = new Point(18, 222), AutoSize = true };
            _shihabGlobSpdNum = new NumericUpDown { Location = new Point(365, 219), Width = 115, Minimum = 16, Maximum = 2000000, Value = Math.Max(16, _shihabConfig.ShihabGlobalSpeedLimitKb) };

            Label mehediUaLbl = new Label { Text = "User-Agent for manually added downloads:", Location = new Point(18, 254), AutoSize = true };
            _shihabUaBox = new TextBox { Location = new Point(18, 276), Width = 585, Text = _shihabConfig.ShihabDefaultUserAgent, BackColor = _surfColor, ForeColor = ForeColor };

            GroupBox shihabQuotaGrp = new GroupBox
            {
                Text = "Rolling Download Quota & Per-Host Connection Limits",
                Location = new Point(18, 310),
                Size = new Size(585, 86),
                ForeColor = ForeColor
            };
            _shihabQuotaChk = new CheckBox { Text = "Enable Rolling Quota (MB):", Location = new Point(14, 24), AutoSize = true, Checked = _shihabConfig.ShihabQuotaEnabled };
            _shihabQuotaMbNum = new NumericUpDown { Location = new Point(205, 21), Width = 95, Minimum = 10, Maximum = 10000000, Value = Math.Max(10, _shihabConfig.ShihabQuotaLimitMegabytes) };
            Label shihabQuotaWinLbl = new Label { Text = "Window (hours):", Location = new Point(312, 24), AutoSize = true };
            _shihabQuotaHrsNum = new NumericUpDown { Location = new Point(415, 21), Width = 65, Minimum = 1, Maximum = 720, Value = Math.Max(1, Math.Min(720, _shihabConfig.ShihabQuotaWindowHours)) };
            Label shihabQuotaUsedLbl = new Label
            {
                Text = "Used in window: " + ShihabFormatHelpers.FormatMehediBytes(_shihabConfig.GetCurrentQuotaWindowBytes()),
                Location = new Point(14, 54),
                AutoSize = true,
                ForeColor = _mutedColor
            };
            Button shihabHostLimBtn = ShihabThemePalette.CreateMehediStyledButton("Per-Host Limits...", _surfColor, ForeColor, 145);
            shihabHostLimBtn.Height = 25;
            shihabHostLimBtn.Location = new Point(425, 50);
            shihabHostLimBtn.Click += delegate { ShowShihabHostLimitsModal(shihabDark); };
            shihabQuotaGrp.Controls.AddRange(new Control[] { _shihabQuotaChk, _shihabQuotaMbNum, shihabQuotaWinLbl, _shihabQuotaHrsNum, shihabQuotaUsedLbl, shihabHostLimBtn });

            mehediConnTab.Controls.AddRange(new Control[]
            {
                shihabConnTypeLbl, _mehediConnTypeCombo, shihabMaxConnLbl, _mehediMaxConnNum,
                _shihabUnlock128Chk, _shihabShedSlowChk, _shihabOverlapChk, _shihabSparseChk,
                mehediRetrySecLbl, _shihabRetrySecNum, mehediMaxRetLbl, _shihabMaxRetNum,
                _shihabGlobalLimChk, mehediGlobSpdLbl, _shihabGlobSpdNum, mehediUaLbl, _shihabUaBox,
                shihabQuotaGrp
            });
        }

        private void BuildProxyTab(TabPage shihabProxyTab, bool shihabDark)
        {
            AddShihabSectionBanner(shihabProxyTab, "Proxy and SOCKS configuration", shihabDark);

            GroupBox mehediHttpPrxGrp = new GroupBox
            {
                Text = "HTTP / HTTPS / FTP Proxy",
                Location = new Point(16, 42),
                Size = new Size(592, 138),
                ForeColor = ForeColor
            };
            _shihabUseProxyChk = new CheckBox { Text = "Use HTTP / HTTPS Proxy server", Location = new Point(14, 24), AutoSize = true, Checked = _shihabConfig.MehediUseHttpProxy };
            Label mehediPrxHostLbl = new Label { Text = "Proxy server address:", Location = new Point(14, 60), AutoSize = true };
            _shihabPrxHostBox = new TextBox { Location = new Point(155, 57), Width = 265, Text = _shihabConfig.ShihabProxyAddress ?? "", BackColor = _surfColor, ForeColor = ForeColor };
            Label mehediPrxPortLbl = new Label { Text = "Port:", Location = new Point(435, 60), AutoSize = true };
            _shihabPrxPortNum = new NumericUpDown { Location = new Point(478, 57), Width = 95, Minimum = 1, Maximum = 65535, Value = Math.Max(1, Math.Min(65535, _shihabConfig.MehediProxyPort > 0 ? _shihabConfig.MehediProxyPort : 8080)) };
            Label mehediPrxUserLbl = new Label { Text = "User name:", Location = new Point(14, 98), AutoSize = true };
            _shihabPrxUserBox = new TextBox { Location = new Point(155, 95), Width = 175, Text = _shihabConfig.ShihabProxyUser ?? "", BackColor = _surfColor, ForeColor = ForeColor };
            Label mehediPrxPassLbl = new Label { Text = "Password:", Location = new Point(345, 98), AutoSize = true };
            _shihabPrxPassBox = new TextBox { Location = new Point(418, 95), Width = 155, UseSystemPasswordChar = true, Text = _shihabConfig.MehediProxyPassword ?? "", BackColor = _surfColor, ForeColor = ForeColor };
            mehediHttpPrxGrp.Controls.AddRange(new Control[] { _shihabUseProxyChk, mehediPrxHostLbl, _shihabPrxHostBox, mehediPrxPortLbl, _shihabPrxPortNum, mehediPrxUserLbl, _shihabPrxUserBox, mehediPrxPassLbl, _shihabPrxPassBox });

            GroupBox shihabSocksGrp = new GroupBox
            {
                Text = "SOCKS Proxy",
                Location = new Point(16, 194),
                Size = new Size(592, 105),
                ForeColor = ForeColor
            };
            _mehediUseSocksChk = new CheckBox { Text = "Use SOCKS4 / SOCKS5 proxy server", Location = new Point(14, 24), AutoSize = true, Checked = _shihabConfig.ShihabUseSocksProxy };
            Label shihabSocksHostLbl = new Label { Text = "SOCKS server address:", Location = new Point(14, 62), AutoSize = true };
            _mehediSocksHostBox = new TextBox { Location = new Point(155, 59), Width = 265, Text = _shihabConfig.MehediSocksHost ?? "", BackColor = _surfColor, ForeColor = ForeColor };
            Label shihabSocksPortLbl = new Label { Text = "Port:", Location = new Point(435, 62), AutoSize = true };
            _mehediSocksPortNum = new NumericUpDown { Location = new Point(478, 59), Width = 95, Minimum = 1, Maximum = 65535, Value = Math.Max(1, Math.Min(65535, _shihabConfig.ShihabSocksPort > 0 ? _shihabConfig.ShihabSocksPort : 1080)) };
            shihabSocksGrp.Controls.AddRange(new Control[] { _mehediUseSocksChk, shihabSocksHostLbl, _mehediSocksHostBox, shihabSocksPortLbl, _mehediSocksPortNum });

            GroupBox shihabPacDnsGrp = new GroupBox
            {
                Text = "PAC Auto-Config Script & DNS Mode (System / Plain UDP / Encrypted DoH)",
                Location = new Point(16, 302),
                Size = new Size(592, 112),
                ForeColor = ForeColor
            };
            Label shihabPacUrlLbl = new Label { Text = "PAC Script URL (.pac):", Location = new Point(14, 22), AutoSize = true };
            _shihabPacUrlBox = new TextBox { Location = new Point(155, 19), Width = 418, Text = _shihabConfig.ShihabPacScriptUrl ?? "", BackColor = _surfColor, ForeColor = ForeColor };

            Label shihabDnsModeLbl = new Label { Text = "DNS Mode:", Location = new Point(14, 52), AutoSize = true };
            _shihabDnsModeCombo = new ComboBox
            {
                Location = new Point(92, 49),
                Width = 122,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = _surfColor,
                ForeColor = ForeColor
            };
            _shihabDnsModeCombo.Items.AddRange(new object[] { "System", "PlainDNS", "EncryptedDoH" });
            int dnsModeIdx = _shihabDnsModeCombo.FindStringExact(_shihabConfig.ShihabDnsMode ?? "System");
            _shihabDnsModeCombo.SelectedIndex = dnsModeIdx >= 0 ? dnsModeIdx : 0;

            _shihabDohProvCombo = new ComboBox
            {
                Location = new Point(222, 49),
                Width = 115,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = _surfColor,
                ForeColor = ForeColor
            };
            _shihabDohProvCombo.Items.AddRange(new object[] { "Cloudflare", "Google", "Quad9", "AdGuard", "NextDNS", "Custom" });
            int dohIdx = _shihabDohProvCombo.FindStringExact(_shihabConfig.ShihabDnsProvider ?? "Cloudflare");
            _shihabDohProvCombo.SelectedIndex = dohIdx >= 0 ? dohIdx : 0;
            _shihabCustomDohBox = new TextBox { Location = new Point(345, 49), Width = 228, Text = _shihabConfig.ShihabCustomDohUrl ?? "", BackColor = _surfColor, ForeColor = ForeColor };

            Label shihabPlainDnsLbl = new Label { Text = "Plain DNS IP:", Location = new Point(14, 82), AutoSize = true };
            _shihabPlainDnsBox = new TextBox { Location = new Point(98, 79), Width = 145, Text = _shihabConfig.ShihabPlainDnsServer ?? "1.1.1.1", BackColor = _surfColor, ForeColor = ForeColor };
            Label shihabNextDnsLbl = new Label { Text = "NextDNS ID:", Location = new Point(256, 82), AutoSize = true };
            _shihabNextDnsBox = new TextBox { Location = new Point(335, 79), Width = 120, Text = _shihabConfig.ShihabNextDnsConfigId ?? "", BackColor = _surfColor, ForeColor = ForeColor };

            shihabPacDnsGrp.Controls.AddRange(new Control[]
            {
                shihabPacUrlLbl, _shihabPacUrlBox, shihabDnsModeLbl, _shihabDnsModeCombo,
                _shihabDohProvCombo, _shihabCustomDohBox, shihabPlainDnsLbl, _shihabPlainDnsBox,
                shihabNextDnsLbl, _shihabNextDnsBox
            });

            shihabProxyTab.Controls.AddRange(new Control[] { mehediHttpPrxGrp, shihabSocksGrp, shihabPacDnsGrp });
        }

        private void BuildLoginsTab(TabPage mehediLoginsTab, bool shihabDark)
        {
            AddShihabSectionBanner(mehediLoginsTab, "Passwords for authenticated HTTP/HTTPS/FTP servers", shihabDark);

            ShihabModernListView mehediLoginsList = new ShihabModernListView
            {
                Location = new Point(18, 44),
                Size = new Size(588, 285),
                BorderStyle = BorderStyle.FixedSingle,
                MehediDarkMode = shihabDark,
                ShihabShowFileTypeBadges = false,
                BackColor = _surfColor,
                ForeColor = ForeColor
            };
            mehediLoginsList.Columns.Add("Server / Path", 280);
            mehediLoginsList.Columns.Add("User", 180);
            mehediLoginsList.Columns.Add("Password", 105);

            _refreshShihabLoginsCallback = delegate
            {
                mehediLoginsList.BeginUpdate();
                mehediLoginsList.Items.Clear();
                for (int shihabI = 0; shihabI < _shihabConfig.ShihabSiteLogins.Count; shihabI++)
                {
                    ShihabSiteLoginCredential mehediC = _shihabConfig.ShihabSiteLogins[shihabI];
                    mehediLoginsList.Items.Add(new ListViewItem(new string[] { mehediC.ShihabServerPattern, mehediC.MehediUserName, "******" }));
                }
                mehediLoginsList.EndUpdate();
            };
            _refreshShihabLoginsCallback();

            TextBox shihabNewSrv = new TextBox { Location = new Point(18, 344), Width = 212, Text = "example.com/members", BackColor = _surfColor, ForeColor = ForeColor };
            TextBox mehediNewUsr = new TextBox { Location = new Point(238, 344), Width = 125, Text = "shihab", BackColor = _surfColor, ForeColor = ForeColor };
            TextBox shihabNewPwd = new TextBox { Location = new Point(370, 344), Width = 110, UseSystemPasswordChar = true, BackColor = _surfColor, ForeColor = ForeColor };
            Button mehediAddLoginBtn = ShihabThemePalette.CreateMehediStyledButton("New", ShihabThemePalette.GetShihabAccentBlue(), Color.White, 56);
            mehediAddLoginBtn.Location = new Point(488, 340);
            mehediAddLoginBtn.Click += delegate
            {
                if (!string.IsNullOrWhiteSpace(shihabNewSrv.Text) && !string.IsNullOrWhiteSpace(mehediNewUsr.Text))
                {
                    _shihabConfig.ShihabSiteLogins.Add(new ShihabSiteLoginCredential
                    {
                        ShihabServerPattern = shihabNewSrv.Text.Trim(),
                        MehediUserName = mehediNewUsr.Text.Trim(),
                        ShihabPassword = shihabNewPwd.Text
                    });
                    _refreshShihabLoginsCallback();
                }
            };
            Button shihabDelLoginBtn = ShihabThemePalette.CreateMehediStyledButton("Del", ShihabThemePalette.GetMehediAccentRed(), Color.White, 56);
            shihabDelLoginBtn.Location = new Point(550, 340);
            shihabDelLoginBtn.Click += delegate
            {
                if (mehediLoginsList.SelectedIndices.Count > 0)
                {
                    _shihabConfig.ShihabSiteLogins.RemoveAt(mehediLoginsList.SelectedIndices[0]);
                    _refreshShihabLoginsCallback();
                }
            };
            mehediLoginsTab.Controls.AddRange(new Control[] { mehediLoginsList, shihabNewSrv, mehediNewUsr, shihabNewPwd, mehediAddLoginBtn, shihabDelLoginBtn });
        }

        private void BuildDialUpVpnTab(TabPage shihabDialTab, bool shihabDark)
        {
            AddShihabSectionBanner(shihabDialTab, "Dial-Up Networking and VPN connection monitor", shihabDark);

            Label mehediDialConnLbl = new Label { Text = "Connection / VPN adapter profile:", Location = new Point(18, 52), AutoSize = true };
            _shihabDialConnBox = new TextBox
            {
                Location = new Point(235, 49),
                Width = 365,
                Text = string.IsNullOrWhiteSpace(_shihabConfig.MehediDialUpConnectionName) ? "Default System Connection" : _shihabConfig.MehediDialUpConnectionName,
                BackColor = _surfColor,
                ForeColor = ForeColor
            };
            _mehediRedialChk = new CheckBox
            {
                Text = "Automatically resume/reconnect queued transfers when network is restored",
                Location = new Point(18, 96),
                AutoSize = true,
                Checked = _shihabConfig.ShihabRedialIfDisconnected
            };
            _shihabHangUpChk = new CheckBox
            {
                Text = "Disconnect Dial-Up / VPN connection when scheduled queue finishes",
                Location = new Point(18, 128),
                AutoSize = true,
                Checked = _shihabConfig.MehediHangUpWhenDone
            };
            Label mehediDialNote = new Label
            {
                Text = "Note: Per-queue start/stop times and exit-on-complete actions can also be configured in the Scheduler window.",
                Location = new Point(18, 164),
                Size = new Size(580, 38),
                ForeColor = _mutedColor
            };

            GroupBox shihabVpnBindGrp = new GroupBox
            {
                Text = "VPN Kill-Switch (Bind Transfers to Specific Network Adapter / Interface)",
                Location = new Point(18, 210),
                Size = new Size(584, 118),
                ForeColor = ForeColor
            };
            Label shihabBindLbl = new Label { Text = "Bind outbound sockets to adapter (fail-closed if adapter drops):", Location = new Point(14, 26), AutoSize = true };
            _shihabBindAdapterCombo = new ComboBox
            {
                Location = new Point(14, 52),
                Width = 550,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = _surfColor,
                ForeColor = ForeColor
            };
            _shihabBindAdapterCombo.Items.Add("(Any / System Default Routing)");
            if (!string.IsNullOrWhiteSpace(_shihabConfig.ShihabBoundNetworkAdapter))
            {
                _shihabBindAdapterCombo.Items.Add(_shihabConfig.ShihabBoundNetworkAdapter);
                _shihabBindAdapterCombo.SelectedIndex = 1;
            }
            else
            {
                _shihabBindAdapterCombo.SelectedIndex = 0;
            }

            Task.Run(delegate
            {
                List<ShihabNetworkAdapterChoice> liveAdapters = ShihabDnsAndNetworkManager.GetAvailableNetworkAdapters();
                if (IsDisposed || !IsHandleCreated) return;
                try
                {
                    BeginInvoke((Action)delegate
                    {
                        if (IsDisposed || _shihabBindAdapterCombo.IsDisposed) return;
                        _shihabBindAdapterCombo.BeginUpdate();
                        _shihabBindAdapterCombo.Items.Clear();
                        _shihabBindAdapterCombo.Items.Add("(Any / System Default Routing)");
                        int selAdapterIdx = 0;
                        for (int ai = 0; ai < liveAdapters.Count; ai++)
                        {
                            if (string.IsNullOrEmpty(liveAdapters[ai].AdapterId)) continue;
                            string label = liveAdapters[ai].ToString();
                            _shihabBindAdapterCombo.Items.Add(label);
                            if (!string.IsNullOrWhiteSpace(_shihabConfig.ShihabBoundNetworkAdapter) &&
                                label.StartsWith(_shihabConfig.ShihabBoundNetworkAdapter, StringComparison.OrdinalIgnoreCase))
                            {
                                selAdapterIdx = _shihabBindAdapterCombo.Items.Count - 1;
                            }
                        }
                        if (selAdapterIdx == 0 && !string.IsNullOrWhiteSpace(_shihabConfig.ShihabBoundNetworkAdapter))
                        {
                            _shihabBindAdapterCombo.Items.Add(_shihabConfig.ShihabBoundNetworkAdapter + " (Offline)");
                            selAdapterIdx = _shihabBindAdapterCombo.Items.Count - 1;
                        }
                        _shihabBindAdapterCombo.SelectedIndex = selAdapterIdx;
                        _shihabBindAdapterCombo.EndUpdate();
                    });
                }
                catch { }
            });

            _shihabNetwatchChk = new CheckBox
            {
                Text = "Auto-resume stalled downloads when network connectivity returns (netwatch)",
                Location = new Point(14, 86),
                AutoSize = true,
                Checked = _shihabConfig.ShihabAutoResumeOnNetworkRestore
            };
            shihabVpnBindGrp.Controls.AddRange(new Control[] { shihabBindLbl, _shihabBindAdapterCombo, _shihabNetwatchChk });

            shihabDialTab.Controls.AddRange(new Control[] { mehediDialConnLbl, _shihabDialConnBox, _mehediRedialChk, _shihabHangUpChk, mehediDialNote, shihabVpnBindGrp });
        }

        private void BuildSoundsTab(TabPage mehediSoundsTab, bool shihabDark)
        {
            AddShihabSectionBanner(mehediSoundsTab, "Audio notifications for download events", shihabDark);

            GroupBox shihabSndCompGrp = new GroupBox
            {
                Text = "Download complete",
                Location = new Point(16, 46),
                Size = new Size(592, 96),
                ForeColor = ForeColor
            };
            _mehediSndCompChk = new CheckBox { Text = "Play sound when a download finishes (leave blank for default system chime):", Location = new Point(14, 24), AutoSize = true, Checked = _shihabConfig.MehediPlaySoundComplete };
            _shihabSndCompBox = new TextBox { Location = new Point(14, 54), Width = 385, Text = _shihabConfig.ShihabSoundCompleteWav ?? "", BackColor = _surfColor, ForeColor = ForeColor };
            Button mehediSndCompBrowse = ShihabThemePalette.CreateMehediStyledButton("Browse", _surfColor, ForeColor, 88);
            mehediSndCompBrowse.Height = 25;
            mehediSndCompBrowse.Location = new Point(408, 52);
            mehediSndCompBrowse.Click += delegate
            {
                using (OpenFileDialog shihabOfd = new OpenFileDialog { Filter = "Wave Audio (*.wav)|*.wav" })
                {
                    if (shihabOfd.ShowDialog(this) == DialogResult.OK) _shihabSndCompBox.Text = shihabOfd.FileName;
                }
            };
            Button shihabSndCompPlay = ShihabThemePalette.CreateMehediStyledButton("Play ▶", ShihabThemePalette.GetShihabAccentBlue(), Color.White, 74);
            shihabSndCompPlay.Height = 25;
            shihabSndCompPlay.Location = new Point(504, 52);
            shihabSndCompPlay.Click += delegate { PreviewShihabSoundFile(_shihabSndCompBox.Text, false); };
            shihabSndCompGrp.Controls.AddRange(new Control[] { _mehediSndCompChk, _shihabSndCompBox, mehediSndCompBrowse, shihabSndCompPlay });

            GroupBox mehediSndErrGrp = new GroupBox
            {
                Text = "Download failed / error",
                Location = new Point(16, 158),
                Size = new Size(592, 96),
                ForeColor = ForeColor
            };
            _shihabSndErrChk = new CheckBox { Text = "Play sound when a download encounters an error:", Location = new Point(14, 24), AutoSize = true, Checked = _shihabConfig.MehediPlaySoundError };
            _mehediSndErrBox = new TextBox { Location = new Point(14, 54), Width = 385, Text = _shihabConfig.ShihabSoundErrorWav ?? "", BackColor = _surfColor, ForeColor = ForeColor };
            Button shihabSndErrBrowse = ShihabThemePalette.CreateMehediStyledButton("Browse", _surfColor, ForeColor, 88);
            shihabSndErrBrowse.Height = 25;
            shihabSndErrBrowse.Location = new Point(408, 52);
            shihabSndErrBrowse.Click += delegate
            {
                using (OpenFileDialog mehediOfd = new OpenFileDialog { Filter = "Wave Audio (*.wav)|*.wav" })
                {
                    if (mehediOfd.ShowDialog(this) == DialogResult.OK) _mehediSndErrBox.Text = mehediOfd.FileName;
                }
            };
            Button mehediSndErrPlay = ShihabThemePalette.CreateMehediStyledButton("Play ▶", ShihabThemePalette.GetShihabAccentBlue(), Color.White, 74);
            mehediSndErrPlay.Height = 25;
            mehediSndErrPlay.Location = new Point(504, 52);
            mehediSndErrPlay.Click += delegate { PreviewShihabSoundFile(_mehediSndErrBox.Text, true); };
            mehediSndErrGrp.Controls.AddRange(new Control[] { _shihabSndErrChk, _mehediSndErrBox, shihabSndErrBrowse, mehediSndErrPlay });

            mehediSoundsTab.Controls.AddRange(new Control[] { shihabSndCompGrp, mehediSndErrGrp });
        }

        private void BuildDebridVaultTab(TabPage shihabDebridTab, bool shihabDark)
        {
            AddShihabSectionBanner(shihabDebridTab, "Multi-Host Debrid Accounts & DPAPI Encrypted Secret Vault", shihabDark);

            ShihabModernListView shihabDebridView = new ShihabModernListView
            {
                Location = new Point(16, 44),
                Size = new Size(592, 165),
                BorderStyle = BorderStyle.FixedSingle,
                MehediDarkMode = shihabDark,
                ShihabShowFileTypeBadges = false,
                BackColor = _surfColor,
                ForeColor = ForeColor
            };
            shihabDebridView.Columns.Add("Provider", 130);
            shihabDebridView.Columns.Add("Enabled", 75);
            shihabDebridView.Columns.Add("API Token", 150);
            shihabDebridView.Columns.Add("Account Status", 220);

            Action refreshDebridList = delegate
            {
                shihabDebridView.BeginUpdate();
                shihabDebridView.Items.Clear();
                if (_shihabConfig.ShihabDebridAccounts != null)
                {
                    for (int di = 0; di < _shihabConfig.ShihabDebridAccounts.Count; di++)
                    {
                        ShihabDebridAccount acc = _shihabConfig.ShihabDebridAccounts[di];
                        string rawTok = ShihabSecretsVault.UnsealSecret(acc.ShihabApiToken);
                        string masked = string.IsNullOrEmpty(rawTok) ? "(not set)" : ("dpapi:****" + (rawTok.Length > 4 ? rawTok.Substring(rawTok.Length - 4) : ""));
                        ListViewItem row = new ListViewItem(new string[]
                        {
                            acc.ShihabProviderName,
                            acc.ShihabEnabled ? "Yes" : "No",
                            masked,
                            acc.ShihabAccountStatus ?? "Not configured"
                        });
                        row.Tag = acc;
                        shihabDebridView.Items.Add(row);
                    }
                }
                shihabDebridView.EndUpdate();
            };
            refreshDebridList();

            Label shihabDebProvLbl = new Label { Text = "Provider:", Location = new Point(16, 222), AutoSize = true };
            ComboBox shihabDebProvCombo = new ComboBox { Location = new Point(80, 219), Width = 135, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = _surfColor, ForeColor = ForeColor };
            shihabDebProvCombo.Items.AddRange(new object[] { "Real-Debrid", "AllDebrid", "Premiumize" });
            shihabDebProvCombo.SelectedIndex = 0;

            Label shihabDebTokLbl = new Label { Text = "API Token:", Location = new Point(225, 222), AutoSize = true };
            TextBox shihabDebTokBox = new TextBox { Location = new Point(295, 219), Width = 195, UseSystemPasswordChar = true, BackColor = _surfColor, ForeColor = ForeColor };

            Button shihabDebSaveBtn = ShihabThemePalette.CreateMehediStyledButton("Save & Test", ShihabThemePalette.GetShihabAccentBlue(), Color.White, 108);
            shihabDebSaveBtn.Height = 26;
            shihabDebSaveBtn.Location = new Point(500, 217);
            shihabDebSaveBtn.Click += delegate
            {
                string prov = shihabDebProvCombo.SelectedItem.ToString();
                string tok = shihabDebTokBox.Text.Trim();
                if (_shihabConfig.ShihabDebridAccounts == null) _shihabConfig.ShihabDebridAccounts = new List<ShihabDebridAccount>();
                ShihabDebridAccount target = null;
                for (int di = 0; di < _shihabConfig.ShihabDebridAccounts.Count; di++)
                {
                    if (string.Equals(_shihabConfig.ShihabDebridAccounts[di].ShihabProviderName, prov, StringComparison.OrdinalIgnoreCase))
                    {
                        target = _shihabConfig.ShihabDebridAccounts[di];
                        break;
                    }
                }
                if (target == null)
                {
                    target = new ShihabDebridAccount { ShihabProviderName = prov };
                    _shihabConfig.ShihabDebridAccounts.Add(target);
                }
                target.ShihabApiToken = ShihabSecretsVault.SealSecret(tok);
                target.ShihabEnabled = !string.IsNullOrEmpty(tok);
                if (string.IsNullOrEmpty(tok))
                {
                    target.ShihabAccountStatus = "Disabled";
                    refreshDebridList();
                    return;
                }
                target.ShihabAccountStatus = "Checking...";
                refreshDebridList();
                shihabDebSaveBtn.Enabled = false;
                shihabDebSaveBtn.Text = "Testing...";
                Task.Run(delegate
                {
                    string statusText;
                    try
                    {
                        statusText = ShihabDebridManager.CheckAccountStatus(prov, tok);
                    }
                    catch (Exception ex)
                    {
                        statusText = "Error: " + ex.Message;
                    }
                    if (IsDisposed || !IsHandleCreated) return;
                    try
                    {
                        BeginInvoke((Action)delegate
                        {
                            if (IsDisposed) return;
                            target.ShihabAccountStatus = statusText;
                            shihabDebSaveBtn.Enabled = true;
                            shihabDebSaveBtn.Text = "Save & Test";
                            refreshDebridList();
                        });
                    }
                    catch { }
                });
            };

            GroupBox shihabVaultGrp = new GroupBox
            {
                Text = "Windows DPAPI Secret Vault & Security Controls",
                Location = new Point(16, 260),
                Size = new Size(592, 130),
                ForeColor = ForeColor
            };
            Label shihabVaultDesc = new Label
            {
                Text = "All site login passwords, proxy credentials, and Debrid API tokens are encrypted at rest using Windows Data Protection API (DPAPI CurrentUser scope) with 'dpapi:' envelopes.\r\nClick 'Forget All Sign-Ins' to immediately purge all saved passwords, tokens, and remote sessions.",
                Location = new Point(14, 24),
                Size = new Size(560, 54),
                ForeColor = _mutedColor
            };
            Button shihabForgetAllBtn = ShihabThemePalette.CreateMehediStyledButton("Forget All Sign-Ins (Purge Vault)", ShihabThemePalette.GetMehediAccentRed(), Color.White, 245);
            shihabForgetAllBtn.Location = new Point(14, 84);
            shihabForgetAllBtn.Click += delegate
            {
                if (MessageBox.Show(this, "Purge all saved site logins, proxy passwords, Debrid API tokens, and paired remote devices?", "Forget All Sign-Ins", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    int count = ShihabSecretsVault.ForgetAllSignIns(_mehediStore);
                    _shihabConfig.ShihabSiteLogins.Clear();
                    _vaultWasPurged = true;
                    _snapSiteLogins.Clear();
                    _snapDebridAccounts.Clear();
                    if (_refreshShihabLoginsCallback != null) _refreshShihabLoginsCallback();
                    refreshDebridList();
                    MessageBox.Show(this, "Purged " + count + " credentials and tokens from the DPAPI vault.", "Vault Purged", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };
            shihabVaultGrp.Controls.AddRange(new Control[] { shihabVaultDesc, shihabForgetAllBtn });

            shihabDebridTab.Controls.AddRange(new Control[]
            {
                shihabDebridView, shihabDebProvLbl, shihabDebProvCombo, shihabDebTokLbl, shihabDebTokBox, shihabDebSaveBtn, shihabVaultGrp
            });
        }

        private void BuildUpdatesP2pTab(TabPage shihabUpdatesP2pTab, bool shihabDark)
        {
            AddShihabSectionBanner(shihabUpdatesP2pTab, "GitHub Releases Auto-Updater, BitTorrent P2P & Watchdog", shihabDark);

            GroupBox shihabUpdGrp = new GroupBox
            {
                Text = "GitHub Releases Auto-Updater (" + (_shihabConfig.ShihabUpdateRepoSlug ?? "sshihabb007/MHS-DOWNLOAD-MANAGER") + ")",
                Location = new Point(16, 42),
                Size = new Size(592, 168),
                ForeColor = ForeColor
            };
            _shihabAutoUpdChk = new CheckBox
            {
                Text = "Automatically check GitHub Releases for software updates (startup + every 6 hours)",
                Location = new Point(14, 24),
                AutoSize = true,
                Checked = _shihabConfig.ShihabAutoCheckUpdates
            };
            _shihabPreRelChk = new CheckBox
            {
                Text = "Include pre-release / beta builds when checking for updates",
                Location = new Point(14, 50),
                AutoSize = true,
                Checked = _shihabConfig.ShihabUpdateIncludePrereleases
            };
            Label shihabCurVerLbl = new Label
            {
                Text = "Installed Version: v" + ShihabAutoUpdater.GetCurrentAppVersion() +
                       "  |  Last Check: " + (string.IsNullOrWhiteSpace(_shihabConfig.ShihabLastUpdateCheckUtc) ? "Never" : _shihabConfig.ShihabLastUpdateCheckUtc) +
                       (string.IsNullOrWhiteSpace(_shihabConfig.ShihabSkippedUpdateVersion) ? "" : ("  |  Skipped: v" + _shihabConfig.ShihabSkippedUpdateVersion)),
                Location = new Point(14, 78),
                Size = new Size(564, 34),
                ForeColor = _mutedColor
            };
            Button shihabCheckUpdNowBtn = ShihabThemePalette.CreateMehediStyledButton("Check for Updates Now...", ShihabThemePalette.GetShihabAccentBlue(), Color.White, 205);
            shihabCheckUpdNowBtn.Location = new Point(14, 120);
            shihabCheckUpdNowBtn.Click += delegate
            {
                shihabCheckUpdNowBtn.Enabled = false;
                shihabCheckUpdNowBtn.Text = "Checking GitHub...";
                bool prevPre = _shihabConfig.ShihabUpdateIncludePrereleases;
                bool includePre = _shihabPreRelChk.Checked;
                Task.Run(delegate
                {
                    MehediAppConfiguration checkCfg = _shihabConfig;
                    checkCfg.ShihabUpdateIncludePrereleases = includePre;
                    ShihabUpdateCheckResult updRes = ShihabAutoUpdater.CheckForUpdates(checkCfg, true);
                    string utcStamp = DateTime.UtcNow.ToString("u");
                    if (IsDisposed || !IsHandleCreated) return;
                    try
                    {
                        BeginInvoke((Action)delegate
                        {
                            if (IsDisposed) return;
                            _shihabConfig.ShihabLastUpdateCheckUtc = utcStamp;
                            _shihabConfig.ShihabUpdateIncludePrereleases = prevPre;
                            shihabCheckUpdNowBtn.Enabled = true;
                            shihabCheckUpdNowBtn.Text = "Check for Updates Now...";
                            shihabCurVerLbl.Text = "Installed Version: v" + ShihabAutoUpdater.GetCurrentAppVersion() +
                                "  |  Last Check: " + _shihabConfig.ShihabLastUpdateCheckUtc;
                            if (!string.IsNullOrEmpty(updRes.ErrorMessage))
                            {
                                MessageBox.Show(this, "Could not check GitHub Releases:\r\n" + updRes.ErrorMessage, "Update Check", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            }
                            else if (updRes.UpdateAvailable)
                            {
                                using (ShihabAutoUpdateDialog dlg = new ShihabAutoUpdateDialog(updRes, _shihabConfig, _mehediStore, _shihabCleanExitCallback))
                                {
                                    dlg.ShowDialog(this);
                                }
                            }
                            else
                            {
                                MessageBox.Show(this, "You are running the latest version of MHS Download Manager (v" + ShihabAutoUpdater.GetCurrentAppVersion() + ").", "Up to Date", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                        });
                    }
                    catch { }
                });
            };
            Button shihabResetSkipBtn = ShihabThemePalette.CreateMehediStyledButton("Reset Skipped Version", _surfColor, ForeColor, 165);
            shihabResetSkipBtn.Location = new Point(230, 120);
            shihabResetSkipBtn.Click += delegate
            {
                _shihabConfig.ShihabSkippedUpdateVersion = "";
                _mehediStore.SaveMehediConfig(_shihabConfig);
                shihabCurVerLbl.Text = "Installed Version: v" + ShihabAutoUpdater.GetCurrentAppVersion() + "  |  Skipped version cleared.";
            };
            shihabUpdGrp.Controls.AddRange(new Control[] { _shihabAutoUpdChk, _shihabPreRelChk, shihabCurVerLbl, shihabCheckUpdNowBtn, shihabResetSkipBtn });

            GroupBox shihabP2pGrp = new GroupBox
            {
                Text = "BitTorrent P2P Defaults, IP Blocklist & Crash Watchdog",
                Location = new Point(16, 220),
                Size = new Size(592, 182),
                ForeColor = ForeColor
            };
            _shihabTorSeqDefChk = new CheckBox
            {
                Text = "Default to sequential piece download (stream media while downloading)",
                Location = new Point(14, 24),
                AutoSize = true,
                Checked = _shihabConfig.ShihabTorrentSequentialDefault
            };
            _shihabTorSeedDefChk = new CheckBox
            {
                Text = "Keep seeding completed torrents by default",
                Location = new Point(14, 50),
                AutoSize = true,
                Checked = _shihabConfig.ShihabTorrentKeepSeedingDefault
            };
            Label shihabBlkUrlLbl = new Label { Text = "P2P IP Blocklist URL / Path (P2P / CIDR):", Location = new Point(14, 82), AutoSize = true };
            _shihabBlkUrlBox = new TextBox
            {
                Location = new Point(14, 104),
                Width = 560,
                Text = _shihabConfig.ShihabTorrentBlocklistUrl ?? "",
                BackColor = _surfColor,
                ForeColor = ForeColor
            };
            _shihabCrashMonChk = new CheckBox
            {
                Text = "Enable out-of-process Crash Monitor & automatic interrupted-transfer recovery prompt",
                Location = new Point(14, 140),
                AutoSize = true,
                Checked = _shihabConfig.ShihabCrashMonitorEnabled
            };
            shihabP2pGrp.Controls.AddRange(new Control[] { _shihabTorSeqDefChk, _shihabTorSeedDefChk, shihabBlkUrlLbl, _shihabBlkUrlBox, _shihabCrashMonChk });

            shihabUpdatesP2pTab.Controls.AddRange(new Control[] { shihabUpdGrp, shihabP2pGrp });
        }
    }
}

