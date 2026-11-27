using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace FiddlerWillowLite
{
    /// <summary>
    /// WinForms 的 TreeView 原生只支持单选。此子类通过 Ctrl/Shift 点击维护一个
    /// 额外的选中集合，并用自绘高亮显示，使「删除」按钮可以一次处理多个节点。
    /// 勾选框（启用/禁用）由基类照常绘制，不受影响。
    /// </summary>
    internal sealed class MultiSelectTree : TreeView
    {
        private readonly HashSet<TreeNode> _selected = new HashSet<TreeNode>();
        private TreeNode _anchor;

        public MultiSelectTree()
        {
            DrawMode = TreeViewDrawMode.OwnerDrawText;
            HideSelection = false;
        }

        /// <summary>当前选中的全部节点（含系统选中的单个节点）。</summary>
        public IList<TreeNode> SelectedNodes
        {
            get
            {
                var list = new List<TreeNode>();
                if (SelectedNode != null && !_selected.Contains(SelectedNode))
                {
                    list.Add(SelectedNode);
                }
                list.AddRange(_selected);
                return list;
            }
        }

        public void ClearSelection()
        {
            _selected.Clear();
            _anchor = null;
            SelectedNode = null;
            Invalidate();
        }

        protected override void OnNodeMouseClick(TreeNodeMouseClickEventArgs e)
        {
            if (e.Button == MouseButtons.Left && e.Node != null)
            {
                if ((Control.ModifierKeys & Keys.Control) != 0)
                {
                    if (!_selected.Add(e.Node))
                    {
                        _selected.Remove(e.Node);
                    }
                    _anchor = e.Node;
                }
                else if ((Control.ModifierKeys & Keys.Shift) != 0 && _anchor != null)
                {
                    var nodes = Flatten(Nodes);
                    var a = nodes.IndexOf(_anchor);
                    var b = nodes.IndexOf(e.Node);
                    if (a >= 0 && b >= 0)
                    {
                        for (var i = Math.Min(a, b); i <= Math.Max(a, b); i++)
                        {
                            _selected.Add(nodes[i]);
                        }
                    }
                }
                else
                {
                    _selected.Clear();
                    _selected.Add(e.Node);
                    _anchor = e.Node;
                }
                Invalidate();
            }
            base.OnNodeMouseClick(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.A)
            {
                foreach (var node in Flatten(Nodes))
                {
                    _selected.Add(node);
                }
                Invalidate();
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnDrawNode(DrawTreeNodeEventArgs e)
        {
            if (_selected.Contains(e.Node))
            {
                using (var brush = new SolidBrush(SystemColors.Highlight))
                {
                    e.Graphics.FillRectangle(brush, e.Bounds);
                }
                var color = e.Node.Checked ? SystemColors.HighlightText : SystemColors.ControlLight;
                TextRenderer.DrawText(e.Graphics, e.Node.Text, Font, e.Bounds, color,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            }
            else
            {
                e.DrawDefault = true;
            }
            base.OnDrawNode(e);
        }

        private static IList<TreeNode> Flatten(TreeNodeCollection nodes)
        {
            var list = new List<TreeNode>();
            foreach (TreeNode node in nodes)
            {
                list.Add(node);
                foreach (var child in Flatten(node.Nodes))
                {
                    list.Add(child);
                }
            }
            return list;
        }
    }
}
