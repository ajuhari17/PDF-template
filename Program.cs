using DIC.MPMLT.Application.Features.Secretariats.LtSittings.Queries;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;

static TimeOnly T(int hour, int minute) => new(hour, minute);

// Fixed timestamp so the header looks the same on every run (matches the screenshot).
// In the real app, leave generatedAt out and it uses DateTime.Now.
var generatedAt = new DateTime(2026, 9, 28, 9, 59, 0);

// ---------------------------------------------------------------------------
// 1) Full sample - the same items as the agenda JSON / portal screenshot.
//
//    DTO order: SequenceNo, ItemType, SectionType, ItemTitle, PaperCategory,
//               DurationMinutes, StartTime, EndTime
//
//    - SectionType is only set for breaks (the section they are printed in); null otherwise.
//    - PaperCategory carries the chip text. In the JSON that is "paperType"
//      (Endorsement / Information), NOT "paperCategory" (Commercial / Operational ...).
// ---------------------------------------------------------------------------
var fullSitting = new LtSittingSchedulePdfDto(
    "10/2026",
    new DateOnly(2026, 10, 22),
    T(9, 0),
    T(17, 10),
    null,   // venue left empty -> shows blank, as in the portal
    new List<LtSittingScheduleAgendaItemDto>
    {
        // Agenda Items
        new(1, "Agenda Item", null, "HSE", null, 10, T(9, 0), T(9, 10)),
        new(2, "Agenda Item", null, "Integrity", null, 10, T(9, 10), T(9, 20)),
        new(3, "Break", "Agenda Item", "rest2", null, 55, T(9, 20), T(10, 15)),
        new(4, "Agenda Item", null, "Desired Behaviour Moment", null, 10, T(10, 15), T(10, 25)),

        // Scheduled Papers
        new(1, "Scheduled Paper", null, "testing paper error", "Endorsement", 45, T(10, 25), T(11, 10)),
        new(2, "Scheduled Paper", null, "testing paper error", "Endorsement", 45, T(11, 10), T(11, 55)),
        new(3, "Break", "Scheduled Paper", "Lunch", null, 30, T(11, 55), T(12, 25)),
        new(4, "Scheduled Paper", null, "Digitalization and Technology Opportunities", "Endorsement", 45, T(12, 25), T(13, 10)),
        new(5, "Scheduled Paper", null, "Technology Roadmap", "Information", 30, T(13, 10), T(13, 40)),
        new(6, "Break", "Scheduled Paper", "Jumaat Prayer", null, 90, T(13, 40), T(15, 10)),
        new(7, "Scheduled Paper", null, "Error testing 3", "Information", 30, T(15, 10), T(15, 40)),

        // AOB
        new(1, "AOB", null, "Enterprise review", null, 5, T(15, 40), T(15, 45)),

        // Matter Arising (the 15-minute slot is hardcoded in the document) + trailing break
        // new(1, "Matter Arising", null, "Matter Arising", null, 15, T(15, 45), T(16, 0)),
        // new(2, "Break", "Matter Arising", "Evening Tea", null, 70, T(16, 0), T(17, 10)),
    });

new LtSittingSchedulePdfDocument(fullSitting, generatedAt: generatedAt)
    .GeneratePdf("Preview_Agenda.pdf");
Console.WriteLine("Created Preview_Agenda.pdf");

// ---------------------------------------------------------------------------
// 2) Minimal sample - NO Matter Arising item and NO breaks, with a venue.
//    Checks that the 15-minute Matter Arising slot is added automatically and
//    shows the published title ("Agenda") instead of the draft one.
// ---------------------------------------------------------------------------
var minimalSitting = new LtSittingSchedulePdfDto(
    "09/2026",
    new DateOnly(2026, 9, 15),
    T(9, 0),
    T(10, 20),
    "MPM Leadership Meeting Room",
    new List<LtSittingScheduleAgendaItemDto>
    {
        new(1, "Agenda Item", null, "HSE", null, 10, T(9, 0), T(9, 10)),
        new(2, "Agenda Item", null, "Integrity", null, 10, T(9, 10), T(9, 20)),
        new(1, "Scheduled Paper", null, "Enhancement of Digital Transformation and Operational Efficiency", "Endorsement", 45, T(9, 20), T(10, 5)),
    });

new LtSittingSchedulePdfDocument(minimalSitting, title: "Agenda", generatedAt: generatedAt)
    .GeneratePdf("Preview_Agenda_Minimal.pdf");
Console.WriteLine("Created Preview_Agenda_Minimal.pdf");
