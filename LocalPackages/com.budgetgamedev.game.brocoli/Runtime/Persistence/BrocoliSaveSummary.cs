using System;
using System.Globalization;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Turns a checkpoint into the two lines the save list shows for it: what the
    /// run reached, and how long ago it was last played.
    /// </summary>
    internal static class BrocoliSaveSummary
    {
        /// <summary>What the run has reached: level, depth and score.</summary>
        internal static string Headline(BrocoliRunSave save)
        {
            if (save == null)
                return string.Empty;

            int level = Mathf.Max(1, Mathf.FloorToInt(save.player.level));
            int rooms = Mathf.Max(0, save.dungeon.roomsVisited);
            return string.Format(
                CultureInfo.InvariantCulture,
                "LV {0}   ·   {1} {2}   ·   SCORE {3:N0}",
                level,
                rooms,
                rooms == 1 ? "ROOM" : "ROOMS",
                Mathf.Max(0, save.game.score)
            );
        }

        /// <summary>How much of the player's evening the run has had, and when.</summary>
        internal static string Detail(BrocoliRunSave save, DateTime utcNow)
        {
            if (save == null)
                return string.Empty;

            return $"{PlayTime(save.game.gameTime)} PLAYED   ·   {Age(save.savedAtTicks, utcNow)}";
        }

        /// <summary>Elapsed run time as H:MM:SS, or MM:SS for a run under an hour.</summary>
        internal static string PlayTime(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f)
                seconds = 0f;

            var span = TimeSpan.FromSeconds(Mathf.FloorToInt(seconds));
            return span.TotalHours >= 1d
                ? string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}:{1:00}:{2:00}",
                    (int)span.TotalHours,
                    span.Minutes,
                    span.Seconds
                )
                : string.Format(
                    CultureInfo.InvariantCulture,
                    "{0:00}:{1:00}",
                    span.Minutes,
                    span.Seconds
                );
        }

        /// <summary>
        /// How long ago the run was last played, in the coarsest unit that still
        /// says something: a list of ten runs is scanned, not read.
        /// </summary>
        internal static string Age(long savedAtTicks, DateTime utcNow)
        {
            if (savedAtTicks <= 0L)
                return "UNKNOWN";

            var savedAt = new DateTime(
                Math.Clamp(savedAtTicks, DateTime.MinValue.Ticks, DateTime.MaxValue.Ticks),
                DateTimeKind.Utc
            );
            TimeSpan age = utcNow - savedAt;

            // A clock that moved backwards - a device timezone fix, a browser profile
            // copied between machines - reads as "now" rather than as a negative age.
            if (age.TotalMinutes < 1d)
                return "JUST NOW";
            if (age.TotalHours < 1d)
                return Plural((int)age.TotalMinutes, "MINUTE");
            if (age.TotalDays < 1d)
                return Plural((int)age.TotalHours, "HOUR");
            if (age.TotalDays < 30d)
                return Plural((int)age.TotalDays, "DAY");

            return savedAt
                .ToLocalTime()
                .ToString("d MMM yyyy", CultureInfo.InvariantCulture)
                .ToUpperInvariant();
        }

        private static string Plural(int count, string unit)
        {
            return count == 1 ? $"1 {unit} AGO" : $"{count} {unit}S AGO";
        }
    }
}
