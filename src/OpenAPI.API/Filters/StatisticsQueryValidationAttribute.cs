using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using OpenAPI.API.Constants;
using System.Globalization;
using System.Text.RegularExpressions;

namespace OpenAPI.API.Filters
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class StatisticsQueryValidationAttribute : ActionFilterAttribute
    {
        private static readonly HashSet<string> AllowedFormats = new(StringComparer.OrdinalIgnoreCase)
        {
            "json",
            "sdmx"
        };

        private static readonly HashSet<string> AllowedFrequencies = new(StringComparer.OrdinalIgnoreCase)
        {
            "A",
            "C",
            "D",
            "E",
            "H",
            "M",
            "O",
            "Q",
            "W",
        };

        private static readonly HashSet<string> AllowedQuarters = new(StringComparer.OrdinalIgnoreCase)
        {
            "1", "2", "3", "4",
            "Q1", "Q2", "Q3", "Q4"
        };

        private static readonly Regex SafeTokenRegex = new(@"^[A-Za-z0-9 _-]{1,50}$", RegexOptions.Compiled);

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var query = context.HttpContext.Request.Query;

            string? errorMessage;
            if (TryValidatePeriod(query, out errorMessage) ||
                TryValidateFormat(query, out errorMessage) ||
                TryValidateFrequency(query, out errorMessage) ||
                TryValidateYear(query, out errorMessage) ||
                TryValidateQuarter(query, out errorMessage) ||
                TryValidateSafeToken(query, "market", out errorMessage) ||
                TryValidateSafeToken(query, "type", out errorMessage))
            {
                context.Result = new BadRequestObjectResult(errorMessage);
                return;
            }

            base.OnActionExecuting(context);
        }

        private static bool TryValidatePeriod(IQueryCollection query, out string? error)
        {
            error = null;
            if (!query.TryGetValue("period", out var periodValues))
                return false;

            var period = periodValues.ToString();
            if (!string.IsNullOrWhiteSpace(period) &&
                !DateTime.TryParseExact(period, DataTypes.DateFormatYyyyMmDd, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            {
                error = ApiErrorMessages.InvalidPeriodFormat;
                return true;
            }

            return false;
        }

        private static bool TryValidateFormat(IQueryCollection query, out string? error)
        {
            error = null;
            if (!query.TryGetValue("format", out var formatValues))
                return false;

            var format = formatValues.ToString();
            if (!string.IsNullOrWhiteSpace(format) && !AllowedFormats.Contains(format))
            {
                error = "format must be either 'json' or 'sdmx'.";
                return true;
            }

            return false;
        }

        private static bool TryValidateFrequency(IQueryCollection query, out string? error)
        {
            error = null;
            if (!query.TryGetValue("frequency", out var frequencyValues))
                return false;

            var frequency = frequencyValues.ToString();
            if (!string.IsNullOrWhiteSpace(frequency) && !AllowedFrequencies.Contains(frequency))
            {
                error = "frequency must be one of D, W, M, Q, A.";
                return true;
            }

            return false;
        }

        private static bool TryValidateYear(IQueryCollection query, out string? error)
        {
            error = null;
            if (!query.TryGetValue("year", out var yearValues))
                return false;

            var year = yearValues.ToString();
            if (!string.IsNullOrWhiteSpace(year) && !Regex.IsMatch(year, @"^\d{4}$"))
            {
                error = "year must be a 4-digit value.";
                return true;
            }

            return false;
        }

        private static bool TryValidateQuarter(IQueryCollection query, out string? error)
        {
            error = null;
            if (!query.TryGetValue("quarter", out var quarterValues))
                return false;

            var quarter = quarterValues.ToString();
            if (!string.IsNullOrWhiteSpace(quarter) && !AllowedQuarters.Contains(quarter))
            {
                error = "quarter must be one of 1, 2, 3, 4 (or Q1-Q4).";
                return true;
            }

            return false;
        }

        private static bool TryValidateSafeToken(IQueryCollection query, string key, out string? error)
        {
            error = null;
            if (!query.TryGetValue(key, out var values))
                return false;

            var value = values.ToString();
            if (!string.IsNullOrWhiteSpace(value) && !SafeTokenRegex.IsMatch(value))
            {
                error = $"{key} contains unsupported characters.";
                return true;
            }

            return false;
        }
    }
}
