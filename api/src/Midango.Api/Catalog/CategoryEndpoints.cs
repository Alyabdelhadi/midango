using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Midango.Api.Data;
using Midango.Api.Identity;

namespace Midango.Api.Catalog;

public static partial class CategoryEndpoints
{
    public sealed record FeatureDto(Guid Id, string NameEn, string NameAr, string? IconUrl);

    public sealed record CategoryDto(
        Guid Id, string NameEn, string NameAr, string IconKey, int[] DurationsMinutes,
        int SortOrder, bool Active, IReadOnlyList<FeatureDto> Features, string? IconUrl)
    {
        public static CategoryDto From(SportCategory c) => new(
            c.Id, c.NameEn, c.NameAr, c.IconKey, c.DurationsMinutes, c.SortOrder, c.Active,
            c.Features.OrderBy(f => f.SortOrder).Select(f => new FeatureDto(f.Id, f.NameEn, f.NameAr, f.IconPath)).ToList(),
            c.IconPath);
    }

    /// Features are listed in display order. An item with an id renames that feature;
    /// an item without one adds a feature; features left out are removed.
    public sealed record FeatureInput(Guid? Id, string? NameEn, string? NameAr);

    public sealed record CategoryInput(
        string? NameEn, string? NameAr, string? IconKey, int[]? DurationsMinutes,
        int SortOrder, bool Active, IReadOnlyList<FeatureInput>? Features);

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex IconKeyPattern();

    public static void MapCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        // Public: the app builds its sport chips and filters from this.
        app.MapGet("/categories", async (MidangoDb db, CancellationToken ct) =>
        {
            var categories = await db.SportCategories.Include(c => c.Features)
                .Where(c => c.Active).OrderBy(c => c.SortOrder).ThenBy(c => c.NameEn).ToListAsync(ct);
            return Results.Ok(categories.Select(CategoryDto.From));
        });

        var admin = app.MapGroup("/admin/categories").RequireAuthorization(Policies.PlatformOperator);

        admin.MapGet("", async (MidangoDb db, CancellationToken ct) =>
        {
            var categories = await db.SportCategories.Include(c => c.Features)
                .OrderBy(c => c.SortOrder).ThenBy(c => c.NameEn).ToListAsync(ct);
            return Results.Ok(categories.Select(CategoryDto.From));
        });

        admin.MapPost("", async (CategoryInput input, MidangoDb db, CancellationToken ct) =>
        {
            if (Validate(input) is { } error) return ApiErrors.Fail(400, error);
            if (input.Features?.Any(f => f.Id is not null) == true)
                return ApiErrors.Fail(400, "A new category can't refer to existing features.");

            var category = new SportCategory { NameEn = "", NameAr = "", IconKey = "" };
            Apply(category, input);
            db.SportCategories.Add(category);
            await db.SaveChangesAsync(ct);
            return Results.Json(CategoryDto.From(category), statusCode: 201);
        });

        admin.MapPut("/{id:guid}", async (Guid id, CategoryInput input, MidangoDb db, CancellationToken ct) =>
        {
            if (Validate(input) is { } error) return ApiErrors.Fail(400, error);
            var category = await db.SportCategories.Include(c => c.Features).SingleOrDefaultAsync(c => c.Id == id, ct);
            if (category is null) return ApiErrors.Fail(404, "Category not found.");

            var known = category.Features.Select(f => f.Id).ToHashSet();
            if (input.Features?.Any(f => f.Id is { } fid && !known.Contains(fid)) == true)
                return ApiErrors.Fail(400, "One of the features doesn't belong to this category.");

            Apply(category, input);
            await db.SaveChangesAsync(ct);
            return Results.Ok(CategoryDto.From(category));
        });
    }

    private static string? Validate(CategoryInput input)
    {
        if (!Input.Bilingual(input.NameEn, input.NameAr, 60))
            return "Enter the category name in English and Arabic, up to 60 characters each.";
        if (input.IconKey is null || input.IconKey.Length > 40 || !IconKeyPattern().IsMatch(input.IconKey))
            return "Choose an icon.";

        var durations = input.DurationsMinutes ?? [];
        if (durations.Length is < 1 or > 4
            || durations.Any(m => m is < 30 or > 240 || m % 30 != 0)
            || durations.Distinct().Count() != durations.Length)
            return "Choose 1 to 4 booking lengths between 30 and 240 minutes, in steps of 30.";

        if (input.Features?.Any(f => !Input.Bilingual(f.NameEn, f.NameAr, 60)) == true)
            return "Enter every court feature in English and Arabic, up to 60 characters each.";
        return null;
    }

    private static void Apply(SportCategory category, CategoryInput input)
    {
        category.NameEn = input.NameEn!.Trim();
        category.NameAr = input.NameAr!.Trim();
        category.IconKey = input.IconKey!;
        category.DurationsMinutes = [.. input.DurationsMinutes!.Order()];
        category.SortOrder = input.SortOrder;
        category.Active = input.Active;

        var wanted = input.Features ?? [];
        category.Features.RemoveAll(f => !wanted.Any(w => w.Id == f.Id));
        for (var position = 0; position < wanted.Count; position++)
        {
            var item = wanted[position];
            var feature = category.Features.FirstOrDefault(f => f.Id == item.Id);
            if (feature is null)
            {
                feature = new CourtFeature { CategoryId = category.Id, NameEn = "", NameAr = "" };
                category.Features.Add(feature);
            }
            feature.NameEn = item.NameEn!.Trim();
            feature.NameAr = item.NameAr!.Trim();
            feature.SortOrder = position;
        }
    }
}
