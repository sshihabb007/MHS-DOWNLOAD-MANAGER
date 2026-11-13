using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text;

namespace ShihabMehediIDM
{
    // Real ISO/IEC 18004 Scannable QR Code Encoder (Byte Mode, ECC Level L, full N1-N4 mask penalties)
    public static class ShihabQrCodeEncoder
    {
        private static readonly int[] TotalCodewordsByVer = new int[] { 0, 26, 44, 70, 100, 134, 172, 196, 242, 292, 346 };
        private static readonly int[] EccCodewordsPerBlockL = new int[] { 0, 7, 10, 15, 20, 26, 18, 20, 24, 30, 18 };
        private static readonly int[] Group1BlocksL = new int[] { 0, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2 };
        private static readonly int[] Group2BlocksL = new int[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2 };

        private static readonly int[][] AlignmentCenters = new int[][]
        {
            new int[0],
            new int[0],
            new int[] { 6, 18 },
            new int[] { 6, 22 },
            new int[] { 6, 26 },
            new int[] { 6, 30 },
            new int[] { 6, 34 },
            new int[] { 6, 22, 38 },
            new int[] { 6, 24, 42 },
            new int[] { 6, 26, 46 },
            new int[] { 6, 28, 50 }
        };

        public static bool[,] EncodeText(string text)
        {
            byte[] payload = Encoding.UTF8.GetBytes(text ?? "");
            int version = 0;
            int dataCapacityBytes = 0;
            for (int v = 1; v <= 10; v++)
            {
                int totalBlocks = Group1BlocksL[v] + Group2BlocksL[v];
                int totalDataCw = TotalCodewordsByVer[v] - (EccCodewordsPerBlockL[v] * totalBlocks);
                int headerBytes = v <= 9 ? 2 : 3;
                if (payload.Length <= totalDataCw - headerBytes)
                {
                    version = v;
                    dataCapacityBytes = totalDataCw;
                    break;
                }
            }
            if (version == 0 || dataCapacityBytes == 0)
            {
                throw new ArgumentException("QR payload exceeds maximum Version 10 ECC-L capacity (271 UTF-8 bytes).", "text");
            }

            List<int> bits = new List<int>(dataCapacityBytes * 8);
            AppendBits(bits, 0x4, 4); // Byte mode
            AppendBits(bits, payload.Length, version <= 9 ? 8 : 16);
            for (int i = 0; i < payload.Length; i++) AppendBits(bits, payload[i], 8);

            int maxDataBits = dataCapacityBytes * 8;
            AppendBits(bits, 0, Math.Min(4, maxDataBits - bits.Count));
            while ((bits.Count & 7) != 0) bits.Add(0);

            byte[] dataCw = new byte[dataCapacityBytes];
            int byteCount = bits.Count / 8;
            for (int i = 0; i < byteCount; i++)
            {
                int b = 0;
                for (int j = 0; j < 8; j++) b = (b << 1) | bits[i * 8 + j];
                dataCw[i] = (byte)b;
            }
            for (int i = byteCount; i < dataCapacityBytes; i++)
            {
                dataCw[i] = ((i - byteCount) % 2 == 0) ? (byte)0xEC : (byte)0x11;
            }

            byte[] allCodewords = BuildInterleavedCodewords(version, dataCw);
            int size = version * 4 + 17;
            bool[,] modules = new bool[size, size];
            bool[,] isFunction = new bool[size, size];

            DrawFunctionPatterns(version, size, modules, isFunction);
            PlaceDataCodewords(size, allCodewords, modules, isFunction);

            int bestMask = 0;
            int bestPenalty = int.MaxValue;
            for (int mask = 0; mask < 8; mask++)
            {
                bool[,] trial = (bool[,])modules.Clone();
                ApplyDataMask(size, mask, trial, isFunction);
                DrawFormatBits(size, mask, trial, isFunction);
                int pen = ComputeMaskPenalty(size, trial);
                if (pen < bestPenalty)
                {
                    bestPenalty = pen;
                    bestMask = mask;
                }
            }

            ApplyDataMask(size, bestMask, modules, isFunction);
            DrawFormatBits(size, bestMask, modules, isFunction);
            return modules;
        }

        public static Bitmap RenderBitmap(string text, int pixelSize)
        {
            return RenderBitmap(text, pixelSize, Color.FromArgb(14, 18, 28), Color.White);
        }

