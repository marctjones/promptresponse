using FluentAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Desktop.ViewModels;
using Xunit;

namespace PromptResponse.Desktop.Tests.ViewModels;

/// <summary>
/// Unit tests for PromptViewModel smart controls and behavior.
/// </summary>
public class PromptViewModelTests
{
    #region Smart Control Detection Tests

    [Fact]
    public void HasSmartControl_WithNoHints_ShouldBeFalse()
    {
        // Arrange
        var prompt = new Prompt { Id = "test", Label = "Test" };
        var viewModel = new PromptViewModel(prompt);

        // Assert
        viewModel.HasSmartControl.Should().BeFalse();
    }

    [Theory]
    [InlineData("phone")]
    [InlineData("ssn")]
    [InlineData("ein")]
    [InlineData("creditcard")]
    [InlineData("zipcode")]
    [InlineData("PHONE")]  // Case insensitive
    [InlineData("SSN")]
    public void IsFormattedField_WithFormattedDataType_ShouldBeTrue(string dataType)
    {
        // Arrange
        var prompt = CreatePromptWithDataType(dataType);
        var viewModel = new PromptViewModel(prompt);

        // Assert
        viewModel.IsFormattedField.Should().BeTrue($"'{dataType}' should be a formatted field");
        viewModel.HasSmartControl.Should().BeTrue();
    }

    [Theory]
    [InlineData("text")]
    [InlineData("number")]
    [InlineData("email")]
    [InlineData("url")]
    [InlineData(null)]
    public void IsFormattedField_WithNonFormattedDataType_ShouldBeFalse(string? dataType)
    {
        // Arrange
        var prompt = CreatePromptWithDataType(dataType);
        var viewModel = new PromptViewModel(prompt);

        // Assert
        viewModel.IsFormattedField.Should().BeFalse();
    }

    [Fact]
    public void IsDateField_WithDateType_ShouldBeTrue()
    {
        // Arrange
        var prompt = CreatePromptWithDataType("date");
        var viewModel = new PromptViewModel(prompt);

        // Assert
        viewModel.IsDateField.Should().BeTrue();
        viewModel.HasSmartControl.Should().BeTrue();
    }

    [Fact]
    public void IsDateField_WithDateTypeAndSuggestedValues_ShouldBeFalse()
    {
        // Arrange - date field with suggested values should use selection control instead
        var prompt = CreatePromptWithDataType("date");
        prompt.Hints.SuggestedValues = new List<string> { "2024-01-01", "2024-06-01" };
        var viewModel = new PromptViewModel(prompt);

        // Assert
        viewModel.IsDateField.Should().BeFalse("date field with suggested values should not show date picker");
        viewModel.UseRadioButtons.Should().BeTrue("should use radio buttons instead");
    }

    [Fact]
    public void IsBooleanField_WithBooleanType_ShouldBeTrue()
    {
        // Arrange
        var prompt = CreatePromptWithDataType("boolean");
        var viewModel = new PromptViewModel(prompt);

        // Assert
        viewModel.IsBooleanField.Should().BeTrue();
        viewModel.HasSmartControl.Should().BeTrue();
    }

    [Fact]
    public void IsBooleanField_WithBooleanTypeAndSuggestedValues_ShouldBeFalse()
    {
        // Arrange
        var prompt = CreatePromptWithDataType("boolean");
        prompt.Hints.SuggestedValues = new List<string> { "Yes", "No", "N/A" };
        var viewModel = new PromptViewModel(prompt);

        // Assert
        viewModel.IsBooleanField.Should().BeFalse("boolean field with suggested values should use selection control");
    }

    [Fact]
    public void IsTableField_WithTableType_ShouldBeTrue()
    {
        // Arrange
        var prompt = CreatePromptWithDataType("table");
        var viewModel = new PromptViewModel(prompt);

        // Assert
        viewModel.IsTableField.Should().BeTrue();
        viewModel.HasSmartControl.Should().BeTrue();
    }

    #endregion

    #region Radio Buttons vs Dropdown Tests

