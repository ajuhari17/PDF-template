using System.Globalization;
using DIC.MPMLT.Application.Features.Secretariats.Agendas;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DIC.MPMLT.Application.Features.Secretariats.LtSittings.Queries;

/// <summary>
/// One row of the agenda. <paramref name="SectionType"/> is only filled for breaks: it names the section the
/// break is printed in ("Agenda Item", "Scheduled Paper", "Matter Arising" ...). It is null for normal items.
/// </summary>
public sealed record LtSittingScheduleAgendaItemDto(
    int SequenceNo,
    string ItemType,
    string? SectionType,
    string ItemTitle,
    string? PaperCategory,
    int DurationMinutes,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    bool IsBreak = false);

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
/// <param name="fitOnePage">When true (default) the content is scaled down, if needed, so the whole agenda fits on a single page.
/// Ignored for very long agendas (more than 30 items) where scaling would make the text unreadable.</param>
public sealed class LtSittingSchedulePdfDocument(
    LtSittingSchedulePdfDto sitting,
    string title = "Preview Agenda (Draft)",
    string headerText = "Secretariat Management",
    string footerText = "localhost:4200/mpm-lt/secretariat-management",
    DateTime? generatedAt = null,
    bool fitOnePage = true) : IDocument
{
    private const int MaxItemsForSinglePage = 30;

    // The Matter Arising slot is fixed: it is always shown, always 15 minutes long.
    private const string MatterArisingType = "Matter Arising";
    private const int MatterArisingMinutes = 15;

    /// <summary>Agenda items as printed: the DTO items plus the hardcoded 15-minute Matter Arising slot.</summary>
    private readonly IReadOnlyList<LtSittingScheduleAgendaItemDto> _items = BuildItems(sitting);

    private readonly DateTime _generatedOn = generatedAt ?? DateTime.Now;

    // A property (not a field initializer) because it reads another instance field.
    private bool ScaleToOnePage => fitOnePage && _items.Count <= MaxItemsForSinglePage;

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

    private const string BreakType = "Break";

    // Coffee-cup icon (Lucide "coffee") used for break rows.
    private const string CupSvg =
        "<svg xmlns='http://www.w3.org/2000/svg' width='24' height='24' viewBox='0 0 24 24' fill='none' " +
        "stroke='#374151' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'>" +
        "<path d='M10 2v2'/><path d='M14 2v2'/><path d='M6 2v2'/>" +
        "<path d='M16 8a1 1 0 0 1 1 1v8a4 4 0 0 1-4 4H7a4 4 0 0 1-4-4V9a1 1 0 0 1 1-1h14a4 4 0 1 1 0 8h-1'/></svg>";

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
            page.MarginVertical(24);
            page.DefaultTextStyle(x => x.FontSize(10).FontColor(InkColor));

            // ---- Header: date/time (left) | header text (centre) ----
            page.Header().PaddingBottom(6).Row(row =>
            {
                row.RelativeItem().AlignLeft()
                    .Text(_generatedOn.ToString("M/d/yy, h:mm tt", CultureInfo.InvariantCulture)).FontSize(9);
                row.RelativeItem().AlignCenter().Text(headerText).FontSize(9);
                row.RelativeItem(); // keeps the centre text truly centred
            });

            // ScaleToFit shrinks the content just enough to fit the page (never enlarges it).
            (ScaleToOnePage ? page.Content().ScaleToFit() : page.Content()).Column(column =>
            {
                column.Spacing(16);

                // ---- Title + meta bar ----
                column.Item().Column(header =>
                {
                    header.Item().Text(title).FontSize(22).Bold().FontColor(TitleColor);

                    header.Item().PaddingTop(8).Element(ComposeMetaBar);

                    header.Item().PaddingTop(12).LineHorizontal(0.75f).LineColor(RowLineColor);
                });

                // ---- Sections ----
                List<(string Type, List<LtSittingScheduleAgendaItemDto> Items)> sections = BuildSections();

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
                if (_items.Count > 0)
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
    // Builds the printed item list. The Matter Arising slot is hardcoded to 15 minutes:
    //  - any Matter Arising item from the DTO is replaced by a single 15-minute slot
    //    (keeping its title / start time when it has them);
    //  - if there is none, one is added right after the last other item.
    // ------------------------------------------------------------------
    private static List<LtSittingScheduleAgendaItemDto> BuildItems(LtSittingSchedulePdfDto source)
    {
        static bool IsSlot(LtSittingScheduleAgendaItemDto x) => x.ItemType == MatterArisingType && !IsBreakItem(x);

        List<LtSittingScheduleAgendaItemDto> others = source.AgendaItems
            .Where(x => !IsSlot(x))
            .ToList();

        LtSittingScheduleAgendaItemDto? existing = source.AgendaItems
            .Where(IsSlot)
            .OrderBy(x => x.SequenceNo)
            .FirstOrDefault();

        // Start = existing slot's start, else the end of the last other item, else session start + their durations.
        TimeOnly fallbackStart = source.StartTime.AddMinutes(others.Sum(x => x.DurationMinutes));
        TimeOnly slotStart = existing?.StartTime
            ?? others.Where(x => x.EndTime is not null)
                     .Select(x => x.EndTime!.Value)
                     .DefaultIfEmpty(fallbackStart)
                     .Max();

        LtSittingScheduleAgendaItemDto slot = new(
            SequenceNo: existing?.SequenceNo ?? 1,
            ItemType: MatterArisingType,
            SectionType: null,
            ItemTitle: existing?.ItemTitle ?? MatterArisingType,
            PaperCategory: null,
            DurationMinutes: MatterArisingMinutes,
            StartTime: slotStart,
            EndTime: slotStart.AddMinutes(MatterArisingMinutes));

        return others.Append(slot).ToList();
    }

    // ------------------------------------------------------------------
    // Breaks (Lunch, Prayer, Tea ...). An item is a break when IsBreak is set, or its
    // ItemType / PaperCategory is "Break".
    // ------------------------------------------------------------------
    private static bool IsBreakItem(LtSittingScheduleAgendaItemDto x) =>
        x.IsBreak || x.ItemType == BreakType || x.PaperCategory == BreakType;

    // ------------------------------------------------------------------
    // Groups the items into numbered sections, in AgendaItemTimeline.ItemTypeOrder.
    // Items are ordered by start time so breaks land where they happen in the day.
    // A break is printed in its SectionType (e.g. "Scheduled Paper"). Only when that is missing
    // does it join the section of the item before it (or the item after it, when it is the
    // very first thing in the day).
    // ------------------------------------------------------------------
    private List<(string Type, List<LtSittingScheduleAgendaItemDto> Items)> BuildSections()
    {
        List<LtSittingScheduleAgendaItemDto> ordered = _items
            .OrderBy(x => x.StartTime ?? TimeOnly.MaxValue)
            .ThenBy(x => x.SequenceNo)
            .ToList();

        static bool IsKnownSection(string? type) =>
            !string.IsNullOrWhiteSpace(type) && AgendaItemTimeline.ItemTypeOrder.Contains(type);

        string?[] sectionOf = new string?[ordered.Count];
        for (int i = 0; i < ordered.Count; i++)
        {
            LtSittingScheduleAgendaItemDto x = ordered[i];

            if (!IsBreakItem(x))
            {
                sectionOf[i] = x.ItemType;          // normal item: its own type
            }
            else if (IsKnownSection(x.SectionType))
            {
                sectionOf[i] = x.SectionType;       // break: the section it was placed in
            }
            else if (x.ItemType != BreakType && IsKnownSection(x.ItemType))
            {
                sectionOf[i] = x.ItemType;          // flagged break that still carries a real item type
            }
            // otherwise resolved from the neighbouring items below
        }

        for (int i = 0; i < ordered.Count; i++)
        {
            if (sectionOf[i] is not null) continue;

            sectionOf[i] = sectionOf.Take(i).LastOrDefault(s => s is not null)
                ?? sectionOf.Skip(i + 1).FirstOrDefault(s => s is not null)
                ?? AgendaItemTimeline.ItemTypeOrder.First();
        }

        return AgendaItemTimeline.ItemTypeOrder
            .Select(type => (Type: type,
                Items: ordered.Where((_, i) => sectionOf[i] == type).ToList()))
            .Where(x => x.Items.Count > 0)
            .ToList();
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
                AppendMeta(text, "Venue:", sitting.Venue?.Trim() ?? string.Empty, isLast: true);
            });
    }

    private static void AppendMeta(TextDescriptor text, string label, string value, bool isLast)
    {
        text.Span(label + " ").FontSize(9).FontColor(MutedColor);
        if (!string.IsNullOrEmpty(value))
        {
            text.Span(value).FontSize(9).Bold().FontColor(InkColor);
        }
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

        // Keep a whole section on one page when it reasonably fits; only very long
        // sections are allowed to continue on the next page (header row repeats).
        IContainer section = items.Count <= 10 ? container.ShowEntire() : container;

        section.Column(column =>
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
                    bool isBreak = IsBreakItem(item);
                    string pillLabel = isBreak
                        ? BreakType
                        : isPaperLike ? (item.PaperCategory ?? item.ItemType) : item.ItemType;

                    // Numbering restarts at 1 in every section and a break uses up a number,
                    // but shows the cup icon instead of it (as in the portal preview).
                    if (isBreak)
                    {
                        BodyCell(table).AlignCenter().Width(11).Height(11).Svg(CupSvg);
                        BodyCell(table).Row(r =>
                        {
                            r.ConstantItem(11).AlignMiddle().Height(11).Svg(CupSvg);
                            r.RelativeItem().PaddingLeft(6).AlignMiddle().Text(item.ItemTitle).FontSize(9);
                        });
                    }
                    else
                    {
                        BodyCell(table).AlignCenter().Text((i + 1).ToString()).FontSize(9);
                        BodyCell(table).Text(item.ItemTitle).FontSize(9);
                    }
                    BodyCell(table).Element(c => ComposeTypePill(c, pillLabel));
                    BodyCell(table).AlignCenter().Text(FormatTimingRange(item.StartTime, item.EndTime)).FontSize(9);
                }
            });
        });
    }

    private static IContainer BodyCell(TableDescriptor table) =>
        table.Cell()
            .BorderBottom(0.75f).BorderColor(RowLineColor)
            .PaddingVertical(7).PaddingHorizontal(6)
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
        "Break" => ("#94A3B8", "#FFFFFF"),
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
        int totalMinutes = _items.Sum(x => x.DurationMinutes);
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
