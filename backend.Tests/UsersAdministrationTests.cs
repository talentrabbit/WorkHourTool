using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using backend.Contracts;
using backend.Data;
using backend.DbModel;
using backend.Security;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests;

// Every test owns a fresh factory and its own open SQLite in-memory database.
public sealed class UsersAdministrationTests : IDisposable
{
    private readonly TestServerFactory _factory = new();
    private readonly HttpClient _client;
    private readonly int _adminId;

    public UsersAdministrationTests()
    {
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Test-Identity", @"DOMAIN\admin");
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var admin = new User { Gid = "admin", FullName = "Admin Person", Mail = "admin@example.com", Role = "Administrators, workers" };
        db.Users.Add(admin);
        db.SaveChanges();
        _adminId = admin.Id;
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private static object ValidRequest(string gid = "new.user", string? mail = "new.user@example.com") =>
        new { gid, fullName = "New User", mail, roles = new[] { "Worker" } };

    private async Task<UserResponse> CreateAsync(object request)
    {
        using var response = await _client.PostAsJsonAsync("/api/users", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<UserResponse>())!;
    }

    [Fact]
    public async Task CreateListGetDelete_RoundTripsTrimmedCanonicalRolesAndLocation()
    {
        using var response = await _client.PostAsJsonAsync("/api/users", new
        {
            gid = "  new.user  ", fullName = "  New User  ", mail = "  new.user@example.com  ",
            roles = new[] { "workers", "Production_Managers", "PROCESS-ENGINEERS", "administrator", "Worker" }
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<UserResponse>())!;
        Assert.True(created.Id > 0);
        Assert.Equal("new.user", created.Gid);
        Assert.Equal("New User", created.FullName);
        Assert.Equal("new.user@example.com", created.Mail);
        Assert.Equal(new[] { "Worker", "ProductionManager", "ProcessEngineer", "Administrator" }, created.Roles);
        Assert.EndsWith($"/api/users/{created.Id}", response.Headers.Location!.ToString());

        var fetched = (await _client.GetFromJsonAsync<UserResponse>(response.Headers.Location))!;
        Assert.Equal(created.Id, fetched.Id);
        Assert.Equal(created.Roles, fetched.Roles);
        var list = (await _client.GetFromJsonAsync<UserResponse[]>("/api/users"))!;
        Assert.Equal(2, list.Length);
        Assert.Contains(list, u => u.Id == created.Id);
        Assert.Equal(new[] { "Administrator", "Worker" }, list.Single(u => u.Id == _adminId).Roles);
        Assert.Equal("Administrators, workers", list.Single(u => u.Id == _adminId).Role);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal("Worker,ProductionManager,ProcessEngineer,Administrator", (await db.Users.FindAsync(created.Id))!.Role);
        }

        using var deleted = await _client.DeleteAsync($"/api/users/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Empty(await deleted.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/users/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync($"/api/users/{created.Id}")).StatusCode);
    }

    [Fact]
    public async Task Update_ChangesOnlyMailAndCanonicalRoles_AndRemovesSingleRole()
    {
        var created = await CreateAsync(ValidRequest());
        using var response = await _client.PutAsJsonAsync($"/api/users/{created.Id}", new
        {
            gid = "must-not-change", fullName = "Must Not Change", mail = " updated@example.com ",
            roles = new[] { "workers", "Production_Managers", "PROCESS-ENGINEERS", "Worker" }
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<UserResponse>())!;
        Assert.Equal(created.Id, updated.Id);
        Assert.Equal(created.Gid, updated.Gid);
        Assert.Equal(created.FullName, updated.FullName);
        Assert.Equal("updated@example.com", updated.Mail);
        Assert.Equal(new[] { "Worker", "ProductionManager", "ProcessEngineer" }, updated.Roles);
        Assert.Equal("Worker,ProductionManager,ProcessEngineer", updated.Role);

        using var removed = await _client.PutAsJsonAsync($"/api/users/{created.Id}", new
        {
            mail = updated.Mail, roles = new[] { "Worker", "ProcessEngineer" }
        });
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        var fetched = (await _client.GetFromJsonAsync<UserResponse>($"/api/users/{created.Id}"))!;
        Assert.Equal(new[] { "Worker", "ProcessEngineer" }, fetched.Roles);
        using var scope = _factory.Services.CreateScope();
        var stored = (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.FindAsync(created.Id))!;
        Assert.Equal("Worker,ProcessEngineer", stored.Role);
        Assert.Equal(created.Gid, stored.Gid);
        Assert.Equal(created.FullName, stored.FullName);
    }

    public static IEnumerable<object[]> InvalidUpdateRequests()
    {
        yield return new object[] { new { mail = "not-mail", roles = new[] { "Worker" } } };
        yield return new object[] { new { mail = new string('m', 195) + "@x.com", roles = new[] { "Worker" } } };
        yield return new object[] { new { mail = "valid@example.com" } };
        yield return new object[] { new { roles = (string[]?)null } };
        yield return new object[] { new { roles = Array.Empty<string>() } };
        yield return new object[] { new { roles = new[] { " " } } };
        yield return new object[] { new { roles = new[] { "Worker", "Unknown" } } };
        yield return new object[] { new { roles = new string?[] { null } } };
        yield return new object[] { new { roles = new[] { "Worker,Administrator" } } };
    }

    [Theory]
    [MemberData(nameof(InvalidUpdateRequests))]
    public async Task Update_InvalidInput_ReturnsValidationProblemWithoutWriting(object request)
    {
        var created = await CreateAsync(ValidRequest());
        using var response = await _client.PutAsJsonAsync($"/api/users/{created.Id}", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.TryGetProperty("errors", out _));
        var fetched = (await _client.GetFromJsonAsync<UserResponse>($"/api/users/{created.Id}"))!;
        Assert.Equal(created.Mail, fetched.Mail);
        Assert.Equal(created.Roles, fetched.Roles);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \r\n ")]
    public async Task Update_OptionalMail_UsesEmptyStringInLegacyNotNullColumn(string? mail)
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE Users RENAME TO Users_old;");
            await db.Database.ExecuteSqlRawAsync("CREATE TABLE Users (Id INTEGER PRIMARY KEY AUTOINCREMENT, Gid TEXT NOT NULL, FullName TEXT NOT NULL, Mail TEXT NOT NULL, Role TEXT NOT NULL);");
            await db.Database.ExecuteSqlRawAsync("INSERT INTO Users SELECT Id, COALESCE(Gid, ''), FullName, COALESCE(Mail, ''), Role FROM Users_old;");
            await db.Database.ExecuteSqlRawAsync("DROP TABLE Users_old;");
        }
        var first = await CreateAsync(ValidRequest("first", "first@example.com"));
        var second = await CreateAsync(ValidRequest("second", "second@example.com"));
        foreach (var user in new[] { first, second })
        {
            // Null also exercises an omitted mail property.
            object request = mail is null ? new { roles = new[] { "Worker" } }
                : new { mail, roles = new[] { "Worker" } };
            using var response = await _client.PutAsJsonAsync($"/api/users/{user.Id}", request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Null((await response.Content.ReadFromJsonAsync<UserResponse>())!.Mail);
            using var scope = _factory.Services.CreateScope();
            Assert.Equal(string.Empty, (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.FindAsync(user.Id))!.Mail);
        }
    }

    [Fact]
    public async Task Update_MailAllowsOwnAddressAndTrimmedMaximumLength_ButRejectsDuplicates()
    {
        var created = await CreateAsync(ValidRequest());
        using var own = await _client.PutAsJsonAsync($"/api/users/{created.Id}", new
        {
            mail = " NEW.USER@EXAMPLE.COM ", roles = new[] { "Worker" }
        });
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        using var duplicate = await _client.PutAsJsonAsync($"/api/users/{created.Id}", new
        {
            mail = " ADMIN@EXAMPLE.COM ", roles = new[] { "ProcessEngineer" }
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("application/problem+json", duplicate.Content.Headers.ContentType!.MediaType);
        var unchanged = (await _client.GetFromJsonAsync<UserResponse>($"/api/users/{created.Id}"))!;
        Assert.Equal("NEW.USER@EXAMPLE.COM", unchanged.Mail);
        Assert.Equal(new[] { "Worker" }, unchanged.Roles);
        using var maximum = await _client.PutAsJsonAsync($"/api/users/{created.Id}", new
        {
            mail = " " + new string('m', 194) + "@x.com ", roles = new[] { "Worker" }
        });
        Assert.Equal(HttpStatusCode.OK, maximum.StatusCode);
        Assert.Equal(200, (await maximum.Content.ReadFromJsonAsync<UserResponse>())!.Mail!.Length);
    }

    [Theory]
    [InlineData(@"domain\ADMIN")]
    [InlineData("ADMIN@EXAMPLE.COM")]
    [InlineData("admin person")]
    public async Task Update_SelfDemotionConflictsBeforeMailChanges_EvenWithAnotherAdministrator(string identity)
    {
        await CreateAsync(new { gid = "otheradmin", fullName = "Other Admin", roles = new[] { "Administrator" } });
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Identity", identity);
        using var response = await client.PutAsJsonAsync($"/api/users/{_adminId}", new
        {
            mail = "changed@example.com", roles = new[] { "Worker" }
        });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("current user", await response.Content.ReadAsStringAsync());
        var unchanged = (await _client.GetFromJsonAsync<UserResponse>($"/api/users/{_adminId}"))!;
        Assert.Equal("admin@example.com", unchanged.Mail);
        Assert.Equal("Administrators, workers", unchanged.Role);
    }

    [Fact]
    public async Task Update_LastAdministratorGuard_IsIndependentOfSelfDemotion()
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IUserAdministrationService>();
        var result = await service.UpdateAsync(_adminId,
            new UpdateUserRequest { Mail = "changed@example.com", Roles = new[] { "Worker" } },
            new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "someone-else") }, "Test")),
            CancellationToken.None);
        Assert.Equal(UserMutationStatus.Conflict, result.Status);
        Assert.Contains("last administrator", result.Detail);
        var stored = (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.FindAsync(_adminId))!;
        Assert.Equal("admin@example.com", stored.Mail);
        Assert.Equal("Administrators, workers", stored.Role);
    }

