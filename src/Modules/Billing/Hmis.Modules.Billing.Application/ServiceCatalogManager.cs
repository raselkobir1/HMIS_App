using Hmis.BuildingBlocks.Application.Exceptions;
using Hmis.BuildingBlocks.Application.Pagination;
using Hmis.Modules.Billing.Domain.Entities;

namespace Hmis.Modules.Billing.Application;

/// <summary>The service price list.</summary>
public sealed class ServiceCatalogManager(IBillableServiceRepository services, IBillingUnitOfWork unitOfWork) : IServiceCatalogManager
{
    public async Task<PagedResponse<BillableServiceDto>> SearchAsync(ServiceQuery query, CancellationToken cancellationToken) =>
        (await services.SearchAsync(query, cancellationToken)).Map(ToDto);

    public async Task<BillableServiceDto> CreateAsync(ServiceCreateDto request, CancellationToken cancellationToken)
    {
        if (await services.CodeExistsAsync(request.Code.Trim().ToUpperInvariant(), cancellationToken))
        {
            throw new ConflictException("Another service already uses this code.", BillingErrorCodes.ServiceCodeExists);
        }

        var service = BillableService.Create(request.Code, request.Name, request.Category, request.Price, request.IsPriceEditable);
        await services.AddAsync(service, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(service);
    }

    public async Task<BillableServiceDto> UpdateAsync(Guid id, ServiceUpdateDto request, CancellationToken cancellationToken)
    {
        var service = await GetAsync(id, cancellationToken);
        services.SetExpectedRowVersion(service, request.RowVersion);
        service.Update(request.Name, request.Category, request.Price, request.IsPriceEditable);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(service);
    }

    public async Task<BillableServiceDto> SetStatusAsync(Guid id, ServiceStatusDto request, CancellationToken cancellationToken)
    {
        var service = await GetAsync(id, cancellationToken);
        service.SetActive(request.IsActive);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(service);
    }

    private async Task<BillableService> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await services.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Service", id);

    private static BillableServiceDto ToDto(BillableService s) =>
        new(s.Id, s.Code, s.Name, s.Category, s.Price, s.IsPriceEditable, s.IsActive, s.RowVersion);
}
