using Chats.BE.Services.Containers;
using Chats.BE.UnitTest.CodeInterpreter;
using Chats.DB;
using Chats.DB.Enums;
using Chats.DockerInterface;
using Chats.DockerInterface.Models;
using Docker.DotNet.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Chats.BE.UnitTest.Containers;

public sealed class ContainerPortMappingTests
{
    private const int OwnerId = 10;
    private const int AdminId = 20;

    [Fact]
    public void AllocateHostPort_ReturnsLowestFreePort_AndNullWhenExhausted()
    {
        Assert.Equal(41000, ContainerPortMappingService.AllocateHostPort(41000, 41002, []));
        Assert.Equal(41001, ContainerPortMappingService.AllocateHostPort(41000, 41002, [41000, 41002]));
        Assert.Null(ContainerPortMappingService.AllocateHostPort(41000, 41002, [41000, 41001, 41002]));
    }

    [Theory]
    [InlineData("192.168.1.2", null, "192.168.1.2:41000")]
    [InlineData(null, "tcp://10.0.0.5:2375", "10.0.0.5:41000")]
    [InlineData(null, "unix:///var/run/docker.sock", "127.0.0.1:41000")]
    [InlineData(null, null, "127.0.0.1:41000")]
    [InlineData("::1", null, "[::1]:41000")]
    public void GetUpstreamAddress_UsesPublicHostThenEndpointThenLoopback(string? publicHost, string? endpoint, string expected)
    {
        ContainerRuntimeNode node = new() { Name = "n", AiName = "n", PublicHost = publicHost, Endpoint = endpoint };
        Assert.Equal(expected, ContainerPortMappingService.GetUpstreamAddress(node, 41000));
    }

    [Fact]
    public async Task EnsureMapping_AllocatesFromPool_IsIdempotent_AndRecreatesContainer()
    {
        await using ChatsDB db = CreateDb();
        (ContainerResource resource, FakeDockerService docker, ContainerPortMappingService service) = await SetupAsync(db, OwnerId, portRange: (41000, 41001));

        ContainerPortMapping first = await service.EnsureMappingAsync(resource.Id, 8080, "TCP", CancellationToken.None);
        ContainerPortMapping again = await service.EnsureMappingAsync(resource.Id, 8080, "tcp", CancellationToken.None);

        Assert.Equal(41000, first.HostPort);
        Assert.Equal("tcp", first.Protocol);
        Assert.Equal(first.Id, again.Id);
        Assert.Equal(1, docker.CreateContainerCalls);
        ContainerCreateOptions options = Assert.IsType<ContainerCreateOptions>(docker.CreateOptions.Single());
        ContainerPortBindingSpec binding = Assert.Single(options.PortBindings);
        Assert.Equal((8080, 41000, "tcp"), (binding.ContainerPort, binding.HostPort, binding.Protocol));
        Assert.Contains("old-container", docker.DeletedContainers);
        Assert.NotEqual("old-container", resource.BackendResourceId);
    }