    [Fact]
    public async Task Update_AllowsOtherAdministratorDemotionAndOwnNonAdministratorRoleRemoval()
    {
        var other = await CreateAsync(new { gid = "otheradmin", fullName = "Other Admin", roles = new[] { "Administrator", "Worker" } });
        using var demoted = await _client.PutAsJsonAsync($"/api/users/{other.Id}", new { roles = new[] { "Worker" } });
        Assert.Equal(HttpStatusCode.OK, demoted.StatusCode);
        Assert.Equal(new[] { "Worker" }, (await demoted.Content.ReadFromJsonAsync<UserResponse>())!.Roles);
        using var own = await _client.PutAsJsonAsync($"/api/users/{_adminId}", new
        {
            mail = "admin@example.com", roles = new[] { "Administrator" }
        });
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Equal(new[] { "Administrator" }, (await own.Content.ReadFromJsonAsync<UserResponse>())!.Roles);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/users")).StatusCode);
    }

    [Fact]
    public async Task Update_MissingUser_ReturnsNotFound()
    {
        using var response = await _client.PutAsJsonAsync("/api/users/2147483647", new { roles = new[] { "Worker" } });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
    }

    public static IEnumerable<object[]> InvalidRequests()
    {
        yield return new object[] { new { gid = new string('g', 101), fullName = "Name", roles = new[] { "Worker" } } };
        yield return new object[] { new { gid = "gid", roles = new[] { "Worker" } } };
        yield return new object[] { new { gid = "gid", fullName = " \r\n ", roles = new[] { "Worker" } } };
        yield return new object[] { new { gid = "gid", fullName = new string('n', 201), roles = new[] { "Worker" } } };
        yield return new object[] { new { gid = "gid", fullName = "Name", mail = "not-mail", roles = new[] { "Worker" } } };
        yield return new object[] { new { gid = "gid", fullName = "Name", mail = new string('m', 195) + "@x.com", roles = new[] { "Worker" } } };
        yield return new object[] { new { gid = "gid", fullName = "Name" } };
        yield return new object[] { new { gid = "gid", fullName = "Name", roles = (string[]?)null } };
        yield return new object[] { new { gid = "gid", fullName = "Name", roles = Array.Empty<string>() } };
        yield return new object[] { new { gid = "gid", fullName = "Name", roles = new[] { " " } } };
        yield return new object[] { new { gid = "gid", fullName = "Name", roles = new[] { "Worker", "Unknown" } } };
        yield return new object[] { new { gid = "gid", fullName = "Name", roles = new string?[] { null } } };
        yield return new object[] { new { gid = "gid", fullName = "Name", roles = new[] { "Worker,Administrator" } } };
    }

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task InvalidInput_ReturnsValidationProblemWithoutWriting(object request)
    {
        using var response = await _client.PostAsJsonAsync("/api/users", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.TryGetProperty("errors", out _));
        using var scope = _factory.Services.CreateScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.CountAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task OptionalMail_AllowsMultipleUsersWithoutAnAddress(string? mail)
    {
        var first = await CreateAsync(ValidRequest("first", mail));
        var second = await CreateAsync(ValidRequest("second", mail));
        Assert.Null(first.Mail);
        Assert.Null(second.Mail);
    }

    [Fact]
    public async Task OnlyFullNameAndRoles_AreRequired()
    {
        var first = await CreateAsync(new { fullName = "First User", roles = new[] { "Worker", "ProcessEngineer" } });
        var second = await CreateAsync(new { gid = "   ", fullName = "Second User", mail = " ", roles = new[] { "Worker" } });
        Assert.Null(first.Gid);
        Assert.Null(first.Mail);
        Assert.Equal(new[] { "Worker", "ProcessEngineer" }, first.Roles);
        Assert.Null(second.Gid);
        Assert.Null(second.Mail);
        var list = (await _client.GetFromJsonAsync<UserResponse[]>("/api/users"))!;
        Assert.Contains(list, u => u.Id == first.Id);
        Assert.Contains(list, u => u.Id == second.Id);
    }

    [Fact]
    public async Task LegacyNotNullColumns_AllowOmittedGidAndMail()
    {
        // Rebuild only the isolated test directory table to match imported databases.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE Users RENAME TO Users_old;");
            await db.Database.ExecuteSqlRawAsync("CREATE TABLE Users (Id INTEGER PRIMARY KEY AUTOINCREMENT, Gid TEXT NOT NULL, FullName TEXT NOT NULL, Mail TEXT NOT NULL, Role TEXT NOT NULL);");
            await db.Database.ExecuteSqlRawAsync("INSERT INTO Users SELECT Id, COALESCE(Gid, ''), FullName, COALESCE(Mail, ''), Role FROM Users_old;");
            await db.Database.ExecuteSqlRawAsync("DROP TABLE Users_old;");
        }
        var first = await CreateAsync(new { fullName = "Legacy First", roles = new[] { "Worker", "ProcessEngineer" } });
        var second = await CreateAsync(new { fullName = "Legacy Second", roles = new[] { "Worker" } });
        Assert.Null(first.Mail);
        Assert.Null(first.Gid);
        Assert.Null(second.Mail);
        using var verification = _factory.Services.CreateScope();
        var stored = await verification.ServiceProvider.GetRequiredService<AppDbContext>().Users.FindAsync(first.Id);
        Assert.Equal(string.Empty, stored!.Mail);
        Assert.Equal(string.Empty, stored.Gid);
        Assert.Equal("Worker,ProcessEngineer", stored.Role);
    }

