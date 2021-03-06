﻿using ExtractorSharp.Command;
using ExtractorSharp.Component;
using ExtractorSharp.Composition;
using ExtractorSharp.Config;
using ExtractorSharp.Core;
using ExtractorSharp.Core.Lib;
using ExtractorSharp.Data;
using ExtractorSharp.Draw;
using ExtractorSharp.Draw.Paint;
using ExtractorSharp.Effect.Sprite;
using ExtractorSharp.EventArguments;
using ExtractorSharp.Handle;
using ExtractorSharp.View;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace ExtractorSharp {
    public partial class MainForm : ESForm {

        private Viewer Viewer { get; }
        private Drawer Drawer { get; }
        private Controller Controller { get; }
        private decimal ImageScale => scaleBox.Value / 100;
        public string Path {
            set => pathBox.Text = value;
            get => pathBox.Text;
        }

        private Point Hotpot { set; get; }
        

        private IPaint Rule { set; get; }

        private IPaint Grid { set; get; }

        private IPaint Border { set; get; }

        private int move_mode = -1;


        public MainForm() : base(new MainConnector()) {
            (this.Connector as MainConnector).MainForm = this;
            InitializeComponent();
            Controller = Program.Controller;
            Viewer = Program.Viewer;
            Drawer = Program.Drawer;
            box.Cursor = Drawer.Brush.Cursor;
            dropPanel = new DropPanel(Connector);
            player = new AudioPlayer();
            Controls.Add(dropPanel);
            Controls.Add(player);
            player.BringToFront();
            previewPanel.BringToFront();
            messager.BringToFront();
            AddListenter();
            AddShow();
            AddBrush();
            AddPaint();
            AddConfig();
            AddSpriteConverter();
        }
        

        private void AddSpriteConverter() {
            AddSpriteConverter(new UnCanvasEffect());
            AddSpriteConverter(new LinearDodgeEffect(Config));
            AddSpriteConverter(new RealPositionEffect(Config));
        }

        private void AddSpriteConverter(IEffect converter) {
            Connector.SpriteEffects.Add(converter);
            converter.Enable = Config[$"{converter.Name}SpriteEffect"].Boolean;
            converter.Index = Config[$"{converter.Name}SpriteEffectIndex"].Integer;
        }

        private void AddConfig() {
            linearDodge.Checked = Config["LinearDodge"].Boolean;
            realPositionBox.Checked = Config["RealPosition"].Boolean;
            pixelateBox.Checked = Config["Pixelate"].Boolean;
            scaleBox.Value = Config["CanvasScale"].Integer;
            onionskinBox.Checked = Config["OnionSkin"].Boolean;
            displayBox.Checked = Config["Animation"].Boolean;
        }

        private void AddBrush() {
            foreach (var entry in Drawer.Brushes) {
                var item = new ToolStripMenuItem(Language[entry.Key]);
                item.CheckOnClick = true;
                if (Drawer.IsSelect(entry.Key)) {
                    item.Checked = true;
                }
                item.Click += (o, e) => {
                    Drawer.Select(entry.Key);
                    foreach (ToolStripMenuItem i in toolsMenu.DropDownItems) {
                        i.Checked = false;
                    }
                    item.Checked = true;
                };
                toolsMenu.DropDownItems.Add(item);
            }
        }

        private void AddPaint() {
            Rule = new Rule();
            Grid = new Grid();
            Border = new Border();
            AddPaint(displayRuleItem, Rule);
            AddPaint(gridItem, Grid);
            AddPaint(borderItem, Border);
        }

        private void AddPaint(ToolStripMenuItem item, IPaint paint) {
            paint.Visible = item.Checked;
            item.CheckOnClick = true;
            item.Click += Flush;
            item.CheckedChanged += (o, e) => paint.Visible = item.Checked;
        }


        /// <summary>
        /// 给不需要动态参数的窗口-菜单添加监听
        /// </summary>
        private void AddShow() {
            AddShow(aboutItem, "about");
            AddShow(feedbackItem, "debug", "feedback");
            AddShow(settingItem, "setting");
            AddShow(versionItem, "version");
        }

        public void AddShow(ToolStripMenuItem item, string name, params object[] args) => item.Click += (o, e) => Viewer.Show(name, args);


        public void AddCommand(Control control, string name) {
            control.Click += (o, e) => Connector.Do(name, Connector);
        }

        public void AddCommand(ToolStripItem control, string name) {
            control.Click += (o, e) => Connector.Do(name, Connector);
        }


        public ToolStripMenuItem AddMenuItem(IMenuItem item) {
            var toolItem = new ToolStripMenuItem(Language[item.Name]);
            switch (item.Parent) {
                case MenuItemType.MAIN:
                    mainMenu.Items.Add(toolItem);
                    break;
                case MenuItemType.FILE:
                    fileMenu.DropDownItems.Add(toolItem);
                    break;
                case MenuItemType.EDIT:
                    editMenu.DropDownItems.Add(toolItem);
                    break;
                case MenuItemType.VIEW:
                    editMenu.DropDownItems.Add(toolItem);
                    break;
                case MenuItemType.MODEL:
                    modelMenu.DropDownItems.Add(toolItem);
                    break;
                case MenuItemType.TOOLS:
                    toolsMenu.DropDownItems.Add(toolItem);
                    break;
                case MenuItemType.ABOUT:
                    aboutMenu.DropDownItems.Add(toolItem);
                    break;
                case MenuItemType.FILELIST:
                    albumListMenu.Items.Add(toolItem);
                    break;
                case MenuItemType.IMAGELIST:
                    imageListMenu.Items.Add(toolItem);
                    break;
                default:
                    return null;
            }
            if (!string.IsNullOrEmpty(item.Command)) {
                var command = item.Command;
                switch (item.Click) {
                    case ClickType.Command:
                        AddCommand(toolItem, command);
                        break;
                    case ClickType.View:
                        AddShow(toolItem, command);
                        break;
                }
            }
            if (item.Childrens != null) {
                AddChildItem(item);
            }
            return toolItem;
        }

        public void AddChildItem(IMenuItem item) {
            foreach (var child in item.Childrens) {
                var childItem = new ToolStripMenuItem(Language[child.Name]);
                childItem.DropDownItems.Add(childItem);
                if (string.IsNullOrEmpty(item.Command)) {
                    var command = child.Command;
                    switch (item.Click) {
                        case ClickType.Command:
                            AddCommand(childItem, command);
                            break;
                        case ClickType.View:
                            AddShow(childItem, command);
                            break;
                    }
                }
                if (item.Childrens != null) {
                    AddChildItem(child);
                }
            }
        }


        /// <summary>
        /// 添加监听
        /// </summary>
        private void AddListenter() {
            addFileItem.Click += AddFile;
            openDirItem.Click += InputDirectory;
            saveAsFileItem.Click += OutputFile;
            saveDirItem.Click += OutputDirectory;
            exitItem.Click += (o, e) => Application.Exit();
            replaceItem.Click += ReplaceFile;
            saveAsItem.Click += SaveAsImg;
            renameItem.Click += RenameImg;
            addMergeItem.Click += AddMerge;
            addOutsideMergeItem.Click += AddOutMerge;
            runMergeItem.Click += DisplayMerge;
            albumList.SelectedIndexChanged += ImageChanged;
            albumList.Deleted = DeleteImg;
            albumList.ItemDraged += MoveFileIndex;
            albumList.DragDrop += DragDropInput;
            box.Paint += OnPainting;
            box.MouseClick += OnMouseClick;
            box.MouseDown += OnMouseDown;
            box.MouseUp += OnMouseUp;
            box.MouseMove += OnMouseMove;
            box.MouseWheel += OnMouseWheel;
            saveImageItem.Click += SaveImage;
            saveSingleImageItem.Click += SaveSingleImage;
            saveAllImageItem.Click += SaveAllImage;
            saveGifItem.Click += SaveGif;
            replaceImageItem.Click += ReplaceImage;
            hideCheckImageItem.Click += (o, e) => Connector.Do("hideImage", Connector.SelectedFile, Connector.CheckedImageIndices);
            linkImageItem.Click += LinkImage;
            imageList.Deleted = DeleteImage;
            imageList.ItemDraged += MoveImageIndex;
            imageList.SelectedIndexChanged += SelectImageChanged;
            imageList.ItemHoverChanged += PreviewHover;
            changePositionItem.Click += (o, e) => Viewer.Show("changePosition", Connector.CheckedImages);
            changeSizeItem.Click += (o, e) => Viewer.Show("changeSize", Connector.SelectedFile, imageList.SelectIndexes, ImageScale);
            searchBox.TextChanged += (o, e) => ListFlush();

            newImageItem.Click += (o, e) => Viewer.Show("newImage", Connector.SelectedFile);
            realPositionBox.CheckedChanged += RealPosition;
            displayBox.CheckedChanged += Display;
            newImgItem.Click += ShowNewImgDialog;
            hideImgItem.Click += HideImg;
            convertItem.Click += ShowConvert;
            DragEnter += DragEnterInput;
            DragDrop += DragDropInput;
            undoItem.Click += (o, e) => Controller.Move(-1);
            redoItem.Click += (o, e) => Controller.Move(1);
            closeButton.Click += CloseFile;
            historyButton.Click += ShowHistory;
            scaleBox.ValueChanged += ScaleChange;
            scaleBox.Increment = 30;
            pixelateBox.CheckedChanged += Flush;
            sortItem.Click += Sort;
            classifyItem.CheckedChanged += Classify;
            displayRuleCrossHairItem.Click += Flush;
            adjustRuleItem.Click += AjustRule;
            openButton.Click += AddFile;
            pathBox.TextChanged += (o, e) => pathBox.SelectionStart = pathBox.Text.Length;//光标移到最后，以便显示名称
            pathBox.Click += SelectSavePath;
            openFileItem.Click += AddFile;
            saveFileItem.Click += (o, e) => Connector.Save();
            lockRuleItem.Click += LockRule;
            mutipleLayerItem.CheckedChanged += Flush;
            replaceLayerItem.Click += ReplaceLayer;
            layerList.ItemCheck += HideLayer;
            layerList.Cleared += ClearLayer;
            layerList.Deleted += DeleteLayer;
            renameLayerItem.Click += RenameLayer;
            addLayerItem.Click += AddLayer;
            adjustEntityPositionItem.Click += AdjustLayer;
            adjustPositionItem.Click += AdjustPosition;
            repairFileItem.Click += (o, e) => Connector.Do("repairFile", Connector.CheckedFiles);

            Drawer.BrushChanged += (o, e) => box.Cursor = e.Brush.Cursor;
            Drawer.LayerChanged += (o, e) => LayerFlush();
            Drawer.LayerVisibleChanged += LayerVisibleChanged;

            linearDodge.CheckedChanged += LinearDodge;
            onionskinBox.CheckedChanged += Onionskin;
            previewItem.CheckedChanged += PreviewChanged;
            trackBar.ValueChanged += TabLayer;
            Drawer.ColorChanged += ColorChanged;
            colorPanel.MouseClick += ColorChanged;
            lineDodgeItem.Click += LinearDodge;
            splitFileItem.Click += (o, e) => Connector.Do("splitFile", Connector.CheckedFiles);
            mixFileItem.Click += (o, e) => Connector.Do("mixFile", Connector.CheckedFiles);
            cutImageItem.Click += CutImage;
            copyImageItem.Click += CopyImage;
            pasteImageItem.Click += PasteImage;
            cutImgItem.Click += CutImg;
            copyImgItem.Click += CutImg;
            pasteImgItem.Click += PasteImg;

            canvasCutItem.Click += CutImage;
            canvasCopyItem.Click += CopyImage;
            canvasPasteItem.Click += CanvasPasteImage;

            selectAllHideItem.Click += (o, e) => imageList.Filter(sprite => sprite.Hidden);
            selectAllLinkItem.Click += (o, e) => imageList.Filter(sprite => sprite.Type == ColorBits.LINK);
            selectThisLinkItm.Click += SelectThisLink;
            selectThisTargetItem.Click += SelectThisTarget;
            Controller.CommandDid += CommandDid;
        }

        private void LayerVisibleChanged(object sender, LayerEventArgs e) {
            var index = e.ChangedIndex;
            var layer = Drawer.LayerList[index];
            var pos = layerList.Items.IndexOf(layer);
            if (pos > -1) {
                layerList.SetItemChecked(pos, layer.Visible);
            }
        }

        private void LayerFlush() {
            var arr = Drawer.LayerList.ToArray();
            layerList.Items.Clear();
            for(var i = 0; i < arr.Length; i++) {
                layerList.Items.Add(arr[i], arr[i].Visible);
            }
        }

        private void CommandDid(object sender,CommandEventArgs e) {
            if (e.Command.IsFlush) {
                Connector.FileListFlush();
            }
            if (e.Command.IsChanged) {//发生更改
                Connector.OnSaveChanged();
            }
            if (e.Command is ICommandMessage) {
                Connector.SendSuccess(e.Command.Name);
            }
        }

        private void ScaleChange(object sender, EventArgs e) {
            Config["CanvasScale"] = new ConfigValue(scaleBox.Value);
            Flush(sender, e);
        }

        private void RealPosition(object sender, EventArgs e) {
            if (Connector.SelectedImage != null) {
                Drawer.CurrentLayer.Location = realPositionBox.Checked ? Connector.SelectedImage.Location : Point.Empty;

            }
            Flush(sender, e);
        }

        private void Onionskin(object sender, EventArgs e) {
            Drawer.LastLayerVisible = onionskinBox.Checked;
            this.Flush(sender, e);
        }

        private void SelectThisLink(object sender, EventArgs e) {
            var cur = imageList.SelectedItem;
            if (cur != null && cur.Type != ColorBits.LINK) {
                imageList.Filter(sprite => sprite.Type == ColorBits.LINK && cur.Equals(sprite.Target));
            }
        }

        private void SelectThisTarget(object sender, EventArgs e) {
            var cur = imageList.SelectedItem;
            if (cur != null && cur.Type == ColorBits.LINK) {
                for (var i = 0; i < imageList.Items.Count; i++) {
                    if (imageList.Items[i].Equals(cur.Target)) {
                        imageList.SelectedIndex = i;
                    }
                }
            }
        }

        /// <summary>
        /// 粘贴img
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PasteImg(object sender, EventArgs e) {
            var index = Connector.SelectedFileIndex;
            index = index < 0 ? Connector.FileCount : index;
            Connector.Do("pasteImg", index);
        }

        /// <summary>
        /// 复制/剪切img
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CutImg(object sender, EventArgs e) {
            var mode = ClipMode.Copy;
            if (sender.Equals(cutImgItem)) {
                mode = ClipMode.Cut;
            }
            Connector.Do("cutImg", Connector.CheckedFiles, mode);
        }

        private void CopyImage(object sender, EventArgs e) {
            CutImage(ClipMode.Copy);
        }



        /// <summary>
        /// 复制/剪切图片
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CutImage(object sender, EventArgs e) {
            CutImage(ClipMode.Cut);
        }


        private void CutImage(ClipMode mode) {
            var al = Connector.SelectedFile;
            if (al != null) {
                var indexes = Connector.CheckedImageIndices;
                Connector.Do("cutImage", al, indexes, mode);
            }
        }

        private void CanvasPasteImage(object sender, EventArgs e) {
            var al = Connector.SelectedFile;
            if (al != null) {
                var image = Connector.SelectedImage;
                Connector.Do("pasteSingleImage", image, Hotpot);
            }
        }


        /// <summary>
        /// 粘贴图片
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PasteImage(object sender, EventArgs e) {
            var al = Connector.SelectedFile;
            if (al != null) {
                var index = Connector.SelectedImageIndex;
                index = index < 0 ? Connector.ImageCount : index;
                Connector.Do("pasteImage", al, index);
            }
        }

        /// <summary>
        /// 线性减淡
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LinearDodge(object sender, EventArgs e) {
            Config["LinearDodge"] = new ConfigValue(linearDodge.Checked);
            Flush(sender, e);
        }

        /// <summary>
        /// 当绘制器的颜色发生改变时
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ColorChanged(object sender, ColorEventArgs e) {
            colorPanel.BackColor = e.NewColor;
        }

        /// <summary>
        /// 点击颜色选择框切换颜色
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ColorChanged(object sender, MouseEventArgs e) {
            if (e.Button == MouseButtons.Right) {
                Drawer.Color = Color.Empty;
                return;
            }
            if (colorDialog.ShowDialog() == DialogResult.OK) {
                Drawer.Color = colorDialog.Color;
            }
        }

        private void ImageChanged(object sender, EventArgs e) {
            Drawer.OnImageChanged(new FileEventArgs {
                Entity = Connector.SelectedImage,
                Album = Connector.SelectedFile
            });
            ImageFlush(true);
        }

        private void PreviewChanged(object sender, EventArgs e) {
            Config["Preview"] = new ConfigValue(previewItem.Checked);
            previewPanel.Visible = previewItem.Checked;
        }

        private void PreviewHover(object sender, ItemHoverEventArgs e) {
            var entity = e.Item as Sprite;
            if (previewItem.Checked && entity != null) {
                previewPanel.BackgroundImage = entity.Picture;
                previewPanel.Visible = true;
            }
        }


        private void DeleteLayer() {
            var array = layerList.SelectItems;
            foreach (var item in array) {
                Drawer.LayerList.Remove(item);
                layerList.Items.Remove(item);
            }
        }

        private void ClearLayer() {
            layerList.Items.Clear();
        }

        private void AdjustPosition(object sender, EventArgs e) {
            var index = Connector.SelectedImageIndex;
            var item = Connector.SelectedFile;
            if (index > -1 && item != null) {
                Connector.Do("changePosition", item, new int[] { index }, new int[] { Drawer.CurrentLayer.Location.X, Drawer.CurrentLayer.Location.Y, 0, 0 }, new bool[] { true, true, false, false, false });
            }
        }

        /// <summary>
        /// 重命名
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RenameLayer(object sender, EventArgs e) {
            if (mutipleLayerItem.Checked) {
                if (layerList.SelectedItem is Layer item) {
                    var dialog = new ESTextDialog();
                    dialog.InputText = item?.ToString();
                    dialog.Text = Language["Rename"];
                    if (dialog.Show() == DialogResult.OK) {
                        Connector.Do("renameLayer", item, dialog.InputText);
                    }
                }
            }
        }

        /// <summary>
        /// 加入图层
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AddLayer(object sender, EventArgs e) {
            var array = Connector.CheckedImages;
            if (array.Length > 0) {
                Drawer.AddLayer(array);
                layerList.Items.Clear();
                layerList.Items.AddRange(Drawer.LayerList.ToArray());
                if (!mutipleLayerItem.Checked) {
                    mutipleLayerItem.Checked = true;
                } else {
                    CanvasFlush();
                }
                Connector.SendSuccess("AddLayer");
            }
        }


        /// <summary>
        /// 校正坐标
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AdjustLayer(object sender, EventArgs e) {

        }

        /// <summary>
        /// 隐藏图层
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HideLayer(object sender, EventArgs e) {
            if (mutipleLayerItem.Checked) {
                if (layerList.SelectedItem is Layer item) {
                    item.Visible = !item.Visible;
                }
                CanvasFlush();
            }
        }

        /// <summary>
        /// 切换图层
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TabLayer(object sender, EventArgs e) {
            var value = trackBar.Value;
            if (value == trackBar.Maximum && value < Config["LayerMaximum"].Integer - 1) {
                trackBar.Maximum++;
            }
            Drawer.TabLayer(value);
            layerList.Items.Clear();
            layerList.Items.AddRange(Drawer.LayerList.ToArray());
            CanvasFlush();
        }

        /// <summary>
        /// 替换图层
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ReplaceLayer(object sender, EventArgs e) {
            var array = Connector.ImageArray;
            Drawer.ReplaceLayer(array);
            Connector.SendSuccess("ReplaceImage");
            CanvasFlush();
        }



        private void LockRule(object sender, EventArgs e) {
            Rule.Locked = lockRuleItem.Checked;
        }


        /// <summary>
        /// 移动文件序列
        /// </summary>
        private void MoveFileIndex(object sender, ItemDragEventArgs<Album> e) {
            if (e.Index > -1 && Connector.FileCount > 0) {
                Connector.Do("moveFile", e.Index, e.Target);
                Connector.SelectedFileIndex = e.Target;
            }
        }


        /// <summary>
        /// 移动贴图序列
        /// </summary>
        private void MoveImageIndex(object sender, ItemDragEventArgs<Sprite> e) {
            var al = Connector.SelectedFile;
            if (al != null && e.Index > -1 && Connector.ImageCount > 0) {
                Connector.Do("moveImage", al, e.Index, e.Target);
                Connector.SelectedImageIndex = e.Target;
            }
        }


        protected override void OnFormClosing(FormClosingEventArgs e) {
            if (!Connector.IsSave) {
                var rs = MessageBox.Show(Language["SaveTips"],Language["Tips"], MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (rs == DialogResult.Cancel) {
                    e.Cancel = true;
                    return;
                }
                if (rs == DialogResult.Yes) {
                    Connector.Save();
                    e.Cancel = !Connector.IsSave;
                }
                player.Close();
            }
            SaveConfig();
            e.Cancel = false;
            base.OnFormClosing(e);
        }

        private void SaveConfig() {
            Config["CanvasScale"] = new ConfigValue(scaleBox.Value);
            Config["Pixelate"] = new ConfigValue(pixelateBox.Checked);
            Config["Brush"] = new ConfigValue(Drawer.Brush.Name);
            Config["BrushColor"] = new ConfigValue(Drawer.Color);

            Config["LinearDodge"] = new ConfigValue(linearDodge.Checked);
            Config["OnionSkin"] = new ConfigValue(onionskinBox.Checked);
            Config["RealPosition"] = new ConfigValue(realPositionBox.Checked);
            Config["Animation"] = new ConfigValue(displayBox.Checked);

            Config["Ruler"] = new ConfigValue(displayRuleItem.Checked);
            Config["RulerCrosshair"] = new ConfigValue(displayRuleCrossHairItem.Checked);
            Config["RulerLocked"] = new ConfigValue(lockRuleItem.Checked);

            Config["Grid"] = new ConfigValue(gridItem.Checked);
            Config.Save();
        }




        private void SelectSavePath(object sender, EventArgs e) {
            var con = Connector as MainConnector;
            con.SelectSavePath();
            pathBox.Text = con.SavePath;
        }



        private void AjustRule(object sender, EventArgs e) {
            Rule.Location = Drawer.CurrentLayer.Location;
            Flush(sender, e);
        }

        private void SelectImageChanged(object sender, EventArgs e) {
            Drawer.CurrentLayer = new Canvas();
            Drawer.LastLayerVisible = onionskinBox.Checked;
            if (realPositionBox.Checked && Connector.SelectedImage != null) {
                var entity = Connector.SelectedImage;
                Drawer.CurrentLayer.Location = entity.Location;
            }
            Flush(sender, e);
        }

        private void Sort(object sender, EventArgs e) {
            Connector.Do("sortImg");
        }

        private void Classify(object sender, EventArgs e) {
            ListFlush();
        }
       

        /// <summary>
        /// 列表刷新
        /// </summary>
        public void ListFlush() {
            var items = albumList.CheckedItems;
            var index = albumList.SelectedIndex;
            var itemArray = new Album[items.Count];
            items.CopyTo(itemArray, 0);
            albumList.Items.Clear();
            var condition = searchBox.Text.Trim().Split(" ");
            var array = Npks.Find(Connector.List, condition);
            if (classifyItem.Checked) {
                var path = "";
                foreach (var al in array) {
                    var p = al.Path.Replace(al.Name, "");
                    if (p != path) {
                        path = p;
                        var sp = new Album();
                        sp.Path = "---------------分割线---------------";
                        albumList.Items.Add(sp);
                    }
                    albumList.Items.Add(al);
                }
            } else {
                albumList.Items.AddRange(array.ToArray());
            }
            for (var i = 0; i < array.Count; i++) {
                if (itemArray.Contains(array[i])) {
                    albumList.SetItemChecked(i, true);
                }
            }
            if (albumList.Items.Count > 0) {
                if (index < 1 || index > albumList.Items.Count - 1) {
                    index = Math.Min(index, albumList.Items.Count - 1);
                    index = Math.Max(index, 0);
                }
                albumList.SelectedIndex = index;
            }
        }



        private void AddOutMerge(object sender, EventArgs e) {
            var dialog = new OpenFileDialog();
            dialog.Multiselect = true;
            dialog.Filter = $"${Language["ImageSources"]}|*.img;*.gif;*.npk";
            if (dialog.ShowDialog() == DialogResult.OK) {
                var array = Npks.Load(dialog.FileNames).ToArray();
                Connector.Do("addMerge", array);
            }
        }


        private void ShowHistory(object sender, EventArgs e) {
            dropPanel.BringToFront();
            dropPanel.Visible = !dropPanel.Visible;
            dropPanel.Refresh();
        }



        private void CloseFile(object sender, EventArgs e) {
            albumList.Items.Clear();
            imageList.Items.Clear();
            Controller.Dispose();
            Viewer.Dispose();
            ImageFlush();
            Connector.List.Clear();
            Connector.IsSave = true;
            pathBox.Text = string.Empty;
        }

        private void OnMouseWheel(object sender, MouseEventArgs e) {
            if (ModifierKeys == Keys.Alt) {
                var i = scaleBox.Value + e.Delta / 2;
                i = i < scaleBox.Maximum ? i : scaleBox.Maximum;
                i = i > scaleBox.Minimum ? i : scaleBox.Minimum;
                scaleBox.Value = i;
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData) {
            if (keyData.HasFlag(Keys.Alt)) {
                box.Focus();
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void DragEnterInput(object sender, DragEventArgs e) {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
                e.Effect = DragDropEffects.All;
            } else {
                e.Effect = DragDropEffects.None;
            }
        }

        private void DragDropInput(object sender, DragEventArgs e) {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
                var args = e.Data.GetData(DataFormats.FileDrop, false) as string[];
                Connector.AddFile(false, args);
            } else if (e.Data.GetDataPresent(DataFormats.Serializable)) {
                (sender as Control)?.DoDragDrop(e.Data, e.Effect);
            }
        }


        private void ShowConvert(object sender, EventArgs e) {
            var array = Connector.CheckedFiles;
            if (array.Length > 0 && CheckOgg(array)) {
                Viewer.Show("convert", array);
            }
        }

        private bool CheckOgg(params Album[] args) {
            foreach (var al in args) {
                if (al.Version == Img_Version.OGG) {
                    Connector.SendWarning("NotHandleFile");
                    return false;
                }
            }
            return true;
        }



        /// <summary>
        /// 播放动画
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Display(object sender, EventArgs e) {
            if (displayBox.Checked) {
                var thread = new Thread(Display);
                thread.IsBackground = true;
                thread.Name = "display";
                thread.Start();
            }
        }

        private void Display() {
            while (displayBox.Checked) {
                DisplayNext();
                Thread.Sleep(1000 / Config["FlashSpeed"].Integer);
            }
        }


        private void DisplayNext() {
            if (mutipleLayerItem.Checked) {
                if (trackBar.InvokeRequired) {
                    trackBar.Invoke(new MethodInvoker(DisplayNext));
                    return;
                }
                var i = trackBar.Value + 1;
                trackBar.Value = i < Drawer.Count ? i : 0;
            } else {
                if (imageList.InvokeRequired) {
                    imageList.Invoke(new MethodInvoker(DisplayNext));
                    return;
                }
                var i = Connector.SelectedImageIndex + 1;
                i = i < Connector.ImageCount ? i : 0;
                if (Connector.ImageCount > 0) {
                    Connector.SelectedImageIndex = i;
                }
            }
        }


        /// <summary>
        /// 隐藏勾选img
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HideImg(object sender, EventArgs e) {
            var list = Connector.CheckedFiles;
            if (list.Length > 0 && CheckOgg(list) && MessageBox.Show(Language["HideTips"], "", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK) {
                Connector.Do("hideImg", list);
            }
        }

        /// <summary>
        /// 打开新建img窗口
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>F
        private void ShowNewImgDialog(object sender, EventArgs e) {
            Viewer.Show("newImg", Connector.List.Count);
        }



        /// <summary>
        /// 替换img
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ReplaceFile(object sender, EventArgs e) {
            var item = Connector.SelectedFile;
            if (item != null) {
                var dialog = new OpenFileDialog();
                dialog.Filter = $"{Language["ImageResources"]}|*.img;*.gif|{Language["SoundResources"]}|*.ogg;*.wav;*.mp3|{Language["AllFormat"]}|*.*";
                if (item.Version == Img_Version.OGG) {
                    dialog.FilterIndex = 2;
                } else if (item.Name.EndsWith(".img")) {
                    dialog.FilterIndex = 1;
                } else {
                    dialog.FilterIndex = 3;
                }
                if (dialog.ShowDialog() == DialogResult.OK) {
                    var filename = dialog.FileName;
                    Album file = null;
                    if (filename.EndsWith(".gif")) {
                        var fs = File.Open(filename, FileMode.Open);
                        var array = Bitmaps.ReadGif(fs);
                        fs.Close();
                        file = new Album(array);
                        file.Path = filename.GetSuffix();
                    } else {
                        var list = Npks.Load(dialog.FileName);
                        file = list.Count > 0 ? list[0] : null;
                    }
                    if (file != null) {
                        Connector.Do("replaceImg", item, file);
                    }
                }
            }
        }

        /// <summary>
        /// img另存为
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SaveAsImg(object sender, EventArgs e) {
            var array = Connector.CheckedFiles;
            if (array.Length == 1) {
                var dialog = new SaveFileDialog();
                dialog.FileName = array[0].Name;
                dialog.Filter = "img|*.img|ogg|*.ogg|mp3|*.mp3|wav|*.wav";
                dialog.FilterIndex = array[0].Version != Img_Version.OGG ? 1 : 2;
                if (dialog.ShowDialog() == DialogResult.OK) {
                    array[0].Save(dialog.FileName);
                }
            } else if (array.Length > 1) {
                var dialog = new FolderBrowserDialog();
                if (dialog.ShowDialog() == DialogResult.OK) {
                    Connector.Do("saveImg", array, dialog.SelectedPath);
                }
            }
        }

        /// <summary>
        /// 删除img
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DeleteImg() {
            var indices = Connector.CheckedFileIndices;
            if (indices.Length > 0 && MessageBox.Show(Language["DeleteTips"],Language["Tips"], MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK) {
                Connector.Do("deleteImg", indices);
            }
        }

        /// <summary>
        /// 全部选择
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CheckAllImg(object sender, EventArgs e) {
            albumList.CheckAll();
        }

        /// <summary>
        /// 反向勾选
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ReverseCheckImg(object sender, EventArgs e) {
            albumList.ReverseCheck();
        }

        /// <summary>
        /// 全部选择
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CheckAllImage(object sender, EventArgs e) => imageList.CheckAll();

        /// <summary>
        /// 反向勾选
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ReverseCheckImage(object sender, EventArgs e) => imageList.ReverseCheck();


        /// <summary>
        /// img重命名
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RenameImg(object sender, EventArgs e) {
            var album = Connector.SelectedFile;
            if (album != null) {
                var dialog = new ESTextDialog();
                dialog.InputText = album.Path;
                dialog.Text = Language["Rename"];
                if (dialog.Show() == DialogResult.OK) {
                    Connector.Do("renameImg", album, dialog.InputText);
                }
            }
        }



        /// <summary>
        /// 选择贴图
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Flush(object sender, EventArgs e) => CanvasFlush();


        public void ImageFlush() => ImageFlush(false);

        /// <summary>
        /// 贴图列表刷新
        /// </summary>
        public void ImageFlush(bool clear) {
            var al = albumList.SelectedItem;            //记录当前所选img
            var index = imageList.SelectedIndex;        //记录当前选择贴图
            var items = imageList.CheckedItems;
            var itemArray = new Sprite[items.Count];
            items.CopyTo(itemArray, 0);
            if (al != null && al.Version == Img_Version.OGG) { //判断是否为ogg音频
                player.Play(al);
            } else {
                player.Visible = false;
                imageList.Items.Clear();
                var array = al?.List.ToArray();
                if (array != null) {
                    imageList.Items.AddRange(array);
                    for (var i = 0; i < array.Length; i++) {
                        if (itemArray.Contains(array[i])) {
                            imageList.SetItemChecked(i, true);
                        }
                    }
                }
                //添加贴图
                index = (index > -1 && index < imageList.Items.Count) ? index : 0;
                if (imageList.Items.Count > 0) {
                    imageList.SelectedIndex = index;
                } else if (imageList.Items.Count == 0) {
                    CanvasFlush();
                }
            }
        }




        /// <summary>
        /// 打开文件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AddFile(object sender, EventArgs e) {
            var dialog = new OpenFileDialog();
            dialog.Filter = $"{Language["ImageSources"]}|*.npk;*.spk;*.img;*.gif;|{Language["SoundResources"]}|*.mp3;*.wav;*.ogg";
            dialog.Multiselect = true;
            if (dialog.ShowDialog() == DialogResult.OK) {
                Connector.AddFile(!sender.Equals(addFileItem), dialog.FileNames);
            }
        }

        /// <summary>
        /// 读取文件夹(img)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void InputDirectory(object sender, EventArgs e) {
            var dialog = new FolderBrowserDialog();
            if (dialog.ShowDialog() == DialogResult.OK) {
                Connector.AddFile(true, new string[] { dialog.SelectedPath });
            }
        }

        /// <summary>
        /// 存为npk
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OutputFile(object sender, EventArgs e) {
            var dialog = new SaveFileDialog();
            dialog.Filter = "npk文件|*.npk";
            dialog.FileName = Path.GetSuffix();
            if (dialog.ShowDialog() == DialogResult.OK) {
                Connector.Save(dialog.FileName);
            }
        }

        /// <summary>
        /// 存为文件夹(img)
        /// </summary>
        /// <param name="sender"></param>
        private void OutputDirectory(object sender, EventArgs e) {
            var dialog = new FolderBrowserDialog();
            if (dialog.ShowDialog() == DialogResult.OK) {
                Connector.Do("saveImg", Connector.FileArray, dialog.SelectedPath);
            }
        }

        private void AddMerge(object sender, EventArgs e) {
            var array = Connector.CheckedFiles;
            if (array.Length > 0 && CheckOgg(array)) {
                Connector.Do("addMerge", array);
            }
        }

        private void DisplayMerge(object sender, EventArgs e) {
            Viewer.Show("Merge", Connector.SelectedFile);
        }

        public void CanvasFlush() => box.Invalidate();

        /// <summary>
        /// 画布刷新
        /// </summary>
        private void OnPainting(object sender, PaintEventArgs e) {
            var g = e.Graphics;
            g.InterpolationMode = pixelateBox.Checked ? InterpolationMode.NearestNeighbor : InterpolationMode.High;
            var entity = Connector.SelectedImage;//获得当前选择的贴图
            var pos = Drawer.CurrentLayer.Location;

            if (Rule.Visible) {//显示标尺        
                Rule.Tag = Drawer.CurrentLayer.Location.Minus(Rule.Location);
                Rule.Size = box.Size;
                Rule.Draw(g);
            }

            if (Grid.Visible) {//显示网格
                Grid.Tag = Config["GridGap"].Integer;
                Grid.Size = box.Size;
                Grid.Draw(g);
            }

            if (entity?.Picture != null) {
                if (entity.Type == ColorBits.LINK && entity.Target != null) {
                    entity = entity.Target;
                }
                var pictrue = entity.Picture;
                var size = entity.Size.Star(ImageScale);
                if (linearDodge.Checked) {
                    pictrue = pictrue.LinearDodge();
                }
                if (Drawer.LastLayer.Visible) {
                    Drawer.LastLayer?.Draw(g);
                }
                Drawer.CurrentLayer.Tag = entity;
                Drawer.CurrentLayer.Size = size;//校正当前图层的宽高
                Drawer.CurrentLayer.Image = pictrue;//校正贴图
                Drawer.CurrentLayer.Draw(g);//绘制贴图
            } 
            if (Border.Visible) {
                Border.Tag = Drawer.CurrentLayer.Rectangle;
                Border.Draw(g);
            }
        }






        /// <summary>
        /// 是否选择到了图片上
        /// </summary>
        /// <returns></returns>
        private int IsSelctImage(Point p) {
            var entity = Connector.SelectedImage;
            if (Rule.Visible && !Rule.Locked) {//是否在圆心上
                if (Rule.Contains(p)) {
                    return 1;
                }
            }
            return Drawer.IndexOfLayer(p);
        }

        /// <summary>
        /// 鼠标左键单击
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnMouseDown(object sender, MouseEventArgs e) {
            if (e.Button == MouseButtons.Left) {
                var point = e.Location;
                move_mode = IsSelctImage(point);
                Drawer.CusorLocation = point;
            }
        }

        private void OnMouseClick(object sender, MouseEventArgs e) {
            Hotpot = e.Location;
            if (e.Button == MouseButtons.Left) {
                if (!Drawer.IsSelect("MoveTool")) {
                    Drawer.Brush.Draw(Drawer.CurrentLayer, Hotpot, ImageScale);
                }
            } else if (e.Button == MouseButtons.Right) {
                canvasMenu.Show(box, Hotpot);
            }
        }


        /// <summary>
        /// 鼠标左键单击释放
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnMouseUp(object sender, MouseEventArgs e) {
            if (e.Button == MouseButtons.Left) {
                move_mode = -1;
            }
        }

        /// <summary>
        /// 鼠标左键单击移动
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>

        private void OnMouseMove(object sender, MouseEventArgs e) {
            if (e.Button == MouseButtons.Left && move_mode != -1) {
                var newPoint = e.Location;
                Drawer.Brush.Draw(Drawer.LayerList[move_mode], newPoint, ImageScale);
                Drawer.CusorLocation = e.Location;
                CanvasFlush();
            }
        }



        /// <summary>
        /// 保存贴图
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SaveImage(object sender, EventArgs e) {
            var indexes = Connector.CheckedImageIndices;
            var album = Connector.SelectedFile;
            if (album == null || indexes.Length < 1) {
                return;
            }
            Viewer.Show("saveImage", album, indexes);
        }

        private void SaveSingleImage(object sender, EventArgs e) {
            var album = Connector.SelectedFile;
            var index = Connector.SelectedImageIndex;
            if (album == null || index < 0) {
                return;
            }
            var dialog = new SaveFileDialog();
            dialog.FileName = album.Name.RemoveSuffix();
            dialog.Filter = "png|*.png|bmp|*.bmp|jpg|*.jpg";
            if (dialog.ShowDialog() == DialogResult.OK) {
                Connector.Do("saveImage", album, 0, new int[] { index }, dialog.FileName);
            }
        }

        private void SaveAllImage(object sender, EventArgs e) {
            var album = Connector.SelectedFile;
            if (album == null || album.List.Count < 1) {
                return;
            }
            var indexes = new int[album.List.Count];
            for (var i = 0; i < indexes.Length; i++) {
                indexes[i] = i;
            }
            Viewer.Show("saveImage", album, indexes);
        }


        /// <summary>
        /// 保存为gif
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SaveGif(object sender, EventArgs e) {
            var array = Connector.CheckedImageIndices;
            if (array.Length < 1) {
                return;
            }
            var dialog = new SaveFileDialog();
            var name = Connector.SelectedFile.Name.RemoveSuffix(".");
            dialog.Filter = $"GIF|*.gif";
            dialog.FileName = name;
            if (dialog.ShowDialog() == DialogResult.OK) {
                Connector.Do("saveGif", Connector.SelectedFile, array, dialog.FileName, Config["GifTransparent"].Color, Config["GifDelay"].Integer);
            }
        }


        /// <summary>
        /// 替换贴图
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ReplaceImage(object sender, EventArgs e) {
            var array = Connector.CheckedImages;
            if (array.Length > 0) {
                Viewer.Show("replace");
            }
            CanvasFlush();
        }




        /// <summary>
        /// 将勾选贴图变成链接贴图
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LinkImage(object sender, EventArgs e) {
            var indexes = Connector.CheckedImageIndices;
            if (indexes.Length < 1) {
                return;
            }
            var dialog = new ESTextDialog();
            dialog.CanEmpty = true;
            dialog.Text = Language["LinkImage"];
            if (dialog.Show() == DialogResult.OK) {
                var str = dialog.InputText;
                if (Regex.IsMatch(str, "^\\d")) {
                    Connector.Do("linkImage", Connector.SelectedFile, int.Parse(str), indexes);
                }
                CanvasFlush();
            }
        }

        /// <summary>
        /// 删除贴图
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DeleteImage() {
            var indexes = Connector.CheckedImageIndices;
            var album = Connector.SelectedFile;
            if (album != null && indexes.Length > 0 && MessageBox.Show(Language["DeleteTips"], Language["Tips"], MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK) {
                Connector.Do("deleteImage", album, indexes);
            }
        }
    }

}
