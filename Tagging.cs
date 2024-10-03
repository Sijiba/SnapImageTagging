using ExifLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tagging
{
    static class Tagging
    {
        public static string nameless = "unknownName";
        public static string eventPrefix = "event";
        public static string[] nametags = [
            "name","discord","twitter", "instagram", "threads",
            "telegram","twitch","tiktok", "bsky", "youtube"];

        public static bool IsTaggingTag(string tag)
        {
            if (tag == nameless) return true;
            else return IsSocialsTag(tag);
        }
        public static bool IsSocialsTag(string tag)
        {
            if (!tag.Contains(':')) return false;
            else
                return nametags.Contains(tag[..tag.IndexOf(':')]);
        }

        public static bool IsEventTag(string tag)
        {
            if (!tag.Contains(':')) return false;
            else return tag.StartsWith(eventPrefix);
        }

        public static bool HasMetadataSocials(List<string> winTags, bool countSkipped = false)
        {
            if (countSkipped && winTags.Contains(nameless)) return false;
            return (winTags.AsParallel().Any(item => IsTaggingTag(item)));
        }

        public static List<string> GetSocialTags(List<string> winTags)
        {
            return new List<string>(winTags.FindAll(IsSocialsTag));
        }

        public static string GetEvent(List<string> winTags)
        {
            return (winTags.FindAll(IsEventTag).Aggregate("", (max, cur) => max.Length > cur.Length ? max : cur));
        }

        public static List<string> GetTagStringList(ImageFile img)
        {
            var tagsMaybe = img.Properties.Get(ExifTag.WindowsKeywords);
            if (tagsMaybe != null)
            {
                return GetTagStringList(((WindowsByteString)tagsMaybe).Value);
            }
            return [];
        }

        public static List<string> GetTagStringList(string tagString)
        {
            var tags = new List<string>();
            tags.AddRange(tagString.Split(";"));
            return tags;
        }

        public static List<string> LoadTagsFromFile(string file)
        {
            var img = ExifLibrary.JPEGFile.FromFile(file);
            if (img != null)
            {
                var tagsMaybe = img.Properties.Get(ExifTag.WindowsKeywords);
                if (tagsMaybe != null)
                {
                    return GetTagStringList((string)tagsMaybe.Value);
                }
            }
            return [];
        }
    }
}
