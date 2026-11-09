using System.Net;
using System.Text.Json;

namespace SmartOps.Web.Bridge;

// Account settings belong to the authenticated 4D account, not the disposable Identity workspace.
public sealed class AccountService(BridgeSession bridge)
{
    public sealed record LayoutPreference(bool CompactContracts, string DiaryView = "month");

    public async Task<bool> GetContractLayout()
    {
        using var client = bridge.Client();
        using var response = await client.GetAsync("api/auth/preferences");
        await Check(response);
        return (await response.Content.ReadFromJsonAsync<LayoutPreference>()
            ?? throw new HttpRequestException("4D returned empty account preferences.")).CompactContracts;
    }

    public async Task SaveContractLayout(bool compactList)
    {
        using var client = bridge.Client();
        using var response = await client.PostAsJsonAsync("api/auth/preferences",
            new { CompactContracts = compactList }, new JsonSerializerOptions { PropertyNamingPolicy = null });
        await Check(response);
    }

    public static bool IsDiaryView(string? view) => view is "list" or "week" or "month";

    public async Task<string> GetDiaryView()
    {
        using var client = bridge.Client();
        using var response = await client.GetAsync("api/auth/preferences");
        await Check(response);
        var preferences = await response.Content.ReadFromJsonAsync<LayoutPreference>()
            ?? throw new HttpRequestException("4D returned empty account preferences.");
        return IsDiaryView(preferences.DiaryView) ? preferences.DiaryView : "month";
    }

    public async Task SaveDiaryView(string view)
    {
        if (!IsDiaryView(view)) throw new ArgumentException("Unknown diary view.", nameof(view));
        using var client = bridge.Client();
        using var response = await client.PostAsJsonAsync("api/auth/preferences",
            new { DiaryView = view }, new JsonSerializerOptions { PropertyNamingPolicy = null });
        await Check(response);
    }

    public sealed record Profile(string Username, string? PhoneNumber, string? ProfilePicture);

    public async Task<Profile> GetProfile()
    {
        using var client = bridge.Client();
        using var response = await client.GetAsync("api/auth/profile");
        await Check(response);
        return await response.Content.ReadFromJsonAsync<Profile>()
            ?? throw new HttpRequestException("4D returned an empty account profile.");
    }

    public async Task SaveProfile(string? phoneNumber, byte[]? picture)
    {
        using var client = bridge.Client();
        var values = new Dictionary<string, string?> { ["PhoneNumber"] = phoneNumber ?? "" };
        if (picture is not null) values["ProfilePicture"] = Convert.ToBase64String(picture);
        using var response = await client.PostAsJsonAsync("api/auth/profile", values,
            new JsonSerializerOptions { PropertyNamingPolicy = null, DictionaryKeyPolicy = null });
        await Check(response);
    }

    public async Task ChangePassword(string newPassword)
    {
        using var client = bridge.Client();
        using var response = await client.PostAsJsonAsync("api/web/password",
            new { NewPassword = newPassword },
            new JsonSerializerOptions { PropertyNamingPolicy = null });
        await Check(response);
    }

    public bool IsCurrentUser(string id) => bridge.Snapshot["account"]?["Id"]?.GetValue<string>() == id;

    public async Task<string> SaveUser(SmartOps.Web.Data.AppUser user, IEnumerable<string> roles, string? password, bool create = false)
    {
        var revision = bridge.Snapshot["users"]?.AsArray()
            .FirstOrDefault(x => x?["Id"]?.GetValue<string>() == user.Id)?["Revision"]?.GetValue<int>() ?? 0;
        using var client = bridge.Client();
        using var response = await client.PostAsJsonAsync("api/web/users/save", new {
            Id = create ? "" : user.Id, Revision = revision,
            UserName = user.UserName, user.Email, user.FirstName, user.LastName, BranchCode = user.BranchCode ?? "", user.LockBranch,
            Roles = roles.ToArray(), Password = password ?? "",
            ProfilePicture = user.ProfilePicture is { Length: > 0 } ? Convert.ToBase64String(user.ProfilePicture) : null
        }, new JsonSerializerOptions { PropertyNamingPolicy = null });
        await Check(response);
        using var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return result.RootElement.GetProperty("Id").GetString()!;
    }

    public async Task DeleteUser(string id)
    {
        using var client = bridge.Client();
        using var response = await client.PostAsJsonAsync("api/web/users/delete", new { Id = id },
            new JsonSerializerOptions { PropertyNamingPolicy = null });
        await Check(response);
    }

    private static async Task Check(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
        {
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            throw new AccountValidationException(body.RootElement.GetProperty("error").GetString() ?? "Unable to save account settings.");
        }
        throw new HttpRequestException("Unable to save account settings in 4D. Please try again.", null, response.StatusCode);
    }
}

public sealed class AccountValidationException(string message) : Exception(message);
