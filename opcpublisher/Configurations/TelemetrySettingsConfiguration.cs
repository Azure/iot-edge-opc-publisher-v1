// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace OpcPublisher.Configurations
{
    /// <summary>
    /// Class to control the telemetry publish, name and pattern properties.
    /// </summary>
    public class TelemetrySettingsConfiguration
    {
        /// <summary>
        /// Flag to control if the value should be published.
        /// </summary>
        public bool? Publish
        {
            get => _publish;
            set
            {
                if (value != null)
                {
                    _publish = value;
                }
            }
        }

        /// <summary>
        /// The name under which the telemetry value should be published.
        /// </summary>
        public string Name
        {
            get => _name;
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    _name = value;
                }
            }
        }

        /// <summary>
        /// The pattern which should be applied to the telemetry value.
        /// </summary>
        public string Pattern
        {
            get => _pattern;
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    // validate pattern
                    try
                    {
                        _patternRegex = new Regex(value);
                        _pattern = value;
                    }
                    catch
                    {
                        Program.Instance.Logger.Fatal($"The regular expression '{value}' used for the property 'Pattern' is not a valid regular expression. Please change.");
                        throw new Exception($"The regular expression '{value}' used for the property 'Pattern' is not a valid regular expression. Please change.");
                    }
                }
            }
        }

        /// <summary>
        /// Ctor for telemetry settings object.
        /// </summary>
        public TelemetrySettingsConfiguration()
        {
            _publish = null;
            _name = null;
            _pattern = null;
            _patternRegex = null;
        }

        /// <summary>
        /// Method to apply the regex to the given value if one is defined, otherwise we return the string passed in.
        /// </summary>
        public string PatternMatch(string stringToParse)
        {
            // no pattern set, return full string
            if (_patternRegex == null)
            {
                return stringToParse;
            }

            // build the result string based on the pattern
            string result = string.Empty;
            Match match = _patternRegex.Match(stringToParse);
            if (match.Groups[0].Success)
            {
                foreach (var group in match.Groups.Cast<Group>().Skip(1))
                {
                    result += group.Value;
                }
            }
            return result;
        }

        private bool? _publish;
        private string _name;
        private string _pattern;
        private Regex _patternRegex;
    }
}