    [Fact]
    public async Task MaximumLengths_AreValidatedAfterTrimming()
    {
        var user = await CreateAsync(new
        {
            gid = " " + new string('g', 100) + " ", fullName = " " + new string('n', 200) + " ",
            mail = " " + new string('m', 194) + "@x.com ", roles = new[] { "Worker" }
        });
        Assert.Equal(100, user.Gid!.Length);
        Assert.Equal(200, user.FullName!.Length);
        Assert.Equal(200, user.Mail!.Length);
    }

    [Theory]
    [InlineData(" ADMIN ", "different@example.com")]
    [InlineData("different", " ADMIN@EXAMPLE.COM ")]
    public async Task CaseInsensitiveDuplicates_ReturnConflict(string gid, string mail)
    {
        using var response = await _client.PostAsJsonAsync("/api/users", ValidRequest(gid, mail));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task SelfDeletion_ReturnsConflict_EvenWithAnotherAdministrator()
    {
        await CreateAsync(new { gid = "otheradmin", fullName = "Other Admin", roles = new[] { "Administrator" } });
        using var response = await _client.DeleteAsync($"/api/users/{_adminId}");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("current user", await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync($"/api/users/{_adminId}")).StatusCode);
    }

    [Fact]
    public async Task LastAdministrator_GuardIsIndependentOfSelfDeletion()
    {
        // The policy normally makes the sole administrator the current user. Exercise the service
        // separately so this invariant is covered independently, including legacy multi-role aliases.
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IUserAdministrationService>();
        var result = await service.DeleteAsync(_adminId,
            new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "someone-else") }, "Test")),
            CancellationToken.None);
        Assert.Equal(UserMutationStatus.Conflict, result.Status);
        Assert.Contains("last administrator", result.Detail);
        Assert.NotNull(await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.FindAsync(_adminId));
    }

    [Theory]
    [InlineData(@"domain\ADMIN")]
    [InlineData("admin@company.example")]
    [InlineData("ADMIN@EXAMPLE.COM")]
    [InlineData("admin person")]
    public async Task Policy_AllowsExactDatabaseAccountMatchesWithoutRoleClaims(string identity)
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Identity", identity);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/users/{_adminId}")).StatusCode);
    }

    public static IEnumerable<object[]> ProtectedRoutes()
    {
        foreach (var endpoint in new[] { ("GET", "/api/users"), ("GET", "/api/users/1"), ("POST", "/api/users"), ("PUT", "/api/users/1"), ("DELETE", "/api/users/1") })
            foreach (var scenario in new[] { "anonymous", "nonadmin", "spoofed", "substring", "unknown" })
                yield return new object[] { endpoint.Item1, endpoint.Item2, scenario };
    }

    [Theory]
    [MemberData(nameof(ProtectedRoutes))]
    public async Task RealPolicy_DeniesEveryEndpointDespiteSpoofingAndRoleClaims(string method, string route, string scenario)
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.Add(new User { Gid = "worker", FullName = "Worker Person", Role = "Worker" });
            db.SaveChanges();
        }
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Identity", scenario switch
        {
            "substring" => "adm", "unknown" => "unregistered", _ => @"DOMAIN\worker"
        });
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Administrator");
        client.DefaultRequestHeaders.Add("X-Dev-User", "admin");
        if (scenario == "anonymous") client.DefaultRequestHeaders.Add("X-Test-Anonymous", "1");

        if (scenario == "spoofed")
        {
            // Find-user/manual login responses must not grant server-side privileges or create a login session.
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/find-user?q=admin")).StatusCode);
        }

        using var request = new HttpRequestMessage(new HttpMethod(method), route + "?devUser=admin&roles=Administrator");
        if (method == "POST") request.Content = JsonContent.Create(ValidRequest());
        if (method == "PUT") request.Content = JsonContent.Create(new { mail = "changed@example.com", roles = new[] { "Worker" } });
        using var response = await client.SendAsync(request);
        Assert.Equal(scenario == "anonymous" ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden, response.StatusCode);
        using var verification = _factory.Services.CreateScope();
        Assert.Equal(2, await verification.ServiceProvider.GetRequiredService<AppDbContext>().Users.CountAsync());
    }

    [Fact]
    public async Task Policy_AllowsFullNameOnlyAdministrator()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var admin = (await db.Users.FindAsync(_adminId))!;
            admin.Gid = null;
            admin.Mail = null;
            await db.SaveChangesAsync();
        }
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Identity", "Admin Person");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/users")).StatusCode);
    }

    [Fact]
    public async Task Policy_ReadsCurrentDatabaseRolesOnEveryRequest()
    {
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/users")).StatusCode);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Users.FindAsync(_adminId))!.Role = "Worker";
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync("/api/users")).StatusCode);
    }

    [Theory]
    [InlineData("POST", "null")]
    [InlineData("POST", "{}")]
    [InlineData("POST", "{not valid json}")]
    [InlineData("PUT", "null")]
    [InlineData("PUT", "{}")]
    [InlineData("PUT", "{not valid json}")]
    public async Task MissingOrMalformedBody_ReturnsBadRequest(string method, string body)
    {
        using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(new HttpMethod(method),
            method == "PUT" ? $"/api/users/{_adminId}" : "/api/users") { Content = content };
        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DevelopmentOverride_DoesNotReplaceAuthenticatedAdministrator()
    {
        _client.DefaultRequestHeaders.Add("X-Dev-User", "not-an-admin");
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/users?devUser=not-an-admin")).StatusCode);
    }

    [Fact]
    public async Task OpenApi_DescribesAllUserOperationsAndStatusCodes()
    {
        using var response = await _client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");
        foreach (var (path, method, codes) in new[]
        {
            ("/api/users", "get", new[] { "200", "401", "403" }),
            ("/api/users/{id}", "get", new[] { "200", "401", "403", "404" }),
            ("/api/users", "post", new[] { "201", "400", "401", "403", "409" }),
            ("/api/users/{id}", "delete", new[] { "204", "401", "403", "404", "409" }),
            ("/api/users/{id}", "put", new[] { "200", "400", "401", "403", "404", "409" })
        })
        {
            var operation = paths.GetProperty(path).GetProperty(method);
            Assert.False(string.IsNullOrWhiteSpace(operation.GetProperty("summary").GetString()));
            foreach (var code in codes) Assert.True(operation.GetProperty("responses").TryGetProperty(code, out _));
        }
    }

    [Fact]
    public async Task RegisteredPolicy_RequiresAuthenticationAndDatabaseRequirement()
    {
        using var scope = _factory.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<IAuthorizationPolicyProvider>();
        var policy = await provider.GetPolicyAsync(DatabaseAdministratorRequirement.PolicyName);
        Assert.NotNull(policy);
        Assert.Contains(policy.Requirements, r => r is DatabaseAdministratorRequirement);
        Assert.Contains(policy.Requirements, r => r is Microsoft.AspNetCore.Authorization.Infrastructure.DenyAnonymousAuthorizationRequirement);
    }
}