    [Theory]
    [InlineData(2, true, false)]   // 2 options -> radio buttons
    [InlineData(3, true, false)]   // 3 options -> radio buttons
    [InlineData(4, true, false)]   // 4 options -> radio buttons
    [InlineData(5, true, false)]   // 5 options -> radio buttons
    [InlineData(6, false, true)]   // 6 options -> dropdown
    [InlineData(10, false, true)]  // 10 options -> dropdown
    public void SelectionControl_ShouldUseDifferentControlsBasedOnOptionCount(
        int optionCount, bool expectRadioButtons, bool expectDropdown)
    {
        // Arrange
        var options = Enumerable.Range(1, optionCount).Select(i => $"Option {i}").ToList();
        var prompt = new Prompt
        {
            Id = "test",
            Label = "Test",
            Hints = new PromptHints { SuggestedValues = options }
        };
        var viewModel = new PromptViewModel(prompt);

        // Assert
        viewModel.UseRadioButtons.Should().Be(expectRadioButtons,
            $"{optionCount} options should {(expectRadioButtons ? "" : "not ")}use radio buttons");
        viewModel.UseDropdown.Should().Be(expectDropdown,
            $"{optionCount} options should {(expectDropdown ? "" : "not ")}use dropdown");
        viewModel.HasSmartControl.Should().BeTrue();
    }

    [Fact]
    public void SelectionControl_WithOnlyOneOption_ShouldNotShowSelection()
    {
        // Arrange
        var prompt = new Prompt
        {
            Id = "test",
            Label = "Test",
            Hints = new PromptHints { SuggestedValues = new List<string> { "Only Option" } }
        };
        var viewModel = new PromptViewModel(prompt);

        // Assert
        viewModel.UseRadioButtons.Should().BeFalse("1 option is not enough for radio buttons");
        viewModel.UseDropdown.Should().BeFalse("1 option is not enough for dropdown");
    }

    [Fact]
    public void SelectionControl_WithFormattedType_ShouldNotShowSelection()
    {
        // Arrange - phone field with suggested values should still use formatter
        var prompt = CreatePromptWithDataType("phone");
        prompt.Hints.SuggestedValues = new List<string> { "(555) 123-4567", "(555) 987-6543" };
        var viewModel = new PromptViewModel(prompt);

        // Assert
        viewModel.IsFormattedField.Should().BeTrue();
        viewModel.UseRadioButtons.Should().BeFalse("formatted field should not use radio buttons");
        viewModel.UseDropdown.Should().BeFalse("formatted field should not use dropdown");
    }

    #endregion

    #region Smart Control Toggle Tests

    [Fact]
    public void UseSmartControl_DefaultsToTrue()
    {
        // Arrange
        var prompt = CreatePromptWithDataType("date");
        var viewModel = new PromptViewModel(prompt);

        // Assert
        viewModel.UseSmartControl.Should().BeTrue();
    }

    [Fact]
    public void ShowSmartControl_WhenUseSmartControlIsTrue_ShouldBeTrue()
    {
        // Arrange
        var prompt = CreatePromptWithDataType("date");
        var viewModel = new PromptViewModel(prompt);

        // Assert
        viewModel.ShowSmartControl.Should().BeTrue();
        viewModel.ShowPlainTextBox.Should().BeFalse();
    }

    [Fact]
    public void ShowSmartControl_WhenUseSmartControlIsFalse_ShouldBeFalse()
    {
        // Arrange
        var prompt = CreatePromptWithDataType("date");
        var viewModel = new PromptViewModel(prompt);

        // Act
        viewModel.UseSmartControl = false;

        // Assert
        viewModel.ShowSmartControl.Should().BeFalse();
        viewModel.ShowPlainTextBox.Should().BeTrue();
    }

    [Fact]
    public void ShowPlainTextBox_WhenNoSmartControl_ShouldBeTrue()
    {
        // Arrange
        var prompt = new Prompt { Id = "test", Label = "Test" };
        var viewModel = new PromptViewModel(prompt);

        // Assert
        viewModel.HasSmartControl.Should().BeFalse();
        viewModel.ShowPlainTextBox.Should().BeTrue();
    }

    [Fact]
    public void Toggle_ShouldRaisePropertyChanged()
    {
        // Arrange
        var prompt = CreatePromptWithDataType("date");
        var viewModel = new PromptViewModel(prompt);
        var changedProperties = new List<string>();
        viewModel.PropertyChanged += (s, e) => changedProperties.Add(e.PropertyName!);

        // Act
        viewModel.UseSmartControl = false;

        // Assert
        changedProperties.Should().Contain("UseSmartControl");
        changedProperties.Should().Contain("ShowSmartControl");
        changedProperties.Should().Contain("ShowPlainTextBox");
    }

