import json

import promptresponse as pr
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


def _activation_document():
    return pr.loads(json.dumps({
        "version": "1.0-beta.6",
        "metadata": {"title": "T"},
        "sections": [{"id": "s", "title": "S", "prompts": [
            {"id": "echo_id", "label": "E", "response": "", "hints": {"exprValue": "_id"}},
            {"id": "echo_today", "label": "T", "response": "", "hints": {"exprValue": "_today"}},
            {"id": "echo_ctx", "label": "C", "response": "", "hints": {"exprValue": "ctx['team']"}},
            {"id": "echo_this", "label": "S", "response": "seed", "hints": {"exprValue": "_this"}},
        ]}],
    }))


def test_activation_binds_every_name_the_specification_defines():
    document = _activation_document()
    context = pr.build_expression_context(document, "2026-09-01T12:00:00Z", {"team": "records"})
    values = {p.id: pr.compute_value(p, context) for p in document.all_prompts()}
    assert values["echo_id"] == "echo_id"
    assert values["echo_today"] == "2026-09-01"
    assert values["echo_ctx"] == "records"
    assert values["echo_this"] == "seed"


def test_temporal_names_are_unbound_when_the_caller_supplies_nothing():
    """Reading the host clock would make the same inputs evaluate differently twice."""
    document = _activation_document()
    context = pr.build_expression_context(document)
    prompt = next(p for p in document.all_prompts() if p.id == "echo_today")
    assert pr.compute_value(prompt, context) is None
