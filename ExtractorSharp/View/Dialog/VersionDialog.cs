﻿using System;
using System.ComponentModel;
using System.Windows.Forms;
using ExtractorSharp.Command;
using ExtractorSharp.UI;
using ExtractorSharp.Properties;
using ExtractorSharp.Loose;
using ExtractorSharp.Data;
using ExtractorSharp.Core;

namespace ExtractorSharp.View.Dialog {
    public partial class VersionDialog : EaseDialog {
        private Controller Controller;
        public VersionDialog() {
            Controller = Program.Controller;
            InitializeComponent();
            button.Click += ButtonClose;
            var builder = new LSBuilder();
            var root = builder.ReadJson(Resources.Version);
            var info=new VersionInfo();
            root[root.Count - 1].GetValue(ref info);
            box.Text = info.ToString();
        }

        private void ButtonClose(object sender, EventArgs e) {
            this.DialogResult = System.Windows.Forms.DialogResult.OK;
        }

    }
}