"""Human-in-the-loop prompt helper."""

# ANSI colour codes
_CYAN = "\033[96m"
_YELLOW = "\033[93m"
_RESET = "\033[0m"
_BOLD = "\033[1m"


def ask_human(question: str) -> str:
    """Print a formatted question to the terminal and return the user's answer.

    Args:
        question: The question the agent wants to ask the human operator.

    Returns:
        The stripped text entered by the user.
    """
    print(f"\n{_BOLD}{_CYAN}{'=' * 60}{_RESET}")
    print(f"{_BOLD}{_CYAN}🤖  AGENT NEEDS YOUR INPUT{_RESET}")
    print(f"{_BOLD}{_CYAN}{'=' * 60}{_RESET}")
    print(f"\n{_YELLOW}{question}{_RESET}\n")
    answer = input(f"{_BOLD}👉  Your answer: {_RESET}").strip()
    print(f"{_CYAN}{'=' * 60}{_RESET}\n")
    return answer
