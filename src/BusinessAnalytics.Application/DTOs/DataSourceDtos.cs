using BusinessAnalytics.Domain.Entities;


namespace BusinessAnalytics.Application.DTOs
{
    public class DataSourceDtos
    {
        public record DataSourceCreateRequest(string Name, DataSourceType Type);
        public record DataSourceResponse(int Id, string Name, DataSourceType Type, DateTime CreatedAtUtc);
    }
}