    [Fact]
    public async Task EnsureMapping_ReusesInactivePorts_AndFailsWhenPoolExhausted()
    {
        await using ChatsDB db = CreateDb();
        (ContainerResource resource, _, ContainerPortMappingService service) = await SetupAsync(db, OwnerId, portRange: (41000, 41000));
        db.ContainerPortMappings.Add(new ContainerPortMapping
        {
            ContainerResourceId = resource.Id, RuntimeNodeId = resource.RuntimeNodeId, ContainerPort = 22, HostPort = 41000,
            Protocol = "tcp", IsActive = false, CreatedAt = DateTime.UtcNow, RemovedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        ContainerPortMapping mapping = await service.EnsureMappingAsync(resource.Id, 80, "tcp", CancellationToken.None);
        Assert.Equal(41000, mapping.HostPort);

        ContainerResourceException ex = await Assert.ThrowsAsync<ContainerResourceException>(
            () => service.EnsureMappingAsync(resource.Id, 81, "tcp", CancellationToken.None));
        Assert.Equal(ContainerResourceErrorCodes.PortPoolExhausted, ex.Code);

        // udp has its own port space
        ContainerPortMapping udp = await service.EnsureMappingAsync(resource.Id, 81, "udp", CancellationToken.None);
        Assert.Equal(41000, udp.HostPort);
    }

    [Fact]
    public async Task EnsureMapping_EnforcesQuota_ForUsers_ButAdminsBypass()
    {
        await using ChatsDB db = CreateDb();
        db.UserContainerQuota.Add(new UserContainerQuotum { UserId = null, AllowedNetworkModes = "*", MaxPortMappings = 1, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        (ContainerResource userResource, _, ContainerPortMappingService service) = await SetupAsync(db, OwnerId, portRange: (41000, 41010));

        await service.EnsureMappingAsync(userResource.Id, 80, "tcp", CancellationToken.None);
        ContainerResourceException ex = await Assert.ThrowsAsync<ContainerResourceException>(
            () => service.EnsureMappingAsync(userResource.Id, 81, "tcp", CancellationToken.None));
        Assert.Equal(ContainerResourceErrorCodes.QuotaExceeded, ex.Code);
        Assert.Equal(1, await db.ContainerPortMappings.CountAsync(x => x.IsActive));

        ContainerResource adminResource = await AddResourceAsync(db, AdminId, userResource.RuntimeNodeId);
        await service.EnsureMappingAsync(adminResource.Id, 80, "tcp", CancellationToken.None);
        await service.EnsureMappingAsync(adminResource.Id, 81, "tcp", CancellationToken.None);
        Assert.Equal(2, await db.ContainerPortMappings.CountAsync(x => x.IsActive && x.ContainerResourceId == adminResource.Id));
    }

    [Fact]
    public async Task EnsureMapping_RejectsTemporaryContainers_AndNodesWithoutPool()
    {
        await using ChatsDB db = CreateDb();
        (ContainerResource resource, _, ContainerPortMappingService service) = await SetupAsync(db, OwnerId, portRange: null);
        ContainerResourceException noPool = await Assert.ThrowsAsync<ContainerResourceException>(
            () => service.EnsureMappingAsync(resource.Id, 80, "tcp", CancellationToken.None));
        Assert.Equal(ContainerResourceErrorCodes.PortPoolNotConfigured, noPool.Code);

        resource.IsPermanent = false;
        await db.SaveChangesAsync();
        ContainerResourceException temp = await Assert.ThrowsAsync<ContainerResourceException>(
            () => service.EnsureMappingAsync(resource.Id, 80, "tcp", CancellationToken.None));
        Assert.Equal(ContainerResourceErrorCodes.PermanentContainerRequired, temp.Code);
    }

    [Fact]
    public async Task RemoveMapping_DeactivatesAndRecreatesWithoutBinding()
    {
        await using ChatsDB db = CreateDb();
        (ContainerResource resource, FakeDockerService docker, ContainerPortMappingService service) = await SetupAsync(db, OwnerId, portRange: (41000, 41010));
        ContainerPortMapping mapping = await service.EnsureMappingAsync(resource.Id, 80, "tcp", CancellationToken.None);

        await service.RemoveMappingAsync(mapping.Id, CancellationToken.None);

        Assert.False((await db.ContainerPortMappings.SingleAsync(x => x.Id == mapping.Id)).IsActive);
        Assert.Empty(docker.CreateOptions.Last()!.PortBindings);
        Assert.Empty(await service.ListAsync(resource.Id, CancellationToken.None));
    }

    [Fact]
    public async Task ReleaseResource_DeactivatesMappingsMounts_AndDeletesInternalVolume()
    {
        await using ChatsDB db = CreateDb();
        (ContainerResource resource, FakeDockerService docker, ContainerPortMappingService service) = await SetupAsync(db, OwnerId, portRange: (41000, 41010));
        db.ContainerVolumes.Add(new ContainerVolume
        {
            OwnerUserId = OwnerId, RuntimeNodeId = resource.RuntimeNodeId, ContainerResourceId = resource.Id, IsStandalone = false,
            BackendVolumeId = "chats-vol-internal", Name = "v", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        await service.EnsureMappingAsync(resource.Id, 80, "tcp", CancellationToken.None);
        ContainerRuntimeManager runtime = new(db, new FakeBackendFactory(docker), NullLogger<ContainerRuntimeManager>.Instance);

        await runtime.ReleaseResourceAsync(resource, DateTime.UtcNow, CancellationToken.None);
        await db.SaveChangesAsync();

        Assert.NotNull(resource.DeletedAt);
        Assert.False(await db.ContainerPortMappings.AnyAsync(x => x.IsActive));
        Assert.Contains("chats-vol-internal", docker.DeletedVolumes);
        Assert.False((await db.ContainerVolumes.SingleAsync()).IsActive);
    }

    [Fact]
    public async Task QuotaResolver_ReturnsNullForAdmins_AndFallsBackToDefault()
    {
        await using ChatsDB db = CreateDb();
        AddUsers(db);
        db.UserContainerQuota.Add(new UserContainerQuotum { UserId = null, AllowedNetworkModes = "bridge", MaxContainerCount = 1, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        Assert.Null(await ContainerQuotaResolver.ResolveAsync(db, AdminId, CancellationToken.None));
        UserContainerQuotum? quota = await ContainerQuotaResolver.ResolveAsync(db, OwnerId, CancellationToken.None);
        Assert.Equal(1, quota?.MaxContainerCount);
    }

    [Fact]
    public void BuildCreateOptions_MountsInternalVolumeAtWorkDir_AndSkipsInactive()
    {
        ContainerResource resource = new() { Id = 5, OwnerUserId = OwnerId, IsPermanent = true, Name = "c", Image = "i", BackendResourceId = "b" };
        ContainerVolume internalVolume = new() { BackendVolumeId = "chats-vol-a", IsActive = true, Name = "a" };
        ContainerVolume standalone = new() { BackendVolumeId = "chats-vol-b", IsActive = true, IsStandalone = true, Name = "b" };
        ContainerVolumeMount[] mounts =
        [
            new() { Id = 1, Volume = standalone, ContainerPath = "/data/", IsReadOnly = true, IsActive = true },
            new() { Id = 2, Volume = standalone, ContainerPath = "/old", IsActive = false },
        ];
        ContainerPortMapping[] mappings =
        [
            new() { Id = 1, ContainerPort = 80, HostPort = 41000, Protocol = "tcp", IsActive = true },
            new() { Id = 2, ContainerPort = 81, HostPort = 41001, Protocol = "tcp", IsActive = false },
        ];

        ContainerCreateOptions options = ContainerRuntimeManager.BuildCreateOptions(resource, "/app", internalVolume, mounts, mappings);

        Assert.Equal(
            [new ContainerVolumeMountSpec("chats-vol-a", "/app", false), new ContainerVolumeMountSpec("chats-vol-b", "/data", true)],
            options.Mounts);
        Assert.Equal([new ContainerPortBindingSpec(80, 41000, "tcp")], options.PortBindings);
        Assert.Equal("5", options.Labels[ContainerRuntimeManager.LabelResourceId]);
    }

    [Fact]
    public void BuildCreateContainerParameters_MapsMountsAndPortBindings()
    {
        ContainerCreateOptions options = new()
        {
            Mounts = [new ContainerVolumeMountSpec("vol1", "/data", true)],
            PortBindings = [new ContainerPortBindingSpec(80, 41000), new ContainerPortBindingSpec(53, 41001, "udp")],
            Labels = new Dictionary<string, string> { ["chats.resource-id"] = "7" },
        };

        CreateContainerParameters p = DockerService.BuildCreateContainerParameters(
            new CodePodConfig(), "codepod-x", "img:latest", new ResourceLimits { CpuCores = 1, MemoryBytes = 1 << 30, MaxProcesses = 100 },
            "bridge", options, DateTimeOffset.UtcNow);

        Mount mount = Assert.Single(p.HostConfig.Mounts);
        Assert.Equal(("volume", "vol1", "/data", true), (mount.Type, mount.Source, mount.Target, mount.ReadOnly));
        Assert.Equal("41000", Assert.Single(p.HostConfig.PortBindings["80/tcp"]).HostPort);
        Assert.Equal("0.0.0.0", p.HostConfig.PortBindings["80/tcp"][0].HostIP);
        Assert.Equal("41001", Assert.Single(p.HostConfig.PortBindings["53/udp"]).HostPort);
        Assert.Contains("80/tcp", p.ExposedPorts.Keys);
        Assert.Contains("53/udp", p.ExposedPorts.Keys);
        Assert.Equal("7", p.Labels["chats.resource-id"]);
        Assert.Equal("bridge", p.HostConfig.NetworkMode);
    }

    private static ChatsDB CreateDb()
        => new(new DbContextOptionsBuilder<ChatsDB>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);

    private static void AddUsers(ChatsDB db)
    {
        if (db.Users.Any()) return;
        DateTime now = DateTime.UtcNow;
        db.Users.Add(new User { Id = OwnerId, UserName = "user", DisplayName = "user", Role = "user", Enabled = true, CreatedAt = now, UpdatedAt = now });
        db.Users.Add(new User { Id = AdminId, UserName = "admin", DisplayName = "admin", Role = "admin", Enabled = true, CreatedAt = now, UpdatedAt = now });
    }

    private static async Task<(ContainerResource, FakeDockerService, ContainerPortMappingService)> SetupAsync(ChatsDB db, int ownerId, (int Start, int End)? portRange)
    {
        AddUsers(db);
        DateTime now = DateTime.UtcNow;
        ContainerRuntimeNode node = new()
        {
            Name = "node", AiName = "node", BackendType = (byte)DBContainerBackendType.Docker, IsEnabled = true,
            PublicHost = "192.168.1.2", PortRangeStart = portRange?.Start, PortRangeEnd = portRange?.End, CreatedAt = now, UpdatedAt = now,
        };
        db.ContainerRuntimeNodes.Add(node);
        await db.SaveChangesAsync();
        ContainerResource resource = await AddResourceAsync(db, ownerId, node.Id);
        FakeDockerService docker = new();
        ContainerRuntimeManager runtime = new(db, new FakeBackendFactory(docker), NullLogger<ContainerRuntimeManager>.Instance);
        return (resource, docker, new ContainerPortMappingService(db, runtime, NullLogger<ContainerPortMappingService>.Instance));
    }

    private static async Task<ContainerResource> AddResourceAsync(ChatsDB db, int ownerId, int nodeId)
    {
        DateTime now = DateTime.UtcNow;
        ContainerResource resource = new()
        {
            OwnerUserId = ownerId, RuntimeNodeId = nodeId, IsPermanent = true, BackendResourceId = "old-container",
            Name = "dev", Image = "sdcb/code-interpreter:latest", CreatedAt = now, UpdatedAt = now,
        };
        db.ContainerResources.Add(resource);
        await db.SaveChangesAsync();
        return resource;
    }

    private sealed class FakeBackendFactory(IDockerService docker)
        : ContainerBackendFactory(Options.Create(new CodePodConfig()), NullLoggerFactory.Instance)
    {
        public override IDockerService Get(ContainerRuntimeNode node) => docker;
    }
}
