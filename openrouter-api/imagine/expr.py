"""Expressions: the small arithmetic a rule may ask about the world.

    standing(pins) == 0 and throws < 2
    dist(ball, cup) < 0.3
    time > 60 or health <= 0

The headset evaluates them (Play/Expr.cs implements exactly this grammar); the server only checks
that what the model wrote can be evaluated and writes it back in one tidy spelling. A condition
that cannot be read is dropped and the rule it was on still stands.

    expr  := and ('or' and)*
    and   := not ('and' not)*
    not   := 'not' not | cmp
    cmp   := sum (('<' | '<=' | '>' | '>=' | '==' | '!=') sum)?
    sum   := term (('+' | '-') term)*
    term  := unary (('*' | '/' | '%') unary)*
    unary := '-' unary | atom
    atom  := number | 'text' | name | name '(' args ')' | '(' expr ')'
"""

from __future__ import annotations

import math
import random
import re

MAX_LENGTH = 240
MAX_DEPTH = 24

# name -> (fewest arguments, most arguments). The first group is plain arithmetic; the second asks
# the world, and its arguments are the names of things (an id, an asset, a zone, `player`).
FUNCTIONS = {
    "min": (2, 4), "max": (2, 4), "abs": (1, 1), "floor": (1, 1), "ceil": (1, 1), "round": (1, 1),
    "sqrt": (1, 1), "clamp": (3, 3), "rand": (0, 2),
    "count": (1, 1), "standing": (1, 1), "tipped": (1, 1), "alive": (1, 1), "dist": (2, 2),
    "inside": (1, 2), "holding": (0, 1), "riding": (0, 1), "speed": (0, 1), "height": (0, 1),
    "health": (0, 1), "moving": (1, 1),
}
WORLD = frozenset(("count", "standing", "tipped", "alive", "dist", "inside", "holding", "riding", "speed", "height", "health", "moving"))
WORDS = {"and": "and", "or": "or", "not": "not", "&&": "and", "||": "or", "!": "not", "true": "1", "false": "0", "yes": "1", "no": "0"}

_TOKEN = re.compile(r"""
    \s*(?:
        (?P<number>\d+\.\d+|\.\d+|\d+)
      | (?P<name>[A-Za-z_][A-Za-z0-9_.]*)
      | '(?P<single>[^']*)'
      | "(?P<double>[^"]*)"
      | (?P<op><=|>=|==|!=|&&|\|\||[-+*/%<>()=,!])
    )""", re.VERBOSE)


class ExprError(ValueError):
    pass


def tokens(text: str) -> list[tuple[str, str]]:
    out: list[tuple[str, str]] = []
    at = 0
    text = text.strip()
    while at < len(text):
        match = _TOKEN.match(text, at)
        if not match or match.end() == at:
            raise ExprError(f"cannot read {text[at:at + 12]!r}")
        at = match.end()
        if match.group("number") is not None:
            out.append(("number", match.group("number")))
        elif match.group("name") is not None:
            word = match.group("name").lower()
            if word in WORDS:
                spelt = WORDS[word]
                out.append(("number", spelt) if spelt in "01" else ("op", spelt))
            else:
                out.append(("name", word))
        elif match.group("single") is not None or match.group("double") is not None:
            out.append(("text", (match.group("single") if match.group("single") is not None else match.group("double")).strip().lower()))
        else:
            op = match.group("op")
            out.append(("op", WORDS.get(op, "==" if op == "=" else op)))
    return out


