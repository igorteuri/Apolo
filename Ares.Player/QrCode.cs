/*
 Apolo - QR code generator (byte mode, error correction level M, versions 1-10).
 Written for Apolo following the QR Code (ISO/IEC 18004) specification.
*/
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace Ares.Player
{
    internal static class QrCode
    {
        // error correction level M: codewords per block and number of blocks, versions 1..10
        private static readonly int[] EccPerBlock = { 10, 16, 26, 18, 24, 16, 18, 22, 22, 26 };
        private static readonly int[] NumBlocks = { 1, 1, 1, 2, 2, 4, 4, 4, 5, 5 };

        private static int RawModules(int ver)
        {
            int result = (16 * ver + 128) * ver + 64;
            if (ver >= 2)
            {
                int numAlign = ver / 7 + 2;
                result -= (25 * numAlign - 10) * numAlign - 55;
                if (ver >= 7)
                    result -= 36;
            }
            return result;
        }

        private static int DataCodewords(int ver)
        {
            return RawModules(ver) / 8 - EccPerBlock[ver - 1] * NumBlocks[ver - 1];
        }

        /// <summary>Returns the module matrix (true = dark), or null if the text is too long.</summary>
        public static bool[,] Encode(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            int ver = 0;
            for (int v = 1; v <= 10; v++)
            {
                int countBits = v < 10 ? 8 : 16;
                if (4 + countBits + bytes.Length * 8 <= DataCodewords(v) * 8)
                {
                    ver = v;
                    break;
                }
            }
            if (ver == 0)
                return null;

            // data bits
            List<bool> bits = new List<bool>();
            AppendBits(bits, 4, 4); // byte mode
            AppendBits(bits, bytes.Length, ver < 10 ? 8 : 16);
            foreach (byte b in bytes)
                AppendBits(bits, b, 8);
            int capacityBits = DataCodewords(ver) * 8;
            AppendBits(bits, 0, Math.Min(4, capacityBits - bits.Count));
            AppendBits(bits, 0, (8 - bits.Count % 8) % 8);
            for (int pad = 0xEC; bits.Count < capacityBits; pad ^= 0xEC ^ 0x11)
                AppendBits(bits, pad, 8);
            byte[] data = new byte[bits.Count / 8];
            for (int i = 0; i < bits.Count; i++)
                if (bits[i])
                    data[i >> 3] |= (byte)(0x80 >> (i & 7));

            byte[] all = AddEccAndInterleave(data, ver);
            int size = ver * 4 + 17;
            bool[,] modules = new bool[size, size];
            bool[,] isFunction = new bool[size, size];
            DrawFunctionPatterns(modules, isFunction, ver);
            DrawCodewords(modules, isFunction, all);

            int bestMask = 0;
            int bestPenalty = int.MaxValue;
            for (int mask = 0; mask < 8; mask++)
            {
                ApplyMask(modules, isFunction, mask);
                DrawFormatBits(modules, isFunction, mask);
                int penalty = Penalty(modules);
                if (penalty < bestPenalty)
                {
                    bestPenalty = penalty;
                    bestMask = mask;
                }
                ApplyMask(modules, isFunction, mask); // undo (xor)
            }
            ApplyMask(modules, isFunction, bestMask);
            DrawFormatBits(modules, isFunction, bestMask);
            return modules;
        }

        /// <summary>Draws the QR code (black on white, with the required quiet zone).</summary>
        public static Bitmap Render(string text, int moduleSize)
        {
            bool[,] m = Encode(text);
            if (m == null)
                return null;
            int n = m.GetLength(0);
            int quiet = 4;
            int px = (n + 2 * quiet) * moduleSize;
            Bitmap bmp = new Bitmap(px, px);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                        if (m[y, x])
                            g.FillRectangle(Brushes.Black, (x + quiet) * moduleSize, (y + quiet) * moduleSize, moduleSize, moduleSize);
            }
            return bmp;
        }

        private static void AppendBits(List<bool> bits, int value, int count)
        {
            for (int i = count - 1; i >= 0; i--)
                bits.Add(((value >> i) & 1) != 0);
        }

        // ---- Reed-Solomon over GF(256) / 0x11D ----

        private static int Multiply(int x, int y)
        {
            int z = 0;
            for (int i = 7; i >= 0; i--)
            {
                z = (z << 1) ^ ((z >> 7) * 0x11D);
                z ^= ((y >> i) & 1) * x;
            }
            return z & 0xFF;
        }

        private static byte[] Divisor(int degree)
        {
            byte[] result = new byte[degree];
            result[degree - 1] = 1;
            int root = 1;
            for (int i = 0; i < degree; i++)
            {
                for (int j = 0; j < degree; j++)
                {
                    result[j] = (byte)Multiply(result[j], root);
                    if (j + 1 < degree)
                        result[j] ^= result[j + 1];
                }
                root = Multiply(root, 0x02);
            }
            return result;
        }

        private static byte[] Remainder(byte[] data, int dataLen, byte[] divisor)
        {
            byte[] result = new byte[divisor.Length];
            for (int k = 0; k < dataLen; k++)
            {
                int factor = data[k] ^ result[0];
                Array.Copy(result, 1, result, 0, result.Length - 1);
                result[result.Length - 1] = 0;
                for (int i = 0; i < result.Length; i++)
                    result[i] ^= (byte)Multiply(divisor[i], factor);
            }
            return result;
        }

        private static byte[] AddEccAndInterleave(byte[] data, int ver)
        {
            int numBlocks = NumBlocks[ver - 1];
            int blockEccLen = EccPerBlock[ver - 1];
            int rawCodewords = RawModules(ver) / 8;
            int numShortBlocks = numBlocks - rawCodewords % numBlocks;
            int shortBlockLen = rawCodewords / numBlocks;
            byte[][] blocks = new byte[numBlocks][];
            byte[] divisor = Divisor(blockEccLen);
            for (int i = 0, k = 0; i < numBlocks; i++)
            {
                int datLen = shortBlockLen - blockEccLen + (i < numShortBlocks ? 0 : 1);
                byte[] dat = new byte[datLen];
                Array.Copy(data, k, dat, 0, datLen);
                k += datLen;
                byte[] block = new byte[shortBlockLen + 1];
                Array.Copy(dat, block, datLen);
                byte[] ecc = Remainder(dat, datLen, divisor);
                Array.Copy(ecc, 0, block, block.Length - blockEccLen, ecc.Length);
                blocks[i] = block;
            }
            byte[] result = new byte[rawCodewords];
            int pos = 0;
            for (int i = 0; i < blocks[0].Length; i++)
            {
                for (int j = 0; j < blocks.Length; j++)
                {
                    if (i != shortBlockLen - blockEccLen || j >= numShortBlocks)
                        result[pos++] = blocks[j][i];
                }
            }
            return result;
        }

        // ---- layout ----

        private static void SetFunction(bool[,] modules, bool[,] isFunction, int x, int y, bool dark)
        {
            modules[y, x] = dark;
            isFunction[y, x] = true;
        }

        private static void DrawFunctionPatterns(bool[,] modules, bool[,] isFunction, int ver)
        {
            int size = modules.GetLength(0);
            for (int i = 0; i < size; i++)
            {
                SetFunction(modules, isFunction, 6, i, i % 2 == 0);
                SetFunction(modules, isFunction, i, 6, i % 2 == 0);
            }
            DrawFinder(modules, isFunction, 3, 3);
            DrawFinder(modules, isFunction, size - 4, 3);
            DrawFinder(modules, isFunction, 3, size - 4);

            int[] align = AlignmentPositions(ver, size);
            int numAlign = align.Length;
            for (int i = 0; i < numAlign; i++)
            {
                for (int j = 0; j < numAlign; j++)
                {
                    if ((i == 0 && j == 0) || (i == 0 && j == numAlign - 1) || (i == numAlign - 1 && j == 0))
                        continue;
                    DrawAlignment(modules, isFunction, align[i], align[j]);
                }
            }
            DrawFormatBits(modules, isFunction, 0); // reserves the area
            DrawVersion(modules, isFunction, ver, size);
        }

        private static int[] AlignmentPositions(int ver, int size)
        {
            if (ver == 1)
                return new int[0];
            int numAlign = ver / 7 + 2;
            int step = (ver * 4 + numAlign * 2 + 1) / (numAlign * 2 - 2) * 2;
            int[] result = new int[numAlign];
            result[0] = 6;
            for (int i = numAlign - 1, pos = size - 7; i >= 1; i--, pos -= step)
                result[i] = pos;
            return result;
        }

        private static void DrawFinder(bool[,] modules, bool[,] isFunction, int cx, int cy)
        {
            int size = modules.GetLength(0);
            for (int dy = -4; dy <= 4; dy++)
            {
                for (int dx = -4; dx <= 4; dx++)
                {
                    int dist = Math.Max(Math.Abs(dx), Math.Abs(dy));
                    int x = cx + dx, y = cy + dy;
                    if (x >= 0 && x < size && y >= 0 && y < size)
                        SetFunction(modules, isFunction, x, y, dist != 2 && dist != 4);
                }
            }
        }

        private static void DrawAlignment(bool[,] modules, bool[,] isFunction, int cx, int cy)
        {
            for (int dy = -2; dy <= 2; dy++)
                for (int dx = -2; dx <= 2; dx++)
                    SetFunction(modules, isFunction, cx + dx, cy + dy, Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1);
        }

        private static bool Bit(int value, int i)
        {
            return ((value >> i) & 1) != 0;
        }

        private static void DrawVersion(bool[,] modules, bool[,] isFunction, int ver, int size)
        {
            if (ver < 7)
                return;
            int rem = ver;
            for (int i = 0; i < 12; i++)
                rem = (rem << 1) ^ ((rem >> 11) * 0x1F25);
            int bits = ver << 12 | rem;
            for (int i = 0; i < 18; i++)
            {
                bool bit = Bit(bits, i);
                int a = size - 11 + i % 3, b = i / 3;
                SetFunction(modules, isFunction, a, b, bit);
                SetFunction(modules, isFunction, b, a, bit);
            }
        }

        private static void DrawFormatBits(bool[,] modules, bool[,] isFunction, int mask)
        {
            int size = modules.GetLength(0);
            int data = (0 << 3) | mask; // level M = 0
            int rem = data;
            for (int i = 0; i < 10; i++)
                rem = (rem << 1) ^ ((rem >> 9) * 0x537);
            int bits = (data << 10 | rem) ^ 0x5412;
            for (int i = 0; i <= 5; i++)
                SetFunction(modules, isFunction, 8, i, Bit(bits, i));
            SetFunction(modules, isFunction, 8, 7, Bit(bits, 6));
            SetFunction(modules, isFunction, 8, 8, Bit(bits, 7));
            SetFunction(modules, isFunction, 7, 8, Bit(bits, 8));
            for (int i = 9; i < 15; i++)
                SetFunction(modules, isFunction, 14 - i, 8, Bit(bits, i));
            for (int i = 0; i < 8; i++)
                SetFunction(modules, isFunction, size - 1 - i, 8, Bit(bits, i));
            for (int i = 8; i < 15; i++)
                SetFunction(modules, isFunction, 8, size - 15 + i, Bit(bits, i));
            SetFunction(modules, isFunction, 8, size - 8, true);
        }

        private static void DrawCodewords(bool[,] modules, bool[,] isFunction, byte[] data)
        {
            int size = modules.GetLength(0);
            int i = 0;
            for (int right = size - 1; right >= 1; right -= 2)
            {
                if (right == 6)
                    right = 5;
                for (int vert = 0; vert < size; vert++)
                {
                    for (int j = 0; j < 2; j++)
                    {
                        int x = right - j;
                        bool upward = ((right + 1) & 2) == 0;
                        int y = upward ? size - 1 - vert : vert;
                        if (!isFunction[y, x] && i < data.Length * 8)
                        {
                            modules[y, x] = Bit(data[i >> 3], 7 - (i & 7));
                            i++;
                        }
                    }
                }
            }
        }

        private static void ApplyMask(bool[,] modules, bool[,] isFunction, int mask)
        {
            int size = modules.GetLength(0);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool invert;
                    switch (mask)
                    {
                        case 0: invert = (x + y) % 2 == 0; break;
                        case 1: invert = y % 2 == 0; break;
                        case 2: invert = x % 3 == 0; break;
                        case 3: invert = (x + y) % 3 == 0; break;
                        case 4: invert = (x / 3 + y / 2) % 2 == 0; break;
                        case 5: invert = x * y % 2 + x * y % 3 == 0; break;
                        case 6: invert = (x * y % 2 + x * y % 3) % 2 == 0; break;
                        default: invert = ((x + y) % 2 + x * y % 3) % 2 == 0; break;
                    }
                    if (!isFunction[y, x] && invert)
                        modules[y, x] = !modules[y, x];
                }
            }
        }

        private static int Penalty(bool[,] m)
        {
            int size = m.GetLength(0);
            int result = 0;
            // rule 1: runs of 5 or more, rule 3: finder-like pattern
            for (int dir = 0; dir < 2; dir++)
            {
                for (int a = 0; a < size; a++)
                {
                    int run = 1;
                    for (int b = 1; b < size; b++)
                    {
                        bool cur = dir == 0 ? m[a, b] : m[b, a];
                        bool prev = dir == 0 ? m[a, b - 1] : m[b - 1, a];
                        if (cur == prev)
                        {
                            run++;
                            if (run == 5)
                                result += 3;
                            else if (run > 5)
                                result++;
                        }
                        else
                            run = 1;
                    }
                    for (int b = 0; b + 6 < size; b++)
                    {
                        bool p0 = dir == 0 ? m[a, b] : m[b, a];
                        bool p1 = dir == 0 ? m[a, b + 1] : m[b + 1, a];
                        bool p2 = dir == 0 ? m[a, b + 2] : m[b + 2, a];
                        bool p3 = dir == 0 ? m[a, b + 3] : m[b + 3, a];
                        bool p4 = dir == 0 ? m[a, b + 4] : m[b + 4, a];
                        bool p5 = dir == 0 ? m[a, b + 5] : m[b + 5, a];
                        bool p6 = dir == 0 ? m[a, b + 6] : m[b + 6, a];
                        if (p0 && !p1 && p2 && p3 && p4 && !p5 && p6)
                            result += 40;
                    }
                }
            }
            // rule 2: 2x2 blocks
            for (int y = 0; y < size - 1; y++)
                for (int x = 0; x < size - 1; x++)
                    if (m[y, x] == m[y, x + 1] && m[y, x] == m[y + 1, x] && m[y, x] == m[y + 1, x + 1])
                        result += 3;
            // rule 4: balance
            int dark = 0;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    if (m[y, x])
                        dark++;
            int total = size * size;
            int k = (Math.Abs(dark * 20 - total * 10) + total - 1) / total - 1;
            result += Math.Max(0, k) * 10;
            return result;
        }
    }
}
