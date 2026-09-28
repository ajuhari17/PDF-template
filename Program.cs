using QuestPDF.Fluent;
using DIC.MPMLT.Application.Features.Secretariats.LtSittings.Queries;
using QuestPDF.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;

var dto = new LtSittingSchedulePdfDto(
    "09/2026",
    new DateOnly(2026, 9, 15),
    new TimeOnly(9, 0),
    new TimeOnly(11, 25),
    "MPM Leadership Meeting Room",
    new List<LtSittingScheduleAgendaItemDto>
    {
        new(1, "Agenda Item", "HSE", null, 10, new TimeOnly(9, 0), new TimeOnly(9, 10)),
        new(2, "Agenda Item", "Integrity", null, 10, new TimeOnly(9, 10), new TimeOnly(9, 20)),
        new(3, "Agenda Item", "Desired Behaviour Moment", null, 10, new TimeOnly(9, 20), new TimeOnly(9, 30)),
        new(1, "Scheduled Paper", "Enhancement of Digital Transformation and Operational Efficiency", "Endorsement", 45, new TimeOnly(9, 30), new TimeOnly(10, 15)),
        new(2, "Scheduled Paper", "Test Paper - Circulation", "Circulation", 30, new TimeOnly(10, 15), new TimeOnly(10, 45)),
        new(1, "AOB", "Management", null, 15, new TimeOnly(10, 45), new TimeOnly(11, 0)),
        new(2, "AOB", "Management 2", null, 5, new TimeOnly(11, 0), new TimeOnly(11, 5)),
        new(3, "AOB", "Consulting Management", null, 5, new TimeOnly(11, 5), new TimeOnly(11, 10)),
        new(1, "Matter Arising", "Matter Arising", null, 15, new TimeOnly(11, 10), new TimeOnly(11, 25)),
    });

new LtSittingSchedulePdfDocument(dto).GeneratePdf("Preview_Agenda.pdf");
Console.WriteLine("Created Preview_Agenda.pdf");
