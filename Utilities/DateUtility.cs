using KSP;
using UnityEngine;

namespace KerbalJournal.Utilities
{
    /// <summary>
    /// Provides utility functions related to date handling.
    /// </summary>
    public static class DateUtility
    {
        /// <summary>
        /// Converts legacy date formats to a standardized format.
        /// </summary>
        public static string ConvertLegacyDate(string storedDate)
        {
            if (storedDate.StartsWith("Year"))
                return storedDate;

            if (storedDate.StartsWith("Y"))
            {
                string[] parts = storedDate.Split(' ');
                if (parts.Length >= 2)
                {
                    string y = parts[0].Substring(1); // remove 'Y'
                    string d = parts[1].Substring(1); // remove 'D'
                    return $"Year {y}, Day {d}";
                }
            }
            return storedDate;
        }

        /// <summary>
        /// Retrieves the current in-game date in a standardized format.
        /// </summary>
        public static string GetCurrentKSPDate()
        {
            double UT = Planetarium.GetUniversalTime();
            string fullDate = KSPUtil.PrintDate(UT, false);
            string dateString = "";

            if (!string.IsNullOrEmpty(fullDate))
            {
                // Typically "Year 1, Day 20"
                string[] parts = fullDate.Split(',');
                if (parts.Length >= 2)
                {
                    string yearPart = parts[0].Trim();
                    string dayPart = parts[1].Trim();
                    string yearNumber = "";
                    string dayNumber = "";

                    var yearSplit = yearPart.Split(' ');
                    if (yearSplit.Length >= 2)
                        yearNumber = yearSplit[1];
                    var daySplit = dayPart.Split(' ');
                    if (daySplit.Length >= 2)
                        dayNumber = daySplit[1];

                    dateString = $"Year {yearNumber}, Day {dayNumber}";
                }
                else
                {
                    dateString = fullDate;
                }
            }
            return dateString;
        }
    }
}
