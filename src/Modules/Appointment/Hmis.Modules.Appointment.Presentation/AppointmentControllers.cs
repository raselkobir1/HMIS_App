using Hmis.BuildingBlocks.Application.Pagination;
using Hmis.BuildingBlocks.Application.Responses;
using Hmis.BuildingBlocks.Presentation.Authorization;
using Hmis.BuildingBlocks.Presentation.Controllers;
using Hmis.Modules.Appointment.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Hmis.Modules.Appointment.Presentation;

/// <summary>Assembly marker used to register this module's controllers with the API host.</summary>
public sealed class AppointmentPresentationMarker;

[Route("api/v{version:apiVersion}/doctor-schedules")]
public sealed class DoctorSchedulesController(IScheduleManager manager) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(AppointmentPermissions.View)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<ScheduleDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] ScheduleQuery query, CancellationToken cancellationToken) =>
        OkResponse(await manager.SearchAsync(query, cancellationToken));

    [HttpPost]
    [HasPermission(AppointmentPermissions.SchedulesManage)]
    [ProducesResponseType<ApiResponse<ScheduleDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] ScheduleCreateDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.CreateAsync(request, cancellationToken), "Session created.");

    [HttpPut("{id:guid}")]
    [HasPermission(AppointmentPermissions.SchedulesManage)]
    [ProducesResponseType<ApiResponse<ScheduleDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] ScheduleUpdateDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.UpdateAsync(id, request, cancellationToken), "Session updated.");

    [HttpPatch("{id:guid}/status")]
    [HasPermission(AppointmentPermissions.SchedulesManage)]
    [ProducesResponseType<ApiResponse<ScheduleDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] ScheduleStatusDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.SetStatusAsync(id, request, cancellationToken), request.IsActive ? "Session activated." : "Session deactivated.");
}

