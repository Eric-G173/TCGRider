"""
TCGRider Agentic Fix Loop
--------------------------
A minimal, self-contained agentic coding loop: give it a task in plain
English, and it edits source files, runs the REAL test suite (xUnit for
backend, Jest for frontend), and iterates on failures until tests pass or
it hits the iteration cap.

Setup:
    pip install anthropic python-dotenv
    Create a .env file next to this script containing:
        ANTHROPIC_API_KEY=sk-ant-api03-your-real-key

Usage, run from the repo root:
    python agent_loop.py backend "Fix the bug where ExtractSetNumber doesn't handle set ids with letters after the number"
    python agent_loop.py frontend "Fix the bug where right-clicking near the edge of the screen blocks card selection"

What each part maps to, from the six-step framework:
  1. Write a spec the agent can act on   -> the task argument above
  2. Give the model real write access     -> the read_file / write_file tools
  3. Run the tests programmatically       -> run_tests()
  4. Feed failures back and let it retry  -> the main loop
  5. Add guardrails before you trust it   -> protected_files, MAX_ITERATIONS
  6. Review the full trace                -> agent_transcript.json
"""

import subprocess
import sys
import json
from pathlib import Path
from dotenv import load_dotenv
from anthropic import Anthropic

load_dotenv()  # reads ANTHROPIC_API_KEY from a .env file in this folder

# ---- Configuration ----
client = Anthropic()  # picks up ANTHROPIC_API_KEY, now loaded from .env above

TOOLS = [
    {
        "name": "list_directory",
        "description": "List the files and subdirectories inside a directory, relative to the repo root. Use this FIRST if you're not certain of an exact file path — don't guess.",
        "input_schema": {
            "type": "object",
            "properties": {
                "path": {"type": "string", "description": "Directory path relative to repo root. Use '.' for the repo root itself."}
            },
            "required": ["path"],
        },
    },
    {
        "name": "read_file",
        "description": "Read the contents of a file by path, relative to the repo root.",
        "input_schema": {
            "type": "object",
            "properties": {"path": {"type": "string"}},
            "required": ["path"],
        },
    },
    {
        "name": "write_file",
        "description": "Overwrite a file with new content. Cannot be used on protected test files.",
        "input_schema": {
            "type": "object",
            "properties": {
                "path": {"type": "string"},
                "content": {"type": "string"},
            },
            "required": ["path", "content"],
        },
    },
]


def list_directory(path: str = ".") -> str:
    try:
        p = Path(path)
        if not p.is_dir():
            return f"ERROR: {path} is not a directory (did you mean to read_file it instead?)"
        entries = sorted(p.iterdir())
        lines = [f"{e.name}/" if e.is_dir() else e.name for e in entries]
        return "\n".join(lines) if lines else "(empty directory)"
    except Exception as e:
        return f"ERROR listing {path}: {e}"


def read_file(path: str) -> str:
    try:
        return Path(path).read_text()
    except Exception as e:
        return f"ERROR reading {path}: {e}. Try list_directory on the parent folder to find the real path."


def is_protected(path: str) -> bool:
    """Any file with 'test' anywhere in its name is off-limits to the agent
    — catches CardItem.test.js, SetSortingTests.cs, ModelSerializationtests.cs,
    filterCards.test.js, and any future test file, without needing to list
    exact filenames one at a time (which is exactly how filterCards.test.js
    slipped through unprotected the first time)."""
    return "test" in Path(path).name.lower()


def write_file(path: str, content: str) -> str:
    p = Path(path)
    if is_protected(path):
        return f"REFUSED: {p.name} looks like a test file and cannot be edited by the agent."
    p.write_text(content)
    return f"Wrote {len(content)} characters to {path}"


def run_tests(test_dir, test_command) -> tuple[bool, str]:
    """Runs the real test suite and returns (passed, output). shell=True
    matters specifically on Windows — npm resolves to npm.cmd, which
    subprocess can silently fail to find without going through the shell."""
    result = subprocess.run(
        test_command,
        cwd=test_dir,
        capture_output=True,
        text=True,
        timeout=180,
        shell=True,
    )
    output = result.stdout + result.stderr
    passed = result.returncode == 0
    return passed, output


