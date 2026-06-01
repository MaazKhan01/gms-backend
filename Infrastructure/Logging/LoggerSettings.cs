using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Logging
{
    public class LoggerSettings
    {
        public string AppName { get; set; } = "ProjectTokyo.APIs";
        public string ElasticSearchUrl { get; set; } = string.Empty;
        public bool WriteToFile { get; set; } = false;
        public bool StructuredConsoleLogging { get; set; } = false;
        public string MinimumLogLevel { get; set; } = "Information";
    }
}
