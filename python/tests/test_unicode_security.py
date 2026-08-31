import promptresponse as pr


def test_inspector_reports_visually_deceptive_characters_without_changing_text():
    value = "safe\u202etxt.exe\u0007"
    findings = pr.inspect_text(value)

    assert [finding.code for finding in findings] == ["BIDI_OVERRIDE", "CONTROL_CHARACTER"]
    assert value == "safe\u202etxt.exe\u0007"


def test_unicode_warning_does_not_make_a_document_invalid():
    document = pr.loads(
        '{"version":"1.0-beta.6","metadata":{"title":"T"},"sections":'
        '[{"id":"s","title":"S","prompts":'
        '[{"id":"p","label":"L","response":"a\\u2066b"}]}]}'
    )
    result = pr.validate(document)
    assert result.is_valid
    assert any(warning.code == "BIDI_ISOLATE" and warning.path == "p" for warning in result.warnings)


def test_unicode_security_values_are_preserved_and_advised():
    document = pr.loads(
        '{"version":"1.0-beta.6","metadata":{"title":"T"},"sections":'
        '[{"id":"s","title":"S","prompts":['
        '{"id":"bidi_override","label":"Bidi","response":"safe\\u202etxt.exe"},'
        '{"id":"persian_zwnj","label":"Persian","response":"می‌روم"},'
        '{"id":"emoji_zwj","label":"Emoji","response":"👨‍👩‍👧"},'
        '{"id":"bidi_isolate","label":"Isolate","response":"a\\u2066b"}]}]}'
    )
    responses = {prompt.id: prompt.response for prompt in document.all_prompts()}
    warning_codes = {warning.code for warning in pr.validate(document).warnings}

    assert responses["bidi_override"] == "safe\u202etxt.exe"
    assert responses["persian_zwnj"] == "می‌روم"
    assert responses["emoji_zwj"] == "👨‍👩‍👧"
    assert {"BIDI_OVERRIDE", "BIDI_ISOLATE", "HIDDEN_ZWNJ", "HIDDEN_ZWJ"} <= warning_codes
