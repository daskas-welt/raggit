using System;
using System.Linq;
using System.Net.Http;
using FluentAssertions;
using RAGGit.Client.Maui;
using RAGGit.Client.Maui.Services;
using RAGGit.Client.Maui.ViewModels;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// T019: Client view-model role gating (FR-004). LibraryViewModel must show Upload/Delete
/// iff discovered role is Admin. No role:"Employee" constructor default — must take ClientSession.
/// </summary>
public sealed class ClientRoleGatingTests
{
    [Fact]
    public void LibraryViewModel_TakesClientSession_NotStringRoleDefault()
    {
        var ctors = typeof(LibraryViewModel).GetConstructors();
        // Should have a ctor with ClientSession parameter and no string role default
        var hasSessionCtor = ctors.Any(c =>
            c.GetParameters().Any(p => p.ParameterType == typeof(ClientSession)));
        hasSessionCtor.Should().BeTrue("LibraryViewModel should take ClientSession (remove role = \"Employee\" default) per T022");

        var hasStringRoleDefault = ctors.Any(c =>
            c.GetParameters().Any(p => p.ParameterType == typeof(string) && p.HasDefaultValue));
        hasStringRoleDefault.Should().BeFalse("LibraryViewModel should no longer have role = \"Employee\" default param");
    }

    [Fact]
    public void LibraryViewModel_IsAdmin_TrueWhenSessionRoleAdmin()
    {
        var handler = new StubHandler(_ => throw new InvalidOperationException("should not hit network"));
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5001") };
        var api = new DocumentsApiClient(http);
        var session = new ClientSession { WorkstationUrl = "http://localhost:5001", ApiKey = "k", Role = "Admin" };

        // This ctor should exist after T022
        var vm = CreateLibraryViewModel(api, session);
        vm.IsAdmin.Should().BeTrue();
    }

    [Fact]
    public void LibraryViewModel_IsAdmin_FalseWhenSessionRoleEmployee()
    {
        var handler = new StubHandler(_ => throw new InvalidOperationException("should not hit network"));
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5001") };
        var api = new DocumentsApiClient(http);
        var session = new ClientSession { WorkstationUrl = "http://localhost:5001", ApiKey = "k", Role = "Employee" };

        var vm = CreateLibraryViewModel(api, session);
        vm.IsAdmin.Should().BeFalse();
    }

    [Fact]
    public void LibraryViewModel_IsAdmin_FalseWhenSessionRoleEmpty()
    {
        var handler = new StubHandler(_ => throw new InvalidOperationException("should not hit network"));
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5001") };
        var api = new DocumentsApiClient(http);
        var session = new ClientSession { WorkstationUrl = "http://localhost:5001", ApiKey = "k", Role = string.Empty };

        var vm = CreateLibraryViewModel(api, session);
        vm.IsAdmin.Should().BeFalse();
    }

    private static LibraryViewModel CreateLibraryViewModel(DocumentsApiClient api, ClientSession session)
    {
        // Prefer ClientSession ctor; fallback via reflection for red phase before T022
        var ctor = typeof(LibraryViewModel).GetConstructors()
            .FirstOrDefault(c => c.GetParameters().Any(p => p.ParameterType == typeof(ClientSession)));
        if (ctor is not null)
        {
            var args = ctor.GetParameters().Select(p =>
                p.ParameterType == typeof(ClientSession) ? (object)session :
                p.ParameterType == typeof(DocumentsApiClient) ? api :
                throw new InvalidOperationException($"Unexpected param {p.ParameterType}")).ToArray();
            return (LibraryViewModel)ctor.Invoke(args);
        }
        // Fallback to legacy string role ctor (red phase) — will fail IsAdmin expectations
        var legacy = typeof(LibraryViewModel).GetConstructor(new[] { typeof(DocumentsApiClient), typeof(string) });
        if (legacy is not null)
            return (LibraryViewModel)legacy.Invoke(new object[] { api, session.Role });
        throw new InvalidOperationException("No suitable LibraryViewModel constructor found");
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _func;
        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> func) => _func = func;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(_func(request));
    }
}
