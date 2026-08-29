"""APR's optional CEL expression profile.

The stored APR value remains a string. This module performs the format-specific
binding to CEL and never treats an expression failure as a document failure.
"""

from datetime import datetime, timezone
from typing import Mapping, Optional

from celpy import Environment, celtypes

from .models import AprDocument, Prompt

PROFILE = "core+expressions"
COMPUTED_SOURCE = "computed"


def _cel_type(expected: Optional[str]):
    return {
        "number": celtypes.DoubleType,
        "currency": celtypes.DoubleType,
        "range": celtypes.DoubleType,
        "boolean": celtypes.BoolType,
        "date": celtypes.TimestampType,
        "time": celtypes.TimestampType,
        "datetime": celtypes.TimestampType,
        "multichoice": celtypes.ListType,
    }.get((expected or "").lower(), celtypes.StringType)


def _bind(response: str, expected: Optional[str]):
    kind = (expected or "").lower()
    value = response or ""
    if kind in {"number", "currency", "range"}:
        if not value.strip():
            return None
        try:
            return float(value.strip())
        except ValueError:
            return None
    if kind == "boolean":
        normalized = value.strip().lower()
        if normalized in {"true", "yes", "y", "1", "on", "x", "checked"}:
            return True
        if normalized in {"false", "no", "n", "0", "off", "unchecked"}:
            return False
        return None
    if kind in {"date", "time", "datetime"}:
        if not value.strip():
            return None
        try:
            source = value.strip()
            if kind == "date":
                source += "T00:00:00+00:00"
            elif kind == "time":
                source = "1970-01-01T" + source + "+00:00"
            elif source.endswith("Z"):
                source = source[:-1] + "+00:00"
            return datetime.fromisoformat(source).astimezone(timezone.utc)
        except ValueError:
            return None
    if kind == "multichoice":
        return [part.strip() for part in (value.split("\n") if "\n" in value else value.split(",")) if part.strip()]
    return value


def _stored(value) -> str:
    if isinstance(value, bool):
        return "true" if value else "false"
    if isinstance(value, float):
        return format(value, ".15g")
    if isinstance(value, datetime):
        return value.astimezone(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
    if isinstance(value, (list, tuple)):
        return "\n".join(str(item) for item in value)
    return "" if value is None else str(value)


class ExpressionContext:
    def __init__(self, document: AprDocument, today: Optional[str] = None, ctx: Optional[Mapping[str, str]] = None):
        self.document = document
        self.prompts = {prompt.id: prompt for prompt in document.all_prompts() if prompt.id}
        self.bindings = {}
        self.types = {}
        for prompt_id, prompt in self.prompts.items():
            expected = prompt.hints.expected_data_type
            self.types[prompt_id] = _cel_type(expected)
            bound = _bind(prompt.response, expected)
            if bound is not None:
                self.bindings[prompt_id] = bound
        self.types["_today"] = celtypes.TimestampType
        parsed_today = _bind(today or datetime.now(timezone.utc).isoformat(), "datetime")
        if parsed_today is not None:
            self.bindings["_today"] = parsed_today
        self.types["ctx"] = celtypes.MapType
        self.bindings["ctx"] = dict(ctx or {})

    def evaluate(self, prompt: Prompt, expression: str):
        annotations = dict(self.types)
        annotations["_this"] = _cel_type(prompt.hints.expected_data_type)
        current = _bind(prompt.response, prompt.hints.expected_data_type)
        bindings = dict(self.bindings)
        if current is not None:
            bindings["_this"] = current
        try:
            environment = Environment(annotations=annotations)
            return environment.program(environment.compile(expression)).evaluate(bindings)
        except Exception:
            return None


def build_expression_context(document: AprDocument, today: Optional[str] = None, ctx: Optional[Mapping[str, str]] = None) -> ExpressionContext:
    return ExpressionContext(document, today, ctx)


def compute_value(prompt: Prompt, context: ExpressionContext) -> Optional[str]:
    expression = prompt.hints.expr_value
    if not expression or not expression.strip():
        return None
    result = context.evaluate(prompt, expression)
    return None if result is None else _stored(result)


def condition(prompt: Prompt, expression: Optional[str], context: ExpressionContext) -> bool:
    return bool(context.evaluate(prompt, expression)) if expression and expression.strip() else False


def validation_message(prompt: Prompt, context: ExpressionContext) -> Optional[str]:
    expression = prompt.hints.expr_validation
    if not expression or not expression.strip():
        return None
    result = context.evaluate(prompt, expression)
    message = None if result is None else _stored(result)
    return message or None


def recompute_computed_values(document: AprDocument, today: Optional[str] = None, ctx: Optional[Mapping[str, str]] = None) -> bool:
    changed = False
    for _ in range(5):
        context = build_expression_context(document, today, ctx)
        changed_this_pass = False
        for prompt in document.all_prompts():
            if not prompt.hints.expr_value:
                continue
            authored = bool(prompt.response) and prompt.response_metadata.source != COMPUTED_SOURCE
            if authored:
                continue
            computed = compute_value(prompt, context)
            if computed is not None and computed != prompt.response:
                prompt.response = computed
                prompt.response_metadata.source = COMPUTED_SOURCE
                changed_this_pass = changed = True
        if not changed_this_pass:
            break
    return changed
