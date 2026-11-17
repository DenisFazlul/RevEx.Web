using System.Globalization;
using RevEx.Application.Abstractions.Persistence;
using RevEx.Domain.Contracts.Content;
using RevEx.Domain.Entities;

namespace RevEx.Application.Content;

public sealed class ContentParameterService(
    IContentRepository contents,
    IContentParameterRepository parameters)
{
    public async Task<IReadOnlyCollection<ContentParameterDto>> ListAsync(int contentId, CancellationToken token)
    {
        if (await contents.GetByIdAsync(contentId, token) is null)
        {
            throw new ContentOperationException(ContentFailure.NotFound);
        }

        var items = await parameters.ListAsync(contentId, token);
        return items.Select(Map).ToArray();
    }

    public async Task<ContentParameterDto> GetAsync(int contentId, int id, CancellationToken token)
    {
        return Map(await FindAsync(contentId, id, token));
    }

    public async Task<ContentParameterDto> CreateAsync(int contentId, ContentParameterInput input, CancellationToken token)
    {
        Validate(input);
        if (await contents.GetByIdAsync(contentId, token) is null)
        {
            throw new ContentOperationException(ContentFailure.NotFound);
        }

        var parameter = new ContentParameter
        {
            ContentId = contentId,
            Name = input.Name.Trim(),
            Kind = input.Kind,
            Value = input.Value,
            Help = input.Help?.Trim()
        };
        await parameters.AddAsync(parameter, token);
        return Map(parameter);
    }

    public async Task UpdateAsync(int contentId, int id, ContentParameterInput input, CancellationToken token)
    {
        Validate(input);
        var parameter = await FindAsync(contentId, id, token);
        parameter.Name = input.Name.Trim();
        parameter.Kind = input.Kind;
        parameter.Value = input.Value;
        parameter.Help = input.Help?.Trim();
        await parameters.UpdateAsync(parameter, token);
    }

    public async Task DeleteAsync(int contentId, int id, CancellationToken token)
    {
        var parameter = await FindAsync(contentId, id, token);
        await parameters.DeleteAsync(parameter, token);
    }

    private async Task<ContentParameter> FindAsync(int contentId, int id, CancellationToken token)
    {
        return await parameters.FindAsync(contentId, id, token)
            ?? throw new ContentOperationException(ContentFailure.NotFound);
    }

    private static void Validate(ContentParameterInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
        {
            throw new ContentOperationException(ContentFailure.Validation, "Укажите название параметра.");
        }

        var valid = input.Kind switch
        {
            "string" => input.Value is not null,
            "number" => decimal.TryParse(input.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out _),
            "link" => Uri.TryCreate(input.Value, UriKind.Absolute, out var uri)
                && (uri.Scheme is "http" or "https") && uri.Host.Length > 0,
            _ => false
        };
        if (!valid)
        {
            throw new ContentOperationException(ContentFailure.Validation, "Значение не соответствует типу параметра.");
        }
    }

    private static ContentParameterDto Map(ContentParameter parameter)
    {
        return new ContentParameterDto(parameter.Id, parameter.Name, parameter.Kind, parameter.Value, parameter.Help);
    }
}
