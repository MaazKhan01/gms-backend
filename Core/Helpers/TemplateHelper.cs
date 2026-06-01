using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Core.Helpers
{
    public static class TemplateHelper
    {
        public static string Parse(string content, IDictionary<string, string> tags, string tagPrefix = "{{", string tagSuffix = "}}")
        {
            string pattern = $"\\{tagPrefix}(.+?)\\{tagSuffix}";
            string parsedContent = Regex.Replace(content, pattern, m => tags[m.Groups[0].Value], RegexOptions.Compiled);

            return parsedContent;
        }

        public static IEnumerable<string> GetTags(string content, string tagPrefix = "{{", string tagSuffix = "}}")
        {
            IEnumerable<string> tags = null;
            string pattern = $"\\{tagPrefix}(.+?)\\{tagSuffix}";
            MatchCollection matches = Regex.Matches(content, pattern, RegexOptions.Compiled);

            if (matches != null && matches.Any())
            {
                tags = matches.Select(x => x.Value);
            }

            return tags;
        }
    }
}
