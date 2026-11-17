"""Rule expressions. The headset evaluates them; the server checks and tidies them. One table holds both to the same answers."""

import json
import random
from pathlib import Path

import pytest

from imagine import expr

CASES = json.loads((Path(__file__).resolve().parents[2] / "shared" / "expr-cases.json").read_text(encoding="utf-8"))["cases"]


@pytest.mark.parametrize("case", CASES, ids=[c["expr"] or "(empty)" for c in CASES])
def test_the_shared_table(case):
    tree = expr.parse(case["expr"])
    if case["want"] is None:
        assert tree is None and expr.tidy(case["expr"]) is None
        return
    assert tree is not None, "it should be readable"
    answers = case.get("world", {})

    def world(name, args):
        return answers[name + "(" + ",".join(args) + ")"]

    assert expr.evaluate(tree, case.get("vars", {}), world) == pytest.approx(case["want"])
    if "tidy" in case:
        assert expr.tidy(case["expr"]) == case["tidy"]
    # what the server writes back means the same as what it was given
    again = expr.parse(expr.tidy(case["expr"]))
    assert expr.evaluate(again, case.get("vars", {}), world) == pytest.approx(case["want"])


def test_numbers_and_truths_are_expressions_too():
    assert expr.tidy(3) == "3" and expr.tidy(2.5) == "2.5" and expr.tidy(True) == "1"
    assert expr.tidy(float("nan")) is None and expr.tidy(None) is None and expr.tidy(["a"]) is None


def test_nothing_overlong_or_bottomless_is_let_through():
    assert expr.tidy("1" + " + 1" * 200) is None
    assert expr.tidy("(" * 60 + "1" + ")" * 60) is None


def test_which_counters_an_expression_reads():
    assert expr.names("throws >= 2 or standing(pins) == 0 and dist(ball, cup) < score") == {"throws", "score"}, "names inside a world function are things, not counters"
    assert expr.names("min(a, b) + c") == {"a", "b", "c"}


def test_rand_is_between_its_ends():
    rng = random.Random(7)
    for _ in range(50):
        assert 2 <= expr.evaluate(expr.parse("rand(2, 5)"), rng=rng) <= 5
        assert 0 <= expr.evaluate(expr.parse("rand()"), rng=rng) <= 1
