"""
TCGRider Agentic Fix Loop
--------------------------
A minimal, self-contained agentic coding loop: give it a task in plain
English, and it edits source files, runs the REAL xUnit test suite in
backend-tests/, and iterates on failures until tests pass or it hits the
iteration cap.

Setup:
    pip install anthropic
    setx ANTHROPIC_API_KEY "your-key-here"      (PowerShell, then restart terminal)

Usage, run from the repo root:
    python agent_loop.py "Fix the bug where ExtractSetNumber doesn't handle set ids with letters after the number"

What each part maps to, from the six-step framework:
  1. Write a spec the agent can act on   -> the `task` argument below
  2. Give the model real write access     -> the read_file / write_file tools
  3. Run the tests programmatically       -> run_tests()
  4. Feed failures back and let it retry  -> the main loop
  5. Add guardrails before you trust it   -> PROTECTED_FILES, MAX_ITERATIONS
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
MAX_ITERATIONS = 6
SOURCE_FILE = "backend/APISync.cs"          # the file the agent is allowed to edit
TEST_DIR = Path("backend-tests")
PROTECTED_FILES = {"SetSortingTests.cs", "ModelSerializationtests.cs"}  # never editable

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


def write_file(path: str, content: str) -> str:
    p = Path(path)
    if p.name in PROTECTED_FILES:
        return f"REFUSED: {p.name} is a protected test file and cannot be edited by the agent."
    p.write_text(content)
    return f"Wrote {len(content)} characters to {path}"


def run_tests() -> tuple[bool, str]:
    """Runs the real xUnit suite and returns (passed, output). This is the
    actual ground truth the whole loop checks itself against — not something
    the model reports on its own."""
    result = subprocess.run(
        ["dotnet", "test", "--nologo"],
        cwd=TEST_DIR,
        capture_output=True,
        text=True,
        timeout=120,
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


def main():
    if len(sys.argv) < 2:
        print('Usage: python agent_loop.py "<task description>"')
        sys.exit(1)

    task = sys.argv[1]
    transcript = []

    system_prompt = (
        "You are fixing a bug in a C# backend for a trading card tracking app. "
        "You can read and write source files with the tools provided, but you "
        f"must never edit these protected test files: {PROTECTED_FILES}. After "
        "each change you make, the real test suite runs automatically and "
        "you'll be shown the actual pass/fail output — not a summary, the real "
        "thing. Keep iterating until the tests pass. Be surgical: don't rewrite "
        "unrelated code just because you're in the file."
    )

    messages = [{
        "role": "user",
        "content": f"Task: {task}\n\nStart by reading {SOURCE_FILE} to see the current code."
    }]

    for iteration in range(1, MAX_ITERATIONS + 1):
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

        if made_edit:
            print("\n[Running the real xUnit suite...]")
            passed, output = run_tests()
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

    print(f"\nHit the iteration cap ({MAX_ITERATIONS}) without passing tests.")
    save_transcript(transcript)


if __name__ == "__main__":
    main()