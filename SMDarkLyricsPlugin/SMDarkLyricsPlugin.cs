using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using ImPluginEngine.Abstractions;
using ImPluginEngine.Abstractions.Entities;
using ImPluginEngine.Abstractions.Interfaces;
using ImPluginEngine.Helpers;
using ImPluginEngine.Types;

namespace SMDarkLyricsPlugin
{
    public class SMDarkLyricsPlugin : IPlugin, ILyrics
    {
        public string Name => "SMDarkLyrics";
        public string Version => "0.2.0";

        public async Task GetLyrics(PluginLyricsInput input, CancellationToken ct, Action<PluginLyricsResult> updateAction)
        {
            String url = string.Format("http://www.darklyrics.com/search?q={0}+{1}", HttpUtility.UrlEncode(input.Artist), HttpUtility.UrlEncode(input.Title));
            var client = new HttpClient();
            String web = string.Empty;
            try
            {
                var response = await client.GetAsync(url, ct);
                var data = await response.Content.ReadAsByteArrayAsync();
                web = Encoding.UTF8.GetString(data);
            }
            catch (HttpRequestException)
            {
                return;
            }
            Regex SearchRegex = new Regex(@"<h2><a href=""(?'url'[^""]+)"" target=""_blank"" >(?'artisttitle'[^<]+)</a></h2>", RegexOptions.Compiled);
            MatchCollection matches = SearchRegex.Matches(web);
            foreach (Match match in matches)
            {
                var result = new PluginLyricsResult();
                result.Artist = match.Groups["artisttitle"].Value.Substring(0, match.Groups["artisttitle"].Value.IndexOf(" - "));
                result.Title = match.Groups["artisttitle"].Value.Substring(match.Groups["artisttitle"].Value.IndexOf(" - ") + 3);
                result.FoundByPlugin = string.Format("{0} v{1}", Name, Version);
                result.Lyrics = await DownloadLyrics(string.Format("http://www.darklyrics.com/{0}", match.Groups["url"].Value), result.Title, ct);
                updateAction(result);
            }
        }
        private async Task<String> DownloadLyrics(String url, String title, CancellationToken ct)
        {
            var client = new HttpClient();
            string lyrics = string.Empty;
            string web;
            try
            {
                var response = await client.GetAsync(url, ct);
                var data = await response.Content.ReadAsByteArrayAsync();
                web = Encoding.UTF8.GetString(data);
            }
            catch (HttpRequestException)
            {
                return lyrics;
            }
            String lyricsStart = string.Format(". {0}</a></h3><br />", title);
            String lyricsEnd = "<br /><br />";
            String ignoreStart = "<i>[";
            string source = WebUtility.HtmlDecode(web);
            using (Stream lyr_stream = GenerateStreamFromString(source))
            {
                using (StreamReader lyr_sr = new StreamReader(lyr_stream, Encoding.UTF8))
                {
                    bool start = false;
                    while (lyr_sr.EndOfStream == false)
                    {
                        string line = lyr_sr.ReadLine();
                        if (start && !line.StartsWith(ignoreStart))
                        {
                            if (line == lyricsEnd)
                            {
                                break;
                            }
                            lyrics += CleanLine(line);
                        }
                        if (line.EndsWith(lyricsStart))
                        {
                            start = true;
                        }
                    }
                }
            }
            lyrics = lyrics.Trim().Replace("\n", "<br/>\n").Replace("<br/>\n<br/>\n", "</p>\n<p>");
            lyrics = lyrics.Replace("<p><br/>\n", "<p>\n");
            lyrics = Regex.Replace(lyrics, @"\s+<br/>", "<br/>", RegexOptions.IgnoreCase);
            lyrics = Regex.Replace(lyrics, @"\s+<p/>", "<p/>", RegexOptions.IgnoreCase);
            return "<p>" + lyrics.Trim() + "</p>\n<p><i><sub>powered by Dark Lyrics</sub></i></p>";
        }

        private static string CleanLine(String line)
        {
            line = Regex.Replace(line, "<a href=[^>]+>", "", RegexOptions.IgnoreCase);
            line = line.Replace("</a>", "");
            line = line.Replace("<br />", "").Replace("<br/>", "").Replace("<br>", "").Replace("\n", "");
            line = line.Replace("´", "'").Replace("`", "'").Replace("’", "'").Replace("‘", "'");
            line = line.Replace("…", "...").Replace(" ...", "...").Trim();
            return line.Trim() + "\n";
        }
        public static MemoryStream GenerateStreamFromString(string value)
        {
            return new MemoryStream(Encoding.UTF8.GetBytes(value ?? ""));
        }
    }
}
