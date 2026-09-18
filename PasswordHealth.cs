using System;
using System.Collections.Generic;
using System.Linq;

namespace PasswordManager
{
    /// <summary>
    /// Scans the vault for common security issues using existing strength/stale helpers.
    /// Pure and stateless — no UI dependency.
    /// </summary>
    public static class PasswordHealth
    {
        public enum IssueKind
        {
            Reused,
            Weak,
            Old
        }

        public sealed class Report
        {
            public int TotalEntries { get; init; }
            public IReadOnlyList<Account> Reused { get; init; } = Array.Empty<Account>();
            public IReadOnlyList<Account> Weak { get; init; } = Array.Empty<Account>();
            public IReadOnlyList<Account> Old { get; init; } = Array.Empty<Account>();

            // Breach/exposure scanning is not implemented; always zero for now.
            public int ExposedCount => 0;
        }

        public static Report Analyze(IEnumerable<Account> accounts)
        {
            var list = accounts?.ToList() ?? new List<Account>();

            // Password shared by two or more entries counts as reused for every participant.
            var reusedPasswords = list
                .Where(a => !string.IsNullOrEmpty(a.Password))
                .GroupBy(a => a.Password, StringComparer.Ordinal)
                .Where(g => g.Count() > 1)
                .SelectMany(g => g)
                .Distinct()
                .ToList();

            var weak = list
                .Where(a =>
                {
                    var (_, label, _) = PasswordStrength.Evaluate(a.Password);
                    return string.Equals(label, "Weak", StringComparison.OrdinalIgnoreCase);
                })
                .ToList();

            var old = list.Where(StaleEntryPolicy.IsStale).ToList();

            return new Report
            {
                TotalEntries = list.Count,
                Reused = reusedPasswords,
                Weak = weak,
                Old = old
            };
        }

        public static IReadOnlyList<Account> EntriesFor(Report report, IssueKind kind) => kind switch
        {
            IssueKind.Reused => report.Reused,
            IssueKind.Weak => report.Weak,
            IssueKind.Old => report.Old,
            _ => Array.Empty<Account>()
        };

        public static string TitleFor(IssueKind kind) => kind switch
        {
            IssueKind.Reused => "Reused Passwords",
            IssueKind.Weak => "Weak Passwords",
            IssueKind.Old => "Old Passwords",
            _ => "Security Issue"
        };

        public static string DescriptionFor(IssueKind kind, int count) => kind switch
        {
            IssueKind.Reused => count == 1
                ? "1 entry uses a password shared with another account."
                : $"{count} entries use the same password across multiple accounts.",
            IssueKind.Weak => count == 1
                ? "1 entry has a weak or easily guessable password."
                : $"{count} entries have weak or easily guessable passwords.",
            IssueKind.Old => count == 1
                ? "1 entry has not been changed for a long time."
                : $"{count} entries have not been changed for a long time.",
            _ => string.Empty
        };
    }
}