class _Parser:
    """Recursive descent to a tree of tuples: ('num', 3.0), ('var', 'score'), ('call', 'dist', [...]), ('<', a, b)..."""

    def __init__(self, toks: list[tuple[str, str]]):
        self.toks = toks
        self.at = 0
        self.depth = 0

    def peek(self) -> tuple[str, str]:
        return self.toks[self.at] if self.at < len(self.toks) else ("end", "")

    def take(self, value: str | None = None) -> tuple[str, str]:
        tok = self.peek()
        if value is not None and tok != ("op", value):
            raise ExprError(f"expected {value}")
        self.at += 1
        return tok

    def is_op(self, *values: str) -> bool:
        tok = self.peek()
        return tok[0] == "op" and tok[1] in values

    def expr(self):
        self.depth += 1
        if self.depth > MAX_DEPTH:
            raise ExprError("too deep")
        left = self.conj()
        while self.is_op("or"):
            self.take()
            left = ("or", left, self.conj())
        self.depth -= 1
        return left

    def conj(self):
        left = self.neg()
        while self.is_op("and"):
            self.take()
            left = ("and", left, self.neg())
        return left

    def neg(self):
        if self.is_op("not"):
            self.take()
            return ("not", self.neg())
        return self.cmp()

    def cmp(self):
        left = self.sum()
        if self.is_op("<", "<=", ">", ">=", "==", "!="):
            op = self.take()[1]
            return (op, left, self.sum())
        return left

    def sum(self):
        left = self.term()
        while self.is_op("+", "-"):
            op = self.take()[1]
            left = (op, left, self.term())
        return left

    def term(self):
        left = self.unary()
        while self.is_op("*", "/", "%"):
            op = self.take()[1]
            left = (op, left, self.unary())
        return left

    def unary(self):
        if self.is_op("-"):
            self.take()
            return ("neg", self.unary())
        return self.atom()

    def atom(self):
        kind, value = self.peek()
        if kind == "number":
            self.take()
            return ("num", float(value))
        if kind == "text":
            self.take()
            return ("text", value)
        if kind == "name":
            self.take()
            if self.is_op("("):
                if value not in FUNCTIONS:
                    raise ExprError(f"no function {value}")
                self.take()
                args = []
                if not self.is_op(")"):
                    args.append(self.expr())
                    while self.is_op(","):
                        self.take()
                        args.append(self.expr())
                self.take(")")
                fewest, most = FUNCTIONS[value]
                if not fewest <= len(args) <= most:
                    raise ExprError(f"{value} takes {fewest} to {most} arguments")
                return ("call", value, args)
            return ("var", value)
        if self.is_op("("):
            self.take()
            inner = self.expr()
            self.take(")")
            return inner
        raise ExprError("expected a value")


def parse(text) -> tuple | None:
    """The tree for an expression, or None when it cannot be read."""
    if isinstance(text, bool):
        return ("num", 1.0 if text else 0.0)
    if isinstance(text, (int, float)):
        return ("num", float(text)) if math.isfinite(text) else None
    if not isinstance(text, str) or not text.strip() or len(text) > MAX_LENGTH:
        return None
    try:
        parser = _Parser(tokens(text))
        tree = parser.expr()
        if parser.peek()[0] != "end":
            return None
        return tree
    except (ExprError, RecursionError):
        return None


_BINDING = {"or": 1, "and": 2, "not": 3, "<": 4, "<=": 4, ">": 4, ">=": 4, "==": 4, "!=": 4, "+": 5, "-": 5, "*": 6, "/": 6, "%": 6, "neg": 7}


def _number(value: float) -> str:
    return str(int(value)) if value == int(value) and abs(value) < 1e15 else repr(round(value, 6))


def write(tree, outer: int = 0) -> str:
    """One spelling for a tree: lower case, single spaces, only the brackets it needs."""
    kind = tree[0]
    if kind == "num":
        return _number(tree[1])
    if kind == "text":
        return "'" + tree[1] + "'"
    if kind == "var":
        return tree[1]
    if kind == "call":
        return tree[1] + "(" + ", ".join(write(a) for a in tree[2]) + ")"
    mine = _BINDING[kind]
    if kind == "neg":
        text = "-" + write(tree[1], mine)
    elif kind == "not":
        text = "not " + write(tree[1], mine)
    else:
        # the right side of - / % and of a comparison binds one tighter, so a - (b - c) keeps its brackets
        text = write(tree[1], mine) + " " + kind + " " + write(tree[2], mine + 1)
    return "(" + text + ")" if mine < outer else text


