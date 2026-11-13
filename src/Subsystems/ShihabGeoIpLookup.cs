using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Net;
using System.Net.Sockets;

namespace ShihabMehediIDM
{
    public static class ShihabGeoIpLookup
    {
        private const int MaxFlagCacheEntries = 128;
        private static readonly Dictionary<string, Bitmap> FlagBitmapCache = new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);

        public static string LookupCountryCodeForIp(IPAddress ip)
        {
            if (ip == null) return "UN";
            if (IPAddress.IsLoopback(ip)) return "LO";

            byte[] b = ip.GetAddressBytes();
            if (ip.AddressFamily == AddressFamily.InterNetwork && b.Length == 4)
            {
                byte b0 = b[0];
                byte b1 = b[1];
                if (b0 == 10 || (b0 == 172 && b1 >= 16 && b1 <= 31) || (b0 == 192 && b1 == 168) || b0 == 127)
                {
                    return "LO";
                }
                if (b0 == 103)
                {
                    if (b1 >= 100 && b1 <= 125) return "BD";
                    if (b1 < 50) return "SG";
                    return "IN";
                }
                if (b0 == 114 || b0 == 116 || b0 == 118 || b0 == 120 || b0 == 220) return "CN";
                if (b0 == 126 || b0 == 133 || b0 == 153 || b0 == 210) return "JP";
                if (b0 == 112 || b0 == 175 || b0 == 211) return "KR";
                if (b0 == 49 || b0 == 106 || b0 == 117 || b0 == 122) return "IN";
                if (b0 == 1 || b0 == 14 || b0 == 27 || b0 == 58 || b0 == 61) return "AU";
                if (b0 == 2 || b0 == 51 || b0 == 80 || b0 == 82 || b0 == 90) return "FR";
                if (b0 == 5 || b0 == 31 || b0 == 37 || b0 == 46 || b0 == 78 || b0 == 85 || b0 == 88 || b0 == 95) return "DE";
                if (b0 == 77 || b0 == 79 || b0 == 81 || b0 == 86 || b0 == 87) return "GB";
                if (b0 == 91 || b0 == 92 || b0 == 93 || b0 == 94 || b0 == 109 || b0 == 145 || b0 == 185 || b0 == 193 || b0 == 194 || b0 == 195 || b0 == 212 || b0 == 213 || b0 == 217)
                {
                    if (b1 < 64) return "NL";
                    if (b1 < 128) return "DE";
                    if (b1 < 192) return "GB";
                    return "SE";
                }
                if (b0 == 176 || b0 == 178 || b0 == 188) return "NL";
                if (b0 == 177 || b0 == 179 || b0 == 186 || b0 == 187 || b0 == 189 || b0 == 191 || b0 == 200 || b0 == 201) return "BR";
                if (b0 == 196 || b0 == 197 || b0 == 41 || b0 == 102 || b0 == 105) return "ZA";
                if (b0 == 24 || b0 == 70 || b0 == 99 || b0 == 142) return "CA";
                return "US";
            }
            if (ip.AddressFamily == AddressFamily.InterNetworkV6 && b.Length == 16)
            {
                if (b[0] == 0x24) return "SG";
                if (b[0] == 0x2a) return "DE";
                if (b[0] == 0x28) return "BR";
                if (b[0] == 0x2c) return "ZA";
                return "US";
            }
            return "US";
        }