[Route("api/v{version:apiVersion}/doctor-leaves")]
public sealed class DoctorLeavesController(IScheduleManager manager) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(AppointmentPermissions.View)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<LeaveDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] LeaveQuery query, CancellationToken cancellationToken) =>
        OkResponse(await manager.SearchLeavesAsync(query, cancellationToken));

    [HttpPost]
    [HasPermission(AppointmentPermissions.SchedulesManage)]
    [ProducesResponseType<ApiResponse<LeaveDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] LeaveCreateDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.AddLeaveAsync(request, cancellationToken), "Leave recorded.");

    [HttpDelete("{id:guid}")]
    [HasPermission(AppointmentPermissions.SchedulesManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Remove(Guid id, CancellationToken cancellationToken)
    {
        await manager.RemoveLeaveAsync(id, cancellationToken);
        return NoContent();
    }
}

[Route("api/v{version:apiVersion}/appointments")]
public sealed class AppointmentsController(IAppointmentManager manager) : ApiControllerBase
{
    /// <summary>Sessions of a doctor on a date with remaining places, next serial and estimated time.</summary>
    [HttpGet("availability")]
    [HasPermission(AppointmentPermissions.View)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<SessionAvailabilityDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Availability([FromQuery] AvailabilityQuery query, CancellationToken cancellationToken) =>
        OkResponse(await manager.GetAvailabilityAsync(query, cancellationToken));

    /// <summary>Appointments of a day by status (defaults to today).</summary>
    [HttpGet("summary")]
    [HasPermission(AppointmentPermissions.View)]
    [ProducesResponseType<ApiResponse<AppointmentDaySummaryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Summary([FromQuery] DateOnly? date, CancellationToken cancellationToken) =>
        OkResponse(await manager.GetDaySummaryAsync(date, cancellationToken));

    [HttpGet]
    [HasPermission(AppointmentPermissions.View)]
    [ProducesResponseType<ApiResponse<PagedResponse<AppointmentDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] AppointmentListQuery query, CancellationToken cancellationToken) =>
        OkResponse(await manager.SearchAsync(query, cancellationToken));

    [HttpGet("{id:guid}")]
    [HasPermission(AppointmentPermissions.View)]
    [ProducesResponseType<ApiResponse<AppointmentDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await manager.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [HasPermission(AppointmentPermissions.Book)]
    [ProducesResponseType<ApiResponse<AppointmentDto>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Book([FromBody] AppointmentBookDto request, CancellationToken cancellationToken)
    {
        var appointment = await manager.BookAsync(request, cancellationToken);
        return CreatedResponse(nameof(GetById), new { id = appointment.Id, version = "1" }, appointment,
            $"Booked: serial {appointment.SerialNumber}, about {appointment.EstimatedTime:HH\\:mm}.");
    }

    [HttpPut("{id:guid}/reschedule")]
    [HasPermission(AppointmentPermissions.Book)]
    [ProducesResponseType<ApiResponse<AppointmentDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Reschedule(Guid id, [FromBody] AppointmentRescheduleDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.RescheduleAsync(id, request, cancellationToken), "Appointment rescheduled.");

    [HttpPost("{id:guid}/confirm")]
    [HasPermission(AppointmentPermissions.Book)]
    [ProducesResponseType<ApiResponse<AppointmentDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Confirm(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await manager.ConfirmAsync(id, cancellationToken), "Appointment confirmed.");

    [HttpPost("{id:guid}/check-in")]
    [HasPermission(AppointmentPermissions.CheckIn)]
    [ProducesResponseType<ApiResponse<AppointmentDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckIn(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await manager.CheckInAsync(id, cancellationToken), "Patient checked in.");

    [HttpPost("{id:guid}/no-show")]
    [HasPermission(AppointmentPermissions.CheckIn)]
    [ProducesResponseType<ApiResponse<AppointmentDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> NoShow(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await manager.MarkNoShowAsync(id, cancellationToken), "Marked as no-show.");

    [HttpPost("{id:guid}/cancel")]
    [HasPermission(AppointmentPermissions.Cancel)]
    [ProducesResponseType<ApiResponse<AppointmentDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] AppointmentCancelDto request, CancellationToken cancellationToken) =>
        OkResponse(await manager.CancelAsync(id, request, cancellationToken), "Appointment cancelled.");

    [HttpPost("{id:guid}/start")]
    [HasPermission(AppointmentPermissions.Consult)]
    [ProducesResponseType<ApiResponse<AppointmentDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Start(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await manager.StartConsultationAsync(id, cancellationToken), "Consultation started.");

    [HttpPost("{id:guid}/complete")]
    [HasPermission(AppointmentPermissions.Consult)]
    [ProducesResponseType<ApiResponse<AppointmentDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Complete(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await manager.CompleteAsync(id, cancellationToken), "Consultation completed.");

    /// <summary>The signed-in doctor's queue for a day (today by default).</summary>
    [HttpGet("my-queue")]
    [HasPermission(AppointmentPermissions.Consult)]
    [ProducesResponseType<ApiResponse<MyQueueDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> MyQueue([FromQuery] QueueQuery query, CancellationToken cancellationToken) =>
        OkResponse(await manager.GetMyQueueAsync(query, cancellationToken));

    [HttpPost("my-queue/call-next")]
    [HasPermission(AppointmentPermissions.Consult)]
    [ProducesResponseType<ApiResponse<AppointmentDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CallNext(CancellationToken cancellationToken)
    {
        var appointment = await manager.CallNextAsync(cancellationToken);
        return OkResponse(appointment, $"Serial {appointment.SerialNumber} called.");
    }
}

[Route("api/v{version:apiVersion}/queue-board")]
public sealed class QueueBoardController(IAppointmentManager manager) : ApiControllerBase
{
    /// <summary>Queue display for waiting areas: doctors, rooms and serial numbers only — no patient names.</summary>
    [HttpGet]
    [HasPermission(AppointmentPermissions.QueueDisplay)]
    [ProducesResponseType<ApiResponse<QueueBoardDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get([FromQuery] QueueQuery query, CancellationToken cancellationToken) =>
        OkResponse(await manager.GetQueueBoardAsync(query, cancellationToken));
}
