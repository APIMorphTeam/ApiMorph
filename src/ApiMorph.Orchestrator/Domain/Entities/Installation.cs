namespace ApiMorph.Orchestrator.Domain.Entities;

public class Installation
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<Repository> Repositories { get; set; } = [];
}
