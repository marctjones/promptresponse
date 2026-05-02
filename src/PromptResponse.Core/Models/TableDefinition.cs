namespace PromptResponse.Core.Models;

/// <summary>
/// Layout metadata for a table section: columns and whether rows are fixed or dynamic.
/// </summary>
/// <remarks>
/// Attached to <see cref="Section.TableLayout"/>. The section's child sections are
/// the rows; each row section's prompts are the cells (one per column, with
/// <c>id = "{rowId}.{columnId}"</c>). Tables can be either fixed (predefined rows
/// like "Last 3 years of tax data") or dynamic (user can add/remove rows like
/// "Order line items"). Use either <see cref="FixedRows"/> OR <see cref="DynamicRows"/>,
/// not both.
/// </remarks>
public class TableDefinition
{
    /// <summary>
    /// Gets or sets the column definitions for the table.
    /// </summary>
    public List<TableColumn> Columns { get; set; } = new();

    /// <summary>
    /// Gets or sets the fixed row definitions for tables with predetermined rows.
    /// </summary>
    /// <remarks>
    /// Use this for tables where rows are known in advance (e.g., years, categories).
    /// When FixedRows is set, the row sub-sections are materialized from this list
    /// and users cannot add or remove rows.
    /// </remarks>
    /// <example>
    /// Fixed rows for tax years: [{ id: "year_2024", label: "2024" }, { id: "year_2023", label: "2023" }]
    /// </example>
    public List<FixedRow>? FixedRows { get; set; }

    /// <summary>
    /// Gets or sets the dynamic row configuration for tables where users can add/remove rows.
    /// </summary>
    /// <remarks>
    /// Use this for variable-length lists (e.g., line items, addresses). The actual
    /// row sub-sections live in the document (whatever the user added); MinRows/MaxRows
    /// are advisory bounds on the add/remove commands.
    /// </remarks>
    public DynamicRowConfig? DynamicRows { get; set; }

    /// <summary>
    /// Gets whether this table has fixed rows.
    /// </summary>
    public bool IsFixedTable => FixedRows != null && FixedRows.Count > 0;

    /// <summary>
    /// Gets whether this table has dynamic rows.
    /// </summary>
    public bool IsDynamicTable => DynamicRows != null;
}

/// <summary>
/// Defines a column in a table.
/// </summary>
public class TableColumn
{
    /// <summary>
    /// Gets or sets the unique identifier for this column.
    /// </summary>
    /// <remarks>
    /// Used as the suffix in cell prompt ids: a cell at row "q1", column "revenue"
    /// has prompt id "q1.revenue".
    /// </remarks>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display label for the column header.
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the expected data type for cells in this column.
    /// </summary>
    /// <remarks>
    /// Common values: "text", "number", "currency", "date", "boolean".
    /// Determines what input control the UI shows for cells.
    /// </remarks>
    public string Type { get; set; } = "text";

    /// <summary>
    /// Gets or sets placeholder text for cells in this column.
    /// </summary>
    public string? Placeholder { get; set; }

    /// <summary>
    /// Gets or sets suggested values for cells in this column.
    /// </summary>
    /// <remarks>
    /// Useful for columns with a limited set of valid options.
    /// </remarks>
    public List<string>? SuggestedValues { get; set; }

    /// <summary>
    /// Gets or sets help text for this column.
    /// </summary>
    public string? HelpText { get; set; }
}

/// <summary>
/// Defines a fixed row in a table with predetermined rows.
/// </summary>
public class FixedRow
{
    /// <summary>
    /// Gets or sets the unique identifier for this row.
    /// </summary>
    /// <remarks>
    /// Used as the row sub-Section's id and as the prefix in cell prompt ids
    /// (e.g. "q1.revenue").
    /// </remarks>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display label for the row header.
    /// </summary>
    /// <example>
    /// "2024", "Q1", "January", "Primary Address"
    /// </example>
    public string Label { get; set; } = string.Empty;
}

/// <summary>
/// Configuration for tables with dynamic (user-controlled) row count.
/// </summary>
public class DynamicRowConfig
{
    /// <summary>
    /// Gets or sets the minimum number of rows required.
    /// </summary>
    /// <remarks>
    /// Default is 0 (no minimum). Set to 1 to require at least one row.
    /// </remarks>
    public int MinRows { get; set; } = 0;

    /// <summary>
    /// Gets or sets the maximum number of rows allowed.
    /// </summary>
    /// <remarks>
    /// Default is 100. Set to limit data entry or prevent performance issues.
    /// </remarks>
    public int MaxRows { get; set; } = 100;

    /// <summary>
    /// Gets or sets the label prefix for auto-generated row labels.
    /// </summary>
    /// <remarks>
    /// Used to generate row labels like "Item 1", "Item 2", etc.
    /// </remarks>
    /// <example>
    /// "Item", "Entry", "Row", "Line Item"
    /// </example>
    public string RowLabel { get; set; } = "Row";
}
