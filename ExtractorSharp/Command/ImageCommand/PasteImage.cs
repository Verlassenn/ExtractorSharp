﻿using ExtractorSharp.Core;
using ExtractorSharp.Data;
using ExtractorSharp.Json;
using System.Drawing;
using System.IO;
using System.Windows;

namespace ExtractorSharp.Command.ImageCommand {
    public class PasteImage : ICommand {

        private Album Source,Target;

        private int[] Indexes;

        private int Index;

        private Clipboarder Clipboarder;

        public string Name => "PasteImage";

        public bool CanUndo => true;

        public bool IsChanged => true;

        public bool IsFlush => false;


        public void Do(params object[] args) {
            Target = args[0] as Album;
            Index = (int)args[1];
            Clipboarder = Clipboarder.Default;
            var array = new Sprite[0];
            if (Clipboarder != null) {
                Indexes = Clipboarder.Indexes;
                Source = Clipboarder.Album;
                array = new Sprite[Indexes.Length];
                Source.Adjust();
                for (var i = 0; i < array.Length; i++) {
                    array[i] = Source[Indexes[i]].Clone(Target);
                }
                if (Clipboarder.Mode == ClipMode.Cut) {
                    //如果是剪切，清空剪切板
                    Clipboarder.Default = null;
                    Clipboard.Clear();
                    for (var i = 0; i < array.Length; i++) {
                        Source.List.Remove(array[i]);
                    }
                }
                Source.Adjust();
            } else if (Clipboard.ContainsFileDropList()) {
                var collection = Clipboard.GetFileDropList();
                array = new Sprite[collection.Count];
                var builder = new LSBuilder();
                for (var i = 0; i < collection.Count; i++) {
                    if (!File.Exists(collection[i])) {
                        continue;
                    }
                    var image = Image.FromFile(collection[i]) as Bitmap;
                    var json = collection[i].Replace(".png", ".json");
                    if (File.Exists(json)) {
                        var obj = builder.Read(json);
                        array[i] = obj.GetValue(typeof(Sprite)) as Sprite;
                        array[i].Parent = Target;
                        array[i].Picture = image;
                    }
                }
            }
            Target.List.InsertRange(Index, array);
            Target.Adjust();
        }

        public void Redo() {
            Do(Target, Index);
        }

        public void Undo() {
            var array = Target.List.GetRange(Index, Indexes.Length);
            Target.List.RemoveRange(Index, Indexes.Length);
            if (Clipboarder.Mode == ClipMode.Cut) {
                for (var i = 0; i < array.Count; i++) {
                    Source.List.Insert(Indexes[i], array[i]);
                }
            }
            Target.Adjust();
            Source.Adjust();
        }
        
    }
}
