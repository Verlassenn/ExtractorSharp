﻿using ExtractorSharp.Core.Lib;
using ExtractorSharp.Data;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace ExtractorSharp.Handle {
    public class FirstHandler :Handler{
        public FirstHandler(Album Album) : base(Album) { }

        public override Bitmap ConvertToBitmap(Sprite entity) {
            var data = entity.Data;
            var type = entity.Type;
            var size = entity.Width * entity.Height * (type == ColorBits.ARGB_8888 ? 4 : 2);
            if (entity.Compress == Compress.ZLIB) {
                data = Zlib.Decompress(data, size);
            }
            using (var ms = new MemoryStream(data)) {
                data = new byte[entity.Size.Width * entity.Size.Height * 4];
                for (var i = 0; i < data.Length; i += 4) {
                    var temp = Colors.ReadColor(ms, type);
                    temp.CopyTo(data, i);
                }
            }
            return Bitmaps.FromArray(data, entity.Size);
        }

        public override byte[] ConvertToByte(Sprite entity) {
            using (var ms = new MemoryStream()) {
                Npks.WriteImage(ms, entity);
                return ms.ToArray();
            }
        }

        public override void NewImage(int count, ColorBits type, int index) {
            var array = new Sprite[count];
            if (count < 1) {
                return;
            }
            array[0] = new Sprite(Album);
            array[0].Index = index;
            if (type != ColorBits.LINK) {
                array[0].Type = type;
            }
            for (var i = 1; i < count; i++) {
                array[i] = new Sprite(Album);
                array[i].Type = type;
                if (type == ColorBits.LINK)
                    array[i].Target = array[0];
                array[i].Index = index + i;
            }
            if (index < Album.List.Count && index > 0) {
                Album.List.InsertRange(index, array);
            } else {
                Album.List.AddRange(array);
            }
        }

        public override byte[] AdjustData() {
            using (var ms = new MemoryStream()) {
                foreach (var entity in Album.List) {
                    ms.WriteInt((int)entity.Type);
                    if (entity.Type == ColorBits.LINK) {
                        ms.WriteInt(entity.Target.Index);
                        continue;
                    }
                    ms.WriteInt((int)entity.Compress);
                    ms.WriteInt(entity.Size.Width);
                    ms.WriteInt(entity.Size.Height);
                    ms.WriteInt(entity.Length);
                    ms.WriteInt(entity.Location.X);
                    ms.WriteInt(entity.Location.Y);
                    ms.WriteInt(entity.Canvas_Size.Width);
                    ms.WriteInt(entity.Canvas_Size.Height);
                    ms.Write(entity.Data);
                }
                Album.Info_Length = ms.Length;
                return ms.ToArray();
            }
        }
        
        public override void CreateFromStream(Stream stream) {
            var dic = new Dictionary<Sprite, int>();
            long pos = stream.Position + Album.Info_Length;
            for (var i = 0; i < Album.Count; i++) {
                var image = new Sprite(Album);
                image.Index = Album.List.Count;
                image.Type = (ColorBits)stream.ReadInt();
                Album.List.Add(image);
                if (image.Type == ColorBits.LINK) {
                    dic.Add(image, stream.ReadInt());
                    continue;
                }
                image.Compress = (Compress)stream.ReadInt();
                image.Width = stream.ReadInt();
                image.Height = stream.ReadInt();
                image.Length = stream.ReadInt();
                image.X = stream.ReadInt();
                image.Y = stream.ReadInt();
                image.Canvas_Width = stream.ReadInt();
                image.Canvas_Height = stream.ReadInt();
                if (image.Compress == Compress.NONE) {
                    image.Length = image.Size.Width * image.Size.Height * (image.Type == ColorBits.ARGB_8888 ? 4 : 2);
                }
                var data = new byte[image.Length];
                stream.Read(data);
                image.Data = data;
            }
            foreach (var image in Album.List) {
                if (image.Type == ColorBits.LINK) {
                    if (dic.ContainsKey(image) && dic[image] > -1 && dic[image] < Album.List.Count && dic[image] != image.Index) {
                        image.Target = Album.List[dic[image]];
                        image.Size = image.Target.Size;
                        image.Canvas_Size = image.Target.Canvas_Size;
                        image.Location = image.Target.Location;
                    } else {
                        Album.List.Clear();
                        return;
                    }
                }
            }
        }
        
    }
}
