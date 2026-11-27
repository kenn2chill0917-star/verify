using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace FiddlerWillowLite
{
    /// <summary>抓包列表中的一行快照，不持有 Fiddler Session 引用，避免内存增长。</summary>
    internal sealed class CaptureItem
    {
        public int No { get; set; }
        public string Method { get; set; }
        public string Host { get; set; }
        public string Path { get; set; }
        public string Status { get; set; }
        public string Time { get; set; }
        public string Project { get; set; }
        public bool Matched { get; set; }
        public string Url { get; set; }
    }

    /// <summary>插件在 Fiddler 中新增的选项卡：左侧管理项目/规则，右侧抓包与过滤。</summary>
    internal sealed class WillowView : UserControl
    {
        private const int MaxCached = 2000;
        private const int MaxShown = 500;

        private readonly WillowConfig _config;
        private readonly MultiSelectTree _tree = new MultiSelectTree();
        private readonly DataGridView _grid = new DataGridView();
        private readonly TextBox _filter = new TextBox();
        private readonly CheckBox _onlyMatched = new CheckBox { Text = "仅命中规则" };
        private readonly CheckBox _paused = new CheckBox { Text = "暂停" };
        private readonly CheckBox _mappingEnabled = new CheckBox { Text = "启用 Host 映射" };
        private readonly Label _count = new Label();
        private readonly List<CaptureItem> _all = new List<CaptureItem>();
        private readonly BindingList<CaptureItem> _shown = new BindingList<CaptureItem>();
        private SplitContainer _split;

        internal event Action ConfigChanged;

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            if (_split != null && _split.Width > 200)
            {
                var desired = Math.Min(370, _split.Width - 120);
                if (desired > 80)
                {
                    _split.SplitterDistance = desired;
                }
            }
        }

        internal WillowView(WillowConfig config)
        {
            _config = config ?? new WillowConfig();
            Dock = DockStyle.Fill;
            BuildUi();
            ReloadTree();
            _mappingEnabled.Checked = _config.MappingEnabled;
        }

        private void BuildUi()
        {
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical
            };
            _split = split;
            // Panel sizes and SplitterDistance are applied in OnLoad, after the
            // control has its real width. Setting them here throws when the
            // control still has its default 150px width.
            Controls.Add(split);

            // 左侧：项目树
            _tree.Dock = DockStyle.Fill;
            _tree.CheckBoxes = true;
            _tree.HideSelection = false;
            _tree.AfterCheck += TreeOnAfterCheck;
            _tree.DoubleClick += (sender, args) => EditSelected();
            split.Panel1.Controls.Add(_tree);

            var tools = new ToolStrip { Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden };
            tools.Items.Add(new ToolStripButton("新增项目", null, (sender, args) => AddProject()));
            tools.Items.Add(new ToolStripButton("新增 Host", null, (sender, args) => AddHost()));
            tools.Items.Add(new ToolStripButton("批量添加", null, (sender, args) => AddHostsBulk()));
            tools.Items.Add(new ToolStripButton("编辑", null, (sender, args) => EditSelected()));
            tools.Items.Add(new ToolStripButton("删除", null, (sender, args) => DeleteSelected()));
            tools.Items.Add(new ToolStripButton("批量删除", null, (sender, args) => DeleteHostsBulk()));
            tools.Items.Add(new ToolStripSeparator());
            tools.Items.Add(new ToolStripLabel("勾选切换环境；Ctrl/Shift 多选后删除"));
            split.Panel1.Controls.Add(tools);

            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 74 };
            _mappingEnabled.Left = 12;
            _mappingEnabled.Top = 10;
            _mappingEnabled.AutoSize = true;
            _mappingEnabled.CheckedChanged += (sender, args) =>
            {
                _config.MappingEnabled = _mappingEnabled.Checked;
                MarkDirty();
            };
            bottom.Controls.Add(_mappingEnabled);

            var save = new Button { Text = "立即保存", Left = 12, Top = 38, Width = 90 };
            save.Click += (sender, args) => MarkDirty();
            bottom.Controls.Add(save);

            var hint = new Label
            {
                Left = 112,
                Top = 42,
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Text = "配置: " + RuleStore.ConfigPath
            };
            bottom.Controls.Add(hint);
            split.Panel1.Controls.Add(bottom);

            // 右侧：抓包列表与过滤
            var top = new Panel { Dock = DockStyle.Top, Height = 40 };
            _filter.Left = 8;
            _filter.Top = 9;
            _filter.Width = 190;
            _filter.TextChanged += (sender, args) => ApplyFilter();
            top.Controls.Add(_filter);

            _onlyMatched.Left = 206;
            _onlyMatched.Top = 10;
            _onlyMatched.AutoSize = true;
            _onlyMatched.CheckedChanged += (sender, args) => ApplyFilter();
            top.Controls.Add(_onlyMatched);

            _paused.Left = 310;
            _paused.Top = 10;
            _paused.AutoSize = true;
            top.Controls.Add(_paused);

            var clear = new Button { Text = "清空", Left = 378, Top = 5, Width = 60 };
            clear.Click += (sender, args) =>
            {
                lock (_all)
                {
                    _all.Clear();
                }
                ApplyFilter();
            };
            top.Controls.Add(clear);

            _count.Left = 448;
            _count.Top = 11;
            _count.AutoSize = true;
            top.Controls.Add(_count);
            split.Panel2.Controls.Add(top);

            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.RowHeadersVisible = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.AutoGenerateColumns = false;
            _grid.CellDoubleClick += (sender, args) =>
            {
                if (args.RowIndex >= 0 && args.RowIndex < _shown.Count)
                {
                    var item = _shown[args.RowIndex];
                    if (item != null && item.Url != null)
                    {
                        Clipboard.SetText(item.Url);
                    }
                }
            };
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "#", DataPropertyName = "No", Width = 44 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "方法", DataPropertyName = "Method", Width = 58 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "域名", DataPropertyName = "Host", Width = 170 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "路径",
                DataPropertyName = "Path",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "状态", DataPropertyName = "Status", Width = 56 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "耗时", DataPropertyName = "Time", Width = 78 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "命中项目", DataPropertyName = "Project", Width = 110 });
            _grid.DataSource = _shown;
            split.Panel2.Controls.Add(_grid);
        }

        private void ReloadTree()
        {
            _tree.BeginUpdate();
            _tree.ClearSelection();
            _tree.Nodes.Clear();
            foreach (var project in _config.Projects)
            {
                var projectNode = new TreeNode(project.ToString())
                {
                    Tag = project,
                    Checked = project.Enabled
                };

                foreach (var rule in project.Rules)
                {
                    projectNode.Nodes.Add(new TreeNode(rule.ToString())
                    {
                        Tag = rule,
                        Checked = rule.Enabled
                    });
                }

                _tree.Nodes.Add(projectNode);
            }
            _tree.EndUpdate();
        }

        private void TreeOnAfterCheck(object sender, TreeViewEventArgs e)
        {
            var project = e.Node.Tag as EnvProject;
            if (project != null)
            {
                project.Enabled = e.Node.Checked;
            }
            else
            {
                var rule = e.Node.Tag as HostRule;
                if (rule != null)
                {
                    rule.Enabled = e.Node.Checked;
                }
                else
                {
                    return;
                }
            }
            MarkDirty();
        }

        private EnvProject SelectedProject(TreeNode node)
        {
            if (node == null)
            {
                return null;
            }
            if (node.Tag is EnvProject)
            {
                return (EnvProject)node.Tag;
            }
            if (node.Parent != null && node.Parent.Tag is EnvProject)
            {
                return (EnvProject)node.Parent.Tag;
            }
            return null;
        }

        private void AddProject()
        {
            using (var dialog = new EditDialog("新增环境项目", "项目名称", "", null, null))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK || string.IsNullOrEmpty(dialog.FirstValue))
                {
                    return;
                }

                var project = new EnvProject { Name = dialog.FirstValue, Enabled = _config.Projects.Count == 0 };
                _config.Projects.Add(project);
                ReloadTree();
                MarkDirty();
            }
        }

        private void AddHost()
        {
            var project = SelectedProject(_tree.SelectedNode);
            if (project == null)
            {
                MessageBox.Show(this, "请先选中一个项目，再添加 Host 规则。", "Willow Lite", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var dialog = new EditDialog("新增 Host 规则", "域名", "", "目标 IP[:端口]", ""))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                if (string.IsNullOrEmpty(dialog.FirstValue) || string.IsNullOrEmpty(dialog.SecondValue))
                {
                    MessageBox.Show(this, "域名和目标地址都不能为空。", "Willow Lite", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                project.Rules.Add(new HostRule
                {
                    Domain = dialog.FirstValue,
                    Target = dialog.SecondValue,
                    Enabled = true
                });
                ReloadTree();
                MarkDirty();
            }
        }

        private void AddHostsBulk()
        {
            var project = SelectedProject(_tree.SelectedNode);
            if (project == null)
            {
                MessageBox.Show(this, "请先选中一个项目，再批量添加 Host 规则。", "Willow Lite", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var dialog = new BulkHostDialog(project.Name, project.Rules))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK || dialog.ParsedRules.Count == 0)
                {
                    return;
                }
                foreach (var rule in dialog.ParsedRules)
                {
                    project.Rules.Add(rule);
                }
                ReloadTree();
                MarkDirty();
            }
        }

        private void EditSelected()
        {
            var node = _tree.SelectedNode;
            if (node == null)
            {
                return;
            }

            var project = node.Tag as EnvProject;
            if (project != null)
            {
                using (var dialog = new EditDialog("编辑项目", "项目名称", project.Name, null, null))
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK || string.IsNullOrEmpty(dialog.FirstValue))
                    {
                        return;
                    }
                    project.Name = dialog.FirstValue;
                    node.Text = project.ToString();
                    MarkDirty();
                }
                return;
            }

            var rule = node.Tag as HostRule;
            if (rule == null)
            {
                return;
            }

            using (var dialog = new EditDialog("编辑 Host 规则", "域名", rule.Domain, "目标 IP[:端口]", rule.Target))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                if (string.IsNullOrEmpty(dialog.FirstValue) || string.IsNullOrEmpty(dialog.SecondValue))
                {
                    MessageBox.Show(this, "域名和目标地址都不能为空。", "Willow Lite", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                rule.Domain = dialog.FirstValue;
                rule.Target = dialog.SecondValue;
                node.Text = rule.ToString();
                MarkDirty();
            }
        }

        private void DeleteSelected()
        {
            var nodes = _tree.SelectedNodes;
            if (nodes == null || nodes.Count == 0)
            {
                return;
            }

            // 先收集项目，再收集规则；若规则所属项目也在选中列表中则不重复删除。
            var projects = new List<EnvProject>();
            foreach (TreeNode node in nodes)
            {
                var project = node.Tag as EnvProject;
                if (project != null && !projects.Contains(project))
                {
                    projects.Add(project);
                }
            }

            var rules = new List<KeyValuePair<EnvProject, HostRule>>();
            foreach (TreeNode node in nodes)
            {
                var rule = node.Tag as HostRule;
                var parent = SelectedProject(node);
                if (rule != null && parent != null && !projects.Contains(parent))
                {
                    rules.Add(new KeyValuePair<EnvProject, HostRule>(parent, rule));
                }
            }

            var ruleCount = 0;
            foreach (var project in projects)
            {
                ruleCount += project.Rules.Count;
            }
            ruleCount += rules.Count;
            if (ruleCount == 0 && projects.Count == 0)
            {
                return;
            }

            var description = projects.Count > 0
                ? string.Format("{0} 个项目（含 {1} 条规则）", projects.Count, ruleCount)
                : string.Format("{0} 条规则", ruleCount);
            var answer = MessageBox.Show(this,
                "确定删除选中的 " + description + " 吗？此操作不可撤销。",
                "Willow Lite", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer != DialogResult.Yes)
            {
                return;
            }

            var removedRules = 0;
            foreach (var pair in rules)
            {
                if (pair.Key.Rules.Remove(pair.Value))
                {
                    removedRules++;
                }
            }
            var removedProjects = BulkDelete.RemoveProjects(_config, projects);

            ReloadTree();
            MarkDirty();
            if (removedProjects > 0 || removedRules > 0)
            {
                MessageBox.Show(this,
                    string.Format("已删除 {0} 个项目、{1} 条规则。", removedProjects, removedRules),
                    "Willow Lite", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void DeleteHostsBulk()
        {
            var project = SelectedProject(_tree.SelectedNode);
            using (var dialog = new BulkDeleteDialog(project == null ? null : project.Name, _config, project))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Domains.Count == 0)
                {
                    return;
                }

                var scope = dialog.AllProjects ? null : project;
                var result = BulkDelete.Remove(_config, dialog.Domains, scope);
                ReloadTree();
                MarkDirty();

                var message = string.Format("已删除 {0} 条规则。", result.Removed.Count);
                if (result.NotFound.Count > 0)
                {
                    message += string.Format("\n未找到 {0} 个域名。", result.NotFound.Count);
                }
                MessageBox.Show(this, message, "Willow Lite", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void MarkDirty()
        {
            var handler = ConfigChanged;
            if (handler != null)
            {
                handler();
            }
        }

        /// <summary>由 Fiddler 的后台线程调用，切换到 UI 线程更新列表。</summary>
        internal void Append(CaptureItem item)
        {
            if (item == null || IsDisposed || !IsHandleCreated)
            {
                return;
            }

            try
            {
                if (InvokeRequired)
                {
                    BeginInvoke((MethodInvoker)(() => Append(item)));
                    return;
                }
            }
            catch
            {
                return;
            }

            if (_paused.Checked)
            {
                return;
            }

            lock (_all)
            {
                _all.Insert(0, item);
                if (_all.Count > MaxCached)
                {
                    _all.RemoveRange(MaxCached, _all.Count - MaxCached);
                }
            }
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            if (IsDisposed || !IsHandleCreated)
            {
                return;
            }

            var query = (_filter.Text ?? string.Empty).Trim();
            var onlyMatched = _onlyMatched.Checked;
            var cached = 0;

            _shown.RaiseListChangedEvents = false;
            _shown.Clear();
            lock (_all)
            {
                cached = _all.Count;
                var added = 0;
                foreach (var item in _all)
                {
                    if (onlyMatched && !item.Matched)
                    {
                        continue;
                    }
                    if (query.Length > 0 && !MatchesFilter(item, query))
                    {
                        continue;
                    }
                    _shown.Add(item);
                    if (++added >= MaxShown)
                    {
                        break;
                    }
                }
            }
            _shown.RaiseListChangedEvents = true;
            _shown.ResetBindings();
            _count.Text = string.Format(CultureInfo.InvariantCulture, "显示 {0} / 缓存 {1}", _shown.Count, cached);
        }

        private static bool MatchesFilter(CaptureItem item, string query)
        {
            return Contains(item.Host, query)
                || Contains(item.Path, query)
                || Contains(item.Method, query)
                || Contains(item.Status, query)
                || Contains(item.Project, query)
                || Contains(item.Url, query);
        }

        private static bool Contains(string value, string query)
        {
            return value != null && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
