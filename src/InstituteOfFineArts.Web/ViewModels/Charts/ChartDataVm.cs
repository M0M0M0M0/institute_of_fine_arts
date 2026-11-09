namespace InstituteOfFineArts.Web.ViewModels.Charts;

/// <summary>
/// Chart data in the shape Chart.js expects (F-M15-01): <c>{ labels: [...], datasets: [{ label, data: [...] }] }</c>.
/// Serialized as camelCase JSON by /charts/*; the FE draws the chart.
/// </summary>
public class ChartDataVm
{
    /// <summary>X axis / slice labels.</summary>
    public List<string> Labels { get; set; } = new();

    /// <summary>Series; each has one value per label.</summary>
    public List<ChartDatasetVm> Datasets { get; set; } = new();
}

/// <summary>One series of a chart.</summary>
public class ChartDatasetVm
{
    /// <summary>Series name shown in the legend.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>One value per label of the chart.</summary>
    public List<decimal> Data { get; set; } = new();
}
