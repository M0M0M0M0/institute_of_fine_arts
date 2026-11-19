namespace InstituteOfFineArts.Web.ViewModels.Common;

/// <summary>
/// One card of the "Needs attention" strip at the top of the Manager and Staff dashboards: a count of work waiting,
/// linking straight to the list filtered on exactly those rows. Cards with a count of 0 are not shown.
/// </summary>
/// <param name="Count">How many rows wait</param>
/// <param name="Title">What waits, e.g. "entries to grade" (shown after the number)</param>
/// <param name="Text">One short line of context</param>
/// <param name="Url">Filtered list that shows the same rows</param>
/// <param name="Icon">Bootstrap Icons class</param>
public record AttentionItemVm(int Count, string Title, string Text, string Url, string Icon);