    [Fact]
    public void Toggle_WhenToggledOff_ShouldAllowFreeTextEntry()
    {
        // Arrange - create a choice field with radio buttons
        var prompt = new Prompt
        {
            Id = "test",
            Label = "Test",
            Hints = new PromptHints { SuggestedValues = new List<string> { "A", "B", "C" } }
        };
        var viewModel = new PromptViewModel(prompt);

        // Verify smart control is active
        viewModel.UseRadioButtons.Should().BeTrue();
        viewModel.ShowSmartControl.Should().BeTrue();

        // Act - toggle off
        viewModel.UseSmartControl = false;

        // Assert - plain text box should be visible
        viewModel.ShowSmartControl.Should().BeFalse();
        viewModel.ShowPlainTextBox.Should().BeTrue();

        // User should be able to set any response
        viewModel.Response = "Custom value not in list";
        viewModel.Response.Should().Be("Custom value not in list");
    }

    [Fact]
    public void Toggle_ForFormattedField_ShouldAllowUnformattedEntry()
    {
        // Arrange
        var prompt = CreatePromptWithDataType("phone");
        var viewModel = new PromptViewModel(prompt);

        // Verify formatted field is active
        viewModel.IsFormattedField.Should().BeTrue();
        viewModel.ShowSmartControl.Should().BeTrue();

        // Act - toggle off
        viewModel.UseSmartControl = false;

        // Assert - plain text box should be visible
        viewModel.ShowSmartControl.Should().BeFalse();
        viewModel.ShowPlainTextBox.Should().BeTrue();

        // User should be able to set unformatted value
        viewModel.Response = "call me at five five five";
        viewModel.Response.Should().Be("call me at five five five");
    }

    #endregion

    #region Response Tests

    [Fact]
    public void Response_ShouldUpdateModel()
    {
        // Arrange
        var prompt = new Prompt { Id = "test", Label = "Test" };
        var viewModel = new PromptViewModel(prompt);

        // Act
        viewModel.Response = "New value";

        // Assert
        viewModel.Response.Should().Be("New value");
        prompt.Response.Should().Be("New value");
    }