def run_tool(name: str, tool_input: dict) -> str:
    if name == "list_directory":
        return list_directory(tool_input.get("path", "."))
    if name == "read_file":
        return read_file(tool_input["path"])
    if name == "write_file":
        return write_file(tool_input["path"], tool_input["content"])
    return f"Unknown tool: {name}"


def save_transcript(transcript):
    Path("agent_transcript.json").write_text(json.dumps(transcript, indent=2, default=str))
    print("\nFull transcript saved to agent_transcript.json — this is what you'd")
    print("actually walk through in an interview, including the failed attempts.")


TARGETS = {
    "backend": {
        "test_dir": Path("backend-tests"),
        "test_command": "dotnet test --nologo",
        "stack_description": "a C# ASP.NET Core backend",
        "explore_hint": "backend/",
    },
    "frontend": {
        "test_dir": Path("frontend"),
        "test_command": "npm test -- --watchAll=false",
        "stack_description": "a React frontend",
        "explore_hint": "frontend/src/",
    },
}


def main():
    if len(sys.argv) < 3:
        print('Usage: python agent_loop.py <backend|frontend> "<task description>" [max_iterations]')
        sys.exit(1)

    target_name = sys.argv[1].lower()
    task = sys.argv[2]
    max_iterations = int(sys.argv[3]) if len(sys.argv) > 3 else 6

    if target_name not in TARGETS:
        print(f"Unknown target '{target_name}' — use 'backend' or 'frontend'")
        sys.exit(1)

    target = TARGETS[target_name]
    transcript = []

    system_prompt = (
        f"You are fixing a bug in {target['stack_description']} for a trading "
        "card tracking app. You can read and write source files with the "
        "tools provided, but any file with 'test' anywhere in its name is "
        "off-limits — you cannot edit it, only read it. Use list_directory "
        "to explore before guessing any file paths — don't assume a file's "
        "location. Match the codebase's existing conventions (e.g. default "
        "vs named exports) rather than introducing a new style. After each "
        "change you make, the real test suite runs automatically and you'll "
        "be shown the actual pass/fail output. Keep iterating until the "
        "tests pass. Be surgical: don't rewrite unrelated code just because "
        "you're in the file."
    )

    messages = [{
        "role": "user",
        "content": (
            f"Task: {task}\n\n"
            f"Start by exploring {target['explore_hint']} with list_directory "
            "to find the relevant file(s), then read them before making any changes."
        )
    }]

    for iteration in range(1, max_iterations + 1):
        print(f"\n{'=' * 60}\nITERATION {iteration}\n{'=' * 60}")

        response = client.messages.create(
            model="claude-sonnet-5",
            max_tokens=4096,
            system=system_prompt,
            tools=TOOLS,
            messages=messages,
        )

        messages.append({"role": "assistant", "content": response.content})
        transcript.append({"iteration": iteration, "role": "assistant", "content": str(response.content)})

        tool_results = []
        made_edit = False
        for block in response.content:
            if block.type == "text":
                print(f"[Claude]: {block.text}")
            elif block.type == "tool_use":
                print(f"[Tool call]: {block.name}({block.input.get('path', '')})")
                result = run_tool(block.name, block.input)
                if block.name == "write_file":
                    made_edit = True
                tool_results.append({
                    "type": "tool_result",
                    "tool_use_id": block.id,
                    "content": result,
                })

        if tool_results:
            messages.append({"role": "user", "content": tool_results})
        else:
            # No tool calls this turn means Claude considers itself finished
            # — a pure text answer, not a stuck loop. Most likely for
            # questions rather than actual fix-it tasks, where there was
            # never anything to edit in the first place.
            print("\nClaude finished without calling any tools — treating this as a final answer.")
            save_transcript(transcript)
            return

        if made_edit:
            print(f"\n[Running the real test suite in {target['test_dir']}...]")
            passed, output = run_tests(target["test_dir"], target["test_command"])
            transcript.append({"iteration": iteration, "test_output": output, "passed": passed})

            if passed:
                print(f"\nTests passed after {iteration} iteration(s).")
                save_transcript(transcript)
                return

            print("\nTests failed. Feeding the real output back to Claude.")
            messages.append({
                "role": "user",
                "content": f"Test run failed. Here's the real output:\n\n{output}\n\nFix it and try again."
            })

    print(f"\nHit the iteration cap ({max_iterations}) without passing tests.")
    save_transcript(transcript)


if __name__ == "__main__":
    main()