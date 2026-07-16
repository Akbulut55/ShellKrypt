using ShellKrypt.Core.Items;
using System;
using System.Linq;

namespace ShellKrypt.Desktop.Features.SecurityAudit;

public partial class HealthViewModel
{
    public int HealthScore
    {
        get
        {
            var penalty = _allIssues.Sum(issue => issue.Severity switch
            {
                HealthAuditSeverity.Critical => 20,
                HealthAuditSeverity.High => 14,
                HealthAuditSeverity.Medium => 8,
                HealthAuditSeverity.Low => 3,
                HealthAuditSeverity.Info => 1,
                _ => 0
            });

            return Math.Clamp(100 - penalty, 0, 100);
        }
    }
}