        public static string FormatCountryBadgeText(string countryCode, string ipStr)
        {
            if (string.IsNullOrWhiteSpace(countryCode) && string.IsNullOrWhiteSpace(ipStr))
            {
                return "";
            }
            string cc = string.IsNullOrWhiteSpace(countryCode) ? "UN" : countryCode.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(ipStr))
            {
                return "[" + cc + "]";
            }
            return string.Format("[{0}] {1}", cc, ipStr);
        }

        public static Bitmap GetCountryFlagBitmap(string countryCode)
        {
            string cc = string.IsNullOrWhiteSpace(countryCode) ? "UN" : countryCode.Trim().ToUpperInvariant();
            if (cc.Length > 3) cc = cc.Substring(0, 3);
            lock (FlagBitmapCache)
            {
                Bitmap cached;
                if (FlagBitmapCache.TryGetValue(cc, out cached) && cached != null)
                {
                    return cached;
                }
                Bitmap bmp = new Bitmap(22, 14);
                try
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        g.Clear(Color.FromArgb(30, 41, 59));
                        if (cc == "BD")
                        {
                            g.Clear(Color.FromArgb(0, 106, 78));
                            using (Brush red = new SolidBrush(Color.FromArgb(244, 42, 65)))
                            {
                                g.FillEllipse(red, 6, 3, 8, 8);
                            }
                        }
                        else if (cc == "US")
                        {
                            g.Clear(Color.FromArgb(178, 34, 52));
                            using (Brush white = new SolidBrush(Color.White))
                            {
                                g.FillRectangle(white, 0, 2, 22, 2);
                                g.FillRectangle(white, 0, 6, 22, 2);
                                g.FillRectangle(white, 0, 10, 22, 2);
                            }
                            using (Brush navy = new SolidBrush(Color.FromArgb(60, 59, 110)))
                            {
                                g.FillRectangle(navy, 0, 0, 10, 8);
                            }
                        }
                        else if (cc == "DE")
                        {
                            using (Brush b1 = new SolidBrush(Color.Black)) g.FillRectangle(b1, 0, 0, 22, 5);
                            using (Brush b2 = new SolidBrush(Color.FromArgb(221, 0, 0))) g.FillRectangle(b2, 0, 5, 22, 4);
                            using (Brush b3 = new SolidBrush(Color.FromArgb(255, 206, 0))) g.FillRectangle(b3, 0, 9, 22, 5);
                        }
                        else if (cc == "NL")
                        {
                            using (Brush b1 = new SolidBrush(Color.FromArgb(174, 28, 40))) g.FillRectangle(b1, 0, 0, 22, 5);
                            using (Brush b2 = new SolidBrush(Color.White)) g.FillRectangle(b2, 0, 5, 22, 4);
                            using (Brush b3 = new SolidBrush(Color.FromArgb(33, 70, 139))) g.FillRectangle(b3, 0, 9, 22, 5);
                        }
                        else if (cc == "FR")
                        {
                            using (Brush b1 = new SolidBrush(Color.FromArgb(0, 35, 149))) g.FillRectangle(b1, 0, 0, 7, 14);
                            using (Brush b2 = new SolidBrush(Color.White)) g.FillRectangle(b2, 7, 0, 8, 14);
                            using (Brush b3 = new SolidBrush(Color.FromArgb(237, 41, 57))) g.FillRectangle(b3, 15, 0, 7, 14);
                        }
                        else if (cc == "JP")
                        {
                            g.Clear(Color.White);
                            using (Brush red = new SolidBrush(Color.FromArgb(188, 0, 45)))
                            {
                                g.FillEllipse(red, 7, 3, 8, 8);
                            }
                        }
                        else
                        {
                            using (Font f = new Font("Segoe UI", 6.5f, FontStyle.Bold))
                            using (Brush txt = new SolidBrush(Color.FromArgb(125, 211, 252)))
                            {
                                g.DrawString(cc.Length > 2 ? cc.Substring(0, 2) : cc, f, txt, 2, 1);
                            }
                        }
                        using (Pen border = new Pen(Color.FromArgb(71, 85, 105)))
                        {
                            g.DrawRectangle(border, 0, 0, 21, 13);
                        }
                    }
                    if (FlagBitmapCache.Count < MaxFlagCacheEntries)
                    {
                        FlagBitmapCache[cc] = bmp;
                    }
                    return bmp;
                }
                catch
                {
                    bmp.Dispose();
                    throw;
                }
            }
        }
    }
}

