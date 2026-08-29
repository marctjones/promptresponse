from promptresponse import recompute_computed_values
from promptresponse.models import AprDocument, Prompt, PromptHints, Section


def test_cel_binding_computes_typed_value_without_overwriting_correction():
    total = Prompt(id="total", label="Total", hints=PromptHints(expected_data_type="currency", expr_value="qty * price"))
    document = AprDocument(sections=[Section(id="s", title="S", prompts=[
        Prompt(id="qty", label="Quantity", response="3", hints=PromptHints(expected_data_type="number")),
        Prompt(id="price", label="Price", response="12.5", hints=PromptHints(expected_data_type="currency")),
        total,
    ])])
    assert recompute_computed_values(document)
    assert total.response == "37.5"
    total.response = "40"
    total.response_metadata.source = None
    assert not recompute_computed_values(document)
    assert total.response == "40"
