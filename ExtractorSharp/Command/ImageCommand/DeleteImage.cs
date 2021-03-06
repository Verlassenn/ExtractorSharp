﻿using ExtractorSharp.Core;
using ExtractorSharp.Data;

namespace ExtractorSharp.Command.ImageCommand {
    /// <summary>
    /// 删除贴图
    /// </summary>
    class DeleteImage : ISingleAction,ICommandMessage{

        private Album Album;

        private Sprite[] Array;

        public int[] Indices { set; get; }

        public string Name => "DeleteImage";

        public void Do(params object[] args) {
            Album = args[0] as Album;
            Indices = args[1] as int[];
            Array = new Sprite[Indices.Length];
            for (var i = 0; i < Indices.Length; i++) {
                if (Indices[i] > Album.List.Count - 1 || Indices[i] < 0) {
                    continue;
                }
                Array[i] = Album.List[Indices[i]];
            }
            foreach (var entity in Array) {
                if (entity != null) {
                    var frist = Album.List.Find(item => item.Target == entity);
                    if (frist != null) {
                        Album.List[frist.Index] = entity;
                    }
                    Album.List.RemoveAt(entity.Index);
                }
            }
            Album.AdjustIndex();
        }

        public void Redo() => Do( Album, Indices);


        public void Undo() {
            for (var i = 0; i < Indices.Length; i++) {
                var entity = Array[i];
                if (Indices[i] < Album.List.Count) {
                    Album.List.Insert(Indices[i], entity);
                } else {
                    entity.Index = Album.List.Count;
                    Album.List.Add(entity);
                }
            }
            if (Array.Length > 0) {
                Album.AdjustIndex();
            }
        }

        public void Action(Album Album, int[] indexes) {
            var array = new Sprite[indexes.Length];
            for (int i = 0; i < array.Length; i++) {
                if (indexes[i] < Album.List.Count && indexes[i] > -1) {
                    array[i] = Album.List[indexes[i]];
                }
            }
            foreach (var entity in array) {
                Album.List.Remove(entity);
            }
            Album.AdjustIndex();//校正索引
        }
        

        public bool CanUndo => true;

        public bool IsChanged => true;

        public bool IsFlush => false;
    }
}
