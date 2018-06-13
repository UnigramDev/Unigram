using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unigram.Common
{
    public class TreeNode
    {
        public string Key { get; set; }

        public Dictionary<string, TreeNode> Values { get; set; }

        public bool IsEnded { get; set; }

        public TreeNode(string key)
        {
            Key = key;
            Values = new Dictionary<string, TreeNode>();
        }
    }

    public static class Emoji
    {
        private static Dictionary<string, string> _dict;

        public static Dictionary<string, string> Dict
        {
            get
            {
                if (_dict == null)
                {
                    InitializeDict();
                }

                return _dict;
            }
        }

        private static Dictionary<string, string> _skinnedDict;

        public static Dictionary<string, string> SkinnedDict
        {
            get
            {
                if (_skinnedDict == null)
                {
                    InitializeDict();
                }

                return _skinnedDict;
            }
        }

        private static Dictionary<string, string> _joinedEmojiPartDict;

        public static Dictionary<string, string> JoinedEmojiPartDict
        {
            get
            {
                if (_joinedEmojiPartDict == null)
                {
                    InitializeDict();
                }

                return _joinedEmojiPartDict;
            }
        }

        private static TreeNode _joinedEmojiTree;

        public static TreeNode JoinedEmojiTree
        {
            get
            {
                if (_joinedEmojiTree == null)
                {
                    InitializeDict();
                }

                return _joinedEmojiTree;
            }
        }

        private static Dictionary<string, string> _skinsDict;

        public static Dictionary<string, string> SkinsDict
        {
            get
            {
                if (_skinsDict == null)
                {
                    InitializeDict();
                }

                return _skinsDict;
            }
        }

        private static Dictionary<string, string> _flagPrefixes;

        public static Dictionary<string, string> FlagsPrefixes
        {
            get
            {
                if (_flagPrefixes == null)
                {
                    InitializeDict();
                }

                return _flagPrefixes;
            }
        }

        public static string ZeroWidthJoiner = "200D";

        public static string EmojiStyleVarianSelector = "FE0F";

        public static string TextStyleVarianSelector = "FE0E";

        private static void AddTreeNodes(TreeNode node, int index, params string[] values)
        {
            if (_dict == null) return;
            if (index >= values.Length)
            {
                node.IsEnded = true;
                return;
            }

            var key = values[index];
            TreeNode internalNode;
            if (node.Values.TryGetValue(key, out internalNode))
            {
                AddTreeNodes(internalNode, index + 1, values);
            }
            else
            {
                internalNode = new TreeNode(key);

                node.Values[key] = internalNode;

                AddTreeNodes(internalNode, index + 1, values);
            }
        }

        private static void InitializeDict()
        {
            _dict = new Dictionary<string, string>();
            _skinnedDict = new Dictionary<string, string>();
            _skinsDict = new Dictionary<string, string>();
            _joinedEmojiPartDict = new Dictionary<string, string>();
            _joinedEmojiTree = new TreeNode(string.Empty);
            _flagPrefixes = new Dictionary<string, string>();

            _flagPrefixes["D83CDDE6"] = "D83CDDE6";
            _flagPrefixes["D83CDDE7"] = "D83CDDE7";
            _flagPrefixes["D83CDDE8"] = "D83CDDE8";
            _flagPrefixes["D83CDDE9"] = "D83CDDE9";
            _flagPrefixes["D83CDDEA"] = "D83CDDEA";
            _flagPrefixes["D83CDDEB"] = "D83CDDEB";
            _flagPrefixes["D83CDDEC"] = "D83CDDEC";
            _flagPrefixes["D83CDDED"] = "D83CDDED";
            _flagPrefixes["D83CDDEE"] = "D83CDDEE";
            _flagPrefixes["D83CDDEF"] = "D83CDDEF";
            _flagPrefixes["D83CDDF0"] = "D83CDDF0";
            _flagPrefixes["D83CDDF1"] = "D83CDDF1";
            _flagPrefixes["D83CDDF2"] = "D83CDDF2";
            _flagPrefixes["D83CDDF3"] = "D83CDDF3";
            _flagPrefixes["D83CDDF4"] = "D83CDDF4";
            _flagPrefixes["D83CDDF5"] = "D83CDDF5";
            _flagPrefixes["D83CDDF6"] = "D83CDDF6";
            _flagPrefixes["D83CDDF7"] = "D83CDDF7";
            _flagPrefixes["D83CDDF8"] = "D83CDDF8";
            _flagPrefixes["D83CDDF9"] = "D83CDDF9";
            _flagPrefixes["D83CDDFA"] = "D83CDDFA";
            _flagPrefixes["D83CDDFB"] = "D83CDDFB";
            _flagPrefixes["D83CDDFC"] = "D83CDDFC";
            _flagPrefixes["D83CDDFD"] = "D83CDDFD";
            _flagPrefixes["D83CDDFE"] = "D83CDDFE";
            _flagPrefixes["D83CDDFF"] = "D83CDDFF";

            _joinedEmojiPartDict["D83DDC69"] = "D83DDC69";
            _joinedEmojiPartDict["D83DDC68"] = "D83DDC68";
            _joinedEmojiPartDict["2764"] = "2764";
            _joinedEmojiPartDict["D83DDC8B"] = "D83DDC8B";
            _joinedEmojiPartDict["D83DDC66"] = "D83DDC66";
            _joinedEmojiPartDict["D83DDC67"] = "D83DDC67";
            _joinedEmojiPartDict["D83DDC41"] = "D83DDC41";
            _joinedEmojiPartDict["D83DDDE8"] = "D83DDDE8";

            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC6E");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC6E", "D83CDFFB");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC6E", "D83CDFFC");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC6E", "D83CDFFD");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC6E", "D83CDFFE");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC6E", "D83CDFFF");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC6E", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC6E", "D83CDFFB", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC6E", "D83CDFFC", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC6E", "D83CDFFD", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC6E", "D83CDFFE", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC6E", "D83CDFFF", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFC3");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFC3", "D83CDFFB");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFC3", "D83CDFFC");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFC3", "D83CDFFD");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFC3", "D83CDFFE");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFC3", "D83CDFFF");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFC3", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFC3", "D83CDFFB", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFC3", "D83CDFFC", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFC3", "D83CDFFD", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFC3", "D83CDFFE", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFC3", "D83CDFFF", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC6F", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFB");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFC");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFD");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFE");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFF");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "200D", "2695");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFB", "200D", "2695");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFC", "200D", "2695");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFD", "200D", "2695");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFE", "200D", "2695");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFF", "200D", "2695");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "200D", "2696");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFB", "200D", "2696");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFC", "200D", "2696");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFD", "200D", "2696");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFE", "200D", "2696");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFF", "200D", "2696");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "200D", "2708");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFB", "200D", "2708");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFC", "200D", "2708");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFD", "200D", "2708");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFE", "200D", "2708");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFF", "200D", "2708");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "200D", "2764", "200D", "D83DDC8B", "200D", "D83DDC68");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "200D", "2764", "200D", "D83DDC68");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "200D", "D83CDF3E");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFB", "200D", "D83CDF3E");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFC", "200D", "D83CDF3E");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFD", "200D", "D83CDF3E");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFE", "200D", "D83CDF3E");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFF", "200D", "D83CDF3E");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "200D", "D83CDF73");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFB", "200D", "D83CDF73");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFC", "200D", "D83CDF73");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFD", "200D", "D83CDF73");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFE", "200D", "D83CDF73");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFF", "200D", "D83CDF73");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "200D", "D83CDF93");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFB", "200D", "D83CDF93");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFC", "200D", "D83CDF93");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFD", "200D", "D83CDF93");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFE", "200D", "D83CDF93");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFF", "200D", "D83CDF93");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "200D", "D83CDFA4");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFB", "200D", "D83CDFA4");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFC", "200D", "D83CDFA4");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFD", "200D", "D83CDFA4");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFE", "200D", "D83CDFA4");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFF", "200D", "D83CDFA4");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "200D", "D83CDFA8");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFB", "200D", "D83CDFA8");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFC", "200D", "D83CDFA8");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFD", "200D", "D83CDFA8");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFE", "200D", "D83CDFA8");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFF", "200D", "D83CDFA8");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "200D", "D83CDFEB");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFB", "200D", "D83CDFEB");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFC", "200D", "D83CDFEB");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFD", "200D", "D83CDFEB");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFE", "200D", "D83CDFEB");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFF", "200D", "D83CDFEB");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "200D", "D83CDFED");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFB", "200D", "D83CDFED");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFC", "200D", "D83CDFED");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFD", "200D", "D83CDFED");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFE", "200D", "D83CDFED");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFF", "200D", "D83CDFED");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "200D", "D83DDC66");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "200D", "D83DDC67");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "200D", "D83DDC66", "200D", "D83DDC66");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "200D", "D83DDC67", "200D", "D83DDC66");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "200D", "D83DDC67", "200D", "D83DDC67");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "200D", "D83DDCBB");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFB", "200D", "D83DDCBB");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFC", "200D", "D83DDCBB");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFD", "200D", "D83DDCBB");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFE", "200D", "D83DDCBB");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFF", "200D", "D83DDCBB");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "200D", "D83DDCBC");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFB", "200D", "D83DDCBC");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFC", "200D", "D83DDCBC");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFD", "200D", "D83DDCBC");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFE", "200D", "D83DDCBC");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFF", "200D", "D83DDCBC");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "200D", "D83DDD2C");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFB", "200D", "D83DDD2C");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFC", "200D", "D83DDD2C");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFD", "200D", "D83DDD2C");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFE", "200D", "D83DDD2C");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFF", "200D", "D83DDD2C");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "200D", "D83DDD27");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFB", "200D", "D83DDD27");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFC", "200D", "D83DDD27");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFD", "200D", "D83DDD27");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFE", "200D", "D83DDD27");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFF", "200D", "D83DDD27");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "200D", "D83DDE80");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFB", "200D", "D83DDE80");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFC", "200D", "D83DDE80");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFD", "200D", "D83DDE80");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFE", "200D", "D83DDE80");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFF", "200D", "D83DDE80");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "200D", "D83DDE92");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFB", "200D", "D83DDE92");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFC", "200D", "D83DDE92");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFD", "200D", "D83DDE92");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFE", "200D", "D83DDE92");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "D83CDFFF", "200D", "D83DDE92");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFB");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFC");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFD");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFE");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFF");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "200D", "2695");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFB", "200D", "2695");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFC", "200D", "2695");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFD", "200D", "2695");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFE", "200D", "2695");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFF", "200D", "2695");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "200D", "2696");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFB", "200D", "2696");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFC", "200D", "2696");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFD", "200D", "2696");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFE", "200D", "2696");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFF", "200D", "2696");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "200D", "2708");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFB", "200D", "2708");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFC", "200D", "2708");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFD", "200D", "2708");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFE", "200D", "2708");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFF", "200D", "2708");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "200D", "2764", "200D", "D83DDC8B", "200D", "D83DDC69");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "200D", "2764", "200D", "D83DDC69");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "200D", "D83CDF3E");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFB", "200D", "D83CDF3E");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFC", "200D", "D83CDF3E");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFD", "200D", "D83CDF3E");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFE", "200D", "D83CDF3E");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFF", "200D", "D83CDF3E");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "200D", "D83CDF73");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFB", "200D", "D83CDF73");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFC", "200D", "D83CDF73");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFD", "200D", "D83CDF73");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFE", "200D", "D83CDF73");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFF", "200D", "D83CDF73");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "200D", "D83CDF93");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFB", "200D", "D83CDF93");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFC", "200D", "D83CDF93");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFD", "200D", "D83CDF93");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFE", "200D", "D83CDF93");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFF", "200D", "D83CDF93");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "200D", "D83CDFA4");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFB", "200D", "D83CDFA4");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFC", "200D", "D83CDFA4");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFD", "200D", "D83CDFA4");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFE", "200D", "D83CDFA4");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFF", "200D", "D83CDFA4");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "200D", "D83CDFA8");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFB", "200D", "D83CDFA8");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFC", "200D", "D83CDFA8");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFD", "200D", "D83CDFA8");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFE", "200D", "D83CDFA8");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFF", "200D", "D83CDFA8");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "200D", "D83CDFEB");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "200D", "D83CDFEB");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFB", "200D", "D83CDFEB");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFC", "200D", "D83CDFEB");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFD", "200D", "D83CDFEB");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFE", "200D", "D83CDFEB");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFF", "200D", "D83CDFEB");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "200D", "D83CDFED");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFB", "200D", "D83CDFED");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFC", "200D", "D83CDFED");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFD", "200D", "D83CDFED");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFE", "200D", "D83CDFED");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFF", "200D", "D83CDFED");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "200D", "D83DDC66");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "200D", "D83DDC67");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "200D", "D83DDC66", "200D", "D83DDC66");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "200D", "D83DDC67", "200D", "D83DDC66");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "200D", "D83DDC67", "200D", "D83DDC67");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "200D", "D83DDCBB");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFB", "200D", "D83DDCBB");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFC", "200D", "D83DDCBB");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFD", "200D", "D83DDCBB");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFE", "200D", "D83DDCBB");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFF", "200D", "D83DDCBB");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "200D", "D83DDCBC");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFB", "200D", "D83DDCBC");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFC", "200D", "D83DDCBC");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFD", "200D", "D83DDCBC");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFE", "200D", "D83DDCBC");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFF", "200D", "D83DDCBC");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "200D", "D83DDD2C");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFB", "200D", "D83DDD2C");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFC", "200D", "D83DDD2C");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFD", "200D", "D83DDD2C");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFE", "200D", "D83DDD2C");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFF", "200D", "D83DDD2C");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "200D", "D83DDD27");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFB", "200D", "D83DDD27");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFC", "200D", "D83DDD27");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFD", "200D", "D83DDD27");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFE", "200D", "D83DDD27");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFF", "200D", "D83DDD27");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "200D", "D83DDE80");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFB", "200D", "D83DDE80");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFC", "200D", "D83DDE80");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFD", "200D", "D83DDE80");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFE", "200D", "D83DDE80");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFF", "200D", "D83DDE80");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "200D", "D83DDE92");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFB", "200D", "D83DDE92");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFC", "200D", "D83DDE92");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFD", "200D", "D83DDE92");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFE", "200D", "D83DDE92");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "D83CDFFF", "200D", "D83DDE92");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC71");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC71", "D83CDFFB");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC71", "D83CDFFC");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC71", "D83CDFFD");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC71", "D83CDFFE");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC71", "D83CDFFF");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC71", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC71", "D83CDFFB", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC71", "D83CDFFC", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC71", "D83CDFFD", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC71", "D83CDFFE", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC71", "D83CDFFF", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC73");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC73", "D83CDFFB");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC73", "D83CDFFC");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC73", "D83CDFFD");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC73", "D83CDFFE");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC73", "D83CDFFF");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC73", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC73", "D83CDFFB", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC73", "D83CDFFC", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC73", "D83CDFFD", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC73", "D83CDFFE", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC73", "D83CDFFF", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC77");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC77", "D83CDFFB");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC77", "D83CDFFC");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC77", "D83CDFFD");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC77", "D83CDFFE");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC77", "D83CDFFF");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC77", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC77", "D83CDFFB", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC77", "D83CDFFC", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC77", "D83CDFFD", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC77", "D83CDFFE", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC77", "D83CDFFF", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC81");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC81", "D83CDFFB");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC81", "D83CDFFC");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC81", "D83CDFFD");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC81", "D83CDFFE");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC81", "D83CDFFF");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC81", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC81", "D83CDFFB", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC81", "D83CDFFC", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC81", "D83CDFFD", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC81", "D83CDFFE", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC81", "D83CDFFF", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC82");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC82", "D83CDFFB");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC82", "D83CDFFC");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC82", "D83CDFFD");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC82", "D83CDFFE");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC82", "D83CDFFF");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC82", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC82", "D83CDFFB", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC82", "D83CDFFC", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC82", "D83CDFFD", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC82", "D83CDFFE", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC82", "D83CDFFF", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC86");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC86", "D83CDFFB");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC86", "D83CDFFC");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC86", "D83CDFFD");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC86", "D83CDFFE");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC86", "D83CDFFF");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC86", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC86", "D83CDFFB", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC86", "D83CDFFC", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC86", "D83CDFFD", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC86", "D83CDFFE", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC86", "D83CDFFF", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC87");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC87", "D83CDFFB");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC87", "D83CDFFC");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC87", "D83CDFFD");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC87", "D83CDFFE");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC87", "D83CDFFF");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC87", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC87", "D83CDFFB", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC87", "D83CDFFC", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC87", "D83CDFFD", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC87", "D83CDFFE", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC87", "D83CDFFF", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDD75");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDD75", "D83CDFFB");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDD75", "D83CDFFC");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDD75", "D83CDFFD");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDD75", "D83CDFFE");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDD75", "D83CDFFF");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDD75", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDD75", "D83CDFFB", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDD75", "D83CDFFC", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDD75", "D83CDFFD", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDD75", "D83CDFFE", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDD75", "D83CDFFF", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE4B");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE4B", "D83CDFFB");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE4B", "D83CDFFC");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE4B", "D83CDFFD");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE4B", "D83CDFFE");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE4B", "D83CDFFF");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE4B", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE4B", "D83CDFFB", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE4B", "D83CDFFC", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE4B", "D83CDFFD", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE4B", "D83CDFFE", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE4B", "D83CDFFF", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE4D");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE4D", "D83CDFFB");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE4D", "D83CDFFC");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE4D", "D83CDFFD");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE4D", "D83CDFFE");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE4D", "D83CDFFF");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE4D", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE4D", "D83CDFFB", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE4D", "D83CDFFC", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE4D", "D83CDFFD", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE4D", "D83CDFFE", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE4D", "D83CDFFF", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE4E");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE4E", "D83CDFFB");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE4E", "D83CDFFC");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE4E", "D83CDFFD");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE4E", "D83CDFFE");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE4E", "D83CDFFF");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE4E", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE4E", "D83CDFFB", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE4E", "D83CDFFC", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE4E", "D83CDFFD", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE4E", "D83CDFFE", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE4E", "D83CDFFF", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE45");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE45", "D83CDFFB");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE45", "D83CDFFC");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE45", "D83CDFFD");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE45", "D83CDFFE");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE45", "D83CDFFF");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE45", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE45", "D83CDFFB", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE45", "D83CDFFC", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE45", "D83CDFFD", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE45", "D83CDFFE", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE45", "D83CDFFF", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE46");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE46", "D83CDFFB");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE46", "D83CDFFC");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE46", "D83CDFFD");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE46", "D83CDFFE");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE46", "D83CDFFF");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE46", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE46", "D83CDFFB", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE46", "D83CDFFC", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE46", "D83CDFFD", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE46", "D83CDFFE", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE46", "D83CDFFF", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE47");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE47", "D83CDFFB");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE47", "D83CDFFC");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE47", "D83CDFFD");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE47", "D83CDFFE");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE47", "D83CDFFF");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE47", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE47", "D83CDFFB", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE47", "D83CDFFC", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE47", "D83CDFFD", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE47", "D83CDFFE", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDE47", "D83CDFFF", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDEB6");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDEB6", "D83CDFFB");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDEB6", "D83CDFFC");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDEB6", "D83CDFFD");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDEB6", "D83CDFFE");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDEB6", "D83CDFFF");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDEB6", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDEB6", "D83CDFFB", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDEB6", "D83CDFFC", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDEB6", "D83CDFFD", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDEB6", "D83CDFFE", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDEB6", "D83CDFFF", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD26", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD26", "D83CDFFB", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD26", "D83CDFFC", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD26", "D83CDFFD", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD26", "D83CDFFE", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD26", "D83CDFFF", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD26", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD26", "D83CDFFB", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD26", "D83CDFFC", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD26", "D83CDFFD", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD26", "D83CDFFE", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD26", "D83CDFFF", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD37", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD37", "D83CDFFB", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD37", "D83CDFFC", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD37", "D83CDFFD", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD37", "D83CDFFE", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD37", "D83CDFFF", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD37", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD37", "D83CDFFB", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD37", "D83CDFFC", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD37", "D83CDFFD", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD37", "D83CDFFE", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD37", "D83CDFFF", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "200D", "2764FE0F", "200D", "D83DDC69");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "200D", "2764FE0F", "200D", "D83DDC68");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "200D", "2764FE0F", "200D", "D83DDC8B", "200D", "D83DDC69");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "200D", "2764FE0F", "200D", "D83DDC8B", "200D", "D83DDC68");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC41", "200D", "D83DDDE8");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "200D", "D83DDC69", "200D", "D83DDC67");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "200D", "D83DDC69", "200D", "D83DDC67", "200D", "D83DDC66");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "200D", "D83DDC69", "200D", "D83DDC66", "200D", "D83DDC66");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "200D", "D83DDC69", "200D", "D83DDC67", "200D", "D83DDC67");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "200D", "D83DDC69", "200D", "D83DDC66");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "200D", "D83DDC69", "200D", "D83DDC67");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "200D", "D83DDC69", "200D", "D83DDC67", "200D", "D83DDC66");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "200D", "D83DDC69", "200D", "D83DDC66", "200D", "D83DDC66");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC69", "200D", "D83DDC69", "200D", "D83DDC67", "200D", "D83DDC67");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "200D", "D83DDC68", "200D", "D83DDC66");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "200D", "D83DDC68", "200D", "D83DDC67");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "200D", "D83DDC68", "200D", "D83DDC67", "200D", "D83DDC66");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "200D", "D83DDC68", "200D", "D83DDC66", "200D", "D83DDC66");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDC68", "200D", "D83DDC68", "200D", "D83DDC67", "200D", "D83DDC67");
            AddTreeNodes(_joinedEmojiTree, 0, "26F9");
            AddTreeNodes(_joinedEmojiTree, 0, "26F9D83C", "DFFB");
            AddTreeNodes(_joinedEmojiTree, 0, "26F9D83C", "DFFC");
            AddTreeNodes(_joinedEmojiTree, 0, "26F9D83C", "DFFD");
            AddTreeNodes(_joinedEmojiTree, 0, "26F9D83C", "DFFE");
            AddTreeNodes(_joinedEmojiTree, 0, "26F9D83C", "DFFF");
            AddTreeNodes(_joinedEmojiTree, 0, "26F9", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "26F9", "D83CDFFB", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "26F9", "D83CDFFC", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "26F9", "D83CDFFD", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "26F9", "D83CDFFE", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "26F9", "D83CDFFF", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFC4", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFC4", "D83CDFFB", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFC4", "D83CDFFC", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFC4", "D83CDFFD", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFC4", "D83CDFFE", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFC4", "D83CDFFF", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFC4");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFC4", "D83CDFFB");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFC4", "D83CDFFC");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFC4", "D83CDFFD");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFC4", "D83CDFFE");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFC4", "D83CDFFF");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFCA", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFCA", "D83CDFFB", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFCA", "D83CDFFC", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFCA", "D83CDFFD", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFCA", "D83CDFFE", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFCA", "D83CDFFF", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFCA");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFCA", "D83CDFFB");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFCA", "D83CDFFC");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFCA", "D83CDFFD");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFCA", "D83CDFFE");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFCA", "D83CDFFF");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFCC", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFCC", "D83CDFFB", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFCC", "D83CDFFC", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFCC", "D83CDFFD", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFCC", "D83CDFFE", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFCC", "D83CDFFF", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFCC");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFCC", "D83CDFFB");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFCC", "D83CDFFC");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFCC", "D83CDFFD");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFCC", "D83CDFFE");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFCC", "D83CDFFF");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDEA3", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDEA3", "D83CDFFB", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDEA3", "D83CDFFC", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDEA3", "D83CDFFD", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDEA3", "D83CDFFE", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDEA3", "D83CDFFF", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDEA3");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDEA3", "D83CDFFB");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDEA3", "D83CDFFC");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDEA3", "D83CDFFD");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDEA3", "D83CDFFE");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDEA3", "D83CDFFF");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDEB4");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDEB4", "D83CDFFB");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDEB4", "D83CDFFC");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDEB4", "D83CDFFD");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDEB4", "D83CDFFE");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDEB4", "D83CDFFF");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDEB4", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDEB4", "D83CDFFB", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDEB4", "D83CDFFC", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDEB4", "D83CDFFD", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDEB4", "D83CDFFE", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDEB4", "D83CDFFF", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDEB5");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDEB5", "D83CDFFB");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDEB5", "D83CDFFC");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDEB5", "D83CDFFD");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDEB5", "D83CDFFE");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDEB5", "D83CDFFF");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDEB5", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDEB5", "D83CDFFB", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDEB5", "D83CDFFC", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDEB5", "D83CDFFD", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDEB5", "D83CDFFE", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83DDEB5", "D83CDFFF", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD38", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD38", "D83CDFFB", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD38", "D83CDFFC", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD38", "D83CDFFD", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD38", "D83CDFFE", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD38", "D83CDFFF", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD38", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD38", "D83CDFFB", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD38", "D83CDFFC", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD38", "D83CDFFD", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD38", "D83CDFFE", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD38", "D83CDFFF", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD39", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD39", "D83CDFFB", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD39", "D83CDFFC", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD39", "D83CDFFD", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD39", "D83CDFFE", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD39", "D83CDFFF", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD39", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD39", "D83CDFFB", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD39", "D83CDFFC", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD39", "D83CDFFD", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD39", "D83CDFFE", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD39", "D83CDFFF", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD3C", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD3C", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD3D", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD3D", "D83CDFFB", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD3D", "D83CDFFC", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD3D", "D83CDFFD", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD3D", "D83CDFFE", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD3D", "D83CDFFF", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD3D", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD3D", "D83CDFFB", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD3D", "D83CDFFC", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD3D", "D83CDFFD", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD3D", "D83CDFFE", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD3D", "D83CDFFF", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD3E", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD3E", "D83CDFFB", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD3E", "D83CDFFC", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD3E", "D83CDFFD", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD3E", "D83CDFFE", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD3E", "D83CDFFF", "200D", "2642");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD3E", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD3E", "D83CDFFB", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD3E", "D83CDFFC", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD3E", "D83CDFFD", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD3E", "D83CDFFE", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83EDD3E", "D83CDFFF", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFF3", "200D", "D83CDF08");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFC7");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFC7", "D83CDFFB");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFC7", "D83CDFFC");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFC7", "D83CDFFD");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFC7", "D83CDFFE");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFC7", "D83CDFFF");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFCB");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFCB", "D83CDFFB");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFCB", "D83CDFFC");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFCB", "D83CDFFD");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFCB", "D83CDFFE");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFCB", "D83CDFFF");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFCB", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFCB", "D83CDFFB", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFCB", "D83CDFFC", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFCB", "D83CDFFD", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFCB", "D83CDFFE", "200D", "2640");
            AddTreeNodes(_joinedEmojiTree, 0, "D83CDFCB", "D83CDFFF", "200D", "2640");

            _skinsDict["D83CDFFB"] = "D83CDFFB";
            _skinsDict["D83CDFFC"] = "D83CDFFC";
            _skinsDict["D83CDFFD"] = "D83CDFFD";
            _skinsDict["D83CDFFE"] = "D83CDFFE";
            _skinsDict["D83CDFFF"] = "D83CDFFF";

            _skinnedDict["D83DDC41"] = "D83DDC41";
            _skinnedDict["D83DDC6F"] = "D83DDC6F"; // workaround
            _skinnedDict["D83EDD26"] = "D83EDD26"; // workaround
            _skinnedDict["D83EDD37"] = "D83EDD37"; // workaround
            _skinnedDict["D83DDD75"] = "D83DDD75"; // workaround
            _skinnedDict["D83EDD3C"] = "D83EDD3C"; // workaround
            _skinnedDict["D83EDD38"] = "D83EDD38"; // workaround
            _skinnedDict["D83EDD3E"] = "D83EDD3E"; // workaround
            _skinnedDict["D83CDFCC"] = "D83CDFCC"; // workaround
            _skinnedDict["D83EDD3D"] = "D83EDD3D"; // workaround
            _skinnedDict["D83EDD39"] = "D83EDD39"; // workaround
            _skinnedDict["D83CDFF3"] = "D83CDFF3"; // workaround
            _skinnedDict["D83DDC6E"] = "D83DDC6E"; // workaround
            _skinnedDict["D83DDC82"] = "D83DDC82"; // workaround
            _skinnedDict["D83DDC77"] = "D83DDC77"; // workaround
            _skinnedDict["D83DDC73"] = "D83DDC73"; // workaround
            _skinnedDict["D83DDC71"] = "D83DDC71"; // workaround
            _skinnedDict["D83DDE47"] = "D83DDE47"; // workaround
            _skinnedDict["D83DDC81"] = "D83DDC81"; // workaround
            _skinnedDict["D83DDE45"] = "D83DDE45"; // workaround
            _skinnedDict["D83DDE46"] = "D83DDE46"; // workaround
            _skinnedDict["D83DDE4B"] = "D83DDE4B"; // workaround
            _skinnedDict["D83DDD26"] = "D83DDD26"; // workaround
            _skinnedDict["D83DDE4E"] = "D83DDE4E"; // workaround
            _skinnedDict["D83DDE4D"] = "D83DDE4D"; // workaround
            _skinnedDict["D83DDC87"] = "D83DDC87"; // workaround
            _skinnedDict["D83DDC86"] = "D83DDC86"; // workaround
            _skinnedDict["D83DDEB6"] = "D83DDEB6"; // workaround
            _skinnedDict["D83CDFC3"] = "D83CDFC3"; // workaround
            _skinnedDict["D83DDC69"] = "D83DDC69"; // workaround
            _skinnedDict["D83DDC68"] = "D83DDC68"; // workaround
            _skinnedDict["D83DDEB4"] = "D83DDEB4"; // workaround
            _skinnedDict["D83DDEB5"] = "D83DDEB5"; // workaround
            _skinnedDict["D83CDFC7"] = "D83CDFC7"; // workaround
            _skinnedDict["D83DDEA3"] = "D83DDEA3"; // workaround
            _skinnedDict["D83CDFCA"] = "D83CDFCA"; // workaround
            _skinnedDict["D83CDFC4"] = "D83CDFC4"; // workaround
            _skinnedDict["26F9"] = "26F9"; // workaround
            _skinnedDict["D83CDFCB"] = "D83CDFCB"; // workaround

            _skinnedDict["D83DDD74"] = "D83DDD74";
            _skinnedDict["D83DDD74D83CDFFB"] = "D83DDD74D83CDFFB";
            _skinnedDict["D83DDD74D83CDFFC"] = "D83DDD74D83CDFFC";
            _skinnedDict["D83DDD74D83CDFFD"] = "D83DDD74D83CDFFD";
            _skinnedDict["D83DDD74D83CDFFE"] = "D83DDD74D83CDFFE";
            _skinnedDict["D83DDD74D83CDFFF"] = "D83DDD74D83CDFFF";
            _skinnedDict["D83DDD7A"] = "D83DDD7A";
            _skinnedDict["D83DDD7AD83CDFFB"] = "D83DDD7AD83CDFFB";
            _skinnedDict["D83DDD7AD83CDFFC"] = "D83DDD7AD83CDFFC";
            _skinnedDict["D83DDD7AD83CDFFD"] = "D83DDD7AD83CDFFD";
            _skinnedDict["D83DDD7AD83CDFFE"] = "D83DDD7AD83CDFFE";
            _skinnedDict["D83DDD7AD83CDFFF"] = "D83DDD7AD83CDFFF";
            _skinnedDict["D83EDD30"] = "D83EDD30";
            _skinnedDict["D83EDD30D83CDFFB"] = "D83EDD30D83CDFFB";
            _skinnedDict["D83EDD30D83CDFFC"] = "D83EDD30D83CDFFC";
            _skinnedDict["D83EDD30D83CDFFD"] = "D83EDD30D83CDFFD";
            _skinnedDict["D83EDD30D83CDFFE"] = "D83EDD30D83CDFFE";
            _skinnedDict["D83EDD30D83CDFFF"] = "D83EDD30D83CDFFF";
            _skinnedDict["D83EDD34"] = "D83EDD34";
            _skinnedDict["D83EDD34D83CDFFB"] = "D83EDD34D83CDFFB";
            _skinnedDict["D83EDD34D83CDFFC"] = "D83EDD34D83CDFFC";
            _skinnedDict["D83EDD34D83CDFFD"] = "D83EDD34D83CDFFD";
            _skinnedDict["D83EDD34D83CDFFE"] = "D83EDD34D83CDFFE";
            _skinnedDict["D83EDD34D83CDFFF"] = "D83EDD34D83CDFFF";
            _skinnedDict["D83EDD35"] = "D83EDD35";
            _skinnedDict["D83EDD35D83CDFFB"] = "D83EDD35D83CDFFB";
            _skinnedDict["D83EDD35D83CDFFC"] = "D83EDD35D83CDFFC";
            _skinnedDict["D83EDD35D83CDFFD"] = "D83EDD35D83CDFFD";
            _skinnedDict["D83EDD35D83CDFFE"] = "D83EDD35D83CDFFE";
            _skinnedDict["D83EDD35D83CDFFF"] = "D83EDD35D83CDFFF";
            _skinnedDict["D83EDD36"] = "D83EDD36";
            _skinnedDict["D83EDD36D83CDFFB"] = "D83EDD36D83CDFFB";
            _skinnedDict["D83EDD36D83CDFFC"] = "D83EDD36D83CDFFC";
            _skinnedDict["D83EDD36D83CDFFD"] = "D83EDD36D83CDFFD";
            _skinnedDict["D83EDD36D83CDFFE"] = "D83EDD36D83CDFFE";
            _skinnedDict["D83EDD36D83CDFFF"] = "D83EDD36D83CDFFF";
            _skinnedDict["D83EDD1A"] = "D83EDD1A";
            _skinnedDict["D83EDD1AD83CDFFB"] = "D83EDD1AD83CDFFB";
            _skinnedDict["D83EDD1AD83CDFFC"] = "D83EDD1AD83CDFFC";
            _skinnedDict["D83EDD1AD83CDFFD"] = "D83EDD1AD83CDFFD";
            _skinnedDict["D83EDD1AD83CDFFE"] = "D83EDD1AD83CDFFE";
            _skinnedDict["D83EDD1AD83CDFFF"] = "D83EDD1AD83CDFFF";
            _skinnedDict["D83EDD1B"] = "D83EDD1B";
            _skinnedDict["D83EDD1BD83CDFFB"] = "D83EDD1BD83CDFFB";
            _skinnedDict["D83EDD1BD83CDFFC"] = "D83EDD1BD83CDFFC";
            _skinnedDict["D83EDD1BD83CDFFD"] = "D83EDD1BD83CDFFD";
            _skinnedDict["D83EDD1BD83CDFFE"] = "D83EDD1BD83CDFFE";
            _skinnedDict["D83EDD1BD83CDFFF"] = "D83EDD1BD83CDFFF";
            _skinnedDict["D83EDD1C"] = "D83EDD1C";
            _skinnedDict["D83EDD1CD83CDFFB"] = "D83EDD1CD83CDFFB";
            _skinnedDict["D83EDD1CD83CDFFC"] = "D83EDD1CD83CDFFC";
            _skinnedDict["D83EDD1CD83CDFFD"] = "D83EDD1CD83CDFFD";
            _skinnedDict["D83EDD1CD83CDFFE"] = "D83EDD1CD83CDFFE";
            _skinnedDict["D83EDD1CD83CDFFF"] = "D83EDD1CD83CDFFF";
            _skinnedDict["D83EDD1E"] = "D83EDD1E";
            _skinnedDict["D83EDD1ED83CDFFB"] = "D83EDD1ED83CDFFB";
            _skinnedDict["D83EDD1ED83CDFFC"] = "D83EDD1ED83CDFFC";
            _skinnedDict["D83EDD1ED83CDFFD"] = "D83EDD1ED83CDFFD";
            _skinnedDict["D83EDD1ED83CDFFE"] = "D83EDD1ED83CDFFE";
            _skinnedDict["D83EDD1ED83CDFFF"] = "D83EDD1ED83CDFFF";
            _skinnedDict["D83EDD19"] = "D83EDD19";
            _skinnedDict["D83EDD19D83CDFFB"] = "D83EDD19D83CDFFB";
            _skinnedDict["D83EDD19D83CDFFC"] = "D83EDD19D83CDFFC";
            _skinnedDict["D83EDD19D83CDFFD"] = "D83EDD19D83CDFFD";
            _skinnedDict["D83EDD19D83CDFFE"] = "D83EDD19D83CDFFE";
            _skinnedDict["D83EDD19D83CDFFF"] = "D83EDD19D83CDFFF";
            _skinnedDict["D83DDE4C"] = "D83DDE4C";
            _skinnedDict["D83DDE4CD83CDFFB"] = "D83DDE4CD83CDFFB";
            _skinnedDict["D83DDE4CD83CDFFC"] = "D83DDE4CD83CDFFC";
            _skinnedDict["D83DDE4CD83CDFFD"] = "D83DDE4CD83CDFFD";
            _skinnedDict["D83DDE4CD83CDFFE"] = "D83DDE4CD83CDFFE";
            _skinnedDict["D83DDE4CD83CDFFF"] = "D83DDE4CD83CDFFF";
            _skinnedDict["D83DDC4F"] = "D83DDC4F";
            _skinnedDict["D83DDC4FD83CDFFB"] = "D83DDC4FD83CDFFB";
            _skinnedDict["D83DDC4FD83CDFFC"] = "D83DDC4FD83CDFFC";
            _skinnedDict["D83DDC4FD83CDFFD"] = "D83DDC4FD83CDFFD";
            _skinnedDict["D83DDC4FD83CDFFE"] = "D83DDC4FD83CDFFE";
            _skinnedDict["D83DDC4FD83CDFFF"] = "D83DDC4FD83CDFFF";
            _skinnedDict["D83DDC4B"] = "D83DDC4B";
            _skinnedDict["D83DDC4BD83CDFFB"] = "D83DDC4BD83CDFFB";
            _skinnedDict["D83DDC4BD83CDFFC"] = "D83DDC4BD83CDFFC";
            _skinnedDict["D83DDC4BD83CDFFD"] = "D83DDC4BD83CDFFD";
            _skinnedDict["D83DDC4BD83CDFFE"] = "D83DDC4BD83CDFFE";
            _skinnedDict["D83DDC4BD83CDFFF"] = "D83DDC4BD83CDFFF";
            _skinnedDict["D83DDC4D"] = "D83DDC4D";
            _skinnedDict["D83DDC4DD83CDFFB"] = "D83DDC4DD83CDFFB";
            _skinnedDict["D83DDC4DD83CDFFC"] = "D83DDC4DD83CDFFC";
            _skinnedDict["D83DDC4DD83CDFFD"] = "D83DDC4DD83CDFFD";
            _skinnedDict["D83DDC4DD83CDFFE"] = "D83DDC4DD83CDFFE";
            _skinnedDict["D83DDC4DD83CDFFF"] = "D83DDC4DD83CDFFF";
            _skinnedDict["D83DDC4E"] = "D83DDC4E";
            _skinnedDict["D83DDC4ED83CDFFB"] = "D83DDC4ED83CDFFB";
            _skinnedDict["D83DDC4ED83CDFFC"] = "D83DDC4ED83CDFFC";
            _skinnedDict["D83DDC4ED83CDFFD"] = "D83DDC4ED83CDFFD";
            _skinnedDict["D83DDC4ED83CDFFE"] = "D83DDC4ED83CDFFE";
            _skinnedDict["D83DDC4ED83CDFFF"] = "D83DDC4ED83CDFFF";
            _skinnedDict["D83DDC4A"] = "D83DDC4A";
            _skinnedDict["D83DDC4AD83CDFFB"] = "D83DDC4AD83CDFFB";
            _skinnedDict["D83DDC4AD83CDFFC"] = "D83DDC4AD83CDFFC";
            _skinnedDict["D83DDC4AD83CDFFD"] = "D83DDC4AD83CDFFD";
            _skinnedDict["D83DDC4AD83CDFFE"] = "D83DDC4AD83CDFFE";
            _skinnedDict["D83DDC4AD83CDFFF"] = "D83DDC4AD83CDFFF";
            _skinnedDict["D83DDC4C"] = "D83DDC4C";
            _skinnedDict["D83DDC4CD83CDFFB"] = "D83DDC4CD83CDFFB";
            _skinnedDict["D83DDC4CD83CDFFC"] = "D83DDC4CD83CDFFC";
            _skinnedDict["D83DDC4CD83CDFFD"] = "D83DDC4CD83CDFFD";
            _skinnedDict["D83DDC4CD83CDFFE"] = "D83DDC4CD83CDFFE";
            _skinnedDict["D83DDC4CD83CDFFF"] = "D83DDC4CD83CDFFF";
            _skinnedDict["D83DDC46"] = "D83DDC46";
            _skinnedDict["D83DDC46D83CDFFB"] = "D83DDC46D83CDFFB";
            _skinnedDict["D83DDC46D83CDFFC"] = "D83DDC46D83CDFFC";
            _skinnedDict["D83DDC46D83CDFFD"] = "D83DDC46D83CDFFD";
            _skinnedDict["D83DDC46D83CDFFE"] = "D83DDC46D83CDFFE";
            _skinnedDict["D83DDC46D83CDFFF"] = "D83DDC46D83CDFFF";
            _skinnedDict["D83DDC47"] = "D83DDC47";
            _skinnedDict["D83DDC47D83CDFFB"] = "D83DDC47D83CDFFB";
            _skinnedDict["D83DDC47D83CDFFC"] = "D83DDC47D83CDFFC";
            _skinnedDict["D83DDC47D83CDFFD"] = "D83DDC47D83CDFFD";
            _skinnedDict["D83DDC47D83CDFFE"] = "D83DDC47D83CDFFE";
            _skinnedDict["D83DDC47D83CDFFF"] = "D83DDC47D83CDFFF";
            _skinnedDict["D83DDC48"] = "D83DDC48";
            _skinnedDict["D83DDC48D83CDFFB"] = "D83DDC48D83CDFFB";
            _skinnedDict["D83DDC48D83CDFFC"] = "D83DDC48D83CDFFC";
            _skinnedDict["D83DDC48D83CDFFD"] = "D83DDC48D83CDFFD";
            _skinnedDict["D83DDC48D83CDFFE"] = "D83DDC48D83CDFFE";
            _skinnedDict["D83DDC48D83CDFFF"] = "D83DDC48D83CDFFF";
            _skinnedDict["D83DDC49"] = "D83DDC49";
            _skinnedDict["D83DDC49D83CDFFB"] = "D83DDC49D83CDFFB";
            _skinnedDict["D83DDC49D83CDFFC"] = "D83DDC49D83CDFFC";
            _skinnedDict["D83DDC49D83CDFFD"] = "D83DDC49D83CDFFD";
            _skinnedDict["D83DDC49D83CDFFE"] = "D83DDC49D83CDFFE";
            _skinnedDict["D83DDC49D83CDFFF"] = "D83DDC49D83CDFFF";
            _skinnedDict["D83DDC50"] = "D83DDC50";
            _skinnedDict["D83DDC50D83CDFFB"] = "D83DDC50D83CDFFB";
            _skinnedDict["D83DDC50D83CDFFC"] = "D83DDC50D83CDFFC";
            _skinnedDict["D83DDC50D83CDFFD"] = "D83DDC50D83CDFFD";
            _skinnedDict["D83DDC50D83CDFFE"] = "D83DDC50D83CDFFE";
            _skinnedDict["D83DDC50D83CDFFF"] = "D83DDC50D83CDFFF";
            _skinnedDict["D83DDCAA"] = "D83DDCAA";
            _skinnedDict["D83DDCAAD83CDFFB"] = "D83DDCAAD83CDFFB";
            _skinnedDict["D83DDCAAD83CDFFC"] = "D83DDCAAD83CDFFC";
            _skinnedDict["D83DDCAAD83CDFFD"] = "D83DDCAAD83CDFFD";
            _skinnedDict["D83DDCAAD83CDFFE"] = "D83DDCAAD83CDFFE";
            _skinnedDict["D83DDCAAD83CDFFF"] = "D83DDCAAD83CDFFF";
            _skinnedDict["D83DDE4F"] = "D83DDE4F";
            _skinnedDict["D83DDE4FD83CDFFB"] = "D83DDE4FD83CDFFB";
            _skinnedDict["D83DDE4FD83CDFFC"] = "D83DDE4FD83CDFFC";
            _skinnedDict["D83DDE4FD83CDFFD"] = "D83DDE4FD83CDFFD";
            _skinnedDict["D83DDE4FD83CDFFE"] = "D83DDE4FD83CDFFE";
            _skinnedDict["D83DDE4FD83CDFFF"] = "D83DDE4FD83CDFFF";
            _skinnedDict["D83DDD95"] = "D83DDD95";
            _skinnedDict["D83DDD95D83CDFFB"] = "D83DDD95D83CDFFB";
            _skinnedDict["D83DDD95D83CDFFC"] = "D83DDD95D83CDFFC";
            _skinnedDict["D83DDD95D83CDFFD"] = "D83DDD95D83CDFFD";
            _skinnedDict["D83DDD95D83CDFFE"] = "D83DDD95D83CDFFE";
            _skinnedDict["D83DDD95D83CDFFF"] = "D83DDD95D83CDFFF";
            _skinnedDict["D83DDD90"] = "D83DDD90";
            _skinnedDict["D83DDD90D83CDFFB"] = "D83DDD90D83CDFFB";
            _skinnedDict["D83DDD90D83CDFFC"] = "D83DDD90D83CDFFC";
            _skinnedDict["D83DDD90D83CDFFD"] = "D83DDD90D83CDFFD";
            _skinnedDict["D83DDD90D83CDFFE"] = "D83DDD90D83CDFFE";
            _skinnedDict["D83DDD90D83CDFFF"] = "D83DDD90D83CDFFF";
            _skinnedDict["D83EDD18"] = "D83EDD18";
            _skinnedDict["D83EDD18D83CDFFB"] = "D83EDD18D83CDFFB";
            _skinnedDict["D83EDD18D83CDFFC"] = "D83EDD18D83CDFFC";
            _skinnedDict["D83EDD18D83CDFFD"] = "D83EDD18D83CDFFD";
            _skinnedDict["D83EDD18D83CDFFE"] = "D83EDD18D83CDFFE";
            _skinnedDict["D83EDD18D83CDFFF"] = "D83EDD18D83CDFFF";
            _skinnedDict["D83DDD96"] = "D83DDD96";
            _skinnedDict["D83DDD96D83CDFFB"] = "D83DDD96D83CDFFB";
            _skinnedDict["D83DDD96D83CDFFC"] = "D83DDD96D83CDFFC";
            _skinnedDict["D83DDD96D83CDFFD"] = "D83DDD96D83CDFFD";
            _skinnedDict["D83DDD96D83CDFFE"] = "D83DDD96D83CDFFE";
            _skinnedDict["D83DDD96D83CDFFF"] = "D83DDD96D83CDFFF";
            _skinnedDict["D83DDC85"] = "D83DDC85";
            _skinnedDict["D83DDC85D83CDFFB"] = "D83DDC85D83CDFFB";
            _skinnedDict["D83DDC85D83CDFFC"] = "D83DDC85D83CDFFC";
            _skinnedDict["D83DDC85D83CDFFD"] = "D83DDC85D83CDFFD";
            _skinnedDict["D83DDC85D83CDFFE"] = "D83DDC85D83CDFFE";
            _skinnedDict["D83DDC85D83CDFFF"] = "D83DDC85D83CDFFF";
            _skinnedDict["D83DDC42"] = "D83DDC42";
            _skinnedDict["D83DDC42D83CDFFB"] = "D83DDC42D83CDFFB";
            _skinnedDict["D83DDC42D83CDFFC"] = "D83DDC42D83CDFFC";
            _skinnedDict["D83DDC42D83CDFFD"] = "D83DDC42D83CDFFD";
            _skinnedDict["D83DDC42D83CDFFE"] = "D83DDC42D83CDFFE";
            _skinnedDict["D83DDC42D83CDFFF"] = "D83DDC42D83CDFFF";
            _skinnedDict["D83DDC43"] = "D83DDC43";
            _skinnedDict["D83DDC43D83CDFFB"] = "D83DDC43D83CDFFB";
            _skinnedDict["D83DDC43D83CDFFC"] = "D83DDC43D83CDFFC";
            _skinnedDict["D83DDC43D83CDFFD"] = "D83DDC43D83CDFFD";
            _skinnedDict["D83DDC43D83CDFFE"] = "D83DDC43D83CDFFE";
            _skinnedDict["D83DDC43D83CDFFF"] = "D83DDC43D83CDFFF";
            _skinnedDict["D83DDC76"] = "D83DDC76";
            _skinnedDict["D83DDC76D83CDFFB"] = "D83DDC76D83CDFFB";
            _skinnedDict["D83DDC76D83CDFFC"] = "D83DDC76D83CDFFC";
            _skinnedDict["D83DDC76D83CDFFD"] = "D83DDC76D83CDFFD";
            _skinnedDict["D83DDC76D83CDFFE"] = "D83DDC76D83CDFFE";
            _skinnedDict["D83DDC76D83CDFFF"] = "D83DDC76D83CDFFF";
            _skinnedDict["D83DDC66"] = "D83DDC66";
            _skinnedDict["D83DDC66D83CDFFB"] = "D83DDC66D83CDFFB";
            _skinnedDict["D83DDC66D83CDFFC"] = "D83DDC66D83CDFFC";
            _skinnedDict["D83DDC66D83CDFFD"] = "D83DDC66D83CDFFD";
            _skinnedDict["D83DDC66D83CDFFE"] = "D83DDC66D83CDFFE";
            _skinnedDict["D83DDC66D83CDFFF"] = "D83DDC66D83CDFFF";
            _skinnedDict["D83DDC67"] = "D83DDC67";
            _skinnedDict["D83DDC67D83CDFFB"] = "D83DDC67D83CDFFB";
            _skinnedDict["D83DDC67D83CDFFC"] = "D83DDC67D83CDFFC";
            _skinnedDict["D83DDC67D83CDFFD"] = "D83DDC67D83CDFFD";
            _skinnedDict["D83DDC67D83CDFFE"] = "D83DDC67D83CDFFE";
            _skinnedDict["D83DDC67D83CDFFF"] = "D83DDC67D83CDFFF";
            _skinnedDict["D83DDC74"] = "D83DDC74";
            _skinnedDict["D83DDC74D83CDFFB"] = "D83DDC74D83CDFFB";
            _skinnedDict["D83DDC74D83CDFFC"] = "D83DDC74D83CDFFC";
            _skinnedDict["D83DDC74D83CDFFD"] = "D83DDC74D83CDFFD";
            _skinnedDict["D83DDC74D83CDFFE"] = "D83DDC74D83CDFFE";
            _skinnedDict["D83DDC74D83CDFFF"] = "D83DDC74D83CDFFF";
            _skinnedDict["D83DDC75"] = "D83DDC75";
            _skinnedDict["D83DDC75D83CDFFB"] = "D83DDC75D83CDFFB";
            _skinnedDict["D83DDC75D83CDFFC"] = "D83DDC75D83CDFFC";
            _skinnedDict["D83DDC75D83CDFFD"] = "D83DDC75D83CDFFD";
            _skinnedDict["D83DDC75D83CDFFE"] = "D83DDC75D83CDFFE";
            _skinnedDict["D83DDC75D83CDFFF"] = "D83DDC75D83CDFFF";
            _skinnedDict["D83DDC72"] = "D83DDC72";
            _skinnedDict["D83DDC72D83CDFFB"] = "D83DDC72D83CDFFB";
            _skinnedDict["D83DDC72D83CDFFC"] = "D83DDC72D83CDFFC";
            _skinnedDict["D83DDC72D83CDFFD"] = "D83DDC72D83CDFFD";
            _skinnedDict["D83DDC72D83CDFFE"] = "D83DDC72D83CDFFE";
            _skinnedDict["D83DDC72D83CDFFF"] = "D83DDC72D83CDFFF";
            _skinnedDict["D83DDC83"] = "D83DDC83";
            _skinnedDict["D83DDC83D83CDFFB"] = "D83DDC83D83CDFFB";
            _skinnedDict["D83DDC83D83CDFFC"] = "D83DDC83D83CDFFC";
            _skinnedDict["D83DDC83D83CDFFD"] = "D83DDC83D83CDFFD";
            _skinnedDict["D83DDC83D83CDFFE"] = "D83DDC83D83CDFFE";
            _skinnedDict["D83DDC83D83CDFFF"] = "D83DDC83D83CDFFF";
            _skinnedDict["D83CDF85"] = "D83CDF85";
            _skinnedDict["D83CDF85D83CDFFB"] = "D83CDF85D83CDFFB";
            _skinnedDict["D83CDF85D83CDFFC"] = "D83CDF85D83CDFFC";
            _skinnedDict["D83CDF85D83CDFFD"] = "D83CDF85D83CDFFD";
            _skinnedDict["D83CDF85D83CDFFE"] = "D83CDF85D83CDFFE";
            _skinnedDict["D83CDF85D83CDFFF"] = "D83CDF85D83CDFFF";
            _skinnedDict["D83DDC7C"] = "D83DDC7C";
            _skinnedDict["D83DDC7CD83CDFFB"] = "D83DDC7CD83CDFFB";
            _skinnedDict["D83DDC7CD83CDFFC"] = "D83DDC7CD83CDFFC";
            _skinnedDict["D83DDC7CD83CDFFD"] = "D83DDC7CD83CDFFD";
            _skinnedDict["D83DDC7CD83CDFFE"] = "D83DDC7CD83CDFFE";
            _skinnedDict["D83DDC7CD83CDFFF"] = "D83DDC7CD83CDFFF";
            _skinnedDict["D83DDC78"] = "D83DDC78";
            _skinnedDict["D83DDC78D83CDFFB"] = "D83DDC78D83CDFFB";
            _skinnedDict["D83DDC78D83CDFFC"] = "D83DDC78D83CDFFC";
            _skinnedDict["D83DDC78D83CDFFD"] = "D83DDC78D83CDFFD";
            _skinnedDict["D83DDC78D83CDFFE"] = "D83DDC78D83CDFFE";
            _skinnedDict["D83DDC78D83CDFFF"] = "D83DDC78D83CDFFF";
            _skinnedDict["D83DDC70"] = "D83DDC70";
            _skinnedDict["D83DDC70D83CDFFB"] = "D83DDC70D83CDFFB";
            _skinnedDict["D83DDC70D83CDFFC"] = "D83DDC70D83CDFFC";
            _skinnedDict["D83DDC70D83CDFFD"] = "D83DDC70D83CDFFD";
            _skinnedDict["D83DDC70D83CDFFE"] = "D83DDC70D83CDFFE";
            _skinnedDict["D83DDC70D83CDFFF"] = "D83DDC70D83CDFFF";
            _skinnedDict["D83DDEC0"] = "D83DDEC0";
            _skinnedDict["D83DDEC0D83CDFFB"] = "D83DDEC0D83CDFFB";
            _skinnedDict["D83DDEC0D83CDFFC"] = "D83DDEC0D83CDFFC";
            _skinnedDict["D83DDEC0D83CDFFD"] = "D83DDEC0D83CDFFD";
            _skinnedDict["D83DDEC0D83CDFFE"] = "D83DDEC0D83CDFFE";
            _skinnedDict["D83DDEC0D83CDFFF"] = "D83DDEC0D83CDFFF";

            _skinnedDict["270A"] = "270A";
            _skinnedDict["270AD83CDFFB"] = "270AD83CDFFB";
            _skinnedDict["270AD83CDFFC"] = "270AD83CDFFC";
            _skinnedDict["270AD83CDFFD"] = "270AD83CDFFD";
            _skinnedDict["270AD83CDFFE"] = "270AD83CDFFE";
            _skinnedDict["270AD83CDFFF"] = "270AD83CDFFF";
            _skinnedDict["270B"] = "270B";
            _skinnedDict["270BD83CDFFB"] = "270BD83CDFFB";
            _skinnedDict["270BD83CDFFC"] = "270BD83CDFFC";
            _skinnedDict["270BD83CDFFD"] = "270BD83CDFFD";
            _skinnedDict["270BD83CDFFE"] = "270BD83CDFFE";
            _skinnedDict["270BD83CDFFF"] = "270BD83CDFFF";
            _skinnedDict["270C"] = "270C";
            _skinnedDict["270CD83CDFFB"] = "270CD83CDFFB";
            _skinnedDict["270CD83CDFFC"] = "270CD83CDFFC";
            _skinnedDict["270CD83CDFFD"] = "270CD83CDFFD";
            _skinnedDict["270CD83CDFFE"] = "270CD83CDFFE";
            _skinnedDict["270CD83CDFFF"] = "270CD83CDFFF";
            _skinnedDict["270D"] = "270D";
            _skinnedDict["270DD83CDFFB"] = "270DD83CDFFB";
            _skinnedDict["270DD83CDFFC"] = "270DD83CDFFC";
            _skinnedDict["270DD83CDFFD"] = "270DD83CDFFD";
            _skinnedDict["270DD83CDFFE"] = "270DD83CDFFE";
            _skinnedDict["270DD83CDFFF"] = "270DD83CDFFF";
            _skinnedDict["261D"] = "261D";
            _skinnedDict["261DD83CDFFB"] = "261DD83CDFFB";
            _skinnedDict["261DD83CDFFC"] = "261DD83CDFFC";
            _skinnedDict["261DD83CDFFD"] = "261DD83CDFFD";
            _skinnedDict["261DD83CDFFE"] = "261DD83CDFFE";
            _skinnedDict["261DD83CDFFF"] = "261DD83CDFFF";

            _dict["002320E3"] = "002320E3";
            _dict["003020E3"] = "003020E3";
            _dict["003120E3"] = "003120E3";
            _dict["003220E3"] = "003220E3";
            _dict["003320E3"] = "003320E3";
            _dict["003420E3"] = "003420E3";
            _dict["003520E3"] = "003520E3";
            _dict["003620E3"] = "003620E3";
            _dict["003720E3"] = "003720E3";
            _dict["003820E3"] = "003820E3";
            _dict["003920E3"] = "003920E3";
            _dict["00A9"] = "00A9";
            _dict["00AE"] = "00AE";
            _dict["203C"] = "203C";
            _dict["2049"] = "2049";
            _dict["2122"] = "2122";
            _dict["2139"] = "2139";
            _dict["2194"] = "2194";
            _dict["2195"] = "2195";
            _dict["2196"] = "2196";
            _dict["2197"] = "2197";
            _dict["2198"] = "2198";
            _dict["2199"] = "2199";
            _dict["21A9"] = "21A9";
            _dict["21AA"] = "21AA";
            _dict["231A"] = "231A";
            _dict["231B"] = "231B";
            _dict["2328"] = "2328";
            _dict["23E9"] = "23E9";
            _dict["23EA"] = "23EA";
            _dict["23EB"] = "23EB";
            _dict["23EC"] = "23EC";
            _dict["23ED"] = "23ED";
            _dict["23EE"] = "23EE";
            _dict["23EF"] = "23EF";
            _dict["23F0"] = "23F0";
            _dict["23F1"] = "23F1";
            _dict["23F2"] = "23F2";
            _dict["23F3"] = "23F3";
            _dict["23F8"] = "23F8";
            _dict["23F9"] = "23F9";
            _dict["23FA"] = "23FA";
            _dict["24C2"] = "24C2";
            _dict["25AA"] = "25AA";
            _dict["25AB"] = "25AB";
            _dict["25B6"] = "25B6";
            _dict["25C0"] = "25C0";
            _dict["25FB"] = "25FB";
            _dict["25FC"] = "25FC";
            _dict["25FD"] = "25FD";
            _dict["25FE"] = "25FE";
            _dict["2600"] = "2600";
            _dict["2601"] = "2601";
            _dict["2602"] = "2602";
            _dict["2603"] = "2603";
            _dict["2604"] = "2604";
            _dict["260E"] = "260E";
            _dict["2611"] = "2611";
            _dict["2614"] = "2614";
            _dict["2618"] = "2618";
            _dict["2614FE0F"] = "2614";
            _dict["2615"] = "2615";
            _dict["261D"] = "261D";
            _dict["2620"] = "2620";
            _dict["2622"] = "2622";
            _dict["2623"] = "2623";
            _dict["2626"] = "2626";
            _dict["262A"] = "262A";
            _dict["262E"] = "262E";
            _dict["262F"] = "262F";
            _dict["2638"] = "2638";
            _dict["2639"] = "2639";
            _dict["263A"] = "263A";
            _dict["2648"] = "2648";
            _dict["2649"] = "2649";
            _dict["264A"] = "264A";
            _dict["264B"] = "264B";
            _dict["264C"] = "264C";
            _dict["264D"] = "264D";
            _dict["264E"] = "264E";
            _dict["264F"] = "264F";
            _dict["2650"] = "2650";
            _dict["2651"] = "2651";
            _dict["2652"] = "2652";
            _dict["2653"] = "2653";
            _dict["2660"] = "2660";
            _dict["2663"] = "2663";
            _dict["2665"] = "2665";
            _dict["2666"] = "2666";
            _dict["2668"] = "2668";
            _dict["267B"] = "267B";
            _dict["267F"] = "267F";
            _dict["2692"] = "2692";
            _dict["2693"] = "2693";
            _dict["2694"] = "2694";
            _dict["2696"] = "2696";
            _dict["2697"] = "2697";
            _dict["2699"] = "2699";
            _dict["269B"] = "269B";
            _dict["269C"] = "269C";
            _dict["26A0"] = "26A0";
            _dict["26A1"] = "26A1";
            _dict["26AA"] = "26AA";
            _dict["26AB"] = "26AB";
            _dict["26B0"] = "26B0";
            _dict["26B1"] = "26B1";
            _dict["26BD"] = "26BD";
            _dict["26BE"] = "26BE";
            _dict["26C4"] = "26C4";
            _dict["26C5"] = "26C5";
            _dict["26C8"] = "26C8";
            _dict["26CE"] = "26CE";
            _dict["26CF"] = "26CF";
            _dict["26D1"] = "26D1";
            _dict["26D3"] = "26D3";
            _dict["26D4"] = "26D4";
            _dict["26E9"] = "26E9";
            _dict["26EA"] = "26EA";
            _dict["26F0"] = "26F0";
            _dict["26F1"] = "26F1";
            _dict["26F2"] = "26F2";
            _dict["26F3"] = "26F3";
            _dict["26F4"] = "26F4";
            _dict["26F5"] = "26F5";
            _dict["26F7"] = "26F7";
            _dict["26F8"] = "26F8";
            _dict["26FA"] = "26FA";
            _dict["26FD"] = "26FD";
            _dict["2702"] = "2702";
            _dict["2705"] = "2705";
            _dict["2708"] = "2708";
            _dict["2709"] = "2709";
            _dict["270A"] = "270A";
            _dict["270B"] = "270B";
            _dict["270C"] = "270C";
            _dict["270F"] = "270F";
            _dict["2712"] = "2712";
            _dict["2714"] = "2714";
            _dict["2716"] = "2716";
            _dict["271D"] = "271D";
            _dict["2721"] = "2721";
            _dict["2728"] = "2728";
            _dict["2733"] = "2733";
            _dict["2734"] = "2734";
            _dict["2744"] = "2744";
            _dict["2747"] = "2747";
            _dict["274C"] = "274C";
            _dict["274E"] = "274E";
            _dict["2753"] = "2753";
            _dict["2754"] = "2754";
            _dict["2755"] = "2755";
            _dict["2757"] = "2757";
            _dict["2763"] = "2763";
            _dict["2764"] = "2764";
            _dict["2764FE0F"] = "2764";
            _dict["2795"] = "2795";
            _dict["2796"] = "2796";
            _dict["2797"] = "2797";
            _dict["27A1"] = "27A1";
            _dict["27B0"] = "27B0";
            _dict["27BF"] = "27BF";
            _dict["2934"] = "2934";
            _dict["2935"] = "2935";
            _dict["2B05"] = "2B05";
            _dict["2B06"] = "2B06";
            _dict["2B07"] = "2B07";
            _dict["2B1B"] = "2B1B";
            _dict["2B1C"] = "2B1C";
            _dict["2B50"] = "2B50";
            _dict["2B55"] = "2B55";
            _dict["3030"] = "3030";
            _dict["303D"] = "303D";
            _dict["3297"] = "3297";
            _dict["3299"] = "3299";
            _dict["D83CDC04"] = "D83CDC04";
            _dict["D83CDCCF"] = "D83CDCCF";
            _dict["D83CDD70"] = "D83CDD70";
            _dict["D83CDD71"] = "D83CDD71";
            _dict["D83CDD7E"] = "D83CDD7E";
            _dict["D83CDD7F"] = "D83CDD7F";
            _dict["D83CDD8E"] = "D83CDD8E";
            _dict["D83CDD91"] = "D83CDD91";
            _dict["D83CDD92"] = "D83CDD92";
            _dict["D83CDD93"] = "D83CDD93";
            _dict["D83CDD94"] = "D83CDD94";
            _dict["D83CDD95"] = "D83CDD95";
            _dict["D83CDD96"] = "D83CDD96";
            _dict["D83CDD97"] = "D83CDD97";
            _dict["D83CDD98"] = "D83CDD98";
            _dict["D83CDD99"] = "D83CDD99";
            _dict["D83CDD9A"] = "D83CDD9A";
            _dict["D83CDDE8D83CDDF3"] = "D83CDDE8D83CDDF3";
            _dict["D83CDDE9D83CDDEA"] = "D83CDDE9D83CDDEA";
            _dict["D83CDDEAD83CDDF8"] = "D83CDDEAD83CDDF8";
            _dict["D83CDDEBD83CDDF7"] = "D83CDDEBD83CDDF7";
            _dict["D83CDDECD83CDDE7"] = "D83CDDECD83CDDE7";
            _dict["D83CDDEED83CDDF9"] = "D83CDDEED83CDDF9";
            _dict["D83CDDEFD83CDDF5"] = "D83CDDEFD83CDDF5";
            _dict["D83CDDF0D83CDDF7"] = "D83CDDF0D83CDDF7";
            _dict["D83CDDF7D83CDDFA"] = "D83CDDF7D83CDDFA";
            _dict["D83CDDFAD83CDDF8"] = "D83CDDFAD83CDDF8";

            _dict["D83CDDE6D83CDDFA"] = "D83CDDE6D83CDDFA";
            _dict["D83CDDE6D83CDDF9"] = "D83CDDE6D83CDDF9";
            _dict["D83CDDE6D83CDDFF"] = "D83CDDE6D83CDDFF";
            _dict["D83CDDE6D83CDDFD"] = "D83CDDE6D83CDDFD";
            _dict["D83CDDE6D83CDDF1"] = "D83CDDE6D83CDDF1";
            _dict["D83CDDE9D83CDDFF"] = "D83CDDE9D83CDDFF";
            _dict["D83CDDE6D83CDDF8"] = "D83CDDE6D83CDDF8";
            _dict["D83CDDE6D83CDDEE"] = "D83CDDE6D83CDDEE";
            _dict["D83CDDE6D83CDDF4"] = "D83CDDE6D83CDDF4";
            _dict["D83CDDE6D83CDDE9"] = "D83CDDE6D83CDDE9";
            _dict["D83CDDE6D83CDDF6"] = "D83CDDE6D83CDDF6";
            _dict["D83CDDE6D83CDDEC"] = "D83CDDE6D83CDDEC";
            _dict["D83CDDE6D83CDDF7"] = "D83CDDE6D83CDDF7";
            _dict["D83CDDE6D83CDDF2"] = "D83CDDE6D83CDDF2";
            _dict["D83CDDE6D83CDDFC"] = "D83CDDE6D83CDDFC";
            _dict["D83CDDE6D83CDDEB"] = "D83CDDE6D83CDDEB";
            _dict["D83CDDE7D83CDDF8"] = "D83CDDE7D83CDDF8";
            _dict["D83CDDE7D83CDDE9"] = "D83CDDE7D83CDDE9";
            _dict["D83CDDE7D83CDDE7"] = "D83CDDE7D83CDDE7";
            _dict["D83CDDE7D83CDDED"] = "D83CDDE7D83CDDED";
            _dict["D83CDDE7D83CDDFE"] = "D83CDDE7D83CDDFE";
            _dict["D83CDDE7D83CDDFF"] = "D83CDDE7D83CDDFF";
            _dict["D83CDDE7D83CDDEA"] = "D83CDDE7D83CDDEA";
            _dict["D83CDDE7D83CDDEF"] = "D83CDDE7D83CDDEF";
            _dict["D83CDDE7D83CDDF2"] = "D83CDDE7D83CDDF2";
            _dict["D83CDDE7D83CDDEC"] = "D83CDDE7D83CDDEC";
            _dict["D83CDDE7D83CDDF4"] = "D83CDDE7D83CDDF4";
            _dict["D83CDDE7D83CDDF6"] = "D83CDDE7D83CDDF6";
            _dict["D83CDDE7D83CDDE6"] = "D83CDDE7D83CDDE6";
            _dict["D83CDDE7D83CDDFC"] = "D83CDDE7D83CDDFC";
            _dict["D83CDDE7D83CDDF7"] = "D83CDDE7D83CDDF7";
            _dict["D83CDDEED83CDDF4"] = "D83CDDEED83CDDF4";
            _dict["D83CDDE7D83CDDF3"] = "D83CDDE7D83CDDF3";
            _dict["D83CDDE7D83CDDEB"] = "D83CDDE7D83CDDEB";
            _dict["D83CDDE7D83CDDEE"] = "D83CDDE7D83CDDEE";
            _dict["D83CDDE7D83CDDF9"] = "D83CDDE7D83CDDF9";
            _dict["D83CDDFBD83CDDFA"] = "D83CDDFBD83CDDFA";
            _dict["D83CDDFBD83CDDE6"] = "D83CDDFBD83CDDE6";
            _dict["D83CDDECD83CDDE7"] = "D83CDDECD83CDDE7";
            _dict["D83CDDEDD83CDDFA"] = "D83CDDEDD83CDDFA";
            _dict["D83CDDFBD83CDDEA"] = "D83CDDFBD83CDDEA";
            _dict["D83CDDFBD83CDDEC"] = "D83CDDFBD83CDDEC";
            _dict["D83CDDFBD83CDDEE"] = "D83CDDFBD83CDDEE";
            _dict["D83CDDF9D83CDDF1"] = "D83CDDF9D83CDDF1";
            _dict["D83CDDFBD83CDDF3"] = "D83CDDFBD83CDDF3";
            _dict["D83CDDECD83CDDE6"] = "D83CDDECD83CDDE6";
            _dict["D83CDDEDD83CDDF9"] = "D83CDDEDD83CDDF9";
            _dict["D83CDDECD83CDDFE"] = "D83CDDECD83CDDFE";
            _dict["D83CDDECD83CDDF2"] = "D83CDDECD83CDDF2";
            _dict["D83CDDECD83CDDED"] = "D83CDDECD83CDDED";

            _dict["D83CDDECD83CDDF5"] = "D83CDDECD83CDDF5";
            _dict["D83CDDECD83CDDF9"] = "D83CDDECD83CDDF9";
            _dict["D83CDDECD83CDDF3"] = "D83CDDECD83CDDF3";
            _dict["D83CDDECD83CDDFC"] = "D83CDDECD83CDDFC";
            _dict["D83CDDE9D83CDDEA"] = "D83CDDE9D83CDDEA";
            _dict["D83CDDECD83CDDEC"] = "D83CDDECD83CDDEC";
            _dict["D83CDDECD83CDDEE"] = "D83CDDECD83CDDEE";
            _dict["D83CDDEDD83CDDF3"] = "D83CDDEDD83CDDF3";
            _dict["D83CDDEDD83CDDF0"] = "D83CDDEDD83CDDF0";
            _dict["D83CDDECD83CDDE9"] = "D83CDDECD83CDDE9";
            _dict["D83CDDECD83CDDF1"] = "D83CDDECD83CDDF1";
            _dict["D83CDDECD83CDDF7"] = "D83CDDECD83CDDF7";
            _dict["D83CDDECD83CDDEA"] = "D83CDDECD83CDDEA";
            _dict["D83CDDECD83CDDFA"] = "D83CDDECD83CDDFA";
            _dict["D83CDDE9D83CDDF0"] = "D83CDDE9D83CDDF0";
            _dict["D83CDDEFD83CDDEA"] = "D83CDDEFD83CDDEA";
            _dict["D83CDDE9D83CDDEF"] = "D83CDDE9D83CDDEF";
            _dict["D83CDDE9D83CDDF2"] = "D83CDDE9D83CDDF2";
            _dict["D83CDDE9D83CDDF4"] = "D83CDDE9D83CDDF4";
            _dict["D83CDDEAD83CDDFA"] = "D83CDDEAD83CDDFA";
            _dict["D83CDDEAD83CDDEC"] = "D83CDDEAD83CDDEC";
            _dict["D83CDDFFD83CDDF2"] = "D83CDDFFD83CDDF2";
            _dict["D83CDDEAD83CDDED"] = "D83CDDEAD83CDDED";
            _dict["D83CDDFFD83CDDFC"] = "D83CDDFFD83CDDFC";
            _dict["D83CDDEED83CDDF1"] = "D83CDDEED83CDDF1";
            _dict["D83CDDEED83CDDF3"] = "D83CDDEED83CDDF3";
            _dict["D83CDDEED83CDDE9"] = "D83CDDEED83CDDE9";
            _dict["D83CDDEFD83CDDF4"] = "D83CDDEFD83CDDF4";
            _dict["D83CDDEED83CDDF6"] = "D83CDDEED83CDDF6";
            _dict["D83CDDEED83CDDF7"] = "D83CDDEED83CDDF7";

            _dict["D83CDDEED83CDDEA"] = "D83CDDEED83CDDEA";
            _dict["D83CDDEED83CDDF8"] = "D83CDDEED83CDDF8";
            _dict["D83CDDEAD83CDDF8"] = "D83CDDEAD83CDDF8";
            _dict["D83CDDEED83CDDF9"] = "D83CDDEED83CDDF9";
            _dict["D83CDDFED83CDDEA"] = "D83CDDFED83CDDEA";
            _dict["D83CDDE8D83CDDFB"] = "D83CDDE8D83CDDFB";
            _dict["D83CDDF0D83CDDFF"] = "D83CDDF0D83CDDFF";
            _dict["D83CDDF0D83CDDFE"] = "D83CDDF0D83CDDFE";
            _dict["D83CDDF0D83CDDED"] = "D83CDDF0D83CDDED";
            _dict["D83CDDE8D83CDDF2"] = "D83CDDE8D83CDDF2";
            _dict["D83CDDE8D83CDDE6"] = "D83CDDE8D83CDDE6";
            _dict["D83CDDEED83CDDE8"] = "D83CDDEED83CDDE8";
            _dict["D83CDDF6D83CDDE6"] = "D83CDDF6D83CDDE6";
            _dict["D83CDDF0D83CDDEA"] = "D83CDDF0D83CDDEA";
            _dict["D83CDDE8D83CDDFE"] = "D83CDDE8D83CDDFE";
            _dict["D83CDDF0D83CDDEC"] = "D83CDDF0D83CDDEC";
            _dict["D83CDDF0D83CDDEE"] = "D83CDDF0D83CDDEE";
            _dict["D83CDDE8D83CDDF3"] = "D83CDDE8D83CDDF3";
            _dict["D83CDDF0D83CDDF5"] = "D83CDDF0D83CDDF5";
            _dict["D83CDDE8D83CDDE8"] = "D83CDDE8D83CDDE8";
            _dict["D83CDDE8D83CDDF4"] = "D83CDDE8D83CDDF4";
            _dict["D83CDDF0D83CDDF2"] = "D83CDDF0D83CDDF2";
            _dict["D83CDDE8D83CDDEC"] = "D83CDDE8D83CDDEC";
            _dict["D83CDDE8D83CDDE9"] = "D83CDDE8D83CDDE9";
            _dict["D83CDDFDD83CDDF0"] = "D83CDDFDD83CDDF0";
            _dict["D83CDDE8D83CDDF7"] = "D83CDDE8D83CDDF7";
            _dict["D83CDDE8D83CDDEE"] = "D83CDDE8D83CDDEE";
            _dict["D83CDDE8D83CDDFA"] = "D83CDDE8D83CDDFA";
            _dict["D83CDDF0D83CDDFC"] = "D83CDDF0D83CDDFC";
            _dict["D83CDDE8D83CDDFC"] = "D83CDDE8D83CDDFC";
            _dict["D83CDDF1D83CDDE6"] = "D83CDDF1D83CDDE6";
            _dict["D83CDDF1D83CDDFB"] = "D83CDDF1D83CDDFB";
            _dict["D83CDDF1D83CDDF8"] = "D83CDDF1D83CDDF8";
            _dict["D83CDDF1D83CDDF7"] = "D83CDDF1D83CDDF7";
            _dict["D83CDDF1D83CDDE7"] = "D83CDDF1D83CDDE7";
            _dict["D83CDDF1D83CDDFE"] = "D83CDDF1D83CDDFE";
            _dict["D83CDDF1D83CDDF9"] = "D83CDDF1D83CDDF9";
            _dict["D83CDDF1D83CDDEE"] = "D83CDDF1D83CDDEE";
            _dict["D83CDDF1D83CDDFA"] = "D83CDDF1D83CDDFA";
            _dict["D83CDDF2D83CDDFA"] = "D83CDDF2D83CDDFA";

            _dict["D83CDDF2D83CDDF7"] = "D83CDDF2D83CDDF7";
            _dict["D83CDDF2D83CDDEC"] = "D83CDDF2D83CDDEC";
            _dict["D83CDDFED83CDDF9"] = "D83CDDFED83CDDF9";
            _dict["D83CDDF2D83CDDF4"] = "D83CDDF2D83CDDF4";
            _dict["D83CDDF2D83CDDF0"] = "D83CDDF2D83CDDF0";
            _dict["D83CDDF2D83CDDFC"] = "D83CDDF2D83CDDFC";
            _dict["D83CDDF2D83CDDFE"] = "D83CDDF2D83CDDFE";
            _dict["D83CDDF2D83CDDF1"] = "D83CDDF2D83CDDF1";
            _dict["D83CDDF2D83CDDFB"] = "D83CDDF2D83CDDFB";
            _dict["D83CDDF2D83CDDF9"] = "D83CDDF2D83CDDF9";
            _dict["D83CDDF2D83CDDE6"] = "D83CDDF2D83CDDE6";
            _dict["D83CDDF2D83CDDF6"] = "D83CDDF2D83CDDF6";
            _dict["D83CDDF2D83CDDED"] = "D83CDDF2D83CDDED";
            _dict["D83CDDF2D83CDDFD"] = "D83CDDF2D83CDDFD";
            _dict["D83CDDEBD83CDDF2"] = "D83CDDEBD83CDDF2";
            _dict["D83CDDF2D83CDDFF"] = "D83CDDF2D83CDDFF";
            _dict["D83CDDF2D83CDDE9"] = "D83CDDF2D83CDDE9";
            _dict["D83CDDF2D83CDDE8"] = "D83CDDF2D83CDDE8";
            _dict["D83CDDF2D83CDDF3"] = "D83CDDF2D83CDDF3";
            _dict["D83CDDF2D83CDDF8"] = "D83CDDF2D83CDDF8";
            _dict["D83CDDF2D83CDDF2"] = "D83CDDF2D83CDDF2";
            _dict["D83CDDF3D83CDDE6"] = "D83CDDF3D83CDDE6";
            _dict["D83CDDF3D83CDDF7"] = "D83CDDF3D83CDDF7";
            _dict["D83CDDF3D83CDDF5"] = "D83CDDF3D83CDDF5";
            _dict["D83CDDF3D83CDDEA"] = "D83CDDF3D83CDDEA";
            _dict["D83CDDF3D83CDDEC"] = "D83CDDF3D83CDDEC";
            _dict["D83CDDF3D83CDDF1"] = "D83CDDF3D83CDDF1";
            _dict["D83CDDF3D83CDDEE"] = "D83CDDF3D83CDDEE";
            _dict["D83CDDF3D83CDDFA"] = "D83CDDF3D83CDDFA";
            _dict["D83CDDF3D83CDDFF"] = "D83CDDF3D83CDDFF";
            _dict["D83CDDF3D83CDDE8"] = "D83CDDF3D83CDDE8";
            _dict["D83CDDF3D83CDDF4"] = "D83CDDF3D83CDDF4";
            _dict["D83CDDEED83CDDF2"] = "D83CDDEED83CDDF2";
            _dict["D83CDDF3D83CDDEB"] = "D83CDDF3D83CDDEB";
            _dict["D83CDDE8D83CDDFD"] = "D83CDDE8D83CDDFD";
            _dict["D83CDDF8D83CDDED"] = "D83CDDF8D83CDDED";
            _dict["D83CDDE8D83CDDF0"] = "D83CDDE8D83CDDF0";
            _dict["D83CDDF9D83CDDE8"] = "D83CDDF9D83CDDE8";
            _dict["D83CDDE6D83CDDEA"] = "D83CDDE6D83CDDEA";
            _dict["D83CDDF4D83CDDF2"] = "D83CDDF4D83CDDF2";

            _dict["D83CDDF5D83CDDF0"] = "D83CDDF5D83CDDF0";
            _dict["D83CDDF5D83CDDFC"] = "D83CDDF5D83CDDFC";
            _dict["D83CDDF5D83CDDF8"] = "D83CDDF5D83CDDF8";
            _dict["D83CDDF5D83CDDE6"] = "D83CDDF5D83CDDE6";
            _dict["D83CDDF5D83CDDEC"] = "D83CDDF5D83CDDEC";
            _dict["D83CDDF5D83CDDFE"] = "D83CDDF5D83CDDFE";
            _dict["D83CDDF5D83CDDEA"] = "D83CDDF5D83CDDEA";
            _dict["D83CDDF5D83CDDF3"] = "D83CDDF5D83CDDF3";
            _dict["D83CDDF5D83CDDF1"] = "D83CDDF5D83CDDF1";
            _dict["D83CDDF5D83CDDF9"] = "D83CDDF5D83CDDF9";
            _dict["D83CDDF5D83CDDF7"] = "D83CDDF5D83CDDF7";
            _dict["D83CDDF0D83CDDF7"] = "D83CDDF0D83CDDF7";
            _dict["D83CDDF7D83CDDEA"] = "D83CDDF7D83CDDEA";
            _dict["D83CDDF7D83CDDFA"] = "D83CDDF7D83CDDFA";
            _dict["D83CDDF7D83CDDFC"] = "D83CDDF7D83CDDFC";
            _dict["D83CDDF7D83CDDF4"] = "D83CDDF7D83CDDF4";
            _dict["D83CDDF8D83CDDFB"] = "D83CDDF8D83CDDFB";
            _dict["D83CDDFCD83CDDF8"] = "D83CDDFCD83CDDF8";
            _dict["D83CDDF8D83CDDF2"] = "D83CDDF8D83CDDF2";
            _dict["D83CDDF8D83CDDF9"] = "D83CDDF8D83CDDF9";
            _dict["D83CDDF8D83CDDE6"] = "D83CDDF8D83CDDE6";
            _dict["D83CDDF8D83CDDFF"] = "D83CDDF8D83CDDFF";
            _dict["D83CDDF2D83CDDF5"] = "D83CDDF2D83CDDF5";
            _dict["D83CDDF8D83CDDE8"] = "D83CDDF8D83CDDE8";
            _dict["D83CDDE7D83CDDF1"] = "D83CDDE7D83CDDF1";
            _dict["D83CDDF5D83CDDF2"] = "D83CDDF5D83CDDF2";
            _dict["D83CDDF8D83CDDF3"] = "D83CDDF8D83CDDF3";
            _dict["D83CDDFBD83CDDE8"] = "D83CDDFBD83CDDE8";
            _dict["D83CDDF0D83CDDF3"] = "D83CDDF0D83CDDF3";
            _dict["D83CDDF1D83CDDE8"] = "D83CDDF1D83CDDE8";
            _dict["D83CDDF7D83CDDF8"] = "D83CDDF7D83CDDF8";
            _dict["D83CDDF8D83CDDEC"] = "D83CDDF8D83CDDEC";
            _dict["D83CDDF8D83CDDFD"] = "D83CDDF8D83CDDFD";
            _dict["D83CDDF8D83CDDFE"] = "D83CDDF8D83CDDFE";
            _dict["D83CDDF8D83CDDF0"] = "D83CDDF8D83CDDF0";
            _dict["D83CDDF8D83CDDEE"] = "D83CDDF8D83CDDEE";
            _dict["D83CDDFAD83CDDF8"] = "D83CDDFAD83CDDF8";
            _dict["D83CDDF8D83CDDE7"] = "D83CDDF8D83CDDE7";
            _dict["D83CDDF8D83CDDF4"] = "D83CDDF8D83CDDF4";
            _dict["D83CDDF8D83CDDE9"] = "D83CDDF8D83CDDE9";

            _dict["D83CDDF8D83CDDF7"] = "D83CDDF8D83CDDF7";
            _dict["D83CDDF8D83CDDF1"] = "D83CDDF8D83CDDF1";
            _dict["D83CDDF9D83CDDEF"] = "D83CDDF9D83CDDEF";
            _dict["D83CDDF9D83CDDED"] = "D83CDDF9D83CDDED";
            _dict["D83CDDF9D83CDDFC"] = "D83CDDF9D83CDDFC";
            _dict["D83CDDF9D83CDDFF"] = "D83CDDF9D83CDDFF";
            _dict["D83CDDF9D83CDDEC"] = "D83CDDF9D83CDDEC";
            _dict["D83CDDF9D83CDDF0"] = "D83CDDF9D83CDDF0";
            _dict["D83CDDF9D83CDDF4"] = "D83CDDF9D83CDDF4";
            _dict["D83CDDF9D83CDDF9"] = "D83CDDF9D83CDDF9";
            _dict["D83CDDF9D83CDDFB"] = "D83CDDF9D83CDDFB";
            _dict["D83CDDF9D83CDDF3"] = "D83CDDF9D83CDDF3";
            _dict["D83CDDF9D83CDDF2"] = "D83CDDF9D83CDDF2";
            _dict["D83CDDF9D83CDDF7"] = "D83CDDF9D83CDDF7";
            _dict["D83CDDFAD83CDDEC"] = "D83CDDFAD83CDDEC";
            _dict["D83CDDFAD83CDDFF"] = "D83CDDFAD83CDDFF";
            _dict["D83CDDFAD83CDDE6"] = "D83CDDFAD83CDDE6";
            _dict["D83CDDFCD83CDDEB"] = "D83CDDFCD83CDDEB";
            _dict["D83CDDFAD83CDDFE"] = "D83CDDFAD83CDDFE";
            _dict["D83CDDEBD83CDDF4"] = "D83CDDEBD83CDDF4";
            _dict["D83CDDEBD83CDDEF"] = "D83CDDEBD83CDDEF";
            _dict["D83CDDF5D83CDDED"] = "D83CDDF5D83CDDED";
            _dict["D83CDDEBD83CDDEE"] = "D83CDDEBD83CDDEE";
            _dict["D83CDDEBD83CDDF0"] = "D83CDDEBD83CDDF0";
            _dict["D83CDDEBD83CDDF7"] = "D83CDDEBD83CDDF7";
            _dict["D83CDDECD83CDDEB"] = "D83CDDECD83CDDEB";
            _dict["D83CDDF5D83CDDEB"] = "D83CDDF5D83CDDEB";
            _dict["D83CDDF9D83CDDEB"] = "D83CDDF9D83CDDEB";
            _dict["D83CDDEDD83CDDF7"] = "D83CDDEDD83CDDF7";
            _dict["D83CDDE8D83CDDEB"] = "D83CDDE8D83CDDEB";
            _dict["D83CDDF9D83CDDE9"] = "D83CDDF9D83CDDE9";
            _dict["D83CDDF2D83CDDEA"] = "D83CDDF2D83CDDEA";
            _dict["D83CDDE8D83CDDFF"] = "D83CDDE8D83CDDFF";
            _dict["D83CDDE8D83CDDF1"] = "D83CDDE8D83CDDF1";
            _dict["D83CDDE8D83CDDED"] = "D83CDDE8D83CDDED";
            _dict["D83CDDF8D83CDDEA"] = "D83CDDF8D83CDDEA";
            _dict["D83CDDF1D83CDDF0"] = "D83CDDF1D83CDDF0";
            _dict["D83CDDEAD83CDDE8"] = "D83CDDEAD83CDDE8";
            _dict["D83CDDECD83CDDF6"] = "D83CDDECD83CDDF6";
            _dict["D83CDDEAD83CDDF7"] = "D83CDDEAD83CDDF7";
            _dict["D83CDDEAD83CDDEA"] = "D83CDDEAD83CDDEA";
            _dict["D83CDDEAD83CDDF9"] = "D83CDDEAD83CDDF9";
            _dict["D83CDDFFD83CDDE6"] = "D83CDDFFD83CDDE6";
            _dict["D83CDDECD83CDDF8"] = "D83CDDECD83CDDF8";
            _dict["D83CDDF8D83CDDF8"] = "D83CDDF8D83CDDF8";
            _dict["D83CDDEFD83CDDF2"] = "D83CDDEFD83CDDF2";
            _dict["D83CDDEFD83CDDF5"] = "D83CDDEFD83CDDF5";



            _dict["D83CDE01"] = "D83CDE01";
            _dict["D83CDE02"] = "D83CDE02";
            _dict["D83CDE1A"] = "D83CDE1A";
            _dict["D83CDE2F"] = "D83CDE2F";
            _dict["D83CDE32"] = "D83CDE32";
            _dict["D83CDE33"] = "D83CDE33";
            _dict["D83CDE34"] = "D83CDE34";
            _dict["D83CDE35"] = "D83CDE35";
            _dict["D83CDE36"] = "D83CDE36";
            _dict["D83CDE37"] = "D83CDE37";
            _dict["D83CDE38"] = "D83CDE38";
            _dict["D83CDE39"] = "D83CDE39";
            _dict["D83CDE3A"] = "D83CDE3A";
            _dict["D83CDE50"] = "D83CDE50";
            _dict["D83CDE51"] = "D83CDE51";
            _dict["D83CDF00"] = "D83CDF00";
            _dict["D83CDF01"] = "D83CDF01";
            _dict["D83CDF02"] = "D83CDF02";
            _dict["D83CDF03"] = "D83CDF03";
            _dict["D83CDF04"] = "D83CDF04";
            _dict["D83CDF05"] = "D83CDF05";
            _dict["D83CDF06"] = "D83CDF06";
            _dict["D83CDF07"] = "D83CDF07";
            _dict["D83CDF08"] = "D83CDF08";
            _dict["D83CDF09"] = "D83CDF09";
            _dict["D83CDF0A"] = "D83CDF0A";
            _dict["D83CDF0B"] = "D83CDF0B";
            _dict["D83CDF0C"] = "D83CDF0C";
            _dict["D83CDF0D"] = "D83CDF0D";
            _dict["D83CDF0E"] = "D83CDF0E";
            _dict["D83CDF0F"] = "D83CDF0F";
            _dict["D83CDF10"] = "D83CDF10";
            _dict["D83CDF11"] = "D83CDF11";
            _dict["D83CDF12"] = "D83CDF12";
            _dict["D83CDF13"] = "D83CDF13";
            _dict["D83CDF14"] = "D83CDF14";
            _dict["D83CDF15"] = "D83CDF15";
            _dict["D83CDF16"] = "D83CDF16";
            _dict["D83CDF17"] = "D83CDF17";
            _dict["D83CDF18"] = "D83CDF18";
            _dict["D83CDF19"] = "D83CDF19";
            _dict["D83CDF1A"] = "D83CDF1A";
            _dict["D83CDF1B"] = "D83CDF1B";
            _dict["D83CDF1C"] = "D83CDF1C";
            _dict["D83CDF1D"] = "D83CDF1D";
            _dict["D83CDF1E"] = "D83CDF1E";
            _dict["D83CDF1F"] = "D83CDF1F";
            _dict["D83CDF20"] = "D83CDF20";
            _dict["D83CDF21"] = "D83CDF21";
            _dict["D83CDF24"] = "D83CDF24";
            _dict["D83CDF25"] = "D83CDF25";
            _dict["D83CDF26"] = "D83CDF26";
            _dict["D83CDF27"] = "D83CDF27";
            _dict["D83CDF28"] = "D83CDF28";
            _dict["D83CDF29"] = "D83CDF29";
            _dict["D83CDF2A"] = "D83CDF2A";
            _dict["D83CDF2B"] = "D83CDF2B";
            _dict["D83CDF2C"] = "D83CDF2C";
            _dict["D83CDF2D"] = "D83CDF2D";
            _dict["D83CDF2E"] = "D83CDF2E";
            _dict["D83CDF2F"] = "D83CDF2F";
            _dict["D83CDF30"] = "D83CDF30";
            _dict["D83CDF31"] = "D83CDF31";
            _dict["D83CDF32"] = "D83CDF32";
            _dict["D83CDF33"] = "D83CDF33";
            _dict["D83CDF34"] = "D83CDF34";
            _dict["D83CDF35"] = "D83CDF35";
            _dict["D83CDF36"] = "D83CDF36";
            _dict["D83CDF37"] = "D83CDF37";
            _dict["D83CDF38"] = "D83CDF38";
            _dict["D83CDF39"] = "D83CDF39";
            _dict["D83CDF3A"] = "D83CDF3A";
            _dict["D83CDF3B"] = "D83CDF3B";
            _dict["D83CDF3C"] = "D83CDF3C";
            _dict["D83CDF3D"] = "D83CDF3D";
            _dict["D83CDF3E"] = "D83CDF3E";
            _dict["D83CDF3F"] = "D83CDF3F";
            _dict["D83CDF40"] = "D83CDF40";
            _dict["D83CDF41"] = "D83CDF41";
            _dict["D83CDF42"] = "D83CDF42";
            _dict["D83CDF43"] = "D83CDF43";
            _dict["D83CDF44"] = "D83CDF44";
            _dict["D83CDF45"] = "D83CDF45";
            _dict["D83CDF46"] = "D83CDF46";
            _dict["D83CDF47"] = "D83CDF47";
            _dict["D83CDF48"] = "D83CDF48";
            _dict["D83CDF49"] = "D83CDF49";
            _dict["D83CDF4A"] = "D83CDF4A";
            _dict["D83CDF4B"] = "D83CDF4B";
            _dict["D83CDF4C"] = "D83CDF4C";
            _dict["D83CDF4D"] = "D83CDF4D";
            _dict["D83CDF4E"] = "D83CDF4E";
            _dict["D83CDF4F"] = "D83CDF4F";
            _dict["D83CDF50"] = "D83CDF50";
            _dict["D83CDF51"] = "D83CDF51";
            _dict["D83CDF52"] = "D83CDF52";
            _dict["D83CDF53"] = "D83CDF53";
            _dict["D83CDF54"] = "D83CDF54";
            _dict["D83CDF55"] = "D83CDF55";
            _dict["D83CDF56"] = "D83CDF56";
            _dict["D83CDF57"] = "D83CDF57";
            _dict["D83CDF58"] = "D83CDF58";
            _dict["D83CDF59"] = "D83CDF59";
            _dict["D83CDF5A"] = "D83CDF5A";
            _dict["D83CDF5B"] = "D83CDF5B";
            _dict["D83CDF5C"] = "D83CDF5C";
            _dict["D83CDF5D"] = "D83CDF5D";
            _dict["D83CDF5E"] = "D83CDF5E";
            _dict["D83CDF5F"] = "D83CDF5F";
            _dict["D83CDF60"] = "D83CDF60";
            _dict["D83CDF61"] = "D83CDF61";
            _dict["D83CDF62"] = "D83CDF62";
            _dict["D83CDF63"] = "D83CDF63";
            _dict["D83CDF64"] = "D83CDF64";
            _dict["D83CDF65"] = "D83CDF65";
            _dict["D83CDF66"] = "D83CDF66";
            _dict["D83CDF67"] = "D83CDF67";
            _dict["D83CDF68"] = "D83CDF68";
            _dict["D83CDF69"] = "D83CDF69";
            _dict["D83CDF6A"] = "D83CDF6A";
            _dict["D83CDF6B"] = "D83CDF6B";
            _dict["D83CDF6C"] = "D83CDF6C";
            _dict["D83CDF6D"] = "D83CDF6D";
            _dict["D83CDF6E"] = "D83CDF6E";
            _dict["D83CDF6F"] = "D83CDF6F";
            _dict["D83CDF70"] = "D83CDF70";
            _dict["D83CDF71"] = "D83CDF71";
            _dict["D83CDF72"] = "D83CDF72";
            _dict["D83CDF73"] = "D83CDF73";
            _dict["D83CDF74"] = "D83CDF74";
            _dict["D83CDF75"] = "D83CDF75";
            _dict["D83CDF76"] = "D83CDF76";
            _dict["D83CDF77"] = "D83CDF77";
            _dict["D83CDF78"] = "D83CDF78";
            _dict["D83CDF79"] = "D83CDF79";
            _dict["D83CDF7A"] = "D83CDF7A";
            _dict["D83CDF7B"] = "D83CDF7B";
            _dict["D83CDF7C"] = "D83CDF7C";
            _dict["D83CDF7D"] = "D83CDF7D";
            _dict["D83CDF7E"] = "D83CDF7E";
            _dict["D83CDF7F"] = "D83CDF7F";
            _dict["D83CDF80"] = "D83CDF80";
            _dict["D83CDF81"] = "D83CDF81";
            _dict["D83CDF82"] = "D83CDF82";
            _dict["D83CDF83"] = "D83CDF83";
            _dict["D83CDF84"] = "D83CDF84";
            _dict["D83CDF85"] = "D83CDF85";
            _dict["D83CDF86"] = "D83CDF86";
            _dict["D83CDF87"] = "D83CDF87";
            _dict["D83CDF88"] = "D83CDF88";
            _dict["D83CDF89"] = "D83CDF89";
            _dict["D83CDF8A"] = "D83CDF8A";
            _dict["D83CDF8B"] = "D83CDF8B";
            _dict["D83CDF8C"] = "D83CDF8C";
            _dict["D83CDF8D"] = "D83CDF8D";
            _dict["D83CDF8E"] = "D83CDF8E";
            _dict["D83CDF8F"] = "D83CDF8F";
            _dict["D83CDF90"] = "D83CDF90";
            _dict["D83CDF91"] = "D83CDF91";
            _dict["D83CDF92"] = "D83CDF92";
            _dict["D83CDF93"] = "D83CDF93";
            _dict["D83CDF96"] = "D83CDF96";
            _dict["D83CDF97"] = "D83CDF97";
            _dict["D83CDF99"] = "D83CDF99";
            _dict["D83CDF9A"] = "D83CDF9A";
            _dict["D83CDF9B"] = "D83CDF9B";
            _dict["D83CDF9E"] = "D83CDF9E";
            _dict["D83CDF9F"] = "D83CDF9F";
            _dict["D83CDFA0"] = "D83CDFA0";
            _dict["D83CDFA1"] = "D83CDFA1";
            _dict["D83CDFA2"] = "D83CDFA2";
            _dict["D83CDFA3"] = "D83CDFA3";
            _dict["D83CDFA4"] = "D83CDFA4";
            _dict["D83CDFA5"] = "D83CDFA5";
            _dict["D83CDFA6"] = "D83CDFA6";
            _dict["D83CDFA7"] = "D83CDFA7";
            _dict["D83CDFA8"] = "D83CDFA8";
            _dict["D83CDFA9"] = "D83CDFA9";
            _dict["D83CDFAA"] = "D83CDFAA";
            _dict["D83CDFAB"] = "D83CDFAB";
            _dict["D83CDFAC"] = "D83CDFAC";
            _dict["D83CDFAD"] = "D83CDFAD";
            _dict["D83CDFAE"] = "D83CDFAE";
            _dict["D83CDFAF"] = "D83CDFAF";
            _dict["D83CDFB0"] = "D83CDFB0";
            _dict["D83CDFB1"] = "D83CDFB1";
            _dict["D83CDFB2"] = "D83CDFB2";
            _dict["D83CDFB3"] = "D83CDFB3";
            _dict["D83CDFB4"] = "D83CDFB4";
            _dict["D83CDFB5"] = "D83CDFB5";
            _dict["D83CDFB6"] = "D83CDFB6";
            _dict["D83CDFB7"] = "D83CDFB7";
            _dict["D83CDFB8"] = "D83CDFB8";
            _dict["D83CDFB9"] = "D83CDFB9";
            _dict["D83CDFBA"] = "D83CDFBA";
            _dict["D83CDFBB"] = "D83CDFBB";
            _dict["D83CDFBC"] = "D83CDFBC";
            _dict["D83CDFBD"] = "D83CDFBD";
            _dict["D83CDFBE"] = "D83CDFBE";
            _dict["D83CDFBF"] = "D83CDFBF";
            _dict["D83CDFC0"] = "D83CDFC0";
            _dict["D83CDFC1"] = "D83CDFC1";
            _dict["D83CDFC2"] = "D83CDFC2";
            _dict["D83CDFC5"] = "D83CDFC5";
            _dict["D83CDFC6"] = "D83CDFC6";
            _dict["D83CDFC8"] = "D83CDFC8";
            _dict["D83CDFC9"] = "D83CDFC9";
            _dict["D83CDFCD"] = "D83CDFCD";
            _dict["D83CDFCE"] = "D83CDFCE";
            _dict["D83CDFCF"] = "D83CDFCF";
            _dict["D83CDFD0"] = "D83CDFD0";
            _dict["D83CDFD1"] = "D83CDFD1";
            _dict["D83CDFD2"] = "D83CDFD2";
            _dict["D83CDFD3"] = "D83CDFD3";
            _dict["D83CDFD4"] = "D83CDFD4";
            _dict["D83CDFD5"] = "D83CDFD5";
            _dict["D83CDFD6"] = "D83CDFD6";
            _dict["D83CDFD7"] = "D83CDFD7";
            _dict["D83CDFD8"] = "D83CDFD8";
            _dict["D83CDFD9"] = "D83CDFD9";
            _dict["D83CDFDA"] = "D83CDFDA";
            _dict["D83CDFDB"] = "D83CDFDB";
            _dict["D83CDFDC"] = "D83CDFDC";
            _dict["D83CDFDD"] = "D83CDFDD";
            _dict["D83CDFDE"] = "D83CDFDE";
            _dict["D83CDFDF"] = "D83CDFDF";
            _dict["D83CDFE0"] = "D83CDFE0";
            _dict["D83CDFE1"] = "D83CDFE1";
            _dict["D83CDFE2"] = "D83CDFE2";
            _dict["D83CDFE3"] = "D83CDFE3";
            _dict["D83CDFE4"] = "D83CDFE4";
            _dict["D83CDFE5"] = "D83CDFE5";
            _dict["D83CDFE6"] = "D83CDFE6";
            _dict["D83CDFE7"] = "D83CDFE7";
            _dict["D83CDFE8"] = "D83CDFE8";
            _dict["D83CDFE9"] = "D83CDFE9";
            _dict["D83CDFEA"] = "D83CDFEA";
            _dict["D83CDFEB"] = "D83CDFEB";
            _dict["D83CDFEC"] = "D83CDFEC";
            _dict["D83CDFED"] = "D83CDFED";
            _dict["D83CDFEE"] = "D83CDFEE";
            _dict["D83CDFEF"] = "D83CDFEF";
            _dict["D83CDFF0"] = "D83CDFF0";
            _dict["D83CDFF3"] = "D83CDFF3";
            _dict["D83CDFF4"] = "D83CDFF4";
            _dict["D83CDFF5"] = "D83CDFF5";
            _dict["D83CDFF7"] = "D83CDFF7";
            _dict["D83CDFF8"] = "D83CDFF8";
            _dict["D83CDFF9"] = "D83CDFF9";
            _dict["D83CDFFA"] = "D83CDFFA";
            _dict["D83DDC00"] = "D83DDC00";
            _dict["D83DDC01"] = "D83DDC01";
            _dict["D83DDC02"] = "D83DDC02";
            _dict["D83DDC03"] = "D83DDC03";
            _dict["D83DDC04"] = "D83DDC04";
            _dict["D83DDC05"] = "D83DDC05";
            _dict["D83DDC06"] = "D83DDC06";
            _dict["D83DDC07"] = "D83DDC07";
            _dict["D83DDC08"] = "D83DDC08";
            _dict["D83DDC09"] = "D83DDC09";
            _dict["D83DDC0A"] = "D83DDC0A";
            _dict["D83DDC0B"] = "D83DDC0B";
            _dict["D83DDC0C"] = "D83DDC0C";
            _dict["D83DDC0D"] = "D83DDC0D";
            _dict["D83DDC0E"] = "D83DDC0E";
            _dict["D83DDC0F"] = "D83DDC0F";
            _dict["D83DDC10"] = "D83DDC10";
            _dict["D83DDC11"] = "D83DDC11";
            _dict["D83DDC12"] = "D83DDC12";
            _dict["D83DDC13"] = "D83DDC13";
            _dict["D83DDC14"] = "D83DDC14";
            _dict["D83DDC15"] = "D83DDC15";
            _dict["D83DDC16"] = "D83DDC16";
            _dict["D83DDC17"] = "D83DDC17";
            _dict["D83DDC18"] = "D83DDC18";
            _dict["D83DDC19"] = "D83DDC19";
            _dict["D83DDC1A"] = "D83DDC1A";
            _dict["D83DDC1B"] = "D83DDC1B";
            _dict["D83DDC1C"] = "D83DDC1C";
            _dict["D83DDC1D"] = "D83DDC1D";
            _dict["D83DDC1E"] = "D83DDC1E";
            _dict["D83DDC1F"] = "D83DDC1F";
            _dict["D83DDC20"] = "D83DDC20";
            _dict["D83DDC21"] = "D83DDC21";
            _dict["D83DDC22"] = "D83DDC22";
            _dict["D83DDC23"] = "D83DDC23";
            _dict["D83DDC24"] = "D83DDC24";
            _dict["D83DDC25"] = "D83DDC25";
            _dict["D83DDC26"] = "D83DDC26";
            _dict["D83DDC27"] = "D83DDC27";
            _dict["D83DDC28"] = "D83DDC28";
            _dict["D83DDC29"] = "D83DDC29";
            _dict["D83DDC2A"] = "D83DDC2A";
            _dict["D83DDC2B"] = "D83DDC2B";
            _dict["D83DDC2C"] = "D83DDC2C";
            _dict["D83DDC2D"] = "D83DDC2D";
            _dict["D83DDC2E"] = "D83DDC2E";
            _dict["D83DDC2F"] = "D83DDC2F";
            _dict["D83DDC30"] = "D83DDC30";
            _dict["D83DDC31"] = "D83DDC31";
            _dict["D83DDC32"] = "D83DDC32";
            _dict["D83DDC33"] = "D83DDC33";
            _dict["D83DDC34"] = "D83DDC34";
            _dict["D83DDC35"] = "D83DDC35";
            _dict["D83DDC36"] = "D83DDC36";
            _dict["D83DDC37"] = "D83DDC37";
            _dict["D83DDC38"] = "D83DDC38";
            _dict["D83DDC39"] = "D83DDC39";
            _dict["D83DDC3A"] = "D83DDC3A";
            _dict["D83DDC3B"] = "D83DDC3B";
            _dict["D83DDC3C"] = "D83DDC3C";
            _dict["D83DDC3D"] = "D83DDC3D";
            _dict["D83DDC3E"] = "D83DDC3E";
            _dict["D83DDC3F"] = "D83DDC3F";
            _dict["D83DDC40"] = "D83DDC40";
            _dict["D83DDC41"] = "D83DDC41";
            _dict["D83DDC42"] = "D83DDC42";
            _dict["D83DDC43"] = "D83DDC43";
            _dict["D83DDC44"] = "D83DDC44";
            _dict["D83DDC45"] = "D83DDC45";
            _dict["D83DDC46"] = "D83DDC46";
            _dict["D83DDC47"] = "D83DDC47";
            _dict["D83DDC48"] = "D83DDC48";
            _dict["D83DDC49"] = "D83DDC49";
            _dict["D83DDC4A"] = "D83DDC4A";
            _dict["D83DDC4B"] = "D83DDC4B";
            _dict["D83DDC4C"] = "D83DDC4C";
            _dict["D83DDC4D"] = "D83DDC4D";
            _dict["D83DDC4E"] = "D83DDC4E";
            //_dict["D83DDC4F"] = "D83DDC4F";
            _dict["D83DDC50"] = "D83DDC50";
            _dict["D83DDC51"] = "D83DDC51";
            _dict["D83DDC52"] = "D83DDC52";
            _dict["D83DDC53"] = "D83DDC53";
            _dict["D83DDC54"] = "D83DDC54";
            _dict["D83DDC55"] = "D83DDC55";
            _dict["D83DDC56"] = "D83DDC56";
            _dict["D83DDC57"] = "D83DDC57";
            _dict["D83DDC58"] = "D83DDC58";
            _dict["D83DDC59"] = "D83DDC59";
            _dict["D83DDC5A"] = "D83DDC5A";
            _dict["D83DDC5B"] = "D83DDC5B";
            _dict["D83DDC5C"] = "D83DDC5C";
            _dict["D83DDC5D"] = "D83DDC5D";
            _dict["D83DDC5E"] = "D83DDC5E";
            _dict["D83DDC5F"] = "D83DDC5F";
            _dict["D83DDC60"] = "D83DDC60";
            _dict["D83DDC61"] = "D83DDC61";
            _dict["D83DDC62"] = "D83DDC62";
            _dict["D83DDC63"] = "D83DDC63";
            _dict["D83DDC64"] = "D83DDC64";
            _dict["D83DDC65"] = "D83DDC65";
            _dict["D83DDC66"] = "D83DDC66";
            _dict["D83DDC67"] = "D83DDC67";
            _dict["D83DDC68"] = "D83DDC68";
            _dict["D83DDC6A"] = "D83DDC6A";
            _dict["D83DDC6B"] = "D83DDC6B";
            _dict["D83DDC6C"] = "D83DDC6C";
            _dict["D83DDC6D"] = "D83DDC6D";
            _dict["D83DDC6E"] = "D83DDC6E";
            _dict["D83DDC6F"] = "D83DDC6F";
            _dict["D83DDC70"] = "D83DDC70";
            _dict["D83DDC72"] = "D83DDC72";
            _dict["D83DDC74"] = "D83DDC74";
            _dict["D83DDC75"] = "D83DDC75";
            _dict["D83DDC76"] = "D83DDC76";
            _dict["D83DDC78"] = "D83DDC78";
            _dict["D83DDC79"] = "D83DDC79";
            _dict["D83DDC7A"] = "D83DDC7A";
            _dict["D83DDC7B"] = "D83DDC7B";
            _dict["D83DDC7C"] = "D83DDC7C";
            _dict["D83DDC7D"] = "D83DDC7D";
            _dict["D83DDC7E"] = "D83DDC7E";
            _dict["D83DDC7F"] = "D83DDC7F";
            _dict["D83DDC80"] = "D83DDC80";
            _dict["D83DDC83"] = "D83DDC83";
            _dict["D83DDC84"] = "D83DDC84";
            _dict["D83DDC85"] = "D83DDC85";
            _dict["D83DDC88"] = "D83DDC88";
            _dict["D83DDC89"] = "D83DDC89";
            _dict["D83DDC8A"] = "D83DDC8A";
            _dict["D83DDC8B"] = "D83DDC8B";
            _dict["D83DDC8C"] = "D83DDC8C";
            _dict["D83DDC8D"] = "D83DDC8D";
            _dict["D83DDC8E"] = "D83DDC8E";
            _dict["D83DDC8F"] = "D83DDC8F";
            _dict["D83DDC90"] = "D83DDC90";
            _dict["D83DDC91"] = "D83DDC91";
            _dict["D83DDC92"] = "D83DDC92";
            _dict["D83DDC93"] = "D83DDC93";
            _dict["D83DDC94"] = "D83DDC94";
            _dict["D83DDC95"] = "D83DDC95";
            _dict["D83DDC96"] = "D83DDC96";
            _dict["D83DDC97"] = "D83DDC97";
            _dict["D83DDC98"] = "D83DDC98";
            _dict["D83DDC99"] = "D83DDC99";
            _dict["D83DDC9A"] = "D83DDC9A";
            _dict["D83DDC9B"] = "D83DDC9B";
            _dict["D83DDC9C"] = "D83DDC9C";
            _dict["D83DDC9D"] = "D83DDC9D";
            _dict["D83DDC9E"] = "D83DDC9E";
            _dict["D83DDC9F"] = "D83DDC9F";
            _dict["D83DDCA0"] = "D83DDCA0";
            _dict["D83DDCA1"] = "D83DDCA1";
            _dict["D83DDCA2"] = "D83DDCA2";
            _dict["D83DDCA3"] = "D83DDCA3";
            _dict["D83DDCA4"] = "D83DDCA4";
            _dict["D83DDCA5"] = "D83DDCA5";
            _dict["D83DDCA6"] = "D83DDCA6";
            _dict["D83DDCA7"] = "D83DDCA7";
            _dict["D83DDCA8"] = "D83DDCA8";
            _dict["D83DDCA9"] = "D83DDCA9";
            _dict["D83DDCAA"] = "D83DDCAA";
            _dict["D83DDCAB"] = "D83DDCAB";
            _dict["D83DDCAC"] = "D83DDCAC";
            _dict["D83DDCAD"] = "D83DDCAD";
            _dict["D83DDCAE"] = "D83DDCAE";
            _dict["D83DDCAF"] = "D83DDCAF";
            _dict["D83DDCB0"] = "D83DDCB0";
            _dict["D83DDCB1"] = "D83DDCB1";
            _dict["D83DDCB2"] = "D83DDCB2";
            _dict["D83DDCB3"] = "D83DDCB3";
            _dict["D83DDCB4"] = "D83DDCB4";
            _dict["D83DDCB5"] = "D83DDCB5";
            _dict["D83DDCB6"] = "D83DDCB6";
            _dict["D83DDCB7"] = "D83DDCB7";
            _dict["D83DDCB8"] = "D83DDCB8";
            _dict["D83DDCB9"] = "D83DDCB9";
            _dict["D83DDCBA"] = "D83DDCBA";
            _dict["D83DDCBB"] = "D83DDCBB";
            _dict["D83DDCBC"] = "D83DDCBC";
            _dict["D83DDCBD"] = "D83DDCBD";
            _dict["D83DDCBE"] = "D83DDCBE";
            _dict["D83DDCBF"] = "D83DDCBF";
            _dict["D83DDCC0"] = "D83DDCC0";
            _dict["D83DDCC1"] = "D83DDCC1";
            _dict["D83DDCC2"] = "D83DDCC2";
            _dict["D83DDCC3"] = "D83DDCC3";
            _dict["D83DDCC4"] = "D83DDCC4";
            _dict["D83DDCC5"] = "D83DDCC5";
            _dict["D83DDCC6"] = "D83DDCC6";
            _dict["D83DDCC7"] = "D83DDCC7";
            _dict["D83DDCC8"] = "D83DDCC8";
            _dict["D83DDCC9"] = "D83DDCC9";
            _dict["D83DDCCA"] = "D83DDCCA";
            _dict["D83DDCCB"] = "D83DDCCB";
            _dict["D83DDCCC"] = "D83DDCCC";
            _dict["D83DDCCD"] = "D83DDCCD";
            _dict["D83DDCCE"] = "D83DDCCE";
            _dict["D83DDCCF"] = "D83DDCCF";
            _dict["D83DDCD0"] = "D83DDCD0";
            _dict["D83DDCD1"] = "D83DDCD1";
            _dict["D83DDCD2"] = "D83DDCD2";
            _dict["D83DDCD3"] = "D83DDCD3";
            _dict["D83DDCD4"] = "D83DDCD4";
            _dict["D83DDCD5"] = "D83DDCD5";
            _dict["D83DDCD6"] = "D83DDCD6";
            _dict["D83DDCD7"] = "D83DDCD7";
            _dict["D83DDCD8"] = "D83DDCD8";
            _dict["D83DDCD9"] = "D83DDCD9";
            _dict["D83DDCDA"] = "D83DDCDA";
            _dict["D83DDCDB"] = "D83DDCDB";
            _dict["D83DDCDC"] = "D83DDCDC";
            _dict["D83DDCDD"] = "D83DDCDD";
            _dict["D83DDCDE"] = "D83DDCDE";
            _dict["D83DDCDF"] = "D83DDCDF";
            _dict["D83DDCE0"] = "D83DDCE0";
            _dict["D83DDCE1"] = "D83DDCE1";
            _dict["D83DDCE2"] = "D83DDCE2";
            _dict["D83DDCE3"] = "D83DDCE3";
            _dict["D83DDCE4"] = "D83DDCE4";
            _dict["D83DDCE5"] = "D83DDCE5";
            _dict["D83DDCE6"] = "D83DDCE6";
            _dict["D83DDCE7"] = "D83DDCE7";
            _dict["D83DDCE8"] = "D83DDCE8";
            _dict["D83DDCE9"] = "D83DDCE9";
            _dict["D83DDCEA"] = "D83DDCEA";
            _dict["D83DDCEB"] = "D83DDCEB";
            _dict["D83DDCEC"] = "D83DDCEC";
            _dict["D83DDCED"] = "D83DDCED";
            _dict["D83DDCEE"] = "D83DDCEE";
            _dict["D83DDCEF"] = "D83DDCEF";
            _dict["D83DDCF0"] = "D83DDCF0";
            _dict["D83DDCF1"] = "D83DDCF1";
            _dict["D83DDCF2"] = "D83DDCF2";
            _dict["D83DDCF3"] = "D83DDCF3";
            _dict["D83DDCF4"] = "D83DDCF4";
            _dict["D83DDCF5"] = "D83DDCF5";
            _dict["D83DDCF6"] = "D83DDCF6";
            _dict["D83DDCF7"] = "D83DDCF7";
            _dict["D83DDCF8"] = "D83DDCF8";
            _dict["D83DDCF9"] = "D83DDCF9";
            _dict["D83DDCFA"] = "D83DDCFA";
            _dict["D83DDCFB"] = "D83DDCFB";
            _dict["D83DDCFC"] = "D83DDCFC";
            _dict["D83DDCFD"] = "D83DDCFD";
            _dict["D83DDCFF"] = "D83DDCFF";
            _dict["D83DDD00"] = "D83DDD00";
            _dict["D83DDD01"] = "D83DDD01";
            _dict["D83DDD02"] = "D83DDD02";
            _dict["D83DDD03"] = "D83DDD03";
            _dict["D83DDD04"] = "D83DDD04";
            _dict["D83DDD05"] = "D83DDD05";
            _dict["D83DDD06"] = "D83DDD06";
            _dict["D83DDD07"] = "D83DDD07";
            _dict["D83DDD08"] = "D83DDD08";
            _dict["D83DDD09"] = "D83DDD09";
            _dict["D83DDD0A"] = "D83DDD0A";
            _dict["D83DDD0B"] = "D83DDD0B";
            _dict["D83DDD0C"] = "D83DDD0C";
            _dict["D83DDD0D"] = "D83DDD0D";
            _dict["D83DDD0E"] = "D83DDD0E";
            _dict["D83DDD0F"] = "D83DDD0F";
            _dict["D83DDD10"] = "D83DDD10";
            _dict["D83DDD11"] = "D83DDD11";
            _dict["D83DDD12"] = "D83DDD12";
            _dict["D83DDD13"] = "D83DDD13";
            _dict["D83DDD14"] = "D83DDD14";
            _dict["D83DDD15"] = "D83DDD15";
            _dict["D83DDD16"] = "D83DDD16";
            _dict["D83DDD17"] = "D83DDD17";
            _dict["D83DDD18"] = "D83DDD18";
            _dict["D83DDD19"] = "D83DDD19";
            _dict["D83DDD1A"] = "D83DDD1A";
            _dict["D83DDD1B"] = "D83DDD1B";
            _dict["D83DDD1C"] = "D83DDD1C";
            _dict["D83DDD1D"] = "D83DDD1D";
            _dict["D83DDD1E"] = "D83DDD1E";
            _dict["D83DDD1F"] = "D83DDD1F";
            _dict["D83DDD20"] = "D83DDD20";
            _dict["D83DDD21"] = "D83DDD21";
            _dict["D83DDD22"] = "D83DDD22";
            _dict["D83DDD23"] = "D83DDD23";
            _dict["D83DDD24"] = "D83DDD24";
            _dict["D83DDD25"] = "D83DDD25";
            _dict["D83DDD27"] = "D83DDD27";
            _dict["D83DDD28"] = "D83DDD28";
            _dict["D83DDD29"] = "D83DDD29";
            _dict["D83DDD2A"] = "D83DDD2A";
            _dict["D83DDD2B"] = "D83DDD2B";
            _dict["D83DDD2C"] = "D83DDD2C";
            _dict["D83DDD2D"] = "D83DDD2D";
            _dict["D83DDD2E"] = "D83DDD2E";
            _dict["D83DDD2F"] = "D83DDD2F";
            _dict["D83DDD30"] = "D83DDD30";
            _dict["D83DDD31"] = "D83DDD31";
            _dict["D83DDD32"] = "D83DDD32";
            _dict["D83DDD33"] = "D83DDD33";
            _dict["D83DDD34"] = "D83DDD34";
            _dict["D83DDD35"] = "D83DDD35";
            _dict["D83DDD36"] = "D83DDD36";
            _dict["D83DDD37"] = "D83DDD37";
            _dict["D83DDD38"] = "D83DDD38";
            _dict["D83DDD39"] = "D83DDD39";
            _dict["D83DDD3A"] = "D83DDD3A";
            _dict["D83DDD3B"] = "D83DDD3B";
            _dict["D83DDD3C"] = "D83DDD3C";
            _dict["D83DDD3D"] = "D83DDD3D";
            _dict["D83DDD49"] = "D83DDD49";
            _dict["D83DDD4A"] = "D83DDD4A";
            _dict["D83DDD4B"] = "D83DDD4B";
            _dict["D83DDD4C"] = "D83DDD4C";
            _dict["D83DDD4D"] = "D83DDD4D";
            _dict["D83DDD4E"] = "D83DDD4E";
            _dict["D83DDD50"] = "D83DDD50";
            _dict["D83DDD51"] = "D83DDD51";
            _dict["D83DDD52"] = "D83DDD52";
            _dict["D83DDD53"] = "D83DDD53";
            _dict["D83DDD54"] = "D83DDD54";
            _dict["D83DDD55"] = "D83DDD55";
            _dict["D83DDD56"] = "D83DDD56";
            _dict["D83DDD57"] = "D83DDD57";
            _dict["D83DDD58"] = "D83DDD58";
            _dict["D83DDD59"] = "D83DDD59";
            _dict["D83DDD5A"] = "D83DDD5A";
            _dict["D83DDD5B"] = "D83DDD5B";
            _dict["D83DDD5C"] = "D83DDD5C";
            _dict["D83DDD5D"] = "D83DDD5D";
            _dict["D83DDD5E"] = "D83DDD5E";
            _dict["D83DDD5F"] = "D83DDD5F";
            _dict["D83DDD60"] = "D83DDD60";
            _dict["D83DDD61"] = "D83DDD61";
            _dict["D83DDD62"] = "D83DDD62";
            _dict["D83DDD63"] = "D83DDD63";
            _dict["D83DDD64"] = "D83DDD64";
            _dict["D83DDD65"] = "D83DDD65";
            _dict["D83DDD66"] = "D83DDD66";
            _dict["D83DDD67"] = "D83DDD67";
            _dict["D83DDD6F"] = "D83DDD6F";
            _dict["D83DDD70"] = "D83DDD70";
            _dict["D83DDD73"] = "D83DDD73";
            _dict["D83DDD76"] = "D83DDD76";
            _dict["D83DDD77"] = "D83DDD77";
            _dict["D83DDD78"] = "D83DDD78";
            _dict["D83DDD79"] = "D83DDD79";
            _dict["D83DDD87"] = "D83DDD87";
            _dict["D83DDD8A"] = "D83DDD8A";
            _dict["D83DDD8B"] = "D83DDD8B";
            _dict["D83DDD8C"] = "D83DDD8C";
            _dict["D83DDD8D"] = "D83DDD8D";
            _dict["D83DDDA4"] = "D83DDDA4";
            _dict["D83DDDA5"] = "D83DDDA5";
            _dict["D83DDDA8"] = "D83DDDA8";
            _dict["D83DDDB1"] = "D83DDDB1";
            _dict["D83DDDB2"] = "D83DDDB2";
            _dict["D83DDDBC"] = "D83DDDBC";
            _dict["D83DDDC2"] = "D83DDDC2";
            _dict["D83DDDC3"] = "D83DDDC3";
            _dict["D83DDDC4"] = "D83DDDC4";
            _dict["D83DDDD1"] = "D83DDDD1";
            _dict["D83DDDD2"] = "D83DDDD2";
            _dict["D83DDDD3"] = "D83DDDD3";
            _dict["D83DDDDC"] = "D83DDDDC";
            _dict["D83DDDDD"] = "D83DDDDD";
            _dict["D83DDDDE"] = "D83DDDDE";
            _dict["D83DDDE1"] = "D83DDDE1";
            _dict["D83DDDE3"] = "D83DDDE3";
            _dict["D83DDDE8"] = "D83DDDE8";
            _dict["D83DDDEF"] = "D83DDDEF";
            _dict["D83DDDF3"] = "D83DDDF3";
            _dict["D83DDDFA"] = "D83DDDFA";
            _dict["D83DDDFB"] = "D83DDDFB";
            _dict["D83DDDFC"] = "D83DDDFC";
            _dict["D83DDDFD"] = "D83DDDFD";
            _dict["D83DDDFE"] = "D83DDDFE";
            _dict["D83DDDFF"] = "D83DDDFF";
            _dict["D83DDE00"] = "D83DDE00";
            _dict["D83DDE01"] = "D83DDE01";
            _dict["D83DDE02"] = "D83DDE02";
            _dict["D83DDE03"] = "D83DDE03";
            _dict["D83DDE04"] = "D83DDE04";
            _dict["D83DDE05"] = "D83DDE05";
            _dict["D83DDE06"] = "D83DDE06";
            _dict["D83DDE07"] = "D83DDE07";
            _dict["D83DDE08"] = "D83DDE08";
            _dict["D83DDE09"] = "D83DDE09";
            _dict["D83DDE0A"] = "D83DDE0A";
            _dict["D83DDE0B"] = "D83DDE0B";
            _dict["D83DDE0C"] = "D83DDE0C";
            _dict["D83DDE0D"] = "D83DDE0D";
            _dict["D83DDE0E"] = "D83DDE0E";
            _dict["D83DDE0F"] = "D83DDE0F";
            _dict["D83DDE10"] = "D83DDE10";
            _dict["D83DDE11"] = "D83DDE11";
            _dict["D83DDE12"] = "D83DDE12";
            _dict["D83DDE13"] = "D83DDE13";
            _dict["D83DDE14"] = "D83DDE14";
            _dict["D83DDE15"] = "D83DDE15";
            _dict["D83DDE16"] = "D83DDE16";
            _dict["D83DDE17"] = "D83DDE17";
            _dict["D83DDE18"] = "D83DDE18";
            _dict["D83DDE19"] = "D83DDE19";
            _dict["D83DDE1A"] = "D83DDE1A";
            _dict["D83DDE1B"] = "D83DDE1B";
            _dict["D83DDE1C"] = "D83DDE1C";
            _dict["D83DDE1D"] = "D83DDE1D";
            _dict["D83DDE1E"] = "D83DDE1E";
            _dict["D83DDE1F"] = "D83DDE1F";
            _dict["D83DDE20"] = "D83DDE20";
            _dict["D83DDE21"] = "D83DDE21";
            _dict["D83DDE22"] = "D83DDE22";
            _dict["D83DDE23"] = "D83DDE23";
            _dict["D83DDE24"] = "D83DDE24";
            _dict["D83DDE25"] = "D83DDE25";
            _dict["D83DDE26"] = "D83DDE26";
            _dict["D83DDE27"] = "D83DDE27";
            _dict["D83DDE28"] = "D83DDE28";
            _dict["D83DDE29"] = "D83DDE29";
            _dict["D83DDE2A"] = "D83DDE2A";
            _dict["D83DDE2B"] = "D83DDE2B";
            _dict["D83DDE2C"] = "D83DDE2C";
            _dict["D83DDE2D"] = "D83DDE2D";
            _dict["D83DDE2E"] = "D83DDE2E";
            _dict["D83DDE2F"] = "D83DDE2F";
            _dict["D83DDE30"] = "D83DDE30";
            _dict["D83DDE31"] = "D83DDE31";
            _dict["D83DDE32"] = "D83DDE32";
            _dict["D83DDE33"] = "D83DDE33";
            _dict["D83DDE34"] = "D83DDE34";
            _dict["D83DDE35"] = "D83DDE35";
            _dict["D83DDE36"] = "D83DDE36";
            _dict["D83DDE37"] = "D83DDE37";
            _dict["D83DDE38"] = "D83DDE38";
            _dict["D83DDE39"] = "D83DDE39";
            _dict["D83DDE3A"] = "D83DDE3A";
            _dict["D83DDE3B"] = "D83DDE3B";
            _dict["D83DDE3C"] = "D83DDE3C";
            _dict["D83DDE3D"] = "D83DDE3D";
            _dict["D83DDE3E"] = "D83DDE3E";
            _dict["D83DDE3F"] = "D83DDE3F";
            _dict["D83DDE40"] = "D83DDE40";
            _dict["D83DDE41"] = "D83DDE41";
            _dict["D83DDE42"] = "D83DDE42";
            _dict["D83DDE43"] = "D83DDE43";
            _dict["D83DDE44"] = "D83DDE44";
            _dict["D83DDE47"] = "D83DDE47";
            _dict["D83DDE48"] = "D83DDE48";
            _dict["D83DDE49"] = "D83DDE49";
            _dict["D83DDE4A"] = "D83DDE4A";
            _dict["D83DDE4F"] = "D83DDE4F";
            _dict["D83DDE80"] = "D83DDE80";
            _dict["D83DDE81"] = "D83DDE81";
            _dict["D83DDE82"] = "D83DDE82";
            _dict["D83DDE83"] = "D83DDE83";
            _dict["D83DDE84"] = "D83DDE84";
            _dict["D83DDE85"] = "D83DDE85";
            _dict["D83DDE86"] = "D83DDE86";
            _dict["D83DDE87"] = "D83DDE87";
            _dict["D83DDE88"] = "D83DDE88";
            _dict["D83DDE89"] = "D83DDE89";
            _dict["D83DDE8A"] = "D83DDE8A";
            _dict["D83DDE8B"] = "D83DDE8B";
            _dict["D83DDE8C"] = "D83DDE8C";
            _dict["D83DDE8D"] = "D83DDE8D";
            _dict["D83DDE8E"] = "D83DDE8E";
            _dict["D83DDE8F"] = "D83DDE8F";
            _dict["D83DDE90"] = "D83DDE90";
            _dict["D83DDE91"] = "D83DDE91";
            _dict["D83DDE92"] = "D83DDE92";
            _dict["D83DDE93"] = "D83DDE93";
            _dict["D83DDE94"] = "D83DDE94";
            _dict["D83DDE95"] = "D83DDE95";
            _dict["D83DDE96"] = "D83DDE96";
            _dict["D83DDE97"] = "D83DDE97";
            _dict["D83DDE98"] = "D83DDE98";
            _dict["D83DDE99"] = "D83DDE99";
            _dict["D83DDE9A"] = "D83DDE9A";
            _dict["D83DDE9B"] = "D83DDE9B";
            _dict["D83DDE9C"] = "D83DDE9C";
            _dict["D83DDE9D"] = "D83DDE9D";
            _dict["D83DDE9E"] = "D83DDE9E";
            _dict["D83DDE9F"] = "D83DDE9F";
            _dict["D83DDEA0"] = "D83DDEA0";
            _dict["D83DDEA1"] = "D83DDEA1";
            _dict["D83DDEA2"] = "D83DDEA2";
            _dict["D83DDEA4"] = "D83DDEA4";
            _dict["D83DDEA5"] = "D83DDEA5";
            _dict["D83DDEA6"] = "D83DDEA6";
            _dict["D83DDEA7"] = "D83DDEA7";
            _dict["D83DDEA8"] = "D83DDEA8";
            _dict["D83DDEA9"] = "D83DDEA9";
            _dict["D83DDEAA"] = "D83DDEAA";
            _dict["D83DDEAB"] = "D83DDEAB";
            _dict["D83DDEAC"] = "D83DDEAC";
            _dict["D83DDEAD"] = "D83DDEAD";
            _dict["D83DDEAE"] = "D83DDEAE";
            _dict["D83DDEAF"] = "D83DDEAF";
            _dict["D83DDEB0"] = "D83DDEB0";
            _dict["D83DDEB1"] = "D83DDEB1";
            _dict["D83DDEB2"] = "D83DDEB2";
            _dict["D83DDEB3"] = "D83DDEB3";
            _dict["D83DDEB7"] = "D83DDEB7";
            _dict["D83DDEB8"] = "D83DDEB8";
            _dict["D83DDEB9"] = "D83DDEB9";
            _dict["D83DDEBA"] = "D83DDEBA";
            _dict["D83DDEBB"] = "D83DDEBB";
            _dict["D83DDEBC"] = "D83DDEBC";
            _dict["D83DDEBD"] = "D83DDEBD";
            _dict["D83DDEBE"] = "D83DDEBE";
            _dict["D83DDEBF"] = "D83DDEBF";
            _dict["D83DDEC0"] = "D83DDEC0";
            _dict["D83DDEC1"] = "D83DDEC1";
            _dict["D83DDEC2"] = "D83DDEC2";
            _dict["D83DDEC3"] = "D83DDEC3";
            _dict["D83DDEC4"] = "D83DDEC4";
            _dict["D83DDEC5"] = "D83DDEC5";
            _dict["D83DDECB"] = "D83DDECB";
            _dict["D83DDECC"] = "D83DDECC";
            _dict["D83DDECD"] = "D83DDECD";
            _dict["D83DDECE"] = "D83DDECE";
            _dict["D83DDECF"] = "D83DDECF";
            _dict["D83DDED0"] = "D83DDED0";
            _dict["D83DDED1"] = "D83DDED1";
            _dict["D83DDED2"] = "D83DDED2";
            _dict["D83DDEE0"] = "D83DDEE0";
            _dict["D83DDEE1"] = "D83DDEE1";
            _dict["D83DDEE2"] = "D83DDEE2";
            _dict["D83DDEE3"] = "D83DDEE3";
            _dict["D83DDEE4"] = "D83DDEE4";
            _dict["D83DDEE5"] = "D83DDEE5";
            _dict["D83DDEE9"] = "D83DDEE9";
            _dict["D83DDEEB"] = "D83DDEEB";
            _dict["D83DDEEC"] = "D83DDEEC";
            _dict["D83DDEF0"] = "D83DDEF0";
            _dict["D83DDEF3"] = "D83DDEF3";
            _dict["D83DDEF4"] = "D83DDEF4";
            _dict["D83DDEF5"] = "D83DDEF5";
            _dict["D83DDEF6"] = "D83DDEF6";

            _dict["D83EDD10"] = "D83EDD10";
            _dict["D83EDD11"] = "D83EDD11";
            _dict["D83EDD12"] = "D83EDD12";
            _dict["D83EDD13"] = "D83EDD13";
            _dict["D83EDD14"] = "D83EDD14";
            _dict["D83EDD15"] = "D83EDD15";
            _dict["D83EDD16"] = "D83EDD16";
            _dict["D83EDD17"] = "D83EDD17";
            _dict["D83EDD18"] = "D83EDD18";
            _dict["D83EDD1D"] = "D83EDD1D";
            _dict["D83EDD20"] = "D83EDD20";
            _dict["D83EDD21"] = "D83EDD21";
            _dict["D83EDD22"] = "D83EDD22";
            _dict["D83EDD23"] = "D83EDD23";
            _dict["D83EDD24"] = "D83EDD24";
            _dict["D83EDD25"] = "D83EDD25";
            _dict["D83EDD27"] = "D83EDD27";
            _dict["D83EDD33"] = "D83EDD33";
            _dict["D83EDD35"] = "D83EDD35";
            _dict["D83EDD3A"] = "D83EDD3A";
            _dict["D83EDD40"] = "D83EDD40";
            _dict["D83EDD41"] = "D83EDD41";
            _dict["D83EDD42"] = "D83EDD42";
            _dict["D83EDD43"] = "D83EDD43";
            _dict["D83EDD44"] = "D83EDD44";
            _dict["D83EDD45"] = "D83EDD45";
            _dict["D83EDD47"] = "D83EDD47";
            _dict["D83EDD48"] = "D83EDD48";
            _dict["D83EDD49"] = "D83EDD49";
            _dict["D83EDD4A"] = "D83EDD4A";
            _dict["D83EDD4B"] = "D83EDD4B";
            _dict["D83EDD50"] = "D83EDD50";
            _dict["D83EDD51"] = "D83EDD51";
            _dict["D83EDD52"] = "D83EDD52";
            _dict["D83EDD53"] = "D83EDD53";
            _dict["D83EDD54"] = "D83EDD54";
            _dict["D83EDD55"] = "D83EDD55";
            _dict["D83EDD56"] = "D83EDD56";
            _dict["D83EDD57"] = "D83EDD57";
            _dict["D83EDD58"] = "D83EDD58";
            _dict["D83EDD59"] = "D83EDD59";
            _dict["D83EDD5A"] = "D83EDD5A";
            _dict["D83EDD5B"] = "D83EDD5B";
            _dict["D83EDD5C"] = "D83EDD5C";
            _dict["D83EDD5D"] = "D83EDD5D";
            _dict["D83EDD5E"] = "D83EDD5E";
            _dict["D83EDD80"] = "D83EDD80";
            _dict["D83EDD81"] = "D83EDD81";
            _dict["D83EDD82"] = "D83EDD82";
            _dict["D83EDD83"] = "D83EDD83";
            _dict["D83EDD84"] = "D83EDD84";
            _dict["D83EDD85"] = "D83EDD85";
            _dict["D83EDD86"] = "D83EDD86";
            _dict["D83EDD87"] = "D83EDD87";
            _dict["D83EDD88"] = "D83EDD88";
            _dict["D83EDD89"] = "D83EDD89";
            _dict["D83EDD8A"] = "D83EDD8A";
            _dict["D83EDD8B"] = "D83EDD8B";
            _dict["D83EDD8C"] = "D83EDD8C";
            _dict["D83EDD8D"] = "D83EDD8D";
            _dict["D83EDD8E"] = "D83EDD8E";
            _dict["D83EDD8F"] = "D83EDD8F";
            _dict["D83EDD90"] = "D83EDD90";
            _dict["D83EDD91"] = "D83EDD91";
            _dict["D83EDDC0"] = "D83EDDC0";

            _dict["2639"] = "2639FE0F";
            _dict["263AFE0F"] = "263A";
            _dict["26AAFE0F"] = "26AA";
            _dict["26ABFE0F"] = "26AB";
            _dict["25AAFE0F"] = "25AA";
            _dict["25ABFE0F"] = "25AB";
            _dict["2B1BFE0F"] = "2B1B";
            _dict["2B1CFE0F"] = "2B1C";
            _dict["25FBFE0F"] = "25FB";
            _dict["25FCFE0F"] = "25FC";
            _dict["25FDFE0F"] = "25FD";
            _dict["25FEFE0F"] = "25FE";
            _dict["25B6FE0F"] = "25B6";

            _dict["0023FE0F20E3"] = "002320E3";
            _dict["002AFE0F20E3"] = "002A20E3";
            _dict["0030FE0F20E3"] = "003020E3";
            _dict["0031FE0F20E3"] = "003120E3";
            _dict["0032FE0F20E3"] = "003220E3";
            _dict["0033FE0F20E3"] = "003320E3";
            _dict["0034FE0F20E3"] = "003420E3";
            _dict["0035FE0F20E3"] = "003520E3";
            _dict["0036FE0F20E3"] = "003620E3";
            _dict["0037FE0F20E3"] = "003720E3";
            _dict["0038FE0F20E3"] = "003820E3";
            _dict["0039FE0F20E3"] = "003920E3";
        }
    }

    public static class Emojis
    {
        private static readonly string[] _emojis = new string[]
        {
            //
            "😀", "😃", "😄", "😁", "😆", "😅", "😂", "🤣", "☺️", "😊", "😇", "🙂", "🙃", "😉", "😌", "😍", "😘", "😗", "😙", "😚", "😋", "😛", "😝", "😜", "🤪", "🤨", "🧐", "🤓", "😎", "🤩", "😏", "😒", "😞", "😔", "😟", "😕", "🙁", "☹️", "😣", "😖", "😫", "😩", "😢", "😭", "😤", "😠", "😡", "🤬", "🤯", "😳", "😱", "😨", "😰", "😥", "😓", "🤗", "🤔", "🤭", "🤫", "🤥", "😶", "😐", "😑", "😬", "🙄", "😯", "😦", "😧", "😮", "😲", "😴", "🤤", "😪", "😵", "🤐", "🤢", "🤮", "🤧", "😷", "🤒", "🤕", "🤑", "🤠", "😈", "👿", "👹", "👺", "🤡", "💩", "👻", "💀", "☠️", "👽", "👾", "🤖", "🎃", "😺", "😸", "😹", "😻", "😼", "😽", "🙀", "😿", "😾", "🤲", "👐", "🙌", "👏", "🤝", "👍", "👎", "👊", "✊", "🤛", "🤜", "🤞", "✌️", "🤟", "🤘", "👌", "👈", "👉", "👆", "👇", "☝️", "✋", "🤚", "🖐", "🖖", "👋", "🤙", "💪", "🖕", "✍️", "🙏", "💍", "💄", "💋", "👄", "👅", "👂", "👃", "👣", "👁", "👀", "🧠", "🗣", "👤", "👥", "👶", "👧", "🧒", "👦", "👩", "🧑", "👨", "👱‍♀️", "👱‍♂️", "🧔", "👵", "🧓", "👴", "👲", "👳‍♀️", "👳‍♂️", "🧕", "👮‍♀️", "👮‍♂️", "👷‍♀️", "👷‍♂️", "💂‍♀️", "💂‍♂️", "🕵️‍♀️", "🕵️‍♂️", "👩‍⚕️", "👨‍⚕️", "👩‍🌾", "👨‍🌾", "👩‍🍳", "👨‍🍳", "👩‍🎓", "👨‍🎓", "👩‍🎤", "👨‍🎤", "👩‍🏫", "👨‍🏫", "👩‍🏭", "👨‍🏭", "👩‍💻", "👨‍💻", "👩‍💼", "👨‍💼", "👩‍🔧", "👨‍🔧", "👩‍🔬", "👨‍🔬", "👩‍🎨", "👨‍🎨", "👩‍🚒", "👨‍🚒", "👩‍✈️", "👨‍✈️", "👩‍🚀", "👨‍🚀", "👩‍⚖️", "👨‍⚖️", "👰", "🤵", "👸", "🤴", "🤶", "🎅", "🧙‍♀️", "🧙‍♂️", "🧝‍♀️", "🧝‍♂️", "🧛‍♀️", "🧛‍♂️", "🧟‍♀️", "🧟‍♂️", "🧞‍♀️", "🧞‍♂️", "🧜‍♀️", "🧜‍♂️", "🧚‍♀️", "🧚‍♂️", "👼", "🤰", "🤱", "🙇‍♀️", "🙇‍♂️", "💁‍♀️", "💁‍♂️", "🙅‍♀️", "🙅‍♂️", "🙆‍♀️", "🙆‍♂️", "🙋‍♀️", "🙋‍♂️", "🤦‍♀️", "🤦‍♂️", "🤷‍♀️", "🤷‍♂️", "🙎‍♀️", "🙎‍♂️", "🙍‍♀️", "🙍‍♂️", "💇‍♀️", "💇‍♂️", "💆‍♀️", "💆‍♂️", "🧖‍♀️", "🧖‍♂️", "💅", "🤳", "💃", "🕺", "👯‍♀️", "👯‍♂️", "🕴", "🚶‍♀️", "🚶‍♂️", "🏃‍♀️", "🏃‍♂️", "👫", "👭", "👬", "💑", "👩‍❤️‍👩", "👨‍❤️‍👨", "💏", "👩‍❤️‍💋‍👩", "👨‍❤️‍💋‍👨", "👪", "👨‍👩‍👧", "👨‍👩‍👧‍👦", "👨‍👩‍👦‍👦", "👨‍👩‍👧‍👧", "👩‍👩‍👦", "👩‍👩‍👧", "👩‍👩‍👧‍👦", "👩‍👩‍👦‍👦", "👩‍👩‍👧‍👧", "👨‍👨‍👦", "👨‍👨‍👧", "👨‍👨‍👧‍👦", "👨‍👨‍👦‍👦", "👨‍👨‍👧‍👧", "👩‍👦", "👩‍👧", "👩‍👧‍👦", "👩‍👦‍👦", "👩‍👧‍👧", "👨‍👦", "👨‍👧", "👨‍👧‍👦", "👨‍👦‍👦", "👨‍👧‍👧", "🧥", "👚", "👕", "👖", "👔", "👗", "👙", "👘", "👠", "👡", "👢", "👞", "👟", "🧦", "🧤", "🧣", "🎩", "🧢", "👒", "🎓", "⛑", "👑", "👝", "👛", "👜", "💼", "🎒", "👓", "🕶", "🌂",
            //
            "🐶", "🐱", "🐭", "🐹", "🐰", "🦊", "🐻", "🐼", "🐨", "🐯", "🦁", "🐮", "🐷", "🐽", "🐸", "🐵", "🙈", "🙉", "🙊", "🐒", "🐔", "🐧", "🐦", "🐤", "🐣", "🐥", "🦆", "🦅", "🦉", "🦇", "🐺", "🐗", "🐴", "🦄", "🐝", "🐛", "🦋", "🐌", "🐚", "🐞", "🐜", "🦗", "🕷", "🕸", "🦂", "🐢", "🐍", "🦎", "🦖", "🦕", "🐙", "🦑", "🦐", "🦀", "🐡", "🐠", "🐟", "🐬", "🐳", "🐋", "🦈", "🐊", "🐅", "🐆", "🦓", "🦍", "🐘", "🦏", "🐪", "🐫", "🦒", "🐃", "🐂", "🐄", "🐎", "🐖", "🐏", "🐑", "🐐", "🦌", "🐕", "🐩", "🐈", "🐓", "🦃", "🕊", "🐇", "🐁", "🐀", "🐿", "🦔", "🐾", "🐉", "🐲", "🌵", "🎄", "🌲", "🌳", "🌴", "🌱", "🌿", "☘️", "🍀", "🎍", "🎋", "🍃", "🍂", "🍁", "🍄", "🌾", "💐", "🌷", "🌹", "🥀", "🌺", "🌸", "🌼", "🌻", "🌞", "🌝", "🌛", "🌜", "🌚", "🌕", "🌖", "🌗", "🌘", "🌑", "🌒", "🌓", "🌔", "🌙", "🌎", "🌍", "🌏", "💫", "⭐️", "🌟", "✨", "⚡️", "☄️", "💥", "🔥", "🌪", "🌈", "☀️", "🌤", "⛅️", "🌥", "☁️", "🌦", "🌧", "⛈", "🌩", "🌨", "❄️", "☃️", "⛄️", "🌬", "💨", "💧", "💦", "☔️", "☂️", "🌊", "🌫",
            //
            "🍏", "🍎", "🍐", "🍊", "🍋", "🍌", "🍉", "🍇", "🍓", "🍈", "🍒", "🍑", "🍍", "🥥", "🥝", "🍅", "🍆", "🥑", "🥦", "🥒", "🌶", "🌽", "🥕", "🥔", "🍠", "🥐", "🍞", "🥖", "🥨", "🧀", "🥚", "🍳", "🥞", "🥓", "🥩", "🍗", "🍖", "🌭", "🍔", "🍟", "🍕", "🥪", "🥙", "🌮", "🌯", "🥗", "🥘", "🥫", "🍝", "🍜", "🍲", "🍛", "🍣", "🍱", "🥟", "🍤", "🍙", "🍚", "🍘", "🍥", "🥠", "🍢", "🍡", "🍧", "🍨", "🍦", "🥧", "🍰", "🎂", "🍮", "🍭", "🍬", "🍫", "🍿", "🍩", "🍪", "🌰", "🥜", "🍯", "🥛", "🍼", "☕️", "🍵", "🥤", "🍶", "🍺", "🍻", "🥂", "🍷", "🥃", "🍸", "🍹", "🍾", "🥄", "🍴", "🍽", "🥣", "🥡", "🥢",
            //
            "⚽️", "🏀", "🏈", "⚾️", "🎾", "🏐", "🏉", "🎱", "🏓", "🏸", "🥅", "🏒", "🏑", "🏏", "⛳️", "🏹", "🎣", "🥊", "🥋", "🎽", "⛸", "🥌", "🛷", "🎿", "⛷", "🏂", "🏋️‍♀️", "🏋️‍♂️", "🤼‍♀️", "🤼‍♂️", "🤸‍♀️", "🤸‍♂️", "⛹️‍♀️", "⛹️‍♂️", "🤺", "🤾‍♀️", "🤾‍♂️", "🏌️‍♀️", "🏌️‍♂️", "🏇", "🧘‍♀️", "🧘‍♂️", "🏄‍♀️", "🏄‍♂️", "🏊‍♀️", "🏊‍♂️", "🤽‍♀️", "🤽‍♂️", "🚣‍♀️", "🚣‍♂️", "🧗‍♀️", "🧗‍♂️", "🚵‍♀️", "🚵‍♂️", "🚴‍♀️", "🚴‍♂️", "🏆", "🥇", "🥈", "🥉", "🏅", "🎖", "🏵", "🎗", "🎫", "🎟", "🎪", "🤹‍♀️", "🤹‍♂️", "🎭", "🎨", "🎬", "🎤", "🎧", "🎼", "🎹", "🥁", "🎷", "🎺", "🎸", "🎻", "🎲", "🎯", "🎳", "🎮", "🎰",
            //
            "🚗", "🚕", "🚙", "🚌", "🚎", "🏎", "🚓", "🚑", "🚒", "🚐", "🚚", "🚛", "🚜", "🛴", "🚲", "🛵", "🏍", "🚨", "🚔", "🚍", "🚘", "🚖", "🚡", "🚠", "🚟", "🚃", "🚋", "🚞", "🚝", "🚄", "🚅", "🚈", "🚂", "🚆", "🚇", "🚊", "🚉", "✈️", "🛫", "🛬", "🛩", "💺", "🛰", "🚀", "🛸", "🚁", "🛶", "⛵️", "🚤", "🛥", "🛳", "⛴", "🚢", "⚓️", "⛽️", "🚧", "🚦", "🚥", "🚏", "🗺", "🗿", "🗽", "🗼", "🏰", "🏯", "🏟", "🎡", "🎢", "🎠", "⛲️", "⛱", "🏖", "🏝", "🏜", "🌋", "⛰", "🏔", "🗻", "🏕", "⛺️", "🏠", "🏡", "🏘", "🏚", "🏗", "🏭", "🏢", "🏬", "🏣", "🏤", "🏥", "🏦", "🏨", "🏪", "🏫", "🏩", "💒", "🏛", "⛪️", "🕌", "🕍", "🕋", "⛩", "🛤", "🛣", "🗾", "🎑", "🏞", "🌅", "🌄", "🌠", "🎇", "🎆", "🌇", "🌆", "🏙", "🌃", "🌌", "🌉", "🌁",
            //
            "⌚️", "📱", "📲", "💻", "⌨️", "🖥", "🖨", "🖱", "🖲", "🕹", "🗜", "💽", "💾", "💿", "📀", "📼", "📷", "📸", "📹", "🎥", "📽", "🎞", "📞", "☎️", "📟", "📠", "📺", "📻", "🎙", "🎚", "🎛", "⏱", "⏲", "⏰", "🕰", "⌛️", "⏳", "📡", "🔋", "🔌", "💡", "🔦", "🕯", "🗑", "🛢", "💸", "💵", "💴", "💶", "💷", "💰", "💳", "💎", "⚖️", "🔧", "🔨", "⚒", "🛠", "⛏", "🔩", "⚙️", "⛓", "🔫", "💣", "🔪", "🗡", "⚔️", "🛡", "🚬", "⚰️", "⚱️", "🏺", "🔮", "📿", "💈", "⚗️", "🔭", "🔬", "🕳", "💊", "💉", "🌡", "🚽", "🚰", "🚿", "🛁", "🛀", "🛎", "🔑", "🗝", "🚪", "🛋", "🛏", "🛌", "🖼", "🛍", "🛒", "🎁", "🎈", "🎏", "🎀", "🎊", "🎉", "🎎", "🏮", "🎐", "✉️", "📩", "📨", "📧", "💌", "📥", "📤", "📦", "🏷", "📪", "📫", "📬", "📭", "📮", "📯", "📜", "📃", "📄", "📑", "📊", "📈", "📉", "🗒", "🗓", "📆", "📅", "📇", "🗃", "🗳", "🗄", "📋", "📁", "📂", "🗂", "🗞", "📰", "📓", "📔", "📒", "📕", "📗", "📘", "📙", "📚", "📖", "🔖", "🔗", "📎", "🖇", "📐", "📏", "📌", "📍", "✂️", "🖊", "🖋", "✒️", "🖌", "🖍", "📝", "✏️", "🔍", "🔎", "🔏", "🔐", "🔒", "🔓",
            //
            "❤️", "🧡", "💛", "💚", "💙", "💜", "🖤", "💔", "❣️", "💕", "💞", "💓", "💗", "💖", "💘", "💝", "💟", "☮️", "✝️", "☪️", "🕉", "☸️", "✡️", "🔯", "🕎", "☯️", "☦️", "🛐", "⛎", "♈️", "♉️", "♊️", "♋️", "♌️", "♍️", "♎️", "♏️", "♐️", "♑️", "♒️", "♓️", "🆔", "⚛️", "🉑", "☢️", "☣️", "📴", "📳", "🈶", "🈚️", "🈸", "🈺", "🈷️", "✴️", "🆚", "💮", "🉐", "㊙️", "㊗️", "🈴", "🈵", "🈹", "🈲", "🅰️", "🅱️", "🆎", "🆑", "🅾️", "🆘", "❌", "⭕️", "🛑", "⛔️", "📛", "🚫", "💯", "💢", "♨️", "🚷", "🚯", "🚳", "🚱", "🔞", "📵", "🚭", "❗️", "❕", "❓", "❔", "‼️", "⁉️", "🔅", "🔆", "〽️", "⚠️", "🚸", "🔱", "⚜️", "🔰", "♻️", "✅", "🈯️", "💹", "❇️", "✳️", "❎", "🌐", "💠", "Ⓜ️", "🌀", "💤", "🏧", "🚾", "♿️", "🅿️", "🈳", "🈂️", "🛂", "🛃", "🛄", "🛅", "🚹", "🚺", "🚼", "🚻", "🚮", "🎦", "📶", "🈁", "🔣", "ℹ️", "🔤", "🔡", "🔠", "🆖", "🆗", "🆙", "🆒", "🆕", "🆓", "0️⃣", "1️⃣", "2️⃣", "3️⃣", "4️⃣", "5️⃣", "6️⃣", "7️⃣", "8️⃣", "9️⃣", "🔟", "🔢", "#️⃣", "*️⃣", "⏏️", "▶️", "⏸", "⏯", "⏹", "⏺", "⏭", "⏮", "⏩", "⏪", "⏫", "⏬", "◀️", "🔼", "🔽", "➡️", "⬅️", "⬆️", "⬇️", "↗️", "↘️", "↙️", "↖️", "↕️", "↔️", "↪️", "↩️", "⤴️", "⤵️", "🔀", "🔁", "🔂", "🔄", "🔃", "🎵", "🎶", "➕", "➖", "➗", "✖️", "💲", "💱", "™️", "©️", "®️", "〰️", "➰", "➿", "🔚", "🔙", "🔛", "🔝", "🔜", "✔️", "☑️", "🔘", "⚪️", "⚫️", "🔴", "🔵", "🔺", "🔻", "🔸", "🔹", "🔶", "🔷", "🔳", "🔲", "▪️", "▫️", "◾️", "◽️", "◼️", "◻️", "⬛️", "⬜️", "🔈", "🔇", "🔉", "🔊", "🔔", "🔕", "📣", "📢", "👁‍🗨", "💬", "💭", "🗯", "♠️", "♣️", "♥", "♦️", "🃏", "🎴", "🀄️", "🕐", "🕑", "🕒", "🕓", "🕔", "🕕", "🕖", "🕗", "🕘", "🕙", "🕚", "🕛", "🕜", "🕝", "🕞", "🕟", "🕠", "🕡", "🕢", "🕣", "🕤", "🕥", "🕦", "🕧"
        };

        public static bool ContainsSingleEmoji(string text)
        {
            text = text.Trim();

            if (text.Contains(" "))
            {
                return false;
            }

            return _emojis.Contains(text);



            var result = false;
            var processed = false;

            foreach (var last in EnumerateByComposedCharacterSequence(text))
            {
                if (processed)
                {
                    result = false;
                    break;
                }
                else if (IsEmoji(last))
                {
                    result = true;
                    processed = true;
                }
                else
                {
                    result = false;
                    break;
                }
            }

            return result;
        }

        public static void Verify()
        {
            foreach (var emoji in _emojis)
            {
                if (IsEmoji(emoji))
                {

                }
                else
                {
                    Debugger.Break();
                }
            }
        }

        public static bool IsEmoji(string text)
        {
            var high = text[0];

            // Surrogate pair (U+1D000-1F77F)
            if (0xd800 <= high && high <= 0xdbff && text.Length >= 2)
            {
                var low = text[1];
                var codepoint = ((high - 0xd800) * 0x400) + (low - 0xdc00) + 0x10000;

                return (0x1d000 <= codepoint && codepoint <= 0x1f77f);

            }
            else
            {
                // Not surrogate pair (U+2100-27BF)
                return (0x2100 <= high && high <= 0x27bf);
            }
        }

        public static IEnumerable<string> EnumerateByComposedCharacterSequence(string text)
        {
            var last = string.Empty;
            var joiner = true;

            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsSurrogatePair(text, i) || IsKeyCapCharacter(text, i) || IsModifierCharacter(text, i))
                {
                    // skin modifier for emoji diversity acts as a joiner
                    if (!joiner && !IsSkinModifierCharacter(text, i))
                    {
                        yield return last;
                        last = string.Empty;
                        joiner = true;
                    }

                    last += text[i + 0];
                    last += text[i + 1];
                    joiner = IsRegionalIndicator(text, i);
                    i++;
                }
                else if (text[i] == 0x200D) // zero width joiner
                //else if (char.IsControl(text, i))
                {
                    last += text[i];
                    joiner = true;
                }
                else
                {
                    if (last.Length > 0)
                    {
                        yield return last;
                    }

                    if (i + 2 < text.Length && IsSkinModifierCharacter(text, i + 1))
                    {
                        last += text[i];
                    }
                    else
                    {
                        yield return text[i].ToString();
                        last = string.Empty;
                    }

                    joiner = true;
                }
            }

            if (last.Length > 0)
            {
                yield return last;
            }
        }

        public static bool IsSkinModifierCharacter(string s, int index)
        {
            if (index + 2 <= s.Length)
            {
                char c1 = s[index + 0];
                char c2 = s[index + 1];
                return c1 == '\uD83C' && c2 >= '\uDFFB' && c2 <= '\uDFFF';
            }

            return false;
        }

        public static bool IsKeyCapCharacter(string s, int index)
        {
            return index + 1 < s.Length && s[index + 1] == '\u20E3';
        }

        public static bool IsModifierCharacter(string s, int index)
        {
            return index + 1 < s.Length && s[index + 1] == '\uFE0F';
        }

        public static bool IsRegionalIndicator(string s, int index)
        {
            if (index + 4 > s.Length)
            {
                return false;
            }

            if (IsRegionalIndicator(s[index], s[index + 1]) && IsRegionalIndicator(s[index + 2], s[index + 3]))
            {
                return true;
            }

            return false;
        }

        public static bool IsRegionalIndicator(char highSurrogate, char lowSurrogate)
        {
            if (char.IsHighSurrogate(highSurrogate) && char.IsLowSurrogate(lowSurrogate))
            {
                var utf32 = char.ConvertToUtf32(highSurrogate, lowSurrogate);
                return utf32 >= 127462u && utf32 <= 127487u;
            }

            return false;
        }

        public static string BuildUri(string string2)
        {
            var result = string.Empty;
            var i = 0;

            do
            {
                if (char.IsSurrogatePair(string2, i))
                {
                    result += char.ConvertToUtf32(string2, i).ToString("x2");
                    i += 2;
                }
                else
                {
                    result += ((short)string2[i]).ToString("x4");
                    i++;
                }

                if (i < string2.Length)
                    result += "-";

            } while (i < string2.Length);

            return $"ms-appx:///Assets/Emojis/{result}.png";
        }
    }
}
