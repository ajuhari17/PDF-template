using System.Globalization;
using DIC.MPMLT.Application.Features.Secretariats.Agendas;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DIC.MPMLT.Application.Features.Secretariats.LtSittings.Queries;

public sealed record LtSittingScheduleAgendaItemDto(
    int SequenceNo,
    string ItemType,
    string ItemTitle,
    string? PaperCategory,
    int DurationMinutes,
    TimeOnly? StartTime,
    TimeOnly? EndTime);

public sealed record LtSittingSchedulePdfDto(
    string SittingLabel,
    DateOnly SittingDate,
    TimeOnly StartTime,
    TimeOnly? EndTime,
    string? Venue,
    IReadOnlyList<LtSittingScheduleAgendaItemDto> AgendaItems);

/// <summary>
/// Renders an LT sitting's schedule/slot details and agenda item list as a PDF, grouped into
/// sections by item type (see AgendaItemTimeline.ItemTypeOrder) with a running duration total per
/// section and for the whole session - mirrors the portal's "Preview Agenda" print view. Attached
/// to the pre-read package and "Agenda Published" emails.
/// </summary>
/// <param name="sitting">Data to render.</param>
/// <param name="title">Page title. Defaults to the draft preview; pass e.g. "Agenda" for the published version.</param>
/// <param name="headerText">Centre text of the page header.</param>
/// <param name="footerText">Left text of the page footer (e.g. the portal page address).</param>
/// <param name="generatedAt">Timestamp shown top-left of the header. Defaults to now (server local time).</param>
public sealed class LtSittingSchedulePdfDocument(
    LtSittingSchedulePdfDto sitting,
    string title = "Preview Agenda (Draft)",
    string headerText = "Secretariat Management",
    string footerText = "localhost:4200/mpm-lt/secretariat-management",
    DateTime? generatedAt = null) : IDocument
{
    private readonly DateTime _generatedOn = generatedAt ?? DateTime.Now;

    // ---- Palette (taken from the portal's Preview Agenda screen) ----
    private static readonly string InkColor = "#111827";
    private static readonly string TitleColor = "#1F2937";
    private static readonly string MutedColor = "#6B7280";
    private static readonly string MetaBarColor = "#F7F8FA";
    private static readonly string SeparatorColor = "#CBD2DA";
    private static readonly string RowLineColor = "#E5E7EB";
    private static readonly string HeaderLineColor = "#CBD2DA";
    private static readonly string BadgeColor = "#10B39A";
    private static readonly string PurpleText = "#7E1FBF";
    private static readonly string PurpleBg = "#F1E6FB";
    private static readonly string ClockCircleColor = "#F1EEF8";

    private const string ClockSvg =
        "<svg xmlns='http://www.w3.org/2000/svg' width='24' height='24' viewBox='0 0 24 24' fill='none' " +
        "stroke='#6B7280' stroke-width='1.8' stroke-linecap='round' stroke-linejoin='round'>" +
        "<circle cx='12' cy='12' r='9'/><polyline points='12 7 12 12 15.5 14'/></svg>";

    private static readonly Dictionary<string, string> SectionTitles = new()
    {
        ["Agenda Item"] = "Agenda Items",
        ["Scheduled Paper"] = "Scheduled Papers",
        ["Special Paper"] = "Special Papers",
        ["AOB"] = "AOB (Any Other Business)",
        ["Matter Arising"] = "Matter Arising Slot"
    };

    private static readonly HashSet<string> PaperLikeTypes = ["Scheduled Paper", "Special Paper"];

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.MarginHorizontal(30);
            page.MarginVertical(28);
            page.DefaultTextStyle(x => x.FontSize(10).FontColor(InkColor));

            // ---- Header: date/time (left) | header text (centre) ----
            page.Header().PaddingBottom(10).Row(row =>
            {
                row.RelativeItem().AlignLeft()
                    .Text(_generatedOn.ToString("M/d/yy, h:mm tt", CultureInfo.InvariantCulture)).FontSize(9);
                row.RelativeItem().AlignCenter().Text(headerText).FontSize(9);
                row.RelativeItem(); // keeps the centre text truly centred
            });

            page.Content().Column(column =>
            {
                column.Spacing(22);

                // ---- Title + meta bar ----
                column.Item().Column(header =>
                {
                    header.Item().Text(title).FontSize(22).Bold().FontColor(TitleColor);

                    header.Item().PaddingTop(8).Element(ComposeMetaBar);

                    header.Item().PaddingTop(12).LineHorizontal(0.75f).LineColor(RowLineColor);
                });

                // ---- Sections ----
                List<(string Type, List<LtSittingScheduleAgendaItemDto> Items)> sections = AgendaItemTimeline.ItemTypeOrder
                    .Select(type => (Type: type,
                        Items: sitting.AgendaItems.Where(x => x.ItemType == type).OrderBy(x => x.SequenceNo).ToList()))
                    .Where(x => x.Items.Count > 0)
                    .ToList();

                if (sections.Count == 0)
                {
                    column.Item().Text("No agenda items scheduled yet.").Italic().FontColor(MutedColor);
                }

                for (int i = 0; i < sections.Count; i++)
                {
                    (string type, List<LtSittingScheduleAgendaItemDto> items) = sections[i];
                    int sectionNo = i + 1;
                    column.Item().Element(c => ComposeSection(c, sectionNo, type, items));
                }

                // ---- Total session duration ----
                if (sitting.AgendaItems.Count > 0)
                {
                    column.Item().Element(ComposeTotalFooter);
                }
            });

            // ---- Footer: address (left) | page/total (right) ----
            page.Footer().PaddingTop(8).Row(row =>
            {
                row.RelativeItem().AlignLeft().Text(footerText).FontSize(9);
                row.AutoItem().Text(text =>
                {
                    text.DefaultTextStyle(x => x.FontSize(9));
                    text.CurrentPageNumber();
                    text.Span("/");
                    text.TotalPages();
                });
            });
        });
    }

    // ------------------------------------------------------------------
    // Meta bar:  LT Sitting No.: 09/2026 | Date: ... | Time: ... | Venue: ...
    // ------------------------------------------------------------------
    private void ComposeMetaBar(IContainer container)
    {
        container.Background(MetaBarColor).CornerRadius(6)
            .PaddingVertical(8).PaddingHorizontal(12)
            .Text(text =>
            {
                AppendMeta(text, "LT Sitting No.:", sitting.SittingLabel, isLast: false);
                AppendMeta(text, "Date:", sitting.SittingDate.ToString("dd MMMM yyyy"), isLast: false);
                AppendMeta(text, "Time:", FormatTiming(sitting.StartTime, sitting.EndTime), isLast: false);
                AppendMeta(text, "Venue:", string.IsNullOrWhiteSpace(sitting.Venue) ? "-" : sitting.Venue, isLast: true);
            });
    }

    private static void AppendMeta(TextDescriptor text, string label, string value, bool isLast)
    {
        text.Span(label + " ").FontSize(9).FontColor(MutedColor);
        text.Span(value).FontSize(9).Bold().FontColor(InkColor);
        if (!isLast)
        {
            text.Span("   |   ").FontSize(9).FontColor(SeparatorColor);
        }
    }

    // ------------------------------------------------------------------
    // Section: [1] Title ........................ (Total: 30 mins)
    //          No | Agenda Item | Type | Timing
    // ------------------------------------------------------------------
    private static void ComposeSection(IContainer container, int sectionNo, string itemType, List<LtSittingScheduleAgendaItemDto> items)
    {
        bool isPaperLike = PaperLikeTypes.Contains(itemType);
        string titleColumnHeader = isPaperLike ? "Paper Title" : "Agenda Item";
        int totalMinutes = items.Sum(x => x.DurationMinutes);

        container.Column(column =>
        {
            // Section header row
            column.Item().EnsureSpace(90).Row(row =>
            {
                row.ConstantItem(20).Height(20).Background(BadgeColor).CornerRadius(4)
                    .AlignCenter().AlignMiddle()
                    .Text(sectionNo.ToString()).FontSize(10).Bold().FontColor(Colors.White);

                row.RelativeItem().PaddingLeft(9).AlignMiddle()
                    .Text(SectionTitles.GetValueOrDefault(itemType, itemType))
                    .FontSize(12).Bold().FontColor(InkColor);

                row.AutoItem().AlignMiddle()
                    .Background(PurpleBg).CornerRadius(10)
                    .PaddingVertical(4).PaddingHorizontal(10)
                    .Text($"Total: {FormatDuration(totalMinutes)}")
                    .FontSize(8.5f).Bold().FontColor(PurpleText);
            });

            // Table
            column.Item().PaddingTop(8).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(40);
                    columns.RelativeColumn(3);
                    columns.ConstantColumn(115);
                    columns.ConstantColumn(120);
                });

                table.Header(header =>
                {
                    AddHeaderCell(header, "No", centered: true);
                    AddHeaderCell(header, titleColumnHeader, centered: false);
                    AddHeaderCell(header, "Type", centered: true);
                    AddHeaderCell(header, "Timing", centered: true);
                });

                for (int i = 0; i < items.Count; i++)
                {
                    LtSittingScheduleAgendaItemDto item = items[i];
                    string pillLabel = isPaperLike ? (item.PaperCategory ?? item.ItemType) : item.ItemType;

                    // Numbering restarts at 1 in every section (as in the portal preview)
                    BodyCell(table).AlignCenter().Text((i + 1).ToString()).FontSize(9);
                    BodyCell(table).Text(item.ItemTitle).FontSize(9);
                    BodyCell(table).Element(c => ComposeTypePill(c, pillLabel));
                    BodyCell(table).AlignCenter().Text(FormatTimingRange(item.StartTime, item.EndTime)).FontSize(9);
                }
            });
        });
    }

    private static IContainer BodyCell(TableDescriptor table) =>
        table.Cell()
            .BorderBottom(0.75f).BorderColor(RowLineColor)
            .PaddingVertical(9).PaddingHorizontal(6)
            .AlignMiddle();

    private static void AddHeaderCell(TableCellDescriptor header, string text, bool centered)
    {
        IContainer cell = header.Cell()
            .BorderBottom(1.5f).BorderColor(HeaderLineColor)
            .PaddingVertical(7).PaddingHorizontal(6);

        if (centered)
        {
            cell.AlignCenter().Text(text).FontSize(9).FontColor(MutedColor);
        }
        else
        {
            cell.Text(text).FontSize(9).FontColor(MutedColor);
        }
    }

    private static void ComposeTypePill(IContainer container, string label)
    {
        (string bg, string fg) = PillColors(label);
        container.AlignCenter().AlignMiddle()
            .Background(bg).CornerRadius(9)
            .PaddingVertical(3).PaddingHorizontal(9)
            .Text(label).FontSize(9).FontColor(fg);
    }

    /// <summary>Pill palette matching the portal's chips.</summary>
    private static (string Bg, string Fg) PillColors(string label) => label switch
    {
        "Circulation" => ("#FF5A2C", "#FFFFFF"),
        "Matter Arising" => ("#FF5A2C", "#FFFFFF"),
        "AOB" => ("#DBEAFE", "#1D4ED8"),
        "Information" => ("#DCFCE7", "#16A34A"),
        "Endorsement" => (PurpleBg, PurpleText),
        _ => (PurpleBg, PurpleText)      // Agenda Item and any other type
    };

    // ------------------------------------------------------------------
    // (clock)  Total Session Duration
    //          2 hours 25 mins | 09:00 AM - 11:25 AM
    // ------------------------------------------------------------------
    private void ComposeTotalFooter(IContainer container)
    {
        int totalMinutes = sitting.AgendaItems.Sum(x => x.DurationMinutes);
        TimeOnly start = sitting.StartTime;
        TimeOnly end = start.AddMinutes(totalMinutes);

        container.EnsureSpace(60).Row(row =>
        {
            row.ConstantItem(38).Height(38).Background(ClockCircleColor).CornerRadius(19)
                .AlignCenter().AlignMiddle()
                .Width(18).Height(18).Svg(ClockSvg);

            row.RelativeItem().PaddingLeft(12).AlignMiddle().Column(col =>
            {
                col.Item().Text("Total Session Duration").FontSize(11).FontColor(InkColor);

                col.Item().PaddingTop(2).Text(text =>
                {
                    text.Span(FormatDuration(totalMinutes)).FontSize(9.5f).Bold().FontColor(PurpleText);
                    text.Span("   |   ").FontSize(9.5f).FontColor(SeparatorColor);
                    text.Span($"{FormatTime(start)} - {FormatTime(end)}").FontSize(9.5f).Bold().FontColor(PurpleText);
                });
            });
        });
    }

    // ------------------------------------------------------------------
    // Formatting helpers
    // ------------------------------------------------------------------
    private static string FormatDuration(int totalMinutes)
    {
        int hours = totalMinutes / 60;
        int minutes = totalMinutes % 60;

        if (hours == 0) return $"{minutes} mins";
        if (minutes == 0) return $"{hours} hour{(hours == 1 ? "" : "s")}";
        return $"{hours} hour{(hours == 1 ? "" : "s")} {minutes} mins";
    }

    private static string FormatTiming(TimeOnly startTime, TimeOnly? endTime)
    {
        string start = startTime.ToString("hh:mm tt");
        return endTime is null ? start : $"{start} - {endTime.Value:hh:mm tt}";
    }

    private static string FormatTimingRange(TimeOnly? start, TimeOnly? end)
    {
        if (start is null) return "-";
        return end is null ? FormatTime(start) : $"{FormatTime(start)} - {FormatTime(end)}";
    }

    private static string FormatTime(TimeOnly? time) => time?.ToString("hh:mm tt") ?? "-";
}
