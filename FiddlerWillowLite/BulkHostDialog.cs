using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace FiddlerWillowLite
{
    /// <summary>批量添加 Host 规则的对话框：粘贴多行文本，实时预览解析结果。</summary>
    internal sealed class BulkHostDialog : Form
    {
        private readonly TextBox _input = new TextBox();
        private readonly DataGridView _preview = new DataGridView();
        private readonly Label _summary = new Label();
        private readonly Button _ok = new Button { Text = "添加", DialogResult = DialogResult.OK, Width = 84 };
        private readonly Button _cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Width = 84 };
        private readonly Panel _footer = new Panel { Dock = DockStyle.Bottom, Height = 46 };
        private readonly List<HostRule> _parsed = new List<HostRule>();
        private readonly IEnumerable<HostRule> _existing;

        internal IList<HostRule> ParsedRules
        {
            get { return _parsed; }
        }

        internal BulkHostDialog(string projectName, IEnumerable<HostRule> existing)
        {
            _existing = existing;
            Text = "批量添加 Host — " + (projectName ?? "未选中项目");
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
                Text = "每行一条规则，支持以下格式（# 或 // 开头为注释）：\n"
                     + "127.0.0.1  api.example.com        （hosts 格式，可一行跟多个域名）\n"
                     + "api.example.com -> 127.0.0.1:8080  （域名 -> 目标）"
            };
            Controls.Add(header);

            _preview.Dock = DockStyle.Fill;
            _preview.ReadOnly = true;
            _preview.AllowUserToAddRows = false;
            _preview.AllowUserToDeleteRows = false;
            _preview.RowHeadersVisible = false;
            _preview.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _preview.AutoGenerateColumns = false;
            _preview.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "域名", DataPropertyName = "Domain", Width = 260 });
            _preview.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "目标", DataPropertyName = "Target", Width = 180 });
            _preview.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "状态",
                DataPropertyName = "Status",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            });
            Controls.Add(_preview);

            var inputPanel = new Panel { Dock = DockStyle.Bottom, Height = 150 };
            var inputLabel = new Label { Text = "规则文本：", Left = 12, Top = 8, AutoSize = true };
            _input.Multiline = true;
            _input.ScrollBars = ScrollBars.Vertical;
            _input.Left = 12;
            _input.Top = 28;
            _input.Width = 652;
            _input.Height = 114;
            _input.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom;
            _input.TextChanged += (sender, args) => RefreshPreview();
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
            RefreshPreview();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            // 构造阶段 footer 的实际宽度尚未确定，若在那时设置 Right 锚点，
            // 按钮会被错误地定位到客户区之外。这里按真实宽度重新布局并锚定。
            int right = _footer.ClientSize.Width;
            _cancel.Left = right - _cancel.Width - 12;
            _ok.Left = _cancel.Left - _ok.Width - 8;
            _cancel.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            _ok.Anchor = AnchorStyles.Right | AnchorStyles.Top;
        }

        private void RefreshPreview()
        {
            var result = HostRuleParser.Parse(_input.Text, _existing);
            _parsed.Clear();
            _parsed.AddRange(result.Rules);

            var rows = new BindingList<PreviewRow>();
            foreach (var rule in result.Rules)
            {
                rows.Add(new PreviewRow { Domain = rule.Domain, Target = rule.Target, Status = "将添加" });
            }
            foreach (var error in result.Errors)
            {
                rows.Add(new PreviewRow { Domain = error, Target = "", Status = "错误（跳过）" });
            }
            _preview.DataSource = rows;

            _summary.Text = string.Format("有效 {0} 条，重复跳过 {1} 条，错误 {2} 条，注释/空行 {3} 行",
                result.Rules.Count, result.Duplicates, result.Errors.Count, result.Skipped);
            _ok.Enabled = result.Rules.Count > 0;
        }

        private sealed class PreviewRow
        {
            public string Domain { get; set; }
            public string Target { get; set; }
            public string Status { get; set; }
        }
    }
}
