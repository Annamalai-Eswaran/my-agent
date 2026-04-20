"""Utilities for creating Git branch names."""

import re


def slugify(text: str) -> str:
    """Convert *text* into a URL/branch-safe slug.

    Steps:
    1. Lowercase the string.
    2. Replace any character that is not a letter, digit, or hyphen with a hyphen.
    3. Collapse consecutive hyphens into a single hyphen.
    4. Strip leading and trailing hyphens.
    5. Truncate to 50 characters (without cutting in the middle of a word where possible).
    """
    text = text.lower()
    text = re.sub(r"[^a-z0-9]+", "-", text)
    text = re.sub(r"-{2,}", "-", text)
    text = text.strip("-")
    if len(text) > 50:
        text = text[:50].rstrip("-")
    return text


def make_branch_name(work_item_id: int, title: str) -> str:
    """Return a branch name following the ``feature/ae/{id}-{slug}`` pattern.

    Args:
        work_item_id: The Azure DevOps work item number.
        title: The work item title used to generate the slug.

    Returns:
        A branch name such as ``feature/ae/4521-fix-login-timeout``.
    """
    slug = slugify(title)
    return f"feature/ae/{work_item_id}-{slug}"
