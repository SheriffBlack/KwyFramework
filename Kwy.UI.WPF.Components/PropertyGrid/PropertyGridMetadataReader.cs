using System.Collections.ObjectModel;
using System.Reflection;
using Kwy.ComponentModel;

namespace Kwy.UI.WPF.Components.PropertyGrid;

internal static class PropertyGridMetadataReader
{
    public static ObservableCollection<PropertyGroupModel> CreateGroups(object? source)
    {
        var groups = new ObservableCollection<PropertyGroupModel>();
        if (source == null)
        {
            return groups;
        }

        var properties = PropertyMetadataReader.GetProperties(source)
            .Where(metadata => metadata.IsBrowsable)
            .Select(metadata => new
            {
                Metadata = metadata,
                Category = ResolveCategory(source, metadata),
                CategoryKey = ResolveCategoryKey(source, metadata),
                Item = new DynamicPropertyItem(source, metadata)
            })
            .ToList();

        foreach (var group in properties.GroupBy(item => new { item.Category, item.CategoryKey, item.Metadata.HasCategory }))
        {
            var normalGroupItems = new List<DynamicPropertyItem>();

            foreach (var item in group)
            {
                if (!string.IsNullOrWhiteSpace(item.Metadata.InlineGroup))
                {
                    if (normalGroupItems.Count > 0)
                    {
                        AddPropertyGroup(groups, group.Key.Category, group.Key.CategoryKey, group.Key.HasCategory, normalGroupItems);
                        normalGroupItems.Clear();
                    }

                    AddSinglePropertyGroup(groups, group.Key.Category, group.Key.CategoryKey, group.Key.HasCategory, item.Item);
                    continue;
                }

                normalGroupItems.Add(item.Item);
            }

            if (normalGroupItems.Count > 0)
            {
                AddPropertyGroup(groups, group.Key.Category, group.Key.CategoryKey, group.Key.HasCategory, normalGroupItems);
            }
        }

        return groups;
    }

    private static string ResolveCategory(object source, PropertyMetadataItem metadata)
    {
        CategorySourceAttribute? attribute = metadata.Property.GetCustomAttribute<CategorySourceAttribute>();
        if (attribute == null)
        {
            return metadata.Category;
        }

        PropertyInfo? categoryProperty = source.GetType().GetProperty(attribute.PropertyName, BindingFlags.Instance | BindingFlags.Public);
        object? value = categoryProperty is { CanRead: true } && categoryProperty.GetIndexParameters().Length == 0
            ? categoryProperty.GetValue(source)
            : null;
        string? category = value?.ToString();
        return string.IsNullOrWhiteSpace(category) ? metadata.Category : category;
    }

    private static string? ResolveCategoryKey(object source, PropertyMetadataItem metadata)
    {
        CategorySourceAttribute? attribute = metadata.Property.GetCustomAttribute<CategorySourceAttribute>();
        return attribute == null ? metadata.CategoryKey : null;
    }

    private static void AddSinglePropertyGroup(ObservableCollection<PropertyGroupModel> groups, string groupName, string? groupNameKey, bool hasGroupHeader, DynamicPropertyItem item)
    {
        AddPropertyGroup(groups, groupName, groupNameKey, hasGroupHeader, new[] { item });
    }

    private static void AddPropertyGroup(ObservableCollection<PropertyGroupModel> groups, string groupName, string? groupNameKey, bool hasGroupHeader, IReadOnlyList<DynamicPropertyItem> items)
    {
        var groupModel = new PropertyGroupModel
        {
            GroupName = groupName,
            GroupNameKey = groupNameKey,
            HasGroupHeader = hasGroupHeader && items.Count > 1,
            WidthRatio = items.Select(item => item.GroupWidth).FirstOrDefault(width => width is > 0) ?? 1.0
        };

        foreach (DynamicPropertyItem item in items)
        {
            groupModel.Properties.Add(item);
        }

        groups.Add(groupModel);
    }
}