def tidy(text) -> str | None:
    """What the model wrote, as the headset will read it; None when it cannot be read."""
    tree = parse(text)
    return write(tree) if tree is not None else None


def names(text) -> set[str]:
    """Every variable an expression reads."""
    found: set[str] = set()

    def walk(tree, in_world_call=False):
        kind = tree[0]
        if kind == "var":
            if not in_world_call:
                found.add(tree[1])
        elif kind == "call":
            for arg in tree[2]:
                walk(arg, tree[1] in WORLD)
        elif kind not in ("num", "text"):
            for child in tree[1:]:
                walk(child, in_world_call)

    tree = parse(text)
    if tree is not None:
        walk(tree)
    return found


# ---------------------------------------------------------------------------- evaluation
# The headset is where expressions run. This copy exists so the two implementations can be held
# to the same answers by one table of cases (shared/expr-cases.json), and for tools.

def _truth(value) -> bool:
    return bool(value) if not isinstance(value, str) else value != ""


def evaluate(tree, variables: dict | None = None, world=None, rng: random.Random | None = None):
    """A number (or a piece of text) for a tree. `world(name, [args])` answers the world functions."""
    variables = variables or {}
    kind = tree[0]
    if kind == "num":
        return tree[1]
    if kind == "text":
        return tree[1]
    if kind == "var":
        return variables.get(tree[1], 0.0)
    if kind == "call":
        name, args = tree[1], tree[2]
        if name in WORLD:
            # arguments name things: a bare word is the name itself, not a variable's value
            named = [a[1] if a[0] in ("var", "text") else _number(float(evaluate(a, variables, world, rng))) for a in args]
            return float(world(name, named)) if world else 0.0
        values = [float(evaluate(a, variables, world, rng) or 0) for a in args]
        if name == "min":
            return min(values)
        if name == "max":
            return max(values)
        if name == "abs":
            return abs(values[0])
        if name == "floor":
            return float(math.floor(values[0]))
        if name == "ceil":
            return float(math.ceil(values[0]))
        if name == "round":
            return float(math.floor(values[0] + 0.5))
        if name == "sqrt":
            return math.sqrt(max(values[0], 0.0))
        if name == "clamp":
            return min(max(values[0], values[1]), values[2])
        if name == "rand":
            lo, hi = (0.0, 1.0) if not values else (0.0, values[0]) if len(values) == 1 else (values[0], values[1])
            return lo + (rng or random).random() * (hi - lo)
        return 0.0
    if kind == "neg":
        return -float(evaluate(tree[1], variables, world, rng) or 0)
    if kind == "not":
        return 0.0 if _truth(evaluate(tree[1], variables, world, rng)) else 1.0
    if kind == "and":
        return 1.0 if _truth(evaluate(tree[1], variables, world, rng)) and _truth(evaluate(tree[2], variables, world, rng)) else 0.0
    if kind == "or":
        return 1.0 if _truth(evaluate(tree[1], variables, world, rng)) or _truth(evaluate(tree[2], variables, world, rng)) else 0.0
    left, right = evaluate(tree[1], variables, world, rng), evaluate(tree[2], variables, world, rng)
    if kind in ("==", "!="):
        if isinstance(left, str) or isinstance(right, str):
            same = str(left) == str(right)
        else:
            same = abs(float(left) - float(right)) < 1e-6
        return 1.0 if same == (kind == "==") else 0.0
    a = float(left) if not isinstance(left, str) else 0.0
    b = float(right) if not isinstance(right, str) else 0.0
    if kind == "<":
        return 1.0 if a < b else 0.0
    if kind == "<=":
        return 1.0 if a <= b else 0.0
    if kind == ">":
        return 1.0 if a > b else 0.0
    if kind == ">=":
        return 1.0 if a >= b else 0.0
    if kind == "+":
        return a + b
    if kind == "-":
        return a - b
    if kind == "*":
        return a * b
    if kind == "/":
        return a / b if abs(b) > 1e-9 else 0.0
    if kind == "%":
        return math.fmod(a, b) if abs(b) > 1e-9 else 0.0
    return 0.0
