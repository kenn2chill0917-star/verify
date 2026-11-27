using System;
using System.Drawing;
using System.Windows.Forms;

namespace FiddlerWillowLite
{
    /// <summary>一个极简的输入对话框，用于新增/编辑项目名和 Host 规则。</summary>
    internal sealed class EditDialog : Form
    {
        private readonly TextBox _first = new TextBox();
        private readonly TextBox _second = new TextBox();
        private readonly bool _twoFields;

        internal string FirstValue
        {
            get { return _first.Text.Trim(); }
        }

        internal string SecondValue
        {
            get { return _second.Text.Trim(); }
        }

        internal EditDialog(string title, string firstLabel, string firstValue, string secondLabel, string secondValue)
        {
            _twoFields = secondLabel != null;

            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(400, _twoFields ? 132 : 92);

            var label1 = new Label { Text = firstLabel, Left = 12, Top = 18, Width = 78 };
            _first.Left = 96;
            _first.Top = 14;
            _first.Width = 292;
            _first.Text = firstValue ?? string.Empty;
            Controls.Add(label1);
            Controls.Add(_first);

            if (_twoFields)
            {
                var label2 = new Label { Text = secondLabel, Left = 12, Top = 56, Width = 78 };
                _second.Left = 96;
                _second.Top = 52;
                _second.Width = 292;
                _second.Text = secondValue ?? string.Empty;
                Controls.Add(label2);
                Controls.Add(_second);
            }

            var buttonTop = _twoFields ? 94 : 54;
            var ok = new Button { Text = "确定", DialogResult = DialogResult.OK, Left = 212, Top = buttonTop, Width = 80 };
            var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Left = 304, Top = buttonTop, Width = 80 };
            Controls.Add(ok);
            Controls.Add(cancel);
            AcceptButton = ok;
            CancelButton = cancel;
        }
    }
}
