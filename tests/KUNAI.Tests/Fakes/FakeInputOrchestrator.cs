using KUNAI.Core.Abstractions;
using KUNAI.Core.Domain;

namespace KUNAI.Tests.Fakes;

public sealed class FakeInputOrchestrator : IInputOrchestrator
{
    public List<Profile> Profiles { get; } = new();

    public int StartCalls { get; private set; }

    public int StopCalls { get; private set; }

    public void SetProfile(Profile profile) => Profiles.Add(profile);

    public void Start() => StartCalls++;

    public void Stop() => StopCalls++;
}
