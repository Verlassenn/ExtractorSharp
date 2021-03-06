﻿using ICSharpCode.SharpZipLib.BZip2;
using System;
using System.IO;
using System.Linq;

namespace ExtractorSharp.Core.Lib {
    public static class Spks {
        private static byte[] HEADER = { 0x42, 0x5a, 0x68, 0x39, 0x31, 0x41, 0x59, 0x26, 0x53, 0x59 };
        private static byte[] MARK = { 0x00, 0x00, 0x00, 0x00, 0x00, 0x10, 0x0e, 0x00, 0xff, 0xff, 0xff, 0xff, 0xff, 0xef, 0xf1, 0xff };
        private static byte[] TAIL = { 0x01, 0x00, 0x00, 0x00 };


        public static byte[] Decompress(byte[] bs) {
            using (var ms = new MemoryStream(bs)) {
                return Decompress(ms);
            }
        }

        /// <summary>
        /// 解压spk
        /// <see href="https://musoucrow.github.io/2017/07/21/spk_analysis/"/>
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        public static byte[] Decompress(Stream stream) {
            stream.Seek(272);
            stream.ReadToEnd(out byte[] content);
            var parts = content.Split(HEADER);
            using (var ms = new MemoryStream()) {
                for (var i = 1; i < parts.Length; i++) {
                    var list = parts[i].Split(MARK);
                    var data = HEADER.Concat(list[0]);
                    using (var ts = new MemoryStream(data)) {
                        BZip2.Decompress(ts, ms, false);
                    }
                    if (list.Length > 1) {
                        for (var j = 1; j < list.Length - 1; j++) {
                            ms.Write(list[j].Sub(32));
                        }
                        var last = list.Last();
                        var pos = last.LastIndexOf(TAIL);
                        if (pos > -1) {
                            ms.Write(last.Sub(32, pos + 1));
                        }
                    }
                }
                return ms.ToArray();
            }
        }

        public static int LastIndexOf(this byte[] data, byte[] pattern) {
            var last = data.Length - 1;
            for (var i = data.Length - 1; i > 0; i--) {
                var j = i;
                while (data[j] == pattern[j - i]) {
                    j++;
                }
                if (j - i == pattern.Length) {
                    last = j;
                    break;
                }
                i = j;
            }
            return last;
        }

        public static byte[] Sub(this byte[] array, int start) {
            return Sub(array, start, array.Length - start);
        }

        public static byte[] Sub(this byte[] array, int start, int length) {
            var newArray = new byte[length];
            Buffer.BlockCopy(array, start, newArray, 0, length);
            return newArray;
        }


    }
}
