using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml;
using ShihabMehediShared;

namespace ShihabMehediIDM
{
    public class ShihabRssEntryItem
    {
        public string GuidId { get; set; }
        public string Title { get; set; }
        public string DownloadUrl { get; set; }
        public string PublishedDate { get; set; }
        public long SizeBytes { get; set; }
    }

    public class ShihabRssItemEntry : ShihabRssEntryItem
    {
    }

    public static class ShihabRssFeedManager
    {
        private const int MaxFeedBytes = 5 * 1024 * 1024; // 5 MiB XML cap
        private const int MaxFeedRedirects = 5;
        private static readonly TimeSpan RssRegexTimeout = TimeSpan.FromMilliseconds(250);

        private static readonly Regex SeasonEpisodeRegex = new Regex(
            @"\bS([0-9]{1,2})E([0-9]{1,3})\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RssRegexTimeout);
        private static readonly Regex AltSeasonEpisodeRegex = new Regex(
            @"\b([0-9]{1,2})x([0-9]{2,3})\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RssRegexTimeout);
        private static readonly Regex SeriesPrefixSanitizeRegex = new Regex(
            @"[^a-zA-Z0-9]+",
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RssRegexTimeout);

        public static List<ShihabRssItemEntry> FetchFeedItems(string feedUrl, string userAgent)
        {
            List<ShihabRssItemEntry> res = new List<ShihabRssItemEntry>();
            try
            {
                string title;
                List<ShihabRssEntryItem> raw = FetchAndParseFeed(feedUrl, null, out title);
                for (int i = 0; i < raw.Count; i++)
                {
                    res.Add(new ShihabRssItemEntry
                    {
                        GuidId = raw[i].GuidId,
                        Title = raw[i].Title,
                        DownloadUrl = raw[i].DownloadUrl,
                        PublishedDate = raw[i].PublishedDate,
                        SizeBytes = raw[i].SizeBytes
                    });
                }
            }
            catch { }
            return res;
        }

        public static List<ShihabRssEntryItem> FetchAndParseFeed(string feedUrl, out string feedTitle)
        {
            return FetchAndParseFeed(feedUrl, null, out feedTitle);
        }

        public static List<ShihabRssEntryItem> FetchAndParseFeed(string feedUrl, MehediAppConfiguration cfg, out string feedTitle)
        {
            feedTitle = "RSS Feed";
            List<ShihabRssEntryItem> items = new List<ShihabRssEntryItem>();
            if (string.IsNullOrWhiteSpace(feedUrl)) return items;

            byte[] xmlBytes = DownloadValidatedFeedBytes(feedUrl.Trim(), cfg);
            if (xmlBytes == null || xmlBytes.Length == 0) return items;

            XmlReaderSettings settings = new XmlReaderSettings();
            settings.DtdProcessing = DtdProcessing.Prohibit;
            settings.XmlResolver = null;
            settings.MaxCharactersFromEntities = 1024;
            settings.MaxCharactersInDocument = MaxFeedBytes;
            settings.IgnoreComments = true;
            settings.IgnoreProcessingInstructions = true;

            XmlDocument doc = new XmlDocument();
            doc.XmlResolver = null;
            using (MemoryStream xmlMs = new MemoryStream(xmlBytes))
            using (XmlReader xr = XmlReader.Create(xmlMs, settings))
            {
                doc.Load(xr);
            }

            XmlNode chTitle = doc.SelectSingleNode("//*[local-name()='channel']/*[local-name()='title']")
                           ?? doc.SelectSingleNode("//*[local-name()='feed']/*[local-name()='title']");
            if (chTitle != null && !string.IsNullOrWhiteSpace(chTitle.InnerText))
            {
                feedTitle = chTitle.InnerText.Trim();
            }

            // RSS 2.0 & RSS 1.0 (RDF) <item>
            XmlNodeList rssItems = doc.SelectNodes("//*[local-name()='item']");
            if (rssItems != null && rssItems.Count > 0)
            {
                for (int i = 0; i < rssItems.Count; i++)
                {
                    XmlNode node = rssItems[i];
                    string title = GetLocalChildText(node, "title");
                    string link = GetLocalChildText(node, "link");
                    string guid = GetLocalChildText(node, "guid");
                    string pubDate = GetLocalChildText(node, "pubDate");
                    if (string.IsNullOrEmpty(pubDate)) pubDate = GetLocalChildText(node, "date");
                    long size = -1;

                    // Torznab / Newznab / Torrent RSS extensions (<torrent:magnetURI>, <torznab:attr name="magneturl">)
                    string magnetChild = GetLocalChildText(node, "magnetURI");
                    if (!string.IsNullOrWhiteSpace(magnetChild) && magnetChild.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase))
                    {
                        link = magnetChild.Trim();
                    }

                    for (int c = 0; c < node.ChildNodes.Count; c++)
                    {
                        XmlNode an = node.ChildNodes[c];
                        if (!string.Equals(an.LocalName, "attr", StringComparison.OrdinalIgnoreCase) || an.Attributes == null) continue;
                        XmlAttribute nm = an.Attributes["name"];
                        XmlAttribute vl = an.Attributes["value"];
                        if (nm != null && vl != null)
                        {
                            if (string.Equals(nm.Value, "magneturl", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(vl.Value))
                            {
                                link = vl.Value.Trim();
                            }
                            else if (string.Equals(nm.Value, "size", StringComparison.OrdinalIgnoreCase))
                            {
                                long.TryParse(vl.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out size);
                            }
                        }
                    }

                    XmlNode enc = FindLocalChildNode(node, "enclosure");
                    if (enc != null && enc.Attributes != null)
                    {
                        XmlAttribute urlAttr = enc.Attributes["url"];
                        if (urlAttr != null && !string.IsNullOrWhiteSpace(urlAttr.Value) &&
                            !link.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase))
                        {
                            link = urlAttr.Value.Trim();
                        }
                        XmlAttribute lenAttr = enc.Attributes["length"];
                        if (lenAttr != null && size <= 0)
                        {
                            long.TryParse(lenAttr.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out size);
                        }
                    }

                    if (IsAllowedRssItemUrl(link))
                    {
                        items.Add(new ShihabRssEntryItem
                        {
                            GuidId = string.IsNullOrWhiteSpace(guid) ? link : guid,
                            Title = string.IsNullOrWhiteSpace(title) ? ShihabFormatHelpers.ExtractMehediFileNameFromUrl(link) : title,
                            DownloadUrl = link,
                            PublishedDate = pubDate,
                            SizeBytes = size
                        });
                    }
                }
                return items;
            }

            // Atom <entry>
            XmlNodeList atomEntries = doc.SelectNodes("//*[local-name()='entry']");
            if (atomEntries != null)
            {
                for (int i = 0; i < atomEntries.Count; i++)
                {
                    XmlNode node = atomEntries[i];
                    string title = GetLocalChildText(node, "title");
                    string id = GetLocalChildText(node, "id");
                    string updated = GetLocalChildText(node, "updated");
                    if (string.IsNullOrEmpty(updated)) updated = GetLocalChildText(node, "published");
                    string href = "";
                    long size = -1;

                    for (int l = 0; l < node.ChildNodes.Count; l++)
                    {
                        XmlNode ln = node.ChildNodes[l];
                        if (!string.Equals(ln.LocalName, "link", StringComparison.OrdinalIgnoreCase) || ln.Attributes == null) continue;
                        XmlAttribute hrefAttr = ln.Attributes["href"];
                        XmlAttribute relAttr = ln.Attributes["rel"];
                        XmlAttribute lenAttr = ln.Attributes["length"];
                        if (hrefAttr != null && !string.IsNullOrWhiteSpace(hrefAttr.Value))
                        {
                            if (string.IsNullOrEmpty(href) || (relAttr != null && string.Equals(relAttr.Value, "enclosure", StringComparison.OrdinalIgnoreCase)))
                            {
                                href = hrefAttr.Value.Trim();
                                if (lenAttr != null) long.TryParse(lenAttr.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out size);
                            }
                        }
                    }

                    if (IsAllowedRssItemUrl(href))
                    {
                        items.Add(new ShihabRssEntryItem
                        {
                            GuidId = string.IsNullOrWhiteSpace(id) ? href : id,
                            Title = string.IsNullOrWhiteSpace(title) ? ShihabFormatHelpers.ExtractMehediFileNameFromUrl(href) : title,
                            DownloadUrl = href,
                            PublishedDate = updated,
                            SizeBytes = size
                        });
                    }
                }
            }
            return items;
        }

        private static byte[] DownloadValidatedFeedBytes(string feedUrl, MehediAppConfiguration cfg)
        {
            string currentUrl = feedUrl;
            for (int hop = 0; hop <= MaxFeedRedirects; hop++)
            {
                Uri parsed;
                if (!ShihabProcessAndPathSafety.TryValidateHttpUrl(currentUrl, false, out parsed))
                {
                    throw new InvalidOperationException("Rejected invalid RSS feed URL scheme or format.");
                }
                if (ShihabProcessAndPathSafety.IsHostDisallowedForOutboundSsrf(parsed.Host, true))
                {
                    throw new InvalidOperationException("Blocked RSS fetch to internal or loopback host: " + parsed.Host);
                }

                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(parsed);
                req.Method = "GET";
                req.AllowAutoRedirect = false;
                req.UserAgent = "MHS-Download-Manager-RSS/" + ShihabAutoUpdater.GetCurrentAppVersion();
                req.Timeout = 15000;
                req.ReadWriteTimeout = 15000;
                req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                if (cfg != null) ShihabDnsAndNetworkManager.ApplyNetworkBindingAndProxy(req, cfg, parsed.AbsoluteUri);

                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                {
                    int code = (int)resp.StatusCode;
                    if (code == 301 || code == 302 || code == 303 || code == 307 || code == 308)
                    {
                        string location = resp.Headers["Location"];
                        if (string.IsNullOrWhiteSpace(location) || hop == MaxFeedRedirects)
                        {
                            throw new WebException("Invalid or excessive redirects while fetching RSS feed.");
                        }
                        Uri nextUri;
                        if (!Uri.TryCreate(parsed, location, out nextUri))
                        {
                            throw new InvalidOperationException("Malformed RSS redirect location.");
                        }
                        currentUrl = nextUri.AbsoluteUri;
                        continue;
                    }

                    using (Stream s = resp.GetResponseStream())
                    {
                        if (s == null) return new byte[0];
                        using (MemoryStream ms = new MemoryStream())
                        {
                            byte[] buf = new byte[16384];
                            int r;
                            while ((r = s.Read(buf, 0, buf.Length)) > 0)
                            {
                                if (ms.Length + r > MaxFeedBytes)
                                {
                                    throw new InvalidOperationException("RSS/Atom feed exceeds 5 MB safety limit.");
                                }
                                ms.Write(buf, 0, r);
                            }
                            return ms.ToArray();
                        }
                    }
                }
            }
            throw new WebException("Exceeded maximum RSS feed redirect hops.");
        }

        private static bool IsAllowedRssItemUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            string s = url.Trim();
            if (s.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase))
            {
                return s.IndexOf("xt=urn:btih:", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            Uri parsed;
            if (!Uri.TryCreate(s, UriKind.Absolute, out parsed)) return false;
            bool allowedScheme =
                parsed.Scheme == Uri.UriSchemeHttp ||
                parsed.Scheme == Uri.UriSchemeHttps ||
                string.Equals(parsed.Scheme, "ftp", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(parsed.Scheme, "ftps", StringComparison.OrdinalIgnoreCase);
            if (!allowedScheme) return false;
            return !ShihabProcessAndPathSafety.IsHostDisallowedForOutboundSsrf(parsed.Host, false);
        }

        public static string ExtractEpisodeIdentityKey(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return "";
            try
            {
                Match se = SeasonEpisodeRegex.Match(title);
                if (se.Success)
                {
                    int sNum = 0, eNum = 0;
                    int.TryParse(se.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out sNum);
                    int.TryParse(se.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out eNum);
                    string seriesPrefix = SeriesPrefixSanitizeRegex.Replace(title.Substring(0, se.Index), "_").Trim('_').ToLowerInvariant();
                    return string.Format(CultureInfo.InvariantCulture, "{0}:s{1:00}e{2:00}", seriesPrefix, sNum, eNum);
                }
                Match nx = AltSeasonEpisodeRegex.Match(title);
                if (nx.Success)
                {
                    int sNum = 0, eNum = 0;
                    int.TryParse(nx.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out sNum);
                    int.TryParse(nx.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out eNum);
                    string seriesPrefix = SeriesPrefixSanitizeRegex.Replace(title.Substring(0, nx.Index), "_").Trim('_').ToLowerInvariant();
                    return string.Format(CultureInfo.InvariantCulture, "{0}:s{1:00}e{2:00}", seriesPrefix, sNum, eNum);
                }
            }
            catch (RegexMatchTimeoutException) { }
            return "";
        }

        public static bool MatchesRssRule(ShihabRssFeedConfig sub, ShihabRssEntryItem entry)
        {
            if (sub == null || entry == null) return false;
            string haystack = ((entry.Title ?? "") + " " + (entry.DownloadUrl ?? "")).Trim();
            string must = string.IsNullOrWhiteSpace(sub.MehediFilterPattern) ? "*" : sub.MehediFilterPattern.Trim();
            if (must != "*" && !ShihabFormatHelpers.WildcardMatchesMehediPattern(haystack, must))
            {
                try
                {
                    if (!Regex.IsMatch(haystack, must, RegexOptions.IgnoreCase, RssRegexTimeout)) return false;
                }
                catch { return false; }
            }

            if (!string.IsNullOrWhiteSpace(sub.ShihabExcludePattern))
            {
                string excl = sub.ShihabExcludePattern.Trim();
                if (ShihabFormatHelpers.WildcardMatchesMehediPattern(haystack, excl)) return false;
                try
                {
                    if (Regex.IsMatch(haystack, excl, RegexOptions.IgnoreCase, RssRegexTimeout)) return false;
                }
                catch { }
            }
            return true;
        }

        public static bool PollDueFeedsInBackground(
            MehediAppConfiguration cfg,
            Action<ShihabRssFeedConfig, ShihabRssEntryItem> onAutoEnqueueItem)
        {
            if (cfg == null || cfg.ShihabRssFeeds == null || cfg.ShihabRssFeeds.Count == 0)
            {
                return false;
            }

            bool configDirty = false;
            DateTime now = DateTime.Now;
            for (int i = 0; i < cfg.ShihabRssFeeds.Count; i++)
            {
                ShihabRssFeedConfig sub = cfg.ShihabRssFeeds[i];
                if (sub == null || string.IsNullOrWhiteSpace(sub.ShihabFeedUrl)) continue;

                int intervalMins = Math.Max(5, sub.MehediCheckIntervalMinutes);
                DateTime lastChecked;
                if (!string.IsNullOrWhiteSpace(sub.ShihabLastChecked) &&
                    DateTime.TryParse(sub.ShihabLastChecked, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out lastChecked))
                {
                    if ((now - lastChecked).TotalMinutes < intervalMins) continue;
                }

                try
                {
                    string title;
                    List<ShihabRssEntryItem> entries = FetchAndParseFeed(sub.ShihabFeedUrl, cfg, out title);
                    if (string.IsNullOrWhiteSpace(sub.ShihabFeedTitle) || sub.ShihabFeedTitle == "RSS Feed")
                    {
                        sub.ShihabFeedTitle = title;
                    }
                    sub.ShihabLastChecked = now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                    if (sub.MehediSeenItemGuids == null) sub.MehediSeenItemGuids = new List<string>();
                    if (sub.ShihabDownloadedEpisodeKeys == null) sub.ShihabDownloadedEpisodeKeys = new List<string>();

                    // Seed initial backlog on first poll so historical items are not mass-enqueued unexpectedly
                    if (!sub.ShihabInitialBacklogSeeded)
                    {
                        for (int e = 0; e < entries.Count; e++)
                        {
                            if (!string.IsNullOrEmpty(entries[e].GuidId) && !sub.MehediSeenItemGuids.Contains(entries[e].GuidId))
                            {
                                sub.MehediSeenItemGuids.Add(entries[e].GuidId);
                                while (sub.MehediSeenItemGuids.Count > 500) sub.MehediSeenItemGuids.RemoveAt(0);
                            }
                        }
                        sub.ShihabInitialBacklogSeeded = true;
                        configDirty = true;
                        continue;
                    }

                    for (int e = 0; e < entries.Count; e++)
                    {
                        ShihabRssEntryItem item = entries[e];
                        if (string.IsNullOrEmpty(item.GuidId) || sub.MehediSeenItemGuids.Contains(item.GuidId)) continue;
                        sub.MehediSeenItemGuids.Add(item.GuidId);
                        while (sub.MehediSeenItemGuids.Count > 500) sub.MehediSeenItemGuids.RemoveAt(0);
                        configDirty = true;

                        if (!sub.ShihabAutoDownload || !MatchesRssRule(sub, item)) continue;

                        string epKey = ExtractEpisodeIdentityKey(item.Title);
                        if (!string.IsNullOrEmpty(epKey))
                        {
                            if (sub.ShihabDownloadedEpisodeKeys.Contains(epKey)) continue;
                            sub.ShihabDownloadedEpisodeKeys.Add(epKey);
                            while (sub.ShihabDownloadedEpisodeKeys.Count > 500) sub.ShihabDownloadedEpisodeKeys.RemoveAt(0);
                        }

                        if (onAutoEnqueueItem != null) onAutoEnqueueItem(sub, item);
                    }
                }
                catch { }
            }
            return configDirty;
        }

        private static XmlNode FindLocalChildNode(XmlNode parent, string localName)
        {
            if (parent == null) return null;
            for (int i = 0; i < parent.ChildNodes.Count; i++)
            {
                XmlNode c = parent.ChildNodes[i];
                if (string.Equals(c.LocalName, localName, StringComparison.OrdinalIgnoreCase))
                {
                    return c;
                }
            }
            return null;
        }

        private static string GetLocalChildText(XmlNode parent, string localName)
        {
            XmlNode n = FindLocalChildNode(parent, localName);
            return n != null ? n.InnerText.Trim() : "";
        }
    }
}
