using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ShihabMehediIDM
{
    public class MehediSchedulerQueuesForm : Form
    {
        private static readonly string[] DayShortNames = new string[] { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };

        private readonly MehediAppConfiguration _shihabConfig;
        private readonly MehediDynamicSegmentEngine _mehediEngine;
        private readonly ShihabStateStore _shihabStore;
        private readonly Font _queuesHeaderFont;
        private ListBox _mehediQueueListBox;
        private CheckBox _shihabEnableStartChk;
        private TextBox _mehediStartHhMmBox;
        private TextBox _shihabOnceDateBox;
        private CheckBox[] _shihabDayChecks;
        private CheckBox _shihabEnableStopChk;
        private TextBox _mehediStopHhMmBox;
        private CheckBox _shihabStartOnStartupChk;
        private NumericUpDown _shihabMaxConcNum;
        private NumericUpDown _mehediRetryNum;
        private CheckBox _shihabPeriodicSyncChk;
        private NumericUpDown _shihabPeriodicSyncMinNum;
        private CheckBox _shihabExitDoneChk;
        private CheckBox _shihabHangUpDoneChk;
        private TextBox _shihabRunCmdDoneBox;
        private ComboBox _shihabPowerDoneCombo;
        private ListView _mehediFilesInQueueView;
        private MehediQueueConfig _currentlyLoadedQueue;
        private bool _suppressQueueSelectionEvent = false;

        public MehediSchedulerQueuesForm(
            MehediAppConfiguration shihabConfig,
            MehediDynamicSegmentEngine mehediEngine,
            ShihabStateStore shihabStore)
        {
            _shihabConfig = shihabConfig;
            _mehediEngine = mehediEngine;
            _shihabStore = shihabStore;
            bool mehediDark = shihabConfig.ShihabDarkMode;
            Color surf = ShihabThemePalette.GetShihabSurfaceBg(mehediDark);

            Text = "Scheduler and Queues — " + ShihabThemePalette.ShihabMainHeaderTitle;
            Size = new Size(780, 560);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = ShihabThemePalette.GetMehediWindowBg(mehediDark);
            ForeColor = ShihabThemePalette.GetShihabPrimaryText(mehediDark);
            Font = ShihabThemePalette.UiRegular9Font;
            Icon = ShihabThemePalette.CreateShihabAppIcon();
            ShihabThemePalette.ApplyMehediNativeDarkFrame(this, mehediDark);

            _queuesHeaderFont = new Font("Segoe UI Semibold", 9.5f);
            Label shihabQLbl = new Label { Text = "Queues:", Location = new Point(14, 14), AutoSize = true, Font = _queuesHeaderFont };
            _mehediQueueListBox = new ListBox
            {
                Location = new Point(14, 38),
                Size = new Size(202, 376),
                BackColor = surf,
                ForeColor = ForeColor
            };

            Button shihabNewQBtn = ShihabThemePalette.CreateMehediStyledButton("+ New Queue", ShihabThemePalette.GetShihabAccentBlue(), Color.White, 98);
            shihabNewQBtn.Location = new Point(14, 422);
            shihabNewQBtn.Click += delegate
            {
                SaveQueueSettingsFor(_currentlyLoadedQueue);
                string defaultName = "Custom Queue #" + (_shihabConfig.MehediQueues.Count + 1);
                string enteredName = PromptForNewQueueName(defaultName, mehediDark);
                if (string.IsNullOrWhiteSpace(enteredName)) return;
                enteredName = enteredName.Trim();
                for (int i = 0; i < _shihabConfig.MehediQueues.Count; i++)
                {
                    if (string.Equals(_shihabConfig.MehediQueues[i].ShihabQueueName, enteredName, StringComparison.OrdinalIgnoreCase))
                    {
                        MessageBox.Show(this, "A queue named \"" + enteredName + "\" already exists.", "Scheduler & Queues", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }
                MehediQueueConfig shihabQ = new MehediQueueConfig { ShihabQueueName = enteredName };
                _shihabConfig.MehediQueues.Add(shihabQ);
                _shihabStore.SaveMehediConfig(_shihabConfig);
                PopulateShihabQueuesList(_shihabConfig.MehediQueues.Count - 1);
            };

            Button mehediDelQBtn = ShihabThemePalette.CreateMehediStyledButton("Delete", ShihabThemePalette.GetMehediAccentRed(), Color.White, 98);
            mehediDelQBtn.Location = new Point(118, 422);
            mehediDelQBtn.Click += delegate
            {
                int shihabIdx = _mehediQueueListBox.SelectedIndex;
                if (shihabIdx < 0 || shihabIdx >= _shihabConfig.MehediQueues.Count) return;
                MehediQueueConfig targetQ = _shihabConfig.MehediQueues[shihabIdx];
                if (targetQ == null) return;
                if (string.Equals(targetQ.ShihabQueueName, "Main Download Queue", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(targetQ.ShihabQueueName, "Synchronization Queue", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(this, "Built-in queues (Main Download Queue and Synchronization Queue) cannot be deleted.", "Scheduler & Queues", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                _mehediEngine.StopShihabQueue(targetQ.ShihabQueueName);
                List<ShihabDownloadItem> allItems = _mehediEngine.GetShihabDownloadsSnapshot();
                bool reassigned = false;
                for (int j = 0; j < allItems.Count; j++)
                {
                    ShihabDownloadItem it = allItems[j];
                    if (it != null && string.Equals(it.MehediQueueName, targetQ.ShihabQueueName, StringComparison.OrdinalIgnoreCase))
                    {
                        it.MehediQueueName = it.GetShihabStatusEnum() == ShihabDownloadStatus.ShihabQueued ? "Main Download Queue" : "";
                        reassigned = true;
                    }
                }
                if (reassigned) _mehediEngine.PersistShihabState();
                _currentlyLoadedQueue = null;
                _shihabConfig.MehediQueues.RemoveAt(shihabIdx);
                _shihabStore.SaveMehediConfig(_shihabConfig);
                _mehediEngine.SyncNextScheduledWakeTimer();
                PopulateShihabQueuesList(Math.Max(0, shihabIdx - 1));
            };

            ShihabDarkTabControl shihabRightTabs = new ShihabDarkTabControl
            {
                ShihabDarkMode = mehediDark,
                Location = new Point(228, 16),
                Size = new Size(522, 442)
            };
            TabPage mehediSchedTab = new TabPage("Schedule") { BackColor = BackColor, ForeColor = ForeColor };
            TabPage shihabFilesTab = new TabPage("Files in the queue") { BackColor = BackColor, ForeColor = ForeColor };

            _shihabEnableStartChk = new CheckBox { Text = "Start queue at (HH:mm):", Location = new Point(16, 16), AutoSize = true };
            _mehediStartHhMmBox = new TextBox { Location = new Point(195, 14), Width = 66, Text = "01:00", BackColor = surf, ForeColor = ForeColor };
            Label onceDateLbl = new Label { Text = "Once date (YYYY-MM-DD):", Location = new Point(268, 17), AutoSize = true };
            _shihabOnceDateBox = new TextBox { Location = new Point(426, 14), Width = 78, Text = "", BackColor = surf, ForeColor = ForeColor };

            Label activeDaysLbl = new Label { Text = "Active days:", Location = new Point(16, 48), AutoSize = true };
            _shihabDayChecks = new CheckBox[7];
            for (int d = 0; d < 7; d++)
            {
                _shihabDayChecks[d] = new CheckBox
                {
                    Text = DayShortNames[d],
                    Location = new Point(102 + d * 54, 47),
                    AutoSize = true,
                    Checked = true
                };
            }

            _shihabEnableStopChk = new CheckBox { Text = "Stop queue at (HH:mm):", Location = new Point(16, 80), AutoSize = true };
            _mehediStopHhMmBox = new TextBox { Location = new Point(195, 78), Width = 68, Text = "07:00", BackColor = surf, ForeColor = ForeColor };
            _shihabStartOnStartupChk = new CheckBox { Text = "Start on app launch", Location = new Point(276, 80), AutoSize = true };

            Label mehediConcLbl = new Label { Text = "Download files at the same time:", Location = new Point(16, 114), AutoSize = true };
            _shihabMaxConcNum = new NumericUpDown { Location = new Point(265, 111), Width = 78, Minimum = 1, Maximum = 16, Value = 4 };

            Label shihabRetLbl = new Label { Text = "Retries per failed file:", Location = new Point(16, 146), AutoSize = true };
            _mehediRetryNum = new NumericUpDown { Location = new Point(265, 143), Width = 78, Minimum = 0, Maximum = 99, Value = 3 };

            _shihabPeriodicSyncChk = new CheckBox { Text = "Periodic sync check for updated files every (min):", Location = new Point(16, 178), AutoSize = true };
            _shihabPeriodicSyncMinNum = new NumericUpDown { Location = new Point(328, 176), Width = 78, Minimum = 5, Maximum = 10080, Value = 120 };

            _shihabExitDoneChk = new CheckBox { Text = "Exit MHS Download Manager when queue finishes", Location = new Point(16, 210), AutoSize = true };
            _shihabHangUpDoneChk = new CheckBox { Text = "Hang up modem / dial-up / VPN connection when done", Location = new Point(16, 238), AutoSize = true };

            Label runCmdLbl = new Label { Text = "Run command when done:", Location = new Point(16, 270), AutoSize = true };
            _shihabRunCmdDoneBox = new TextBox { Location = new Point(185, 267), Width = 295, BackColor = surf, ForeColor = ForeColor };

            Label pwrDoneLbl = new Label { Text = "System power when done:", Location = new Point(16, 304), AutoSize = true };
            _shihabPowerDoneCombo = new ComboBox { Location = new Point(185, 301), Width = 165, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = surf, ForeColor = ForeColor };
            _shihabPowerDoneCombo.Items.AddRange(new object[] { "None", "Sleep", "Hibernate", "Shutdown" });
            _shihabPowerDoneCombo.SelectedIndex = 0;

            Button mehediApplyQBtn = ShihabThemePalette.CreateMehediStyledButton("Apply Schedule", ShihabThemePalette.GetShihabAccentBlue(), Color.White, 140);
            mehediApplyQBtn.Location = new Point(16, 344);
            mehediApplyQBtn.Click += delegate { SaveSelectedMehediQueueSettings(); };

            mehediSchedTab.Controls.AddRange(new Control[]
            {
                _shihabEnableStartChk, _mehediStartHhMmBox, onceDateLbl, _shihabOnceDateBox,
                activeDaysLbl,
                _shihabEnableStopChk, _mehediStopHhMmBox, _shihabStartOnStartupChk,
                mehediConcLbl, _shihabMaxConcNum, shihabRetLbl, _mehediRetryNum,
                _shihabPeriodicSyncChk, _shihabPeriodicSyncMinNum,
                _shihabExitDoneChk, _shihabHangUpDoneChk,
                runCmdLbl, _shihabRunCmdDoneBox, pwrDoneLbl, _shihabPowerDoneCombo, mehediApplyQBtn
            });
            for (int d = 0; d < 7; d++) mehediSchedTab.Controls.Add(_shihabDayChecks[d]);

            _mehediFilesInQueueView = new ShihabModernListView
            {
                Location = new Point(12, 12),
                Size = new Size(468, 340),
                BorderStyle = BorderStyle.FixedSingle,
                MultiSelect = true,
                MehediDarkMode = mehediDark,
                ShihabShowFileTypeBadges = true,
                BackColor = surf,
                ForeColor = ForeColor
            };
            _mehediFilesInQueueView.Columns.Add("File Name", 238);
            _mehediFilesInQueueView.Columns.Add("Size", 95);
            _mehediFilesInQueueView.Columns.Add("Status", 115);

            Button moveUpBtn = ShihabThemePalette.CreateMehediStyledButton("Move Up", surf, ForeColor, 96);
            moveUpBtn.Location = new Point(12, 362);
            moveUpBtn.Click += delegate
            {
                if (_mehediFilesInQueueView.SelectedItems.Count == 0) return;
                ShihabDownloadItem sel = _mehediFilesInQueueView.SelectedItems[0].Tag as ShihabDownloadItem;
                if (sel != null && _mehediEngine.MoveQueueItemOrder(sel, -1))
                {
                    LoadSelectedShihabQueueDetails();
                    SelectItemInQueueListView(sel.ShihabId);
                }
            };

            Button moveDownBtn = ShihabThemePalette.CreateMehediStyledButton("Move Down", surf, ForeColor, 106);
            moveDownBtn.Location = new Point(116, 362);
            moveDownBtn.Click += delegate
            {
                if (_mehediFilesInQueueView.SelectedItems.Count == 0) return;
                ShihabDownloadItem sel = _mehediFilesInQueueView.SelectedItems[0].Tag as ShihabDownloadItem;
                if (sel != null && _mehediEngine.MoveQueueItemOrder(sel, 1))
                {
                    LoadSelectedShihabQueueDetails();
                    SelectItemInQueueListView(sel.ShihabId);
                }
            };

            Button shihabRemoveFromQ = ShihabThemePalette.CreateMehediStyledButton("Remove from Queue", surf, ForeColor, 160);
            shihabRemoveFromQ.Location = new Point(320, 362);
            shihabRemoveFromQ.Click += delegate
            {
                if (_mehediFilesInQueueView.SelectedItems.Count > 0)
                {
                    foreach (ListViewItem lvi in _mehediFilesInQueueView.SelectedItems)
                    {
                        ShihabDownloadItem mehediSel = lvi.Tag as ShihabDownloadItem;
                        if (mehediSel != null)
                        {
                            mehediSel.MehediQueueName = "";
                            if (mehediSel.GetShihabStatusEnum() == ShihabDownloadStatus.ShihabQueued)
                            {
                                mehediSel.SetMehediStatusEnum(ShihabDownloadStatus.ShihabPaused, "Paused");
                            }
                        }
                    }
                    _mehediEngine.PersistShihabState();
                    LoadSelectedShihabQueueDetails();
                }
            };
            shihabFilesTab.Controls.AddRange(new Control[] { _mehediFilesInQueueView, moveUpBtn, moveDownBtn, shihabRemoveFromQ });

            shihabRightTabs.TabPages.Add(mehediSchedTab);
            shihabRightTabs.TabPages.Add(shihabFilesTab);

            _mehediQueueListBox.SelectedIndexChanged += delegate
            {
                if (_suppressQueueSelectionEvent) return;
                if (_currentlyLoadedQueue != null)
                {
                    SaveQueueSettingsFor(_currentlyLoadedQueue);
                }
                LoadSelectedShihabQueueDetails();
            };

            Button shihabStartNowBtn = ShihabThemePalette.CreateMehediStyledButton("Start Now", ShihabThemePalette.GetMehediAccentGreen(), Color.White, 125);
            shihabStartNowBtn.Location = new Point(352, 472);
            shihabStartNowBtn.Click += delegate
            {
                SaveSelectedMehediQueueSettings();
                MehediQueueConfig mehediQ = GetSelectedShihabQueue();
                if (mehediQ != null) _mehediEngine.StartShihabQueue(mehediQ.ShihabQueueName);
                Close();
            };

            Button mehediStopNowBtn = ShihabThemePalette.CreateMehediStyledButton("Stop Queue", ShihabThemePalette.GetShihabAccentAmber(), Color.White, 125);
            mehediStopNowBtn.Location = new Point(486, 472);
            mehediStopNowBtn.Click += delegate
            {
                SaveSelectedMehediQueueSettings();
                MehediQueueConfig shihabQ = GetSelectedShihabQueue();
                if (shihabQ != null) _mehediEngine.StopShihabQueue(shihabQ.ShihabQueueName);
                PopulateQueueDisplayNamesOnly();
            };

            Button shihabCloseBtn = ShihabThemePalette.CreateMehediStyledButton("Close", surf, ForeColor, 110);
            shihabCloseBtn.Location = new Point(620, 472);
            shihabCloseBtn.Click += delegate
            {
                SaveSelectedMehediQueueSettings();
                Close();
            };

            Controls.AddRange(new Control[] { shihabQLbl, _mehediQueueListBox, shihabNewQBtn, mehediDelQBtn, shihabRightTabs, shihabStartNowBtn, mehediStopNowBtn, shihabCloseBtn });
            PopulateShihabQueuesList(0);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _queuesHeaderFont != null)
            {
                _queuesHeaderFont.Dispose();
            }
            base.Dispose(disposing);
        }

        private string PromptForNewQueueName(string defaultName, bool dark)
        {
            using (Form dlg = new Form())
            {
                dlg.Text = "Create New Queue";
                dlg.ClientSize = new Size(380, 134);
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.BackColor = ShihabThemePalette.GetMehediWindowBg(dark);
                dlg.ForeColor = ShihabThemePalette.GetShihabPrimaryText(dark);
                dlg.Font = ShihabThemePalette.UiRegular9Font;
                ShihabThemePalette.ApplyMehediNativeDarkFrame(dlg, dark);

                Label lbl = new Label { Text = "Enter queue name:", Location = new Point(16, 16), AutoSize = true };
                TextBox box = new TextBox
                {
                    Location = new Point(16, 40),
                    Width = 348,
                    Text = defaultName,
                    BackColor = ShihabThemePalette.GetShihabSurfaceBg(dark),
                    ForeColor = dlg.ForeColor
                };
                Button okBtn = ShihabThemePalette.CreateMehediStyledButton("Create", ShihabThemePalette.GetShihabAccentBlue(), Color.White, 96);
                okBtn.Location = new Point(164, 84);
                okBtn.DialogResult = DialogResult.OK;

                Button cancelBtn = ShihabThemePalette.CreateMehediStyledButton("Cancel", ShihabThemePalette.GetShihabSurfaceBg(dark), dlg.ForeColor, 96);
                cancelBtn.Location = new Point(268, 84);
                cancelBtn.DialogResult = DialogResult.Cancel;

                dlg.Controls.AddRange(new Control[] { lbl, box, okBtn, cancelBtn });
                dlg.AcceptButton = okBtn;
                dlg.CancelButton = cancelBtn;
                return dlg.ShowDialog(this) == DialogResult.OK ? box.Text : "";
            }
        }

        private void SelectItemInQueueListView(string itemId)
        {
            for (int i = 0; i < _mehediFilesInQueueView.Items.Count; i++)
            {
                ShihabDownloadItem it = _mehediFilesInQueueView.Items[i].Tag as ShihabDownloadItem;
                if (it != null && string.Equals(it.ShihabId, itemId, StringComparison.Ordinal))
                {
                    _mehediFilesInQueueView.Items[i].Selected = true;
                    _mehediFilesInQueueView.Items[i].EnsureVisible();
                    break;
                }
            }
        }

        private void PopulateShihabQueuesList(int selectIndex)
        {
            _suppressQueueSelectionEvent = true;
            try
            {
                _mehediQueueListBox.BeginUpdate();
                _mehediQueueListBox.Items.Clear();
                for (int shihabI = 0; shihabI < _shihabConfig.MehediQueues.Count; shihabI++)
                {
                    MehediQueueConfig q = _shihabConfig.MehediQueues[shihabI];
                    string suffix = q.MehediIsRunningNow ? " (Running)" : "";
                    _mehediQueueListBox.Items.Add(q.ShihabQueueName + suffix);
                }
                if (_mehediQueueListBox.Items.Count > 0)
                {
                    _mehediQueueListBox.SelectedIndex = Math.Max(0, Math.Min(_mehediQueueListBox.Items.Count - 1, selectIndex));
                }
                _mehediQueueListBox.EndUpdate();
            }
            finally
            {
                _suppressQueueSelectionEvent = false;
            }
            LoadSelectedShihabQueueDetails();
        }

        private void PopulateQueueDisplayNamesOnly()
        {
            int cur = _mehediQueueListBox.SelectedIndex;
            _suppressQueueSelectionEvent = true;
            try
            {
                for (int i = 0; i < _shihabConfig.MehediQueues.Count && i < _mehediQueueListBox.Items.Count; i++)
                {
                    MehediQueueConfig q = _shihabConfig.MehediQueues[i];
                    _mehediQueueListBox.Items[i] = q.ShihabQueueName + (q.MehediIsRunningNow ? " (Running)" : "");
                }
                if (cur >= 0 && cur < _mehediQueueListBox.Items.Count) _mehediQueueListBox.SelectedIndex = cur;
            }
            finally
            {
                _suppressQueueSelectionEvent = false;
            }
        }

        private MehediQueueConfig GetSelectedShihabQueue()
        {
            int mehediIdx = _mehediQueueListBox.SelectedIndex;
            if (mehediIdx >= 0 && mehediIdx < _shihabConfig.MehediQueues.Count)
            {
                return _shihabConfig.MehediQueues[mehediIdx];
            }
            return null;
        }

        private static string NormalizeHhMmInput(string raw, string fallbackHhMm)
        {
            if (string.IsNullOrWhiteSpace(raw)) return fallbackHhMm;
            string[] parts = raw.Trim().Split(':');
            int h, m;
            if (parts.Length == 2 && int.TryParse(parts[0], out h) && int.TryParse(parts[1], out m))
            {
                if (h >= 0 && h <= 23 && m >= 0 && m <= 59)
                {
                    return string.Format("{0:00}:{1:00}", h, m);
                }
            }
            return fallbackHhMm;
        }

        private void LoadSelectedShihabQueueDetails()
        {
            MehediQueueConfig shihabQ = GetSelectedShihabQueue();
            _currentlyLoadedQueue = shihabQ;
            if (shihabQ == null) return;

            _shihabEnableStartChk.Checked = shihabQ.MehediEnableScheduledStart;
            _mehediStartHhMmBox.Text = shihabQ.ShihabStartTimeHhMm ?? "01:00";
            _shihabOnceDateBox.Text = shihabQ.ShihabOnceAtDateYyyyMmDd ?? "";

            string daysCsv = string.IsNullOrEmpty(shihabQ.MehediActiveDaysCsv)
                ? "Sun,Mon,Tue,Wed,Thu,Fri,Sat"
                : shihabQ.MehediActiveDaysCsv;
            for (int d = 0; d < 7; d++)
            {
                _shihabDayChecks[d].Checked = daysCsv.IndexOf(DayShortNames[d], StringComparison.OrdinalIgnoreCase) >= 0;
            }

            _shihabEnableStopChk.Checked = shihabQ.MehediEnableScheduledStop;
            _mehediStopHhMmBox.Text = shihabQ.ShihabStopTimeHhMm ?? "07:00";
            _shihabStartOnStartupChk.Checked = shihabQ.ShihabStartOnStartup;
            _shihabMaxConcNum.Value = Math.Max(1, Math.Min(16, shihabQ.MehediMaxConcurrentFiles));
            _mehediRetryNum.Value = Math.Max(0, Math.Min(99, shihabQ.ShihabRetriesPerFile));
            _shihabPeriodicSyncChk.Checked = shihabQ.MehediEnablePeriodicSync;
            _shihabPeriodicSyncMinNum.Value = Math.Max(5, Math.Min(10080, shihabQ.ShihabPeriodicSyncMinutes > 0 ? shihabQ.ShihabPeriodicSyncMinutes : 120));
            _shihabExitDoneChk.Checked = shihabQ.MehediExitAppWhenDone;
            _shihabHangUpDoneChk.Checked = _shihabConfig.MehediHangUpWhenDone;
            _shihabRunCmdDoneBox.Text = shihabQ.ShihabRunCommandWhenDone ?? "";
            int pIdx = _shihabPowerDoneCombo.FindStringExact(shihabQ.ShihabSystemPowerActionWhenDone ?? "None");
            _shihabPowerDoneCombo.SelectedIndex = pIdx >= 0 ? pIdx : 0;

            _mehediFilesInQueueView.BeginUpdate();
            _mehediFilesInQueueView.Items.Clear();
            List<ShihabDownloadItem> mehediAll = _mehediEngine.GetShihabDownloadsSnapshot();
            for (int shihabI = 0; shihabI < mehediAll.Count; shihabI++)
            {
                ShihabDownloadItem mehediIt = mehediAll[shihabI];
                if (!string.IsNullOrWhiteSpace(mehediIt.MehediQueueName) &&
                    string.Equals(MehediDynamicSegmentEngine.ResolveCanonicalQueueName(mehediIt.MehediQueueName), shihabQ.ShihabQueueName, StringComparison.OrdinalIgnoreCase))
                {
                    ListViewItem shihabRow = new ListViewItem(new string[]
                    {
                        mehediIt.ShihabFileName,
                        mehediIt.ShihabTotalBytes > 0 ? ShihabFormatHelpers.FormatMehediBytes(mehediIt.ShihabTotalBytes) : "--",
                        mehediIt.ShihabStatusText
                    });
                    shihabRow.Tag = mehediIt;
                    _mehediFilesInQueueView.Items.Add(shihabRow);
                }
            }
            _mehediFilesInQueueView.EndUpdate();
        }

        private void SaveSelectedMehediQueueSettings()
        {
            SaveQueueSettingsFor(GetSelectedShihabQueue());
        }

        private void SaveQueueSettingsFor(MehediQueueConfig shihabQ)
        {
            if (shihabQ == null) return;
            shihabQ.MehediEnableScheduledStart = _shihabEnableStartChk.Checked;
            shihabQ.ShihabStartTimeHhMm = NormalizeHhMmInput(_mehediStartHhMmBox.Text, "01:00");
            _mehediStartHhMmBox.Text = shihabQ.ShihabStartTimeHhMm;
            shihabQ.ShihabOnceAtDateYyyyMmDd = _shihabOnceDateBox.Text.Trim();

            List<string> selectedDays = new List<string>();
            for (int d = 0; d < 7; d++)
            {
                if (_shihabDayChecks[d].Checked) selectedDays.Add(DayShortNames[d]);
            }
            if (selectedDays.Count == 0)
            {
                for (int d = 0; d < 7; d++)
                {
                    _shihabDayChecks[d].Checked = true;
                    selectedDays.Add(DayShortNames[d]);
                }
            }
            shihabQ.MehediActiveDaysCsv = string.Join(",", selectedDays.ToArray());

            shihabQ.MehediEnableScheduledStop = _shihabEnableStopChk.Checked;
            shihabQ.ShihabStopTimeHhMm = NormalizeHhMmInput(_mehediStopHhMmBox.Text, "07:00");
            _mehediStopHhMmBox.Text = shihabQ.ShihabStopTimeHhMm;
            shihabQ.ShihabStartOnStartup = _shihabStartOnStartupChk.Checked;
            shihabQ.MehediMaxConcurrentFiles = (int)_shihabMaxConcNum.Value;
            shihabQ.ShihabRetriesPerFile = (int)_mehediRetryNum.Value;
            shihabQ.MehediEnablePeriodicSync = _shihabPeriodicSyncChk.Checked;
            shihabQ.ShihabPeriodicSyncMinutes = (int)_shihabPeriodicSyncMinNum.Value;
            shihabQ.MehediExitAppWhenDone = _shihabExitDoneChk.Checked;
            _shihabConfig.MehediHangUpWhenDone = _shihabHangUpDoneChk.Checked;
            shihabQ.ShihabRunCommandWhenDone = _shihabRunCmdDoneBox.Text.Trim();
            shihabQ.ShihabSystemPowerActionWhenDone = _shihabPowerDoneCombo.SelectedItem != null ? _shihabPowerDoneCombo.SelectedItem.ToString() : "None";
            _shihabStore.SaveMehediConfig(_shihabConfig);
            _mehediEngine.SyncNextScheduledWakeTimer();
        }
    }
}

