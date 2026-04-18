using System.Linq;
using System.Windows;
using Central.ApiClient;
using Brushes = System.Windows.Media.Brushes;

namespace Central.Module.Networking.Audit;

/// <summary>
/// Result modal for <see cref="NetworkingEngineClient.VerifyAuditChainAsync"/>.
/// Clean chains get a green headline + no grid; mismatches surface in a
/// red headline + grid listing each offending row's sequence / reason /
/// expected-vs-stored hash.
///
/// <para>A non-empty mismatches list means the audit chain has been
/// tampered with — an alarm state that an operator should escalate.
/// The engine's verify endpoint is the source of truth; this dialog is
/// just presentation.</para>
/// </summary>
public partial class ChainVerifyResultDialog : DevExpress.Xpf.Core.DXWindow
{
    public ChainVerifyResultDialog(VerifyChainResponse report)
    {
        InitializeComponent();

        if (report.Ok)
        {
            HeadlineLabel.Text = $"\u2714  Chain verified clean ({report.RowsChecked} row{(report.RowsChecked == 1 ? "" : "s")} checked)";
            HeadlineLabel.Foreground = Brushes.LightGreen;
            DetailsLabel.Text = BuildRangeDescription(report);
            MismatchesGrid.Visibility = Visibility.Collapsed;
        }
        else
        {
            HeadlineLabel.Text = $"\u2718  Chain TAMPERED: {report.Mismatches.Count} mismatch{(report.Mismatches.Count == 1 ? "" : "es")} " +
                                  $"in {report.RowsChecked} row{(report.RowsChecked == 1 ? "" : "s")}";
            HeadlineLabel.Foreground = Brushes.IndianRed;
            DetailsLabel.Text = BuildRangeDescription(report) +
                                "  Escalate: audit content does not match the stored hash-chain. " +
                                "Either the audit rows were edited out-of-band, or the hash-chain write path is broken.";
            MismatchesGrid.ItemsSource = report.Mismatches
                .Select(m => new MismatchRow
                {
                    SequenceId = m.SequenceId,
                    Id = m.Id.ToString(),
                    Reason = m.Reason,
                    ExpectedHash = ShortHash(m.ExpectedHash),
                    StoredHash = ShortHash(m.StoredHash),
                }).ToList();
        }
    }

    private static string BuildRangeDescription(VerifyChainResponse r)
    {
        if (r.FirstSequenceId is null || r.LastSequenceId is null) return "Tenant has no audit rows.  ";
        return $"Range checked: sequence {r.FirstSequenceId} .. {r.LastSequenceId}.  ";
    }

    /// <summary>Full SHA-256 hex is 64 chars — hard to eyeball in a
    /// grid cell. Short form (first 12) is enough for admins to
    /// identify, with the full value still on the row if they need to
    /// copy it (keeping the record on the DTO even though we show
    /// the short form).</summary>
    private static string ShortHash(string? full)
    {
        if (string.IsNullOrEmpty(full)) return "";
        return full!.Length > 12 ? full[..12] + "…" : full;
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private sealed class MismatchRow
    {
        public long SequenceId { get; set; }
        public string Id { get; set; } = "";
        public string Reason { get; set; } = "";
        public string ExpectedHash { get; set; } = "";
        public string StoredHash { get; set; } = "";
    }
}
