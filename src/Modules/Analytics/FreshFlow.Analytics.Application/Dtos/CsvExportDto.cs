namespace FreshFlow.Analytics.Application.Dtos;

public sealed record CsvExportDto(string FileName, string ContentType, byte[] Content);
