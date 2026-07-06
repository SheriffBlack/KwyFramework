using System.Collections.ObjectModel;
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
            .Select(metadata => new DynamicPropertyItem(source, metadata))
            .ToList();

        foreach (var group in properties.GroupBy(item => item.GroupName))
        {
            var groupModel = new PropertyGroupModel
            {
                GroupName = group.Key,
                WidthRatio = group.Select(item => item.GroupWidth).FirstOrDefault(width => width is > 0) ?? 1.0
            };

            foreach (var item in group)
            {
                groupModel.Properties.Add(item);
            }

            groups.Add(groupModel);
        }

        return groups;
    }
}
