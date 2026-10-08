using QuestPDF.Fluent;
using DIC.MPMLT.Application.Features.Secretariats.LtSittings.Queries;
using QuestPDF.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;

var dto = new LtSittingSchedulePdfDto(
    "10/2026",
    new DateOnly(2026, 10, 22),
    new TimeOnly(9, 0),
    new TimeOnly(17, 10),
    null,   // venue left empty, as in the screenshot
    new List<LtSittingScheduleAgendaItemDto>
    {
        // Agenda Items
        new(1, "Agenda Item", "HSE", null, 10, new TimeOnly(9, 0), new TimeOnly(9, 10)),
        new(2, "Agenda Item", "Integrity", null, 10, new TimeOnly(9, 10), new TimeOnly(9, 20)),
        new(3, "Break", "rest2", null, 55, new TimeOnly(9, 20), new TimeOnly(10, 15)),
        new(4, "Agenda Item", "Desired Behaviour Moment", null, 10, new TimeOnly(10, 15), new TimeOnly(10, 25)),

        // Scheduled Papers
        new(1, "Scheduled Paper", "testing paper error", "Endorsement", 45, new TimeOnly(10, 25), new TimeOnly(11, 10)),
        new(2, "Scheduled Paper", "testing paper error", "Endorsement", 45, new TimeOnly(11, 10), new TimeOnly(11, 55)),
        new(3, "Break", "Lunch", null, 30, new TimeOnly(11, 55), new TimeOnly(12, 25)),
        new(4, "Scheduled Paper", "Digitalization and Technology Opportunities", "Endorsement", 45, new TimeOnly(12, 25), new TimeOnly(13, 10)),
        new(5, "Scheduled Paper", "Technology Roadmap", "Information", 30, new TimeOnly(13, 10), new TimeOnly(13, 40)),
        new(6, "Break", "Jumaat Prayer", null, 90, new TimeOnly(13, 40), new TimeOnly(15, 10)),
        new(7, "Scheduled Paper", "Error testing 3", "Information", 30, new TimeOnly(15, 10), new TimeOnly(15, 40)),

        // AOB
        new(1, "AOB", "Enterprise review", null, 5, new TimeOnly(15, 40), new TimeOnly(15, 45)),

        // Matter Arising (the 15-minute slot is hardcoded in the document) + trailing break
        new(1, "Matter Arising", "Matter Arising", null, 15, new TimeOnly(15, 45), new TimeOnly(16, 0)),
        new(2, "Break", "Evening Tea", null, 70, new TimeOnly(16, 0), new TimeOnly(17, 10)),
    });

new LtSittingSchedulePdfDocument(dto).GeneratePdf("Preview_Agenda.pdf");
Console.WriteLine("Created Preview_Agenda.pdf");
