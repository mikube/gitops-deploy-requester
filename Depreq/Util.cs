using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using YamlDotNet.RepresentationModel;

namespace Depreq
{
    public static class YamlUtil
    {
        private static Regex arraySuffixPattern = new Regex(@"\[\*\]$", RegexOptions.Compiled);
        // FIXME: Dirty
        // FIXME: Use jsonpath or sth insead
        public static string UpdateByNestedKey(YamlNode root, IEnumerable<string> nestedKey, Func<string, string> oldValToNewVal)
        {
            if (nestedKey.Count() == 0)
            {
                if (root.NodeType != YamlNodeType.Scalar)
                {
                    throw new Exception("Failed to query by the key");
                }

                var oldVal = (root as YamlScalarNode).Value;
                (root as YamlScalarNode).Value = oldValToNewVal(oldVal);
                return oldVal;
            }
            else
            {
                var key = arraySuffixPattern.Replace(nestedKey.First(), "");

                // Check Sequence `[*]`
                if (arraySuffixPattern.IsMatch(nestedKey.First()))
                {
                    switch (root.NodeType)
                    {
                        case YamlNodeType.Mapping:
                            {
                                if (root[key].NodeType != YamlNodeType.Sequence)
                                {
                                    throw new Exception($"Failed to query by the key.\nSequence is expected but {root[key].NodeType}");
                                }
                                break;
                            }
                        case YamlNodeType.Sequence:
                            {
                                if ((root as YamlSequenceNode).Any(node =>
                                    node.NodeType == YamlNodeType.Sequence))
                                {
                                    throw new Exception($"Failed to query by the key.\nSequence is expected but {root[key].NodeType}");
                                }
                                break;
                            }
                        default:
                            {
                                throw new Exception($"Failed to query by the key.");
                            }
                    }
                }

                switch (root.NodeType)
                {
                    case YamlNodeType.Mapping:
                        {
                            return UpdateByNestedKey(root[key], nestedKey.Skip(1), oldValToNewVal);
                        }
                    case YamlNodeType.Sequence:
                        {
                            var oldVal = "";
                            var nodes = root as YamlSequenceNode;
                            foreach (var childNode in nodes)
                            {
                                oldVal = UpdateByNestedKey(childNode[key], nestedKey.Skip(1), oldValToNewVal);
                            }
                            return oldVal;
                        }
                    default:
                        throw new Exception("Failed to query by the key");
                }
            }
        }
    }

    public static class TemplateUtil
    {
        private static Regex placeholderPattern = new Regex(@"\{(.+?)\}", RegexOptions.Compiled);
        // FIXME: Dirty
        public static string Apply<T>(T obj, string input)
        {
            var type = typeof(T);
            return placeholderPattern.Replace(input, m =>
            {
                var len = int.MaxValue;
                var placeholder = m.Value.Trim('{', '}');
                if (m.Value.Contains('#'))
                {
                    var lenStr = placeholder.Substring(placeholder.IndexOf('#') + 1);
                    if (!int.TryParse(lenStr, out len))
                    {
                        throw new Exception($"Invalid format ({input})");
                    }

                    placeholder = placeholder.Substring(
                        0, placeholder.IndexOf('#'));
                }
                var prop = type.GetProperty(placeholder);
                if (prop == null)
                {
                    throw new Exception($"Not supported placeholder {placeholder}");
                }
                var newVal = prop.GetValue(obj)?.ToString() ?? "";
                return newVal.Substring(0, Math.Min(len, newVal.Length));
            });
        }
    }
}