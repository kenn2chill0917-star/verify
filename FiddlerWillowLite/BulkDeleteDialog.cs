using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace FiddlerWillowLite
{
    /// <summary>批量删除对话框：粘贴域名文本，预览命中规则，确认后一次性删除。</summary>
    internal sealed class BulkDeleteDialog : Form
    {
        private readonly TextBox _input = new TextBox();
        private readonly DataGridView _preview = new DataGridView();
        private readonly Label _summary = new Label();
        private readonly ComboBox _scope = new ComboBox();
        private readonly Button _ok = new Button { Text = "删除", DialogResult = DialogResult.OK, Width = 84 };
        private readonly Button _cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Width = 84 };
        private readonly Panel _footer = new Panel { Dock = DockStyle.Bottom, Height = 46 };
        private readonly WillowConfig _config;
        private readonly EnvProject _current;
        private readonly List<string> _domains = new List<string>();
        private string _autoFilledText = "";
        private bool _inputEdited;

        internal IList<string> Domains
        {
            get { return _domains; }
        }

        internal bool AllProjects
        {
            get { return _scope.SelectedIndex == 1; }
        }

        internal BulkDeleteDialog(string projectName, WillowConfig config, EnvProject current)
        {
            _config = config;
            _current = current;
            Text = "批量删除 Host — " + (projectName ?? "未选中项目");
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = true;
            ShowInTaskbar = false;
            ClientSize = new Size(680, 500);
            MinimumSize = new Size(520, 360);

            var header = new Label
            {
                Dock = DockStyle.Top,
                Height = 62,
                Padding = new Padding(12, 8, 12, 0),
                ForeColor = SystemColors.ControlText,
                Text = "已自动填入现有域名：删掉不想删除的行，剩下的即为要删除的域名。\n"
                     + "也可粘贴自定义文本，支持 hosts / 域名 -> 目标 / 域名=目标 / *. 通配（# 或 // 为注释）。"
            };
            Controls.Add(header);

            var scopePanel = new Panel { Dock = DockStyle.Top, Height = 38 };
            var scopeLabel = new Label { Text = "删除范围：", Left = 12, Top = 10, AutoSize = true };
            _scope.DropDownStyle = ComboBoxStyle.DropDownList;
            _scope.Items.Add("仅当前项目");
            _scope.Items.Add("全部项目");
            _scope.SelectedIndex = 0;
            _scope.Left = 80;
            _scope.Top = 6;
            _scope.Width = 150;
            _scope.SelectedIndexChanged += (sender, args) =>
            {
                if (!_inputEdited)
                {
                    FillFromConfig();
                }
                RefreshPreview();
            };
            scopePanel.Controls.Add(scopeLabel);
            scopePanel.Controls.Add(_scope);
            Controls.Add(scopePanel);

            _preview.Dock = DockStyle.Fill;
            _preview.ReadOnly = true;
            _preview.AllowUserToAddRows = false;
            _preview.AllowUserToDeleteRows = false;
            _preview.RowHeadersVisible = false;
            _preview.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _preview.AutoGenerateColumns = false;
            _preview.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "域名", DataPropertyName = "Domain", Width = 180 });
            _preview.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "目标", DataPropertyName = "Target", Width = 150 });
            _preview.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "所属项目", DataPropertyName = "Project", Width = 110 });
            _preview.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "状态",
                DataPropertyName = "Status",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            });
            Controls.Add(_preview);

            var inputPanel = new Panel { Dock = DockStyle.Bottom, Height = 130 };
            var inputLabel = new Label { Text = "域名文本：", Left = 12, Top = 8, AutoSize = true };
            _input.Multiline = true;
            _input.ScrollBars = ScrollBars.Vertical;
            _input.Left = 12;
            _input.Top = 28;
            _input.Width = 652;
            _input.Height = 96;
            _input.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom;
            _input.TextChanged += (sender, args) =>
            {
                if (_input.Text != _autoFilledText)
                {
                    _inputEdited = true;
                }
                RefreshPreview();
            };
            inputPanel.Controls.Add(inputLabel);
            inputPanel.Controls.Add(_input);
            Controls.Add(inputPanel);

            _summary.Left = 12;
            _summary.Top = 14;
            _summary.AutoSize = true;
            _ok.Top = 9;
            _cancel.Top = 9;
            _footer.Controls.Add(_summary);
            _footer.Controls.Add(_ok);
            _footer.Controls.Add(_cancel);
            Controls.Add(_footer);

            AcceptButton = null;
            CancelButton = _cancel;
            FillFromConfig();
            RefreshPreview();
        }

        /// <summary>按当前删除范围，把已有规则的域名填入输入框，方便直接删行。</summary>
        private void FillFromConfig()
        {
            var lines = new List<string>();
            foreach (var project in _config.Projects)
            {
                if (!AllProjects && !ReferenceEquals(project, _current))
                {
                    continue;
                }
                foreach (var rule in project.Rules)
                {
                    if (rule != null && !string.IsNullOrEmpty(rule.Domain) && !lines.Contains(rule.Domain))
                    {
                        lines.Add(rule.Domain);
                    }
                }
            }
            _autoFilledText = string.Join("\n", lines.ToArray());
            _input.Text = _autoFilledText;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            // footer 宽度在构造阶段尚未确定，Right 锚点会算错导致按钮跑到客户区外。
            int right = _footer.ClientSize.Width;
            _cancel.Left = right - _cancel.Width - 12;
            _ok.Left = _cancel.Left - _ok.Width - 8;
            _cancel.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            _ok.Anchor = AnchorStyles.Right | AnchorStyles.Top;
        }

        private void RefreshPreview()
        {
            var parsed = BulkDelete.Parse(_input.Text);
            _domains.Clear();
            _domains.AddRange(parsed.Domains);

            var rows = new BindingList<PreviewRow>();
            var willDelete = 0;
            var notFound = 0;
            foreach (var domain in parsed.Domains)
            {
                var hits = 0;
                foreach (var project in _config.Projects)
                {
                    if (!AllProjects && !ReferenceEquals(project, _current))
                    {
                        continue;
                    }
                    foreach (var rule in project.Rules)
                    {
                        if (rule != null && BulkDelete.Matches(rule.Domain, domain))
                        {
                            rows.Add(new PreviewRow
                            {
                                Domain = rule.Domain,
                                Target = rule.Target,
                                Project = project.Name,
                                Status = "将删除"
                            });
                            hits++;
                        }
                    }
                }
                if (hits > 0)
                {
                    willDelete += hits;
                }
                else
                {
                    notFound++;
                    rows.Add(new PreviewRow { Domain = domain, Target = "", Project = "", Status = "未找到（跳过）" });
                }
            }
            foreach (var error in parsed.Errors)
            {
                rows.Add(new PreviewRow { Domain = error, Target = "", Project = "", Status = "错误（跳过）" });
            }
            _preview.DataSource = rows;

            _summary.Text = string.Format("将删除 {0} 条，未找到 {1} 条，无效 {2} 条，重复 {3} 条，注释/空行 {4} 行",
                willDelete, notFound, parsed.Errors.Count, parsed.Duplicates, parsed.Skipped);
            _ok.Enabled = willDelete > 0;
        }

        private sealed class PreviewRow
        {
            public string Domain { get; set; }
            public string Target { get; set; }
            public string Project { get; set; }
            public string Status { get; set; }
        }
    }
}