    [Fact]
    public void Response_ShouldRaisePropertyChanged()
    {
        // Arrange
        var prompt = new Prompt { Id = "test", Label = "Test" };
        var viewModel = new PromptViewModel(prompt);
        var raised = false;
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == "Response") raised = true;
        };

        // Act
        viewModel.Response = "New value";

        // Assert
        raised.Should().BeTrue();
    }

    [Fact]
    public void Response_WhenSameValue_ShouldNotRaisePropertyChanged()
    {
        // Arrange
        var prompt = new Prompt { Id = "test", Label = "Test", Response = "Existing" };
        var viewModel = new PromptViewModel(prompt);
        var raised = false;
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == "Response") raised = true;
        };

        // Act
        viewModel.Response = "Existing";

        // Assert
        raised.Should().BeFalse();
    }

    #endregion

    #region Table Tests - Fixed Tables

    [Fact]
    public void FixedTable_ShouldInitializeRowsFromDefinition()
    {
        // Arrange
        var prompt = CreateFixedTablePrompt();
        var viewModel = new PromptViewModel(prompt);

        // Act
        var rows = viewModel.TableRows;

        // Assert
        rows.Should().HaveCount(3, "should have 3 fixed rows");
        rows[0].Label.Should().Be("2023");
        rows[1].Label.Should().Be("2022");
        rows[2].Label.Should().Be("2021");
    }

    [Fact]
    public void FixedTable_ShouldInitializeCellsFromColumns()
    {
        // Arrange
        var prompt = CreateFixedTablePrompt();
        var viewModel = new PromptViewModel(prompt);

        // Act
        var rows = viewModel.TableRows;

        // Assert
        rows[0].Cells.Should().HaveCount(2, "should have 2 columns");
        rows[0].Cells[0].ColumnLabel.Should().Be("Revenue");
        rows[0].Cells[1].ColumnLabel.Should().Be("Expenses");
    }

    [Fact]
    public void FixedTable_CellEdit_ShouldUpdateResponse()
    {
        // Arrange
        var prompt = CreateFixedTablePrompt();
        var viewModel = new PromptViewModel(prompt);
        var rows = viewModel.TableRows;

        // Act
        rows[0].Cells[0].Value = "100000";
        rows[0].Cells[1].Value = "50000";

        // Assert - Response should contain JSON
        viewModel.Response.Should().Contain("year_2023");
        viewModel.Response.Should().Contain("revenue");
        viewModel.Response.Should().Contain("100000");
    }

    [Fact]
    public void FixedTable_ShouldParseExistingResponse()
    {
        // Arrange
        var prompt = CreateFixedTablePrompt();
        prompt.Response = @"{""year_2023"":{""revenue"":""500000"",""expenses"":""250000""}}";
        var viewModel = new PromptViewModel(prompt);

        // Act
        var rows = viewModel.TableRows;

        // Assert
        rows[0].Cells[0].Value.Should().Be("500000");
        rows[0].Cells[1].Value.Should().Be("250000");
    }

    [Fact]
    public void FixedTable_CanAddRow_ShouldBeFalse()
    {
        // Arrange
        var prompt = CreateFixedTablePrompt();
        var viewModel = new PromptViewModel(prompt);

        // Assert - Fixed tables can't add rows
        viewModel.CanAddRow.Should().BeFalse();
        viewModel.CanRemoveRow.Should().BeFalse();
    }

    #endregion

    #region Table Tests - Dynamic Tables

    [Fact]
    public void DynamicTable_ShouldInitializeWithMinimumRows()
    {
        // Arrange
        var prompt = CreateDynamicTablePrompt(minRows: 2);
        var viewModel = new PromptViewModel(prompt);

        // Act
        var rows = viewModel.TableRows;

        // Assert
        rows.Should().HaveCount(2, "should initialize with minimum 2 rows");
    }

    [Fact]
    public void DynamicTable_AddRow_ShouldAddNewRow()
    {
        // Arrange
        var prompt = CreateDynamicTablePrompt(minRows: 1, maxRows: 5);
        var viewModel = new PromptViewModel(prompt);
        // Access TableRows first to initialize - this triggers InitializeTableData
        var rows = viewModel.TableRows;
        var initialCount = rows.Count;

        // Act
        viewModel.AddRow();

        // Assert
        viewModel.TableRows.Should().HaveCount(initialCount + 1);
    }

    [Fact]
    public void DynamicTable_AddRow_ShouldNotExceedMaxRows()
    {
        // Arrange
        var prompt = CreateDynamicTablePrompt(minRows: 1, maxRows: 2);
        var viewModel = new PromptViewModel(prompt);
        // Access TableRows first to initialize
        _ = viewModel.TableRows;

        // Add rows to reach max
        while (viewModel.CanAddRow)
        {
            viewModel.AddRow();
        }

        // Assert
        viewModel.TableRows.Should().HaveCount(2);
        viewModel.CanAddRow.Should().BeFalse();
    }

    [Fact]
    public void DynamicTable_RemoveRow_ShouldRemoveLastRow()
    {
        // Arrange
        var prompt = CreateDynamicTablePrompt(minRows: 1, maxRows: 5);
        var viewModel = new PromptViewModel(prompt);
        // Access TableRows first to initialize
        _ = viewModel.TableRows;
        viewModel.AddRow();
        viewModel.AddRow();
        var countBeforeRemove = viewModel.TableRows.Count;

        // Act
        viewModel.RemoveRow();

        // Assert
        viewModel.TableRows.Should().HaveCount(countBeforeRemove - 1);
    }

    [Fact]
    public void DynamicTable_RemoveRow_ShouldNotGoBelowMinRows()
    {
        // Arrange
        var prompt = CreateDynamicTablePrompt(minRows: 2, maxRows: 5);
        var viewModel = new PromptViewModel(prompt);
        // Access TableRows first to initialize
        _ = viewModel.TableRows;

        // Remove until we hit minimum
        while (viewModel.CanRemoveRow)
        {
            viewModel.RemoveRow();
        }

        // Assert
        viewModel.TableRows.Should().HaveCount(2);
        viewModel.CanRemoveRow.Should().BeFalse();
    }

    [Fact]
    public void DynamicTable_ShouldSerializeAsArray()
    {
        // Arrange
        var prompt = CreateDynamicTablePrompt(minRows: 1, maxRows: 5);
        var viewModel = new PromptViewModel(prompt);

        // Edit a cell
        viewModel.TableRows[0].Cells[0].Value = "Test Value";

        // Assert - Response should be JSON array
        viewModel.Response.Should().StartWith("[");
        viewModel.Response.Should().EndWith("]");
        viewModel.Response.Should().Contain("Test Value");
    }

    [Fact]
    public void DynamicTable_ShouldParseExistingArrayResponse()
    {
        // Arrange
        var prompt = CreateDynamicTablePrompt(minRows: 1, maxRows: 10);
        prompt.Response = @"[{""name"":""John""},{""name"":""Jane""},{""name"":""Bob""}]";
        var viewModel = new PromptViewModel(prompt);

        // Assert - should have 3 rows
        viewModel.TableRows.Should().HaveCount(3);
        viewModel.TableRows[0].Cells[0].Value.Should().Be("John");
        viewModel.TableRows[1].Cells[0].Value.Should().Be("Jane");
        viewModel.TableRows[2].Cells[0].Value.Should().Be("Bob");
    }

    [Fact]
    public void DynamicTable_AddRow_ShouldRaisePropertyChanged()
    {
        // Arrange
        var prompt = CreateDynamicTablePrompt(minRows: 1, maxRows: 5);
        var viewModel = new PromptViewModel(prompt);
        // Access TableRows first to initialize
        _ = viewModel.TableRows;

        var changedProperties = new List<string>();
        viewModel.PropertyChanged += (s, e) => changedProperties.Add(e.PropertyName!);

        // Act
        viewModel.AddRow();

        // Assert
        changedProperties.Should().Contain("TableRows");
        changedProperties.Should().Contain("CanAddRow");
        changedProperties.Should().Contain("CanRemoveRow");
    }

    #endregion

    #region Multiline Tests

    [Fact]
    public void UseMultilineTextBox_WithMultilineType_ShouldBeTrue()
    {
        // Arrange
        var prompt = CreatePromptWithDataType("multiline");
        var viewModel = new PromptViewModel(prompt);

        // Assert
        viewModel.UseMultilineTextBox.Should().BeTrue();
    }

    [Fact]
    public void UseMultilineTextBox_WithTextType_ShouldBeFalse()
    {
        // Arrange
        var prompt = CreatePromptWithDataType("text");
        var viewModel = new PromptViewModel(prompt);

        // Assert
        viewModel.UseMultilineTextBox.Should().BeFalse();
    }

    #endregion

    #region Helper Methods

    private static Prompt CreatePromptWithDataType(string? dataType)
    {
        return new Prompt
        {
            Id = "test",
            Label = "Test",
            Hints = new PromptHints { ExpectedDataType = dataType }
        };
    }

    private static Prompt CreateFixedTablePrompt()
    {
        return new Prompt
        {
            Id = "test_table",
            Label = "Financial Summary",
            Hints = new PromptHints
            {
                ExpectedDataType = "table",
                TableDefinition = new TableDefinition
                {
                    Columns = new List<TableColumn>
                    {
                        new TableColumn { Id = "revenue", Label = "Revenue" },
                        new TableColumn { Id = "expenses", Label = "Expenses" }
                    },
                    FixedRows = new List<FixedRow>
                    {
                        new FixedRow { Id = "year_2023", Label = "2023" },
                        new FixedRow { Id = "year_2022", Label = "2022" },
                        new FixedRow { Id = "year_2021", Label = "2021" }
                    }
                }
            }
        };
    }

    private static Prompt CreateDynamicTablePrompt(int minRows = 1, int maxRows = 10)
    {
        return new Prompt
        {
            Id = "test_dynamic_table",
            Label = "Employees",
            Hints = new PromptHints
            {
                ExpectedDataType = "table",
                TableDefinition = new TableDefinition
                {
                    Columns = new List<TableColumn>
                    {
                        new TableColumn { Id = "name", Label = "Name" }
                    },
                    DynamicRows = new DynamicRowConfig
                    {
                        MinRows = minRows,
                        MaxRows = maxRows,
                        RowLabel = "Employee"
                    }
                }
            }
        };
    }

    #endregion
}
