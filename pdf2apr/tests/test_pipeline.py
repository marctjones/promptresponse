"""Tests for everything that does not need a model."""
from __future__ import annotations

import json
from pathlib import Path

import pytest

from pdf2apr import (Blank, MarkedPage, Options, Question, build_document,
                     convert, detect_blanks, mark_pages, parse_reply)

CORPUS = Path(__file__).resolve().parents[2] / "scripts" / "pdf-form-benchmark" / "corpus"


def blank(n: int, page: int = 1, **kw) -> Blank:
    return Blank(id=f"f{n}", page=page, left=10.0 * n, bottom=100.0,
                 right=10.0 * n + 8, top=112.0, **kw)


class TestParseReply:
    def test_one_entry_per_box(self):
        blanks = [blank(1), blank(2)]
        reply = json.dumps({
            "sections": [{"id": "s", "title": "Part I"}],
            "fields": [
                {"n": 1, "label": "First name", "section_id": "s"},
                {"n": 2, "label": "Last name", "section_id": "s"},
            ],
        })
        questions, sections = parse_reply(reply, blanks)
        assert [q.label for q in questions] == ["First name", "Last name"]
        assert sections == [{"id": "s", "title": "Part I"}]

    def test_a_shared_group_becomes_one_question(self):
        # The reason the model is asked about grouping at all: a social
        # security number printed as three boxes is one question, and emitting
        # three asks a person the same thing three times.
        blanks = [blank(1), blank(2), blank(3)]
        reply = json.dumps({"sections": [], "fields": [
            {"n": i, "label": "Social security number", "group": "ssn"}
            for i in (1, 2, 3)
        ]})
        questions, _ = parse_reply(reply, blanks)
        assert len(questions) == 1
        assert len(questions[0].blanks) == 3, "every box is kept, so geometry survives"

    def test_a_box_number_outside_the_page_never_claims_a_real_box(self):
        # This test used to assert such an entry was DROPPED, on the theory
        # that a miscount would mislabel a real field. The premise was right
        # and the remedy was wrong: the entries are usually questions printed
        # on the page that the form declares no widget for, and discarding
        # them cost four points of F1. What must hold is only that they never
        # take a box that belongs to something else.
        questions, _ = parse_reply(
            json.dumps({"fields": [
                {"n": 1, "label": "Real field"},
                {"n": 9, "label": "Something the model saw"},
            ]}),
            [blank(1)],
        )
        assert questions[0].blanks == [blank(1)]
        assert questions[1].blanks == []

    def test_prose_around_the_json_is_tolerated(self):
        questions, _ = parse_reply(
            'Here you go:\n{"fields": [{"n": 1, "label": "Name"}]}\nHope that helps!',
            [blank(1)])
        assert [q.label for q in questions] == ["Name"]

    def test_unusable_output_raises_rather_than_returning_nothing(self):
        # Silence and failure must be distinguishable: a page whose reply could
        # not be read falls back to deterministic labels, and cannot do that if
        # the failure looks like "no fields on this page".
        with pytest.raises(ValueError):
            parse_reply("the model apologises but declines", [blank(1)])


class TestBuildDocument:
    def test_produces_a_valid_shaped_template(self):
        doc = build_document("W-9", [Question("Full name", [blank(1)])], [])
        assert doc["aprVersion"] == "1.0-beta.6"
        assert doc["documentType"] == "template"
        assert doc["metadata"]["title"] == "W-9"
        assert doc["sections"][0]["prompts"][0]["label"] == "Full name"

    def test_carries_no_geometry(self):
        # APR is layout-free. Coordinates leaking into the document would be a
        # format violation, not a convenience.
        doc = build_document("t", [Question("Name", [blank(1)])], [])
        assert "left" not in json.dumps(doc)
        assert "bbox" not in json.dumps(doc)

    def test_ids_are_unique_even_when_labels_repeat(self):
        qs = [Question("Date", [blank(1)]), Question("Date", [blank(2)])]
        prompts = build_document("t", qs, [])["sections"][0]["prompts"]
        assert prompts[0]["id"] != prompts[1]["id"]

    def test_questions_keep_the_order_the_form_asks_them(self):
        qs = [Question(f"Q{i}", [blank(i)]) for i in range(1, 5)]
        prompts = build_document("t", qs, [])["sections"][0]["prompts"]
        assert [p["label"] for p in prompts] == ["Q1", "Q2", "Q3", "Q4"]

    def test_sections_the_model_named_are_used(self):
        qs = [Question("N", [blank(1)], section="part-1")]
        doc = build_document("t", qs, [{"id": "part-1", "title": "Part I — TIN"}])
        assert doc["sections"][0]["title"] == "Part I — TIN"


@pytest.mark.skipif(not (CORPUS / "fed-w9.pdf").exists(), reason="corpus not present")
class TestAgainstARealForm:
    def test_detect_finds_the_forms_own_fields(self):
        blanks = detect_blanks(CORPUS / "fed-w9.pdf")
        assert len(blanks) == 23, "W-9 declares 23 fillable widgets"
        assert all(b.width > 0 and b.height > 0 for b in blanks)

    def test_every_blank_is_drawn_and_numbered(self, tmp_path):
        blanks = detect_blanks(CORPUS / "fed-w9.pdf")
        pages = mark_pages(CORPUS / "fed-w9.pdf", blanks, into=tmp_path)
        assert sum(len(p.blanks) for p in pages) == len(blanks)
        assert all(p.image.exists() for p in pages)

    def test_numbering_follows_reading_order(self, tmp_path):
        blanks = detect_blanks(CORPUS / "fed-w9.pdf")
        page = mark_pages(CORPUS / "fed-w9.pdf", blanks, into=tmp_path)[0]
        tops = [b.top for b in page.blanks]
        assert tops == sorted(tops, reverse=True), "box 1 is nearest the top"

    def test_no_model_still_produces_a_document(self):
        result = convert(CORPUS / "fed-w9.pdf", Options(use_model=False))
        assert result.document is not None
        assert len(result.questions) == len(result.blanks)
        assert not result.used_model


class TestQuestionsWithNoField:
    """The model sometimes names a question stage 1 did not find."""

    def test_a_number_past_the_last_box_is_kept(self):
        # W-9's signature and date lines are printed on the page but are not
        # AcroForm widgets, so they get no box; the model numbers them anyway.
        # Dropping them as out of range cost four points of F1.
        questions, _ = parse_reply(
            json.dumps({"fields": [
                {"n": 1, "label": "Name"},
                {"n": 2, "label": "Signature of U.S. person"},
            ]}),
            [blank(1)],
        )
        assert [q.label for q in questions] == ["Name", "Signature of U.S. person"]
        assert questions[1].blanks == [], "there is nowhere on the page to point at"

    def test_such_a_question_still_reaches_the_document(self):
        questions, _ = parse_reply(
            json.dumps({"fields": [{"n": 9, "label": "Date"}]}), [blank(1)])
        doc = build_document("t", questions, [])
        assert doc["sections"][0]["prompts"][0]["label"] == "Date"

    def test_each_unplaced_question_stays_separate(self):
        # They share no box, so they must not collapse into one another.
        questions, _ = parse_reply(
            json.dumps({"fields": [
                {"n": 8, "label": "Signature"}, {"n": 9, "label": "Date"},
            ]}),
            [blank(1)],
        )
        assert [q.label for q in questions] == ["Signature", "Date"]