        public static Bitmap RenderBitmap(string text, int pixelsPerModule, int quietZoneModules)
        {
            bool[,] matrix = EncodeText(text);
            int modules = matrix.GetLength(0);
            int border = Math.Max(1, quietZoneModules);
            int scale = Math.Max(2, pixelsPerModule);
            int totalMods = modules + border * 2;
            int finalDim = totalMods * scale;

            Bitmap bmp = new Bitmap(finalDim, finalDim);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                g.SmoothingMode = SmoothingMode.None;
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                using (SolidBrush darkBrush = new SolidBrush(Color.FromArgb(14, 18, 28)))
                {
                    for (int y = 0; y < modules; y++)
                    {
                        for (int x = 0; x < modules; x++)
                        {
                            if (matrix[y, x])
                            {
                                g.FillRectangle(darkBrush, (x + border) * scale, (y + border) * scale, scale, scale);
                            }
                        }
                    }
                }
            }
            return bmp;
        }

        public static Bitmap RenderBitmap(string text, int pixelSize, Color darkColor, Color lightColor)
        {
            bool[,] matrix = EncodeText(text);
            int modules = matrix.GetLength(0);
            int border = 4;
            int totalMods = modules + border * 2;
            int scale = Math.Max(3, pixelSize / totalMods);
            int finalDim = totalMods * scale;

            Bitmap bmp = new Bitmap(finalDim, finalDim);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(lightColor);
                g.SmoothingMode = SmoothingMode.None;
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                using (SolidBrush darkBrush = new SolidBrush(darkColor))
                {
                    for (int y = 0; y < modules; y++)
                    {
                        for (int x = 0; x < modules; x++)
                        {
                            if (matrix[y, x])
                            {
                                g.FillRectangle(darkBrush, (x + border) * scale, (y + border) * scale, scale, scale);
                            }
                        }
                    }
                }
            }
            return bmp;
        }

        public static string RenderTerminalUnicode(string text)
        {
            bool[,] matrix = EncodeText(text);
            int n = matrix.GetLength(0);
            int border = 2;
            int total = n + border * 2;
            StringBuilder sb = new StringBuilder();
            for (int y = 0; y < total; y += 2)
            {
                for (int x = 0; x < total; x++)
                {
                    bool topDark = (x >= border && x < border + n && y >= border && y < border + n) && matrix[y - border, x - border];
                    bool botDark = (x >= border && x < border + n && (y + 1) >= border && (y + 1) < border + n) && matrix[(y + 1) - border, x - border];
                    if (!topDark && !botDark) sb.Append('\u2588');
                    else if (topDark && botDark) sb.Append(' ');
                    else if (!topDark && botDark) sb.Append('\u2580');
                    else sb.Append('\u2584');
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private static void AppendBits(List<int> bits, int val, int count)
        {
            for (int i = count - 1; i >= 0; i--)
            {
                bits.Add((val >> i) & 1);
            }
        }

        private static byte[] BuildInterleavedCodewords(int version, byte[] dataCw)
        {
            int g1 = Group1BlocksL[version];
            int g2 = Group2BlocksL[version];
            int totalBlocks = g1 + g2;
            int eccLen = EccCodewordsPerBlockL[version];
            int shortDataLen = dataCw.Length / totalBlocks;
            byte[] rsDiv = ComputeReedSolomonDivisor(eccLen);

            List<byte[]> dataBlocks = new List<byte[]>();
            List<byte[]> eccBlocks = new List<byte[]>();
            int offset = 0;
            for (int b = 0; b < totalBlocks; b++)
            {
                int dLen = shortDataLen + (b < g1 ? 0 : 1);
                byte[] dBlock = new byte[dLen];
                Buffer.BlockCopy(dataCw, offset, dBlock, 0, dLen);
                offset += dLen;
                dataBlocks.Add(dBlock);
                eccBlocks.Add(ComputeReedSolomonRemainder(dBlock, rsDiv));
            }

            List<byte> result = new List<byte>(TotalCodewordsByVer[version]);
            int maxDataLen = shortDataLen + (g2 > 0 ? 1 : 0);
            for (int i = 0; i < maxDataLen; i++)
            {
                for (int b = 0; b < totalBlocks; b++)
                {
                    if (i < dataBlocks[b].Length) result.Add(dataBlocks[b][i]);
                }
            }
            for (int i = 0; i < eccLen; i++)
            {
                for (int b = 0; b < totalBlocks; b++)
                {
                    result.Add(eccBlocks[b][i]);
                }
            }
            return result.ToArray();
        }

        private static byte GfMultiply(byte x, byte y)
        {
            int z = 0;
            for (int i = 7; i >= 0; i--)
            {
                z = (z << 1) ^ ((z >> 7) * 0x11D);
                z ^= ((y >> i) & 1) * x;
            }
            return (byte)z;
        }

        private static byte[] ComputeReedSolomonDivisor(int degree)
        {
            byte[] result = new byte[degree];
            result[degree - 1] = 1;
            byte root = 1;
            for (int i = 0; i < degree; i++)
            {
                for (int j = 0; j < degree; j++)
                {
                    result[j] = GfMultiply(result[j], root);
                    if (j + 1 < degree) result[j] ^= result[j + 1];
                }
                root = GfMultiply(root, 0x02);
            }
            return result;
        }

        private static byte[] ComputeReedSolomonRemainder(byte[] data, byte[] divisor)
        {
            byte[] result = new byte[divisor.Length];
            for (int i = 0; i < data.Length; i++)
            {
                byte factor = (byte)(data[i] ^ result[0]);
                Buffer.BlockCopy(result, 1, result, 0, result.Length - 1);
                result[result.Length - 1] = 0;
                for (int j = 0; j < divisor.Length; j++)
                {
                    result[j] ^= GfMultiply(divisor[j], factor);
                }
            }
            return result;
        }

        private static void SetFuncModule(int size, int x, int y, bool dark, bool[,] modules, bool[,] isFunc)
        {
            if (x < 0 || y < 0 || x >= size || y >= size) return;
            modules[y, x] = dark;
            isFunc[y, x] = true;
        }

        private static void DrawFinderPattern(int size, int cx, int cy, bool[,] modules, bool[,] isFunc)
        {
            for (int dy = -4; dy <= 4; dy++)
            {
                for (int dx = -4; dx <= 4; dx++)
                {
                    int xx = cx + dx;
                    int yy = cy + dy;
                    if (xx < 0 || yy < 0 || xx >= size || yy >= size) continue;
                    int dist = Math.Max(Math.Abs(dx), Math.Abs(dy));
                    SetFuncModule(size, xx, yy, dist != 2 && dist != 4, modules, isFunc);
                }
            }
        }

        private static void DrawAlignmentPattern(int size, int cx, int cy, bool[,] modules, bool[,] isFunc)
        {
            for (int dy = -2; dy <= 2; dy++)
            {
                for (int dx = -2; dx <= 2; dx++)
                {
                    int dist = Math.Max(Math.Abs(dx), Math.Abs(dy));
                    SetFuncModule(size, cx + dx, cy + dy, dist != 1, modules, isFunc);
                }
            }
        }

        private static void DrawFunctionPatterns(int version, int size, bool[,] modules, bool[,] isFunc)
        {
            for (int i = 0; i < size; i++)
            {
                SetFuncModule(size, 6, i, i % 2 == 0, modules, isFunc);
                SetFuncModule(size, i, 6, i % 2 == 0, modules, isFunc);
            }
            DrawFinderPattern(size, 3, 3, modules, isFunc);
            DrawFinderPattern(size, size - 4, 3, modules, isFunc);
            DrawFinderPattern(size, 3, size - 4, modules, isFunc);

            int[] align = AlignmentCenters[version];
            for (int i = 0; i < align.Length; i++)
            {
                for (int j = 0; j < align.Length; j++)
                {
                    if ((i == 0 && j == 0) || (i == 0 && j == align.Length - 1) || (i == align.Length - 1 && j == 0))
                    {
                        continue;
                    }
                    DrawAlignmentPattern(size, align[j], align[i], modules, isFunc);
                }
            }

            DrawFormatBits(size, 0, modules, isFunc);
            if (version >= 7)
            {
                int rem = version;
                for (int i = 0; i < 12; i++) rem = (rem << 1) ^ ((rem >> 11) * 0x1F25);
                int verBits = (version << 12) | rem;
                for (int i = 0; i < 18; i++)
                {
                    bool bit = ((verBits >> i) & 1) != 0;
                    int a = size - 11 + (i % 3);
                    int b = i / 3;
                    SetFuncModule(size, a, b, bit, modules, isFunc);
                    SetFuncModule(size, b, a, bit, modules, isFunc);
                }
            }
        }

        private static void DrawFormatBits(int size, int mask, bool[,] modules, bool[,] isFunc)
        {
            int data = (0x01 << 3) | mask;
            int rem = data;
            for (int i = 0; i < 10; i++) rem = (rem << 1) ^ ((rem >> 9) * 0x537);
            int bits = ((data << 10) | rem) ^ 0x5412;

            for (int i = 0; i <= 5; i++) SetFuncModule(size, 8, i, ((bits >> i) & 1) != 0, modules, isFunc);
            SetFuncModule(size, 8, 7, ((bits >> 6) & 1) != 0, modules, isFunc);
            SetFuncModule(size, 8, 8, ((bits >> 7) & 1) != 0, modules, isFunc);
            SetFuncModule(size, 7, 8, ((bits >> 8) & 1) != 0, modules, isFunc);
            for (int i = 9; i < 15; i++) SetFuncModule(size, 14 - i, 8, ((bits >> i) & 1) != 0, modules, isFunc);

            for (int i = 0; i < 8; i++) SetFuncModule(size, size - 1 - i, 8, ((bits >> i) & 1) != 0, modules, isFunc);
            for (int i = 8; i < 15; i++) SetFuncModule(size, 8, size - 15 + i, ((bits >> i) & 1) != 0, modules, isFunc);
            SetFuncModule(size, 8, size - 8, true, modules, isFunc);
        }

        private static void PlaceDataCodewords(int size, byte[] data, bool[,] modules, bool[,] isFunc)
        {
            int bitIdx = 0;
            int totalBits = data.Length * 8;
            for (int right = size - 1; right >= 1; right -= 2)
            {
                if (right == 6) right = 5;
                for (int vert = 0; vert < size; vert++)
                {
                    for (int j = 0; j < 2; j++)
                    {
                        int x = right - j;
                        bool upward = ((right + 1) & 2) == 0;
                        int y = upward ? (size - 1 - vert) : vert;
                        if (!isFunc[y, x] && bitIdx < totalBits)
                        {
                            modules[y, x] = ((data[bitIdx >> 3] >> (7 - (bitIdx & 7))) & 1) != 0;
                            bitIdx++;
                        }
                    }
                }
            }
        }

        private static void ApplyDataMask(int size, int mask, bool[,] modules, bool[,] isFunc)
        {
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (isFunc[y, x]) continue;
                    bool invert = false;
                    switch (mask)
                    {
                        case 0: invert = (x + y) % 2 == 0; break;
                        case 1: invert = y % 2 == 0; break;
                        case 2: invert = x % 3 == 0; break;
                        case 3: invert = (x + y) % 3 == 0; break;
                        case 4: invert = ((x / 3) + (y / 2)) % 2 == 0; break;
                        case 5: invert = (x * y) % 2 + (x * y) % 3 == 0; break;
                        case 6: invert = ((x * y) % 2 + (x * y) % 3) % 2 == 0; break;
                        case 7: invert = ((x + y) % 2 + (x * y) % 3) % 2 == 0; break;
                    }
                    if (invert) modules[y, x] = !modules[y, x];
                }
            }
        }

        private static int CountFinderLikePatterns(int size, bool[,] m, bool rowScan)
        {
            int matches = 0;
            for (int a = 0; a < size; a++)
            {
                for (int b = 0; b <= size - 7; b++)
                {
                    bool c0 = rowScan ? m[a, b] : m[b, a];
                    bool c1 = rowScan ? m[a, b + 1] : m[b + 1, a];
                    bool c2 = rowScan ? m[a, b + 2] : m[b + 2, a];
                    bool c3 = rowScan ? m[a, b + 3] : m[b + 3, a];
                    bool c4 = rowScan ? m[a, b + 4] : m[b + 4, a];
                    bool c5 = rowScan ? m[a, b + 5] : m[b + 5, a];
                    bool c6 = rowScan ? m[a, b + 6] : m[b + 6, a];
                    if (c0 && !c1 && c2 && c3 && c4 && !c5 && c6)
                    {
                        bool leftWhite = b >= 4;
                        if (leftWhite)
                        {
                            for (int k = 1; k <= 4; k++)
                            {
                                bool w = rowScan ? m[a, b - k] : m[b - k, a];
                                if (w) { leftWhite = false; break; }
                            }
                        }
                        bool rightWhite = b + 10 < size;
                        if (rightWhite)
                        {
                            for (int k = 7; k <= 10; k++)
                            {
                                bool w = rowScan ? m[a, b + k] : m[b + k, a];
                                if (w) { rightWhite = false; break; }
                            }
                        }
                        if (leftWhite || rightWhite) matches++;
                    }
                }
            }
            return matches;
        }

        private static int ComputeMaskPenalty(int size, bool[,] m)
        {
            int penalty = 0;
            int darkCount = 0;

            for (int y = 0; y < size; y++)
            {
                if (m[y, 0]) darkCount++;
                int run = 1;
                for (int x = 1; x < size; x++)
                {
                    if (m[y, x]) darkCount++;
                    if (m[y, x] == m[y, x - 1])
                    {
                        run++;
                        if (run == 5) penalty += 3;
                        else if (run > 5) penalty++;
                    }
                    else run = 1;
                }
            }

            for (int x = 0; x < size; x++)
            {
                int run = 1;
                for (int y = 1; y < size; y++)
                {
                    if (m[y, x] == m[y - 1, x])
                    {
                        run++;
                        if (run == 5) penalty += 3;
                        else if (run > 5) penalty++;
                    }
                    else run = 1;
                }
            }

            for (int y = 0; y < size - 1; y++)
            {
                for (int x = 0; x < size - 1; x++)
                {
                    bool c = m[y, x];
                    if (c == m[y, x + 1] && c == m[y + 1, x] && c == m[y + 1, x + 1]) penalty += 3;
                }
            }

            penalty += (CountFinderLikePatterns(size, m, true) + CountFinderLikePatterns(size, m, false)) * 40;

            int total = size * size;
            int k = Math.Abs(darkCount * 20 - total * 10) / total;
            penalty += k * 10;
            return penalty;
        }
    }
}

