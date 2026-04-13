using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace TIG.IntegrationServer.Common.Converter
{
    public class CriteriaToODataFilterConverter
    {
        private static readonly Dictionary<string, string> ExpressionRegexMappingDictionary = new Dictionary<string, string>
        {
            {@"\s*<=\s*"," le "},
            {@"\s*>=\s*", " ge "},
            {@"\s*<>\s*", " ne "},
            {@"\s*=\s*", " eq "},
            {@"\s*>\s*", " gt "},
            {@"\s*<\s*", " lt "},
            {@"[\[](?<name>\w+)[\]]\s*Between\((?<lt>[\d.\w]+).\s(?<gt>[\d.\w]+)\)",@"($1>=$2 and $1<=$3)"}, // Between
            {@"Contains\([\[](?<name>\w+)[\]],\s+(?<value>'\w+')\)", @"substringof($2, $1)"}, // Contains
            {@"[\[](?<name>\w+)[\]]\s+Like\s+'%(?<value>.*?)%'", @"substringof($2, $1)"}, // Like: case for contain
            {@"[\[](?<name>\w+)[\]]\s+Like\s+'%(?<value>.*?)'", @"startswith($1, '$2')"}, // Like: case for starts with 
            {@"[\[](?<name>\w+)[\]]\s+Like\s+'(?<value>.*?)%'", @"endswith($1, '$2')"}, // Like: case for ends with
            {@"[\[](?<name>\w+)[\]]\s+In\s+\('(?<value>.*?)'\)", @"$1 eq '$2'"}, // IsAnyOf to eq 
            {@"IsNullOrEmpty\([\[](?<name>\w+)[\]]\)", @"$1 eq ''"}, // IsBlank to eq blank
            {@"[\[](?<name>\w+)[\]]", "$1"} // Remove square bracket
        };

        public string Convert(string criteria)
        {
            string result;
            try
            {
                result = ExpressionRegexMappingDictionary.Aggregate(criteria,
                        (current, expression) => Regex.Replace(current, expression.Key, expression.Value,
                            RegexOptions.IgnoreCase));

            }
            catch (Exception)
            {
                return criteria;
            }
            return result;
        }
    